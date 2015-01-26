using System;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Support.V4.Content;
using Toggl.Phoebe.Analytics;
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
                var startTask = StartNewRunning ();

                var app = Application as AndroidApp;
                if (app != null) {
                    app.InitializeComponents ();
                }

                await startTask;
            } finally {
                Receiver.CompleteWakefulIntent (intent);
                StopSelf (startId);
            }
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
                StartWakefulService (context, serviceIntent);
            }
        }
    }
}
