using System;
using System.Threading.Tasks;
using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Views;
using Android.Widget;
using Toggl.Phoebe;
using XPlatUtils;

namespace Toggl.Joey.Widget
{
    [BroadcastReceiver (Label = "@string/WidgetName")]
    [IntentFilter (new  [] { "android.appwidget.action.APPWIDGET_UPDATE" })]
    [MetaData ("android.appwidget.provider", Resource = "@xml/widget_info")]

    public class WidgetProvider : AppWidgetProvider
    {
        public const string OpenAction = "com.toggl.timer.widget.OPEN_ENTRY";
        public const string RefreshListAction = "com.toggl.timer.widget.REFRESH_CONTENT";
        public const string RefreshCompleteAction = "com.toggl.timer.widget.REFRESH_COMPLETE";
        public static readonly string ExtraAppWidgetIds = "appWidgetIds";

        private Context context;
        private int[] appWidgetIds;
        private static HandlerThread workerThread;
        private static Handler workerQueue;

        private IWidgetUpdateService widgetUpdateService;

        private IWidgetUpdateService UpdateService
        {
            get {
                if (widgetUpdateService == null) {
                    widgetUpdateService = ServiceContainer.Resolve<IWidgetUpdateService> ();
                }
                return widgetUpdateService;
            }
        }

        private AppWidgetManager widgetManager;

        private AppWidgetManager WidgetManager
        {
            get {
                if (widgetManager == null) {
                    widgetManager = AppWidgetManager.GetInstance (context);
                }
                return widgetManager;
            }
        }

        private int[] AppWidgetIds
        {
            get {
                if (appWidgetIds == null) {
                    var cn = new ComponentName ( context, Java.Lang.Class.FromType (typeof (WidgetProvider)));
                    appWidgetIds = WidgetManager.GetAppWidgetIds (cn);
                }
                return appWidgetIds;
            }
        }

        private bool IsRunning
        {
            get {
                // Check if an entry is running.
                var isRunning = false;
                foreach (var item in UpdateService.LastEntries) {
                    isRunning = isRunning || item.IsRunning;
                }
                return isRunning;
            }
        }

        public WidgetProvider()
        {
            // Start the worker thread
            workerThread = new HandlerThread ("com.toggl.timer.widgetprovider");
            workerThread.Start();
            workerQueue = new Handler (workerThread.Looper);
        }

        public override void OnEnabled (Context context)
        {
            var serviceIntent = new Intent (context, typeof (WidgetService));
            context.StartService (serviceIntent);
        }

        public override void OnUpdate (Context context, AppWidgetManager appWidgetManager, int[] appWidgetIds)
        {
            this.context = context;
            this.appWidgetIds = appWidgetIds;

            // Setup widget UI.
            SetupWidget ();

            base.OnUpdate (context, appWidgetManager, appWidgetIds);
        }

        public override void OnReceive (Context context, Intent intent)
        {
            String action = intent.Action;

            if (action == RefreshListAction || action == RefreshCompleteAction) {
                ScheduleUpdate (context, action);
            }

            base.OnReceive (context, intent);
        }

        private void SetupWidget ()
        {
            RemoteViews views;

            if (UpdateService.IsUserLogged) {

                views = new RemoteViews (context.PackageName, Resource.Layout.keyguard_widget);

                SetupRunningBtn (context, views, IsRunning);

                var adapterServiceIntent = new Intent (context, typeof (WidgetListViewService));
                adapterServiceIntent.PutExtra (AppWidgetManager.ExtraAppwidgetIds, appWidgetIds);
                adapterServiceIntent.SetData (Android.Net.Uri.Parse (adapterServiceIntent.ToUri (IntentUriType.Scheme)));

                for (int i = 0; i < appWidgetIds.Length; i++) {
                    views.SetRemoteAdapter (appWidgetIds[i], Resource.Id.WidgetRecentEntriesListView, adapterServiceIntent);
                }

                var listItemIntent = new Intent (context, typeof (StartNewTimeEntryService.Receiver));
                listItemIntent.SetData (Android.Net.Uri.Parse (listItemIntent.ToUri (IntentUriType.Scheme)));
                var pendingIntent = PendingIntent.GetBroadcast (context, 0, listItemIntent, PendingIntentFlags.UpdateCurrent);
                views.SetPendingIntentTemplate (Resource.Id.WidgetRecentEntriesListView, pendingIntent);
                views.SetOnClickPendingIntent (Resource.Id.WidgetActionButton, ActionButtonIntent());

            } else {
                views = new RemoteViews (context.PackageName, Resource.Layout.widget_login);
                views.SetOnClickPendingIntent (Resource.Id.WidgetLoginButton, LogInButtonIntent());
            }

            // Update widget view.
            WidgetManager.UpdateAppWidget (appWidgetIds, views);
        }

        private void SetupRunningBtn (Context context, RemoteViews views, bool isRunning)
        {
            var entry = new WidgetSyncManager.WidgetEntryData();

            // Check if an entry is running.
            foreach (var item in UpdateService.LastEntries)
                if ( item.IsRunning) {
                    entry = item;
                }

            if (isRunning) {
                views.SetInt (Resource.Id.WidgetActionButton, "setBackgroundColor", context.Resources.GetColor (Resource.Color.bright_red));
                views.SetInt (Resource.Id.WidgetActionButton, "setText", Resource.String.TimerStopButtonText);
                views.SetInt (Resource.Id.WidgetColorView, "setColorFilter", Color.ParseColor (entry.Color));
                views.SetViewVisibility (Resource.Id.WidgetRunningEntry, ViewStates.Visible);
                views.SetTextViewText (
                    Resource.Id.WidgetRunningDescriptionTextView,
                    String.IsNullOrWhiteSpace (entry.Description) ? "(no description)" : entry.Description);
                views.SetTextViewText (Resource.Id.WidgetDuration, entry.TimeValue);
            } else {
                views.SetInt (Resource.Id.WidgetActionButton, "setBackgroundColor", context.Resources.GetColor (Resource.Color.bright_green));
                views.SetInt (Resource.Id.WidgetActionButton, "setText", Resource.String.TimerStartButtonText);
                views.SetViewVisibility (Resource.Id.WidgetRunningEntry, ViewStates.Invisible);
            }
        }

        private PendingIntent ActionButtonIntent()
        {
            var actionButtonIntent = new Intent (context, typeof (StartNewTimeEntryService.Receiver));

            if (IsRunning) {
                actionButtonIntent = new Intent (context, typeof (StopRunningTimeEntryService.Receiver));
            }
            return PendingIntent.GetBroadcast (context, 0, actionButtonIntent, PendingIntentFlags.UpdateCurrent);
        }

        private PendingIntent LogInButtonIntent()
        {
            var loginIntent = new Intent (Intent.ActionMain)
            .AddCategory (Intent.CategoryLauncher)
            .AddFlags (ActivityFlags.NewTask)
            .SetComponent ( new ComponentName (context.PackageName, typeof (Toggl.Joey.UI.Activities.LoginActivity).FullName));

            return PendingIntent.GetActivity (context, 0, loginIntent, PendingIntentFlags.UpdateCurrent);
        }

        /*
         * Adds a runnable to update the widgets in the worker queue
         * @param context used for creating layouts
         */
        private void ScheduleUpdate (Context context, string action)
        {

            workerQueue.RemoveMessages (0);
            workerQueue.Post (() => {

                var widgetManager = AppWidgetManager.GetInstance (context);
                var cn = new ComponentName ( context, Java.Lang.Class.FromType (typeof (WidgetProvider)));
                var appWidgetIds = widgetManager.GetAppWidgetIds (cn);

                if (action == RefreshCompleteAction) {
                    OnUpdate (context, widgetManager, appWidgetIds);
                } else {
                    var views = new RemoteViews (context.PackageName, Resource.Layout.keyguard_widget);
                    SetupRunningBtn (context, views, IsRunning);
                    widgetManager.NotifyAppWidgetViewDataChanged (appWidgetIds, Resource.Id.WidgetRecentEntriesListView);
                    widgetManager.PartiallyUpdateAppWidget (appWidgetIds, views);
                }
            });
        }

        /**
        * Sends a request to the rich push message to refresh
        * @param context Application context
        */
        public static void RefreshWidget (Context context, string action)
        {
            RefreshWidget (context, action, 0);
        }

        /**
        * Sends a request to the rich push message to refresh with a delay
        * @param context Application context
        * @param delayInMs Delay to wait in milliseconds before sending the request
        */
        public static void RefreshWidget (Context context, string action, long delayInMs)
        {
            Intent refreshIntent = new Intent (context, typeof (WidgetProvider));
            refreshIntent.SetAction (action);

            if (delayInMs > 0) {
                PendingIntent pendingIntent = PendingIntent.GetBroadcast (context, 0, refreshIntent, 0);
                AlarmManager am = (AlarmManager) context.GetSystemService (Context.AlarmService);
                am.Set (AlarmType.RtcWakeup, (long) new TimeSpan (DateTime.Now.Ticks).TotalMilliseconds + delayInMs, pendingIntent);
            } else {
                context.SendBroadcast (refreshIntent);
            }
        }
    }
}
