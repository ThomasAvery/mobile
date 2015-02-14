using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Views;
using Android.Widget;
using Toggl.Phoebe.Data;
using Toggl.Phoebe.Data.Models;
using Toggl.Phoebe.Data.Views;
using Toggl.Phoebe.Net;
using XPlatUtils;

namespace Toggl.Joey.Net
{

    [Service]
    public class WidgetService : Service
    {
        private Context context;
        private RemoteViews remoteViews;
        private AppWidgetManager manager;
        private WidgetDataView widgetDataView;
        private ListEntryData activeEntry;
        private bool isRunning;
        private int[] appWidgetIds;

        public override void OnStart (Intent intent, int startId)
        {
            base.OnStart (intent, startId);
            context = this;
            if (intent.Extras.ContainsKey (WidgetProvider.ExtraAppWidgetIds)) {
                appWidgetIds = intent.GetIntArrayExtra (WidgetProvider.ExtraAppWidgetIds);
            }
            Pulse ();
        }

        private void EnsureAdapter()
        {
            if (manager == null) {
                manager = AppWidgetManager.GetInstance (this.ApplicationContext);
            }

            if (widgetDataView == null) {
                widgetDataView = new WidgetDataView();
            }
        }

        private PendingIntent LogInButtonIntent()
        {
            var loginIntent = new Intent (Intent.ActionMain)
            .AddCategory (Intent.CategoryLauncher)
            .AddFlags (ActivityFlags.NewTask)
            .SetComponent ( new ComponentName (
                                ApplicationContext.PackageName,
                                "toggl.joey.ui.activities.LoginActivity"
                            )
                          );

            return PendingIntent.GetActivity (context, 0, loginIntent, PendingIntentFlags.UpdateCurrent);
        }

        private async void Pulse ()
        {
            EnsureAdapter();
            widgetDataView.Load();
            activeEntry = widgetDataView.Active;
            isRunning = widgetDataView.IsRunning;
            if (IsLogged) {
                RefreshViews ();
            } else {
                LogInNotice ();
            }

            manager.UpdateAppWidget (appWidgetIds, remoteViews);
            manager.NotifyAppWidgetViewDataChanged (appWidgetIds, remoteViews.LayoutId);

            await Task.Delay (TimeSpan.FromMilliseconds (1000));
            Pulse();
        }

        private void RefreshViews ()
        {
            var views = new RemoteViews (context.PackageName, Resource.Layout.keyguard_widget);

            if (isRunning) {
                views.SetInt (Resource.Id.WidgetActionButton, "setBackgroundColor", Resources.GetColor (Resource.Color.bright_red));
                views.SetInt (Resource.Id.WidgetActionButton, "setText", Resource.String.TimerStopButtonText);
                views.SetInt (Resource.Id.WidgetColorView, "setColorFilter", activeEntry.ProjectColor);
                views.SetViewVisibility (Resource.Id.WidgetRunningEntry, ViewStates.Visible);
                views.SetTextViewText (
                    Resource.Id.WidgetRunningDescriptionTextView,
                    String.IsNullOrWhiteSpace (activeEntry.Description) ? "(no description)" : activeEntry.Description);
                views.SetTextViewText (Resource.Id.WidgetDuration, activeEntry.Duration.ToString (@"hh\:mm\:ss"));
            } else {
                views.SetInt (Resource.Id.WidgetActionButton, "setBackgroundColor", Resources.GetColor (Resource.Color.bright_green));
                views.SetInt (Resource.Id.WidgetActionButton, "setText", Resource.String.TimerStartButtonText);
                views.SetViewVisibility (Resource.Id.WidgetRunningEntry, ViewStates.Invisible);
                views.SetTextViewText (Resource.Id.WidgetDuration, "00:00:00");
            }

            var adapterServiceIntent = new Intent (context, typeof (WidgetListViewService));
            adapterServiceIntent.PutExtra (AppWidgetManager.ExtraAppwidgetId, appWidgetIds[0]);
            adapterServiceIntent.SetData (Android.Net.Uri.Parse (adapterServiceIntent.ToUri (IntentUriType.Scheme)));

            views.SetRemoteAdapter (appWidgetIds[0], Resource.Id.WidgetRecentEntriesListView, adapterServiceIntent);

            var listItemIntent = new Intent (context, typeof (StartNewTimeEntryService.Receiver));
            listItemIntent.SetData (Android.Net.Uri.Parse (listItemIntent.ToUri (IntentUriType.Scheme)));
            var pendingIntent = PendingIntent.GetBroadcast (context, 0, listItemIntent, PendingIntentFlags.UpdateCurrent);
            views.SetPendingIntentTemplate (Resource.Id.WidgetRecentEntriesListView, pendingIntent);

            manager.NotifyAppWidgetViewDataChanged (appWidgetIds[0], Resource.Id.WidgetRecentEntriesListView);
            views.SetOnClickPendingIntent (Resource.Id.WidgetActionButton, ActionButtonIntent());
            remoteViews = views;
        }

        private void LogInNotice()
        {
            EnsureAdapter();
            var views = new RemoteViews (context.PackageName, Resource.Layout.widget_login);
            views.SetOnClickPendingIntent (Resource.Id.WidgetLoginButton, LogInButtonIntent());
            remoteViews = views;
        }

        private PendingIntent ActionButtonIntent()
        {
            var actionButtonIntent = StartBlankRunning ();
            if (isRunning) {
                actionButtonIntent = StopRunning ();
            }
            return PendingIntent.GetBroadcast (context, 0, actionButtonIntent, PendingIntentFlags.UpdateCurrent);
        }

        private Intent StopRunning()
        {
            return new Intent (context, typeof (StopRunningTimeEntryService.Receiver));
        }

        private Intent StartBlankRunning()
        {
            return new Intent (context, typeof (StartNewTimeEntryService.Receiver));
        }

        private bool IsLogged
        {
            get {

                var authManager = ServiceContainer.Resolve<AuthManager> ();
                return authManager.IsAuthenticated;
            }
        }

        public override IBinder OnBind (Intent intent)
        {
            return null;
        }
    }

    [Service]
    public class WidgetListViewService : RemoteViewsService
    {
        public override IRemoteViewsFactory OnGetViewFactory (Intent intent)
        {
            return new WidgetListService (ApplicationContext, intent);
        }
    }

    public class WidgetListService : Java.Lang.Object, RemoteViewsService.IRemoteViewsFactory
    {
        public const string FillIntentExtraKey = "listItemAction";

        private List<ListEntryData> dataObject = new List<ListEntryData> ();
        private Context context = null;
        private WidgetDataView widgetDataView;

        public WidgetListService (Context ctx, Intent intent)
        {
            context = ctx;
            FetchData ();
        }

        private void FetchData()
        {
            widgetDataView = new WidgetDataView();
            widgetDataView.Load();
            dataObject = widgetDataView.Data;
        }
        public long GetItemId (int position)
        {
            return position;
        }
        public void OnCreate ()
        {
        }

        public void OnDestroy ()
        {
        }
        public RemoteViews GetViewAt (int position)
        {
            var remoteView = new RemoteViews (context.PackageName, Resource.Layout.widget_list_item);
            var rowData = dataObject [position];

            if (rowData.State == TimeEntryState.Running) {
                remoteView.SetImageViewResource (Resource.Id.WidgetContinueImageButton, Resource.Drawable.IcWidgetStop);
            } else {
                remoteView.SetImageViewResource (Resource.Id.WidgetContinueImageButton, Resource.Drawable.IcWidgetPlay);
            }

            remoteView.SetInt (Resource.Id.WidgetColorView, "setColorFilter", Color.ParseColor (ProjectModel.HexColors [rowData.ProjectColor % ProjectModel.HexColors.Length]));
            remoteView.SetOnClickFillInIntent (Resource.Id.WidgetContinueImageButton, ConstructFillIntent (rowData));
            remoteView.SetViewVisibility (Resource.Id.WidgetColorView, rowData.HasProject ? ViewStates.Visible: ViewStates.Gone);
            remoteView.SetTextViewText (
                Resource.Id.DescriptionTextView,
                String.IsNullOrWhiteSpace (rowData.Description) ?  "(no description)" : rowData.Description);
            remoteView.SetTextViewText (
                Resource.Id.ProjectTextView,
                String.IsNullOrWhiteSpace (rowData.Project) ?  "(no project)" : rowData.Project);
            remoteView.SetTextViewText (Resource.Id.DurationTextView, rowData.Duration.ToString (@"hh\:mm\:ss"));
            return remoteView;
        }

        private Intent ConstructFillIntent (ListEntryData entryData)
        {
            var intentBundle = new Bundle();
            intentBundle.PutString ("EntryId", entryData.Id.ToString() );
            return new Intent().PutExtra (FillIntentExtraKey, intentBundle);
        }

        public void OnDataSetChanged ()
        {
            widgetDataView.Load();
            dataObject = widgetDataView.Data;
        }

        public int Count
        {
            get {

                if (dataObject == null) {

                }
                return dataObject.Count;
            }
        }

        public bool HasStableIds
        {
            get {
                return true;
            }
        }

        public RemoteViews LoadingView
        {
            get {
                return (RemoteViews) null;
            }
        }

        public int ViewTypeCount
        {
            get {
                return 1;
            }
        }
    }
}
