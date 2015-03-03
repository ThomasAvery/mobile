using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Toggl.Phoebe.Data.DataObjects;
using Toggl.Phoebe.Data.Models;
using Toggl.Phoebe.Net;
using XPlatUtils;

namespace Toggl.Phoebe.Data
{
    public sealed class ActiveTimeEntryManager : INotifyPropertyChanged, IDisposable
    {
        private static string GetPropertyName<T> (Expression<Func<ActiveTimeEntryManager, T>> expr)
        {
            return expr.ToPropertyName ();
        }

        public static readonly string PropertyRunning = GetPropertyName (m => m.Running);
        public static readonly string PropertyDraft = GetPropertyName (m => m.Draft);
        public static readonly string PropertyActive = GetPropertyName (m => m.Active);

        private readonly List<TimeEntryData> runningEntries = new List<TimeEntryData> ();
        private readonly List<TimeEntryData> draftEntries = new List<TimeEntryData> ();
        private Subscription<DataChangeMessage> subscriptionDataChange;
        private Subscription<DataStoreIdleMessage> subscriptionDataStoreIdle;
        private Subscription<SyncStartedMessage> subscriptionSyncStarted;
        private Subscription<SyncFinishedMessage> subscriptionSyncFinished;
        private Subscription<AuthChangedMessage> subscriptionAuthChanged;
        private Guid? currentUserId;
        private bool syncRunning;
        private HashSet<string> changedProperties;
        private TimeEntryData backingRunningEntry;
        private TimeEntryData backingDraftEntry;
        private TimeEntryData backingActiveEntry;

        public ActiveTimeEntryManager ()
        {
            var bus = ServiceContainer.Resolve<MessageBus> ();
            subscriptionDataChange = bus.Subscribe<DataChangeMessage> (OnDataChange);
            subscriptionDataStoreIdle = bus.Subscribe<DataStoreIdleMessage> (OnDataStoreIdle);
            subscriptionSyncStarted = bus.Subscribe<SyncStartedMessage> (OnSyncStarted);
            subscriptionSyncFinished = bus.Subscribe<SyncFinishedMessage> (OnSyncFinished);
            subscriptionAuthChanged = bus.Subscribe<AuthChangedMessage> (OnAuthChanged);

            var syncManager = ServiceContainer.Resolve<ISyncManager> ();
            syncRunning = syncManager.IsRunning;

            UpdateCurrentUserId ();
        }

        ~ActiveTimeEntryManager ()
        {
            Dispose (false);
        }

        public void Dispose ()
        {
            Dispose (true);
            GC.SuppressFinalize (this);
        }

        private void Dispose (bool disposing)
        {
            if (disposing) {
                var bus = ServiceContainer.Resolve<MessageBus> ();

                if (subscriptionDataStoreIdle != null) {
                    bus.Unsubscribe (subscriptionDataStoreIdle);
                    subscriptionDataStoreIdle = null;
                }

                if (subscriptionDataChange != null) {
                    bus.Unsubscribe (subscriptionDataChange);
                    subscriptionDataChange = null;
                }

                if (subscriptionSyncStarted != null) {
                    bus.Unsubscribe (subscriptionSyncStarted);
                    subscriptionSyncStarted = null;
                }

                if (subscriptionSyncFinished != null) {
                    bus.Unsubscribe (subscriptionSyncFinished);
                    subscriptionSyncFinished = null;
                }

                if (subscriptionAuthChanged != null) {
                    bus.Unsubscribe (subscriptionAuthChanged);
                    subscriptionAuthChanged = null;
                }
            }
        }

        private void UpdateCurrentUserId ()
        {
            var authManager = ServiceContainer.Resolve<AuthManager> ();
            var userId = authManager.GetUserId ();
            if (currentUserId == userId) {
                return;
            }

            currentUserId = userId;

            runningEntries.RemoveAll (e => e.UserId != currentUserId);
            draftEntries.RemoveAll (e => e.UserId != currentUserId);
            UpdateProperties ();

            LoadTimeEntries ();
        }

        private async void LoadTimeEntries ()
        {
            if (currentUserId == null) {
                return;
            }

            var userId = currentUserId.Value;
            var store = ServiceContainer.Resolve<IDataStore> ();

            // Load data:
            var draftTask = TimeEntryModel.GetDraftAsync ();
            var runningTask = store.Table<TimeEntryData> ()
                              .QueryAsync (r => r.State == TimeEntryState.Running && r.DeletedAt == null && r.UserId == userId);

            await Task.WhenAll (draftTask, runningTask);

            // Check that the user hasn't changed in the mean time
            if (userId != currentUserId) {
                return;
            }

            // Update data
            var draftModel = draftTask.Result;
            if (draftModel != null) {
                var draftData = draftModel.Data;
                if (!draftEntries.UpdateData (draftData)) {
                    draftEntries.Add (draftData);
                }
            }

            foreach (var entry in runningTask.Result) {
                if (!runningEntries.UpdateData (entry)) {
                    runningEntries.Add (entry);
                }
            }

            UpdateProperties ();
            TryEnforceSingleRunning ();
        }

        private async void LoadNewDraft ()
        {
            if (currentUserId == null) {
                return;
            }

            var userId = currentUserId.Value;

            // Load data:
            var draftModel = await TimeEntryModel.GetDraftAsync ();

            // Check that the user hasn't changed in the mean time
            if (userId != currentUserId) {
                return;
            }

            // Update data
            if (draftModel != null) {
                var draftData = draftModel.Data;
                if (!draftEntries.UpdateData (draftData)) {
                    draftEntries.Add (draftData);
                }
            }

            UpdateProperties ();
        }

        private void OnDataChange (DataChangeMessage msg)
        {
            var data = msg.Data as TimeEntryData;
            if (data == null) {
                return;
            }

            var isExcluded = msg.Action == DataAction.Delete
                             || data.DeletedAt != null
                             || data.UserId != currentUserId;
            var isRunning = !isExcluded && data.State == TimeEntryState.Running;
            var isDraft = !isExcluded && data.State == TimeEntryState.New;

            // Update running entries
            if (isRunning) {
                if (!runningEntries.UpdateData (data)) {
                    runningEntries.Add (data);
                }
            } else {
                runningEntries.RemoveAll (e => e.Id == data.Id);
            }

            // Update draft entries
            if (isDraft) {
                if (!draftEntries.UpdateData (data)) {
                    draftEntries.Add (data);
                }
            } else {
                var removed = draftEntries.RemoveAll (e => e.Id == data.Id);
                if (removed > 0 && draftEntries.Count < 1) {
                    LoadNewDraft ();
                }
            }

            UpdateProperties ();
        }

        private void OnDataStoreIdle (DataStoreIdleMessage msg)
        {
            TryEnforceSingleRunning ();
        }

        private void OnSyncStarted (SyncStartedMessage msg)
        {
            syncRunning = true;
        }

        private void OnSyncFinished (SyncFinishedMessage msg)
        {
            syncRunning = false;
            TryEnforceSingleRunning ();
        }

        private void OnAuthChanged (AuthChangedMessage msg)
        {
            UpdateCurrentUserId ();
        }

        private void UpdateProperties ()
        {
            BatchPropertyChanges (delegate {
                Running = runningEntries.OrderByDescending (e => e.ModifiedAt).FirstOrDefault ();
                Draft = draftEntries.OrderByDescending (e => e.ModifiedAt).FirstOrDefault ();
                Active = Running ?? Draft;
            });
        }

        private async void TryEnforceSingleRunning ()
        {
            // During sync we don't want to enforce single timer as it would cause corrup data states most probably, so
            // we wait until the sync has finished.
            if (syncRunning) {
                return;
            }

            if (runningEntries.Count <= 1) {
                return;
            }

            var runningEntry = runningEntries.OrderByDescending (e => e.ModifiedAt).First ();
            var tasks = runningEntries
                        .Where (e => e != runningEntry)
                        .Select (e => new TimeEntryModel (e).StopAsync ())
                        .ToList ();

            // Remove time entries we just stopped:
            runningEntries.Clear ();
            runningEntries.Add (runningEntry);
            UpdateProperties ();

            await Task.WhenAll (tasks).ConfigureAwait (false);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged (string propertyName)
        {
            if (changedProperties != null) {
                changedProperties.Add (propertyName);
            } else {
                var handler = PropertyChanged;
                if (handler != null) {
                    handler (this, new PropertyChangedEventArgs (propertyName));
                }
            }
        }

        private void BatchPropertyChanges (Action action)
        {
            var batch = changedProperties = new HashSet<string> ();
            action ();
            changedProperties = null;

            var handler = PropertyChanged;
            if (handler != null) {
                foreach (var prop in batch) {
                    handler (this, new PropertyChangedEventArgs (prop));
                }
            }
        }

        public TimeEntryData Running
        {
            get { return backingRunningEntry; }
            private set {
                if (backingRunningEntry == value) {
                    return;
                }
                backingRunningEntry = value;
                OnPropertyChanged (PropertyRunning);
            }
        }

        public TimeEntryData Draft
        {
            get { return backingDraftEntry; }
            private set {
                if (backingDraftEntry == value) {
                    return;
                }
                backingDraftEntry = value;
                OnPropertyChanged (PropertyDraft);
            }
        }

        public TimeEntryData Active
        {
            get { return backingActiveEntry; }
            private set {
                if (backingActiveEntry == value) {
                    return;
                }
                backingActiveEntry = value;
                OnPropertyChanged (PropertyActive);
            }
        }
    }
}
