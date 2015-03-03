using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Timers;
using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Views;
using Android.Widget;
using Toggl.Phoebe;
using Toggl.Phoebe.Data;
using Toggl.Phoebe.Data.Models;
using Toggl.Phoebe.Data.Views;
using Toggl.Phoebe.Net;
using XPlatUtils;

namespace Toggl.Joey.Widget
{

    [Service (Exported = false)]
    public class WidgetService : Service
    {
        public WidgetService () : base ()
        {
        }

        public WidgetService (IntPtr javaRef, Android.Runtime.JniHandleOwnership transfer)
        : base (javaRef, transfer)
        {
        }

        private Subscription<SyncFinishedMessage> subscriptionSyncFinishedMessage;

        public override void OnStart (Intent intent, int startId)
        {
            SyncOrStop();
            base.OnStart (intent, startId);
        }

        public override StartCommandResult OnStartCommand (Intent intent, StartCommandFlags flags, int startId)
        {
            OnStart (intent, startId);
            return StartCommandResult.Sticky;
        }

        public override void OnCreate ()
        {
            base.OnCreate ();

            ((AndroidApp)Application).InitializeComponents ();

            var bus = ServiceContainer.Resolve<MessageBus> ();
            subscriptionSyncFinishedMessage = bus.Subscribe<SyncFinishedMessage> (OnSyncFinishedMessage);
        }

        public override void OnDestroy ()
        {
            if (subscriptionSyncFinishedMessage != null) {
                var bus = ServiceContainer.Resolve<MessageBus> ();
                bus.Unsubscribe (subscriptionSyncFinishedMessage);
                subscriptionSyncFinishedMessage = null;
            }

            base.OnDestroy ();
        }

        public override IBinder OnBind (Intent intent)
        {
            return null;
        }

        private void SyncOrStop (bool checkRunning = true)
        {
            var syncManager = ServiceContainer.Resolve<ISyncManager> ();

            // Need to check IsRunning, as it will tell us if the sync actually has finished
            if (checkRunning && syncManager.IsRunning) {
                return;
            }

            syncManager.Run (SyncMode.Pull);
        }

        private void OnSyncFinishedMessage (SyncFinishedMessage msg)
        {
            SyncOrStop (false);
            WidgetProvider.RefreshWidget (this, WidgetProvider.RefreshCompleteAction);
            StopSelf();
        }
    }
}