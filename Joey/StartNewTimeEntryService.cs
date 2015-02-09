using System;
using System.Linq;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Support.V4.Content;
using Toggl.Joey.Net;
using Toggl.Phoebe.Analytics;
using Toggl.Phoebe.Data;
using Toggl.Phoebe.Data.DataObjects;
using Toggl.Phoebe.Data.Models;
using Toggl.Phoebe.Net;
using XPlatUtils;

namespace Toggl.Joey
{
    [Service (Exported = false)]
    public sealed class StartNewTimeEntryService : Service
    {
        private static readonly string Tag = "StartNewTimeEntryService";
        private Guid userActionGuid;

        public StartNewTimeEntryService () : base ()
        {
        }

        public StartNewTimeEntryService (IntPtr javaRef, Android.Runtime.JniHandleOwnership transfer)
        : base (javaRef, transfer)
        {
        }

        public override async void OnStart (Intent intent, int startId)
        {
            try {
                bool hasKey = intent.HasExtra (WidgetListService.FillIntentExtraKey);
                if (hasKey) {
                    userActionGuid = Guid.Parse (intent.GetBundleExtra (WidgetListService.FillIntentExtraKey).GetString ("EntryId"));
                }

                var app = Application as AndroidApp;
                if (app != null) {
                    app.InitializeComponents ();
                }
                if (hasKey) {
                    var startTask = Continue (userActionGuid);
                    await startTask;
                } else {
                    var startTask = StartNewRunning ();
                    await startTask;
                    LaunchTogglApp();
                }
            } finally {
                Receiver.CompleteWakefulIntent (intent);
                StopSelf (startId);
            }
        }

        private void LaunchTogglApp()
        {
            var startAppIntent = new Intent (Intent.ActionMain)
            .AddCategory (Intent.CategoryLauncher)
            .AddFlags (ActivityFlags.NewTask)
            .SetComponent (
                new ComponentName (
                    ApplicationContext.PackageName,
                    "toggl.joey.ui.activities.MainDrawerActivity"
                )
            );

            ApplicationContext.StartActivity (startAppIntent);
        }

        private static async Task StartNewRunning ()
        {
            var user = ServiceContainer.Resolve<AuthManager> ().User;
            var entryData = new TimeEntryData ();
            entryData.UserId = user.Id;
            entryData.WorkspaceId = user.DefaultWorkspaceId;

            var newTimeEntry = new TimeEntryModel (entryData);
            var startTask = newTimeEntry.StartAsync ();

            await startTask;

            ServiceContainer.Resolve<ITracker> ().SendTimerStartEvent (TimerStartSource.WidgetNew);
        }

        private static async Task Continue (Guid entryId)
        {
            var runningEntryId = await GetRunningGuid();

            await StopCurrentRunning();

            if (entryId == runningEntryId) { // if same TE, then action was Stop.
                return;
            }
            var userId = ServiceContainer.Resolve<AuthManager> ().GetUserId ();
            var dataStore = ServiceContainer.Resolve<IDataStore> ();

            var entry = await dataStore.Table<TimeEntryData> ()
                        .QueryAsync (r =>  r.DeletedAt == null && r.UserId == userId && r.Id == entryId)
                        .ConfigureAwait (false);

            var newStart = new TimeEntryData();
            newStart.UserId = entry[0].UserId;
            newStart.TaskId = entry[0].TaskId;
            newStart.Description = entry[0].Description;
            newStart.WorkspaceId = entry[0].WorkspaceId;
            newStart.ProjectId = entry[0].ProjectId;
            newStart.IsBillable = entry[0].IsBillable;
            newStart.DurationOnly = entry[0].DurationOnly;

            await new TimeEntryModel (newStart).StartAsync();
        }

        private static async Task<Guid> GetRunningGuid()
        {
            var userId = ServiceContainer.Resolve<AuthManager> ().GetUserId ();
            var dataStore = ServiceContainer.Resolve<IDataStore> ();

            var runningEntries = await dataStore.Table<TimeEntryData> ()
                                 .QueryAsync (r => r.State == TimeEntryState.Running && r.DeletedAt == null && r.UserId == userId)
                                 .ConfigureAwait (false);

            if (runningEntries.Count > 0) {
                return runningEntries[0].Id;
            }
            return Guid.Empty;
        }

        private static async Task StopCurrentRunning ()
        {
            var userId = ServiceContainer.Resolve<AuthManager> ().GetUserId ();
            var dataStore = ServiceContainer.Resolve<IDataStore> ();

            // Find running tasks:
            var runningEntries = await dataStore.Table<TimeEntryData> ()
                                 .QueryAsync (r => r.State == TimeEntryState.Running && r.DeletedAt == null && r.UserId == userId)
                                 .ConfigureAwait (false);

            var stopTasks = runningEntries
                            .Select (data => new TimeEntryModel (data).StopAsync ());
            await Task.WhenAll (stopTasks).ConfigureAwait (false);

            // Ping analytics
            ServiceContainer.Resolve<ITracker> ().SendTimerStopEvent (TimerStopSource.Notification);
        }

        public override StartCommandResult OnStartCommand (Intent intent, StartCommandFlags flags, int startId)
        {
            OnStart (intent, startId);

            return StartCommandResult.Sticky;
        }

        public override Android.OS.IBinder OnBind (Intent intent)
        {
            return null;
        }

        [BroadcastReceiver (Exported = true)]
        public sealed class Receiver : WakefulBroadcastReceiver
        {
            public override void OnReceive (Context context, Intent intent)
            {
                var serviceIntent = new Intent (context, typeof (StartNewTimeEntryService));
                if (intent.HasExtra (WidgetListService.FillIntentExtraKey)) {
                    serviceIntent.PutExtra (WidgetListService.FillIntentExtraKey, intent.GetBundleExtra (WidgetListService.FillIntentExtraKey));
                }
                StartWakefulService (context, serviceIntent);
            }
        }
    }
}
