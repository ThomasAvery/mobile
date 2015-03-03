using System;
using System.ComponentModel;
using Android.App;
using Android.Content;
using Toggl.Joey.Data;
using Toggl.Joey.UI.Activities;
using Toggl.Phoebe;
using Toggl.Phoebe.Data;
using Toggl.Phoebe.Data.DataObjects;
using Toggl.Phoebe.Data.Models;
using Toggl.Phoebe.Data.Utils;
using Toggl.Phoebe.Net;
using XPlatUtils;
using NotificationCompat = Android.Support.V4.App.NotificationCompat;

namespace Toggl.Joey
{
    sealed class AndroidNotificationManager : IDisposable
    {
        private const int IdleNotifId = 40;
        private const int RunningNotifId = 42;
        private readonly Context ctx;
        private readonly NotificationManager notificationManager;
        private readonly NotificationCompat.Builder runningBuilder;
        private readonly NotificationCompat.Builder idleBuilder;
        private PropertyChangeTracker propertyTracker;
        private ActiveTimeEntryManager timeEntryManager;
        private Subscription<SettingChangedMessage> subscriptionSettingChanged;
        private Subscription<AuthChangedMessage> subscriptionAuthChanged;
        private TimeEntryModel backingRunningTimeEntry;

        public AndroidNotificationManager ()
        {
            ctx = ServiceContainer.Resolve<Context> ();
            notificationManager = (NotificationManager)ctx.GetSystemService (Context.NotificationService);
            runningBuilder = CreateRunningNotificationBuilder (ctx);
            idleBuilder = CreateIdleNotificationBuilder (ctx);
            propertyTracker = new PropertyChangeTracker ();

            timeEntryManager = ServiceContainer.Resolve<ActiveTimeEntryManager> ();
            timeEntryManager.PropertyChanged += OnActiveTimeEntryManagerPropertyChanged;

            var bus = ServiceContainer.Resolve<MessageBus> ();
            subscriptionSettingChanged = bus.Subscribe<SettingChangedMessage> (OnSettingChanged);
            subscriptionAuthChanged = bus.Subscribe<AuthChangedMessage> (OnAuthChanged);

            SyncRunningModel ();
            SyncNotification ();
        }

        public void Dispose ()
        {
            var bus = ServiceContainer.Resolve<MessageBus> ();
            if (propertyTracker != null) {
                propertyTracker.Dispose ();
                propertyTracker = null;
            }
            if (subscriptionSettingChanged != null) {
                bus.Unsubscribe (subscriptionSettingChanged);
                subscriptionSettingChanged = null;
            }
            if (subscriptionAuthChanged != null) {
                bus.Unsubscribe (subscriptionAuthChanged);
                subscriptionAuthChanged = null;
            }
            if (timeEntryManager != null) {
                timeEntryManager.PropertyChanged -= OnActiveTimeEntryManagerPropertyChanged;
                timeEntryManager = null;
            }
            if (propertyTracker != null) {
                propertyTracker.Dispose ();
                propertyTracker = null;
            }
        }

        private void OnActiveTimeEntryManagerPropertyChanged (object sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == ActiveTimeEntryManager.PropertyRunning) {
                SyncRunningModel ();
                SyncNotification ();
            }
        }

        private TimeEntryData RunningTimeEntryData
        {
            get {
                if (timeEntryManager == null) {
                    return null;
                }
                return timeEntryManager.Running;
            }
        }

        private void SyncRunningModel ()
        {
            var data = RunningTimeEntryData;
            if (data != null) {
                if (backingRunningTimeEntry == null) {
                    backingRunningTimeEntry = new TimeEntryModel (data);
                } else {
                    backingRunningTimeEntry.Data = data;
                }
            }
        }

        private TimeEntryModel RunningTimeEntry
        {
            get {
                if (RunningTimeEntryData == null) {
                    return null;
                }
                return backingRunningTimeEntry;
            }
        }

        private void ResetTrackedObservables ()
        {
            if (propertyTracker == null) {
                return;
            }

            propertyTracker.MarkAllStale ();

            var model = RunningTimeEntry;
            if (model != null) {
                propertyTracker.Add (model, HandleTimeEntryPropertyChanged);

                if (model.Project != null) {
                    propertyTracker.Add (model.Project, HandleProjectPropertyChanged);
                }

                if (model.Task != null) {
                    propertyTracker.Add (model.Task, HandleTaskPropertyChanged);
                }
            }

            propertyTracker.ClearStale ();
        }

        private void HandleTimeEntryPropertyChanged (string prop)
        {
            if (prop == TimeEntryModel.PropertyProject
                    || prop == TimeEntryModel.PropertyTask
                    || prop == TimeEntryModel.PropertyDescription
                    || prop == TimeEntryModel.PropertyStartTime) {
                SyncNotification ();
            }
        }

        private void HandleProjectPropertyChanged (string prop)
        {
            if (prop == ProjectModel.PropertyName) {
                SyncNotification ();
            }
        }

        private void HandleTaskPropertyChanged (string prop)
        {
            if (prop == TaskModel.PropertyName) {
                SyncNotification ();
            }
        }

        private void OnSettingChanged (SettingChangedMessage msg)
        {
            if (msg.Name == SettingsStore.PropertyIdleNotification) {
                SyncNotification ();
            }
        }

        private void OnAuthChanged (AuthChangedMessage msg)
        {
            SyncNotification ();
        }

        private void SyncNotification ()
        {
            var currentTimeEntry = RunningTimeEntry;
            ResetTrackedObservables ();

            var authManager = ServiceContainer.Resolve<AuthManager> ();
            if (!authManager.IsAuthenticated) {
                notificationManager.Cancel (RunningNotifId);
                notificationManager.Cancel (IdleNotifId);
            } else if (currentTimeEntry == null) {
                notificationManager.Cancel (RunningNotifId);
                var settings = ServiceContainer.Resolve<SettingsStore> ();
                if (settings.IdleNotification) {
                    notificationManager.Notify (IdleNotifId, idleBuilder.Build ());
                } else {
                    notificationManager.Cancel (IdleNotifId);
                }
            } else {
                notificationManager.Cancel (IdleNotifId);
                var correction = ServiceContainer.Resolve<TimeCorrectionManager> ().Correction;
                var startTime = currentTimeEntry.StartTime - correction;
                runningBuilder
                .SetContentTitle (GetProjectName (currentTimeEntry))
                .SetContentText (GetDescription (currentTimeEntry))
                .SetWhen ((long)startTime.ToUnix ().TotalMilliseconds);

                notificationManager.Notify (RunningNotifId, runningBuilder.Build ());
            }
        }

        private string GetProjectName (TimeEntryModel entry)
        {
            if (entry == null) {
                return null;
            } else if (entry.Project != null) {
                return entry.Project.Name;
            } else {
                return ctx.Resources.GetString (Resource.String.RunningNotificationNoProject);
            }
        }

        private string GetDescription (TimeEntryModel entry)
        {
            string description = entry.Description;
            if (String.IsNullOrWhiteSpace (description)) {
                description = ctx.Resources.GetString (Resource.String.RunningNotificationNoDescription);
            }
            return description;
        }

        private static NotificationCompat.Builder CreateRunningNotificationBuilder (Context ctx)
        {
            var res = ctx.Resources;

            var openIntent = new Intent (ctx, typeof (MainDrawerActivity));
            openIntent.SetAction (Intent.ActionMain);
            openIntent.AddCategory (Intent.CategoryLauncher);
            var pendingOpenIntent = PendingIntent.GetActivity (ctx, 0, openIntent, 0);

            var stopIntent = new Intent (ctx, typeof (StopRunningTimeEntryService.Receiver));
            var pendingStopIntent = PendingIntent.GetBroadcast (ctx, 0, stopIntent, PendingIntentFlags.UpdateCurrent);

            return new NotificationCompat.Builder (ctx)
                   .SetAutoCancel (false)
                   .SetUsesChronometer (true)
                   .SetOngoing (true)
                   .SetSmallIcon (Resource.Drawable.IcNotificationIcon)
                   .AddAction (Resource.Drawable.IcActionStop, res.GetString (Resource.String.RunningNotificationStopButton), pendingStopIntent)
//                    .AddAction (Resource.Drawable.IcActionEdit, res.GetString (Resource.String.RunningNotificationEditButton), editIntent)
                   .SetContentIntent (pendingOpenIntent);
        }

        private static NotificationCompat.Builder CreateIdleNotificationBuilder (Context ctx)
        {
            var res = ctx.Resources;

            var openIntent = new Intent (ctx, typeof (MainDrawerActivity));
            openIntent.SetAction (Intent.ActionMain);
            openIntent.AddCategory (Intent.CategoryLauncher);
            var pendingOpenIntent = PendingIntent.GetActivity (ctx, 0, openIntent, 0);

            return new NotificationCompat.Builder (ctx)
                   .SetAutoCancel (false)
                   .SetOngoing (true)
                   .SetSmallIcon (Resource.Drawable.IcNotificationIconIdle)
                   .SetContentIntent (pendingOpenIntent)
                   .SetContentTitle (res.GetString (Resource.String.IdleNotificationTitle))
                   .SetContentText (res.GetString (Resource.String.IdleNotificationText));
        }
    }
}
