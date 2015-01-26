using System;
using System.Threading.Tasks;
using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Widget;
using Toggl.Phoebe.Data;
using Toggl.Phoebe.Data.DataObjects;
using XPlatUtils;

namespace Toggl.Joey.Net
{
    [Service]
    public class WidgetService : Service
    {
        private Context context;
        private ActiveTimeEntryManager timeEntryManager;
        private RemoteViews remoteViews;
        private AppWidgetManager manager;
        private int[] appWidgetIds;
        public const string WidgetCommand = "command";
        public const string CommandInitial = "initial";
        public const string CommandActionButton = "actionButton";

        public override void OnStart (Intent intent, int startId)
        {
            base.OnStart (intent, startId);
            context = this;
            appWidgetIds = intent.GetIntArrayExtra (HomescreenWidgetProvider.ExtraAppWidgetIds);
            if (intent.Action != null) {
                if (intent.Action == CommandActionButton) {
                    if (CurrentState ==TimeEntryState.Running) {
                        StopRunning();
                        return;
                    } else {
                        StartBlankRunning();
                        return;
                    }
                }
            }
            Pulse ();
        }

        private void StopRunning()
        {
            var stopIntent = new Intent (context, typeof (StopRunningTimeEntryService.Receiver));
            context.SendBroadcast (stopIntent);
        }

        private void StartBlankRunning()
        {
            var startIntent = new Intent (context, typeof (StartNewTimeEntryService.Receiver));
            context.SendBroadcast (startIntent);
            LaunchTogglApp();
        }

        private void LaunchTogglApp()
        {
            var startAppIntent = new Intent ("android.intent.action.MAIN");
            startAppIntent.AddCategory ("android.intent.category.LAUNCHER");
            startAppIntent.AddFlags (ActivityFlags.NewTask);
            startAppIntent.SetComponent (new ComponentName (context.PackageName, "toggl.joey.ui.activities.MainDrawerActivity"));
            var startBundle = new Bundle ();
            startBundle.PutString ("testKey", "testValue");
            startAppIntent.PutExtras (startBundle);
            context.StartActivity (startAppIntent);
        }

        private void EnsureAdapter()
        {
            if (timeEntryManager == null) {
                timeEntryManager = ServiceContainer.Resolve<ActiveTimeEntryManager> ();
            }

            if (manager == null) {
                manager = AppWidgetManager.GetInstance (this.ApplicationContext);
            }
        }

        private PendingIntent ActionButtonIntent()
        {
            var actionButtonIntent = new Intent (context, typeof (WidgetService));
            actionButtonIntent.SetAction (CommandActionButton);
            return PendingIntent.GetService (context, 0, actionButtonIntent, PendingIntentFlags.UpdateCurrent);
        }

        private async void Pulse ()
        {
            RefreshViews ();

            manager.UpdateAppWidget (appWidgetIds, remoteViews);
            manager.NotifyAppWidgetViewDataChanged (appWidgetIds, remoteViews.LayoutId);

            await Task.Delay (TimeSpan.FromMilliseconds (1000));
            Pulse();
        }

        private TimeEntryState CurrentState
        {
            get {
                return timeEntryManager.Active.State;
            }
        }

        private void RefreshViews ()
        {
            EnsureAdapter();
            RemoteViews views = new RemoteViews (context.PackageName, Resource.Layout.homescreen_widget);

            if (CurrentState == TimeEntryState.Running) {
                views.SetInt (Resource.Id.WidgetActionButton, "setBackgroundColor", Color.Red);
                views.SetInt (Resource.Id.WidgetActionButton, "setText", Resource.String.TimerStopButtonText);
            } else {
                views.SetInt (Resource.Id.WidgetActionButton, "setBackgroundColor", Color.Green);
                views.SetInt (Resource.Id.WidgetActionButton, "setText", Resource.String.TimerStartButtonText);
            }
            views.SetOnClickPendingIntent (Resource.Id.WidgetActionButton, ActionButtonIntent());
            views.SetTextViewText (Resource.Id.WidgetDuration, CurrentDuration());
            remoteViews = views;
        }

        private string CurrentDuration()
        {
            if (CurrentState != TimeEntryState.Running) {
                return "00:00:00";
            }
            var activeTE = timeEntryManager.Active;
            var duration = DateTime.Now - activeTE.StartTime;
            return duration.ToString (@"hh\:mm\:ss");
        }

        private TimeEntryData ActiveTimeEntryData
        {
            get {
                if (timeEntryManager == null) {
                    return null;
                }
                return timeEntryManager.Active;
            }
        }

        public override IBinder OnBind (Intent intent)
        {
            return null;
        }
    }
}
