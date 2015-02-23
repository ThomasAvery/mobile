using System;
using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.Graphics;
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
        public const string RefreshTimeAction = "com.toggl.timer.widget.REFRESH_TIME";
        public const string RefreshListAction = "com.toggl.timer.widget.REFRESH_LIST";
        public static readonly string ExtraAppWidgetIds = "appWidgetIds";

        private Context context;
        private bool isRunning;
        private int[] appWidgetIds;
        private string defaultTimeValue = "00:00:00";

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

        public override void OnUpdate (Context context, AppWidgetManager appWidgetManager, int[] appWidgetIds)
        {
            this.context = context;
            this.appWidgetIds = appWidgetIds;
            SetupWidget ();

            base.OnUpdate (context, appWidgetManager, appWidgetIds);
        }

        public override void OnReceive (Context context, Intent intent)
        {
            this.context = context;
            String action = intent.Action;

            if (action == RefreshTimeAction) {
                var views = new RemoteViews (context.PackageName, Resource.Layout.keyguard_widget);
                views.SetTextViewText (Resource.Id.WidgetDuration, UpdateService.RunningEntryDuration);
                WidgetManager.PartiallyUpdateAppWidget (AppWidgetIds, views);
            }

            if (action == RefreshListAction) {
                WidgetManager.NotifyAppWidgetViewDataChanged (AppWidgetIds, Resource.Id.WidgetRecentEntriesListView);
            }

            base.OnReceive (context, intent);
        }

        private void SetupWidget ()
        {
            RemoteViews views;

            if (UpdateService.IsUserLogged) {

                views = new RemoteViews (context.PackageName, Resource.Layout.keyguard_widget);

                var activeEntry = new WidgetSyncManager.WidgetEntryData();

                // Check if an entry is running.
                isRunning = false;
                foreach (var item in UpdateService.LastEntries) {
                    isRunning = isRunning || item.IsRunning;
                    if ( item.IsRunning) {
                        activeEntry = item;
                    }
                }

                if (isRunning) {
                    views.SetInt (Resource.Id.WidgetActionButton, "setBackgroundColor", Resource.Color.bright_red);
                    views.SetInt (Resource.Id.WidgetActionButton, "setText", Resource.String.TimerStopButtonText);
                    views.SetInt (Resource.Id.WidgetColorView, "setColorFilter", Color.ParseColor (activeEntry.Color));
                    views.SetViewVisibility (Resource.Id.WidgetRunningEntry, ViewStates.Visible);
                    views.SetTextViewText (
                        Resource.Id.WidgetRunningDescriptionTextView,
                        String.IsNullOrWhiteSpace (activeEntry.Description) ? "(no description)" : activeEntry.Description);
                    views.SetTextViewText (Resource.Id.WidgetDuration, activeEntry.TimeValue);
                } else {
                    views.SetInt (Resource.Id.WidgetActionButton, "setBackgroundColor", Resource.Color.bright_green);
                    views.SetInt (Resource.Id.WidgetActionButton, "setText", Resource.String.TimerStartButtonText);
                    views.SetViewVisibility (Resource.Id.WidgetRunningEntry, ViewStates.Invisible);
                    views.SetTextViewText (Resource.Id.WidgetDuration, defaultTimeValue);
                }

                var adapterServiceIntent = new Intent (context, typeof (WidgetListViewService));
                adapterServiceIntent.PutExtra (AppWidgetManager.ExtraAppwidgetId, appWidgetIds[0]);
                adapterServiceIntent.SetData (Android.Net.Uri.Parse (adapterServiceIntent.ToUri (IntentUriType.Scheme)));

                views.SetRemoteAdapter (appWidgetIds[0], Resource.Id.WidgetRecentEntriesListView, adapterServiceIntent);

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

        private PendingIntent ActionButtonIntent()
        {
            var actionButtonIntent = StartBlankRunning ();
            if (isRunning) {
                actionButtonIntent = StopRunning ();
            }
            return PendingIntent.GetBroadcast (context, 0, actionButtonIntent, PendingIntentFlags.UpdateCurrent);
        }

        private PendingIntent LogInButtonIntent()
        {
            var loginIntent = new Intent (Intent.ActionMain)
            .AddCategory (Intent.CategoryLauncher)
            .AddFlags (ActivityFlags.NewTask)
            .SetComponent ( new ComponentName (context.PackageName, "toggl.joey.ui.activities.LoginActivity")
                          );
            return PendingIntent.GetActivity (context, 0, loginIntent, PendingIntentFlags.UpdateCurrent);
        }

        private Intent StopRunning()
        {
            return new Intent (context, typeof (StopRunningTimeEntryService.Receiver));
        }

        private Intent StartBlankRunning()
        {
            return new Intent (context, typeof (StartNewTimeEntryService.Receiver));
        }
    }
}
