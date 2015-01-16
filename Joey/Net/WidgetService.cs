using System;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Widget;
using Android.Appwidget;
using Toggl.Phoebe.Data;
using XPlatUtils;
using Android.Graphics;
using System.Threading.Tasks;

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
        public const string CommandStart = "start";
        public const string CommandStop = "stop";

        public override void OnStart (Intent intent, int startId)
        {
            base.OnStart (intent, startId);
            context = this;
            if (intent != null && intent.Action.Equals (CommandStart)) {
                StartEntryAndTogglApp ();
            }
            appWidgetIds = intent.GetIntArrayExtra (HomescreenWidgetProvider.ExtraAppWidgetIds);
            Pulse ();
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
        private void StartEntryAndTogglApp()
        {

        }

        private void AttachEvents()
        {

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
            var startAppIntent = new Intent ("android.intent.action.MAIN");
            startAppIntent.AddCategory ("android.intent.category.LAUNCHER");
            startAppIntent.AddFlags (ActivityFlags.NoAnimation);
            startAppIntent.SetComponent (new ComponentName (context.PackageName, "toggl.joey.ui.activities.MainDrawerActivity"));
            var startBundle = new Bundle ();
            startBundle.PutString ("testKey", "testValue");
            startAppIntent.PutExtras (startBundle);
            var pendingIntent = PendingIntent.GetActivity (context, 0, startAppIntent, 0);
            views.SetOnClickPendingIntent (Resource.Id.WidgetActionButton, pendingIntent);
            views.SetTextViewText (Resource.Id.WidgetDuration, CurrentDuration());
            remoteViews = views;
        }

        private void StopRunningTimeEntry()
        {
            EnsureAdapter();
            var activeTimeEntryData = timeEntryManager.Active;
            activeTimeEntryData.StopTime = DateTime.Now;
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

        public override IBinder OnBind (Intent intent)
        {
            return null;
        }
    }
}

