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
using Toggl.Phoebe.Data.Models;
using Toggl.Phoebe;
using System.Collections.Generic;
using Toggl.Phoebe.Net;

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
        private int runningProjectColor;
        public const string WidgetCommand = "command";
        public const string CommandInitial = "initial";
        public const string CommandActionButton = "actionButton";
        private List<TimeEntryData> recentTimeEntries;
        private ListView recentEntriesListView;

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

        private Intent StopRunning()
        {
            return new Intent (context, typeof (StopRunningTimeEntryService.Receiver));
        }

        private Intent StartBlankRunning()
        {
            return new Intent (context, typeof (StartNewTimeEntryService.Receiver));
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
            var actionButtonIntent = StartBlankRunning ();
            if (CurrentState == TimeEntryState.Running) {
                actionButtonIntent = StopRunning ();
            }
            return PendingIntent.GetBroadcast (context, 0, actionButtonIntent, PendingIntentFlags.UpdateCurrent);
        }

        private async void Pulse ()
        {
            RefreshViews ();
            Console.WriteLine ("PULSE");
            manager.UpdateAppWidget (appWidgetIds, remoteViews);
            manager.NotifyAppWidgetViewDataChanged (appWidgetIds, remoteViews.LayoutId);

            await Task.Delay (TimeSpan.FromMilliseconds (1000));
            Pulse();
        }

        private TimeEntryState CurrentState
        {
            get {
                if (timeEntryManager != null && timeEntryManager.Active != null) {
                    return timeEntryManager.Active.State;
                }
                return TimeEntryState.New;
            }
        }

        private void RefreshViews ()
        {
            EnsureAdapter();
            RemoteViews views = new RemoteViews (context.PackageName, Resource.Layout.homescreen_widget);
            if (CurrentState == TimeEntryState.Running) {
                views.SetInt (Resource.Id.WidgetActionButton, "setBackgroundColor", Color.Red);
                views.SetInt (Resource.Id.WidgetActionButton, "setText", Resource.String.TimerStopButtonText);
                views.SetInt (Resource.Id.WidgetColorView, "setColorFilter", RunningProjectColor);
                views.SetViewVisibility (Resource.Id.WidgetRunningEntry, Android.Views.ViewStates.Visible);
                views.SetTextViewText (Resource.Id.WidgetRunningDescriptionTextView, CurrentDescription);
            } else {
                views.SetInt (Resource.Id.WidgetActionButton, "setBackgroundColor", Color.Green);
                views.SetInt (Resource.Id.WidgetActionButton, "setText", Resource.String.TimerStartButtonText);
                views.SetViewVisibility (Resource.Id.WidgetRunningEntry, Android.Views.ViewStates.Gone);
            }
//            FetchRecentEntries (3);
            Intent adapterServiceIntent = new Intent (ApplicationContext, typeof (WidgetListService));
            adapterServiceIntent.PutExtra (AppWidgetManager.ExtraAppwidgetIds, appWidgetIds);
            Console.WriteLine ("Setting remote adapter");
//            remoteViews.SetRemoteAdapter(appWidgetIds[0], Resource.Id.WidgetRecentEntriesListView, adapterServiceIntent);
            views.SetOnClickPendingIntent (Resource.Id.WidgetActionButton, ActionButtonIntent());
            views.SetTextViewText (Resource.Id.WidgetDuration, CurrentDuration);
            remoteViews = views;
        }

        private async void FetchProjectColor()
        {
            var store = ServiceContainer.Resolve<IDataStore> ();
            var project = await store.Table<ProjectData> ()
                          .QueryAsync (r => r.Id == ActiveTimeEntryData.ProjectId);
            runningProjectColor = project.Count > 0 ?  project [0].Color : 0;
        }

        private async void FetchRecentEntries (int maxCount = 3)
        {
            var store = ServiceContainer.Resolve<IDataStore> ();

            // Group only items in the past 9 days
            var queryStartDate = Time.UtcNow - TimeSpan.FromDays (9);
            var query = store.Table<TimeEntryData> ()
                        .OrderBy (r => r.StartTime, false)
                        .Take (maxCount)
                        .Where (r => r.DeletedAt == null
                                && r.UserId == ActiveTimeEntryData.UserId
                                && r.State != TimeEntryState.New
                                && r.StartTime >= queryStartDate);
            var entries = await query.QueryAsync ().ConfigureAwait (false);
            recentTimeEntries = entries;
        }

        private string CurrentDescription
        {
            get {
                if (ActiveTimeEntryData != null && ActiveTimeEntryData.Description != null && ActiveTimeEntryData.Description.Length > 0) {
                    return ActiveTimeEntryData.Description;
                }
                return Resources.GetText (Resource.String.RunningWidgetNoDescription);
            }
        }

        private int RunningProjectColor
        {
            get {
//                if (runningProjectColor) {
                FetchProjectColor (); // too much for every call.
//                }
                return Color.ParseColor (ProjectModel.HexColors [runningProjectColor % ProjectModel.HexColors.Length]);
            }
        }

        private string CurrentDuration
        {
            get {
                if (CurrentState != TimeEntryState.Running) {
                    return "00:00:00";
                }
                var activeTE = timeEntryManager.Active;
                var duration = DateTime.Now - activeTE.StartTime;
                return duration.ToString (@"hh\:mm\:ss");
            }
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

    public class WidgetListViewService : RemoteViewsService
    {

        public override IRemoteViewsFactory OnGetViewFactory (Intent intent)
        {
            Console.WriteLine ("OnGetViewFactory");
            return new WidgetListService (ApplicationContext, intent);
        }
    }

    public class WidgetListService : RemoteViewsService.IRemoteViewsFactory
    {
        private List<TimeEntryData> dataObject = new List<TimeEntryData> ();
        private Context context = null;
        private int appWidgetId;

        public WidgetListService (Context ctx, Intent intent)
        {
            Console.WriteLine ("WidgetService");
            context = ctx;
            appWidgetId = intent.GetIntExtra (AppWidgetManager.ExtraAppwidgetIds, AppWidgetManager.InvalidAppwidgetId);
        }

        public long GetItemId (int position)
        {
            return position;
        }

        public RemoteViews GetViewAt (int position)
        {
            Console.WriteLine ("WidgetService GetViewAt");
            var remoteView = new RemoteViews (context.PackageName, Resource.Layout.RecentTimeEntryListItem);
            var timeEntry = dataObject [position]; // Should check if exists.
            var duration = DateTime.Now - timeEntry.StartTime;
            remoteView.SetTextViewText (Resource.Id.DescriptionTextView, timeEntry.Description);
            remoteView.SetTextViewText (Resource.Id.DurationTextView, duration.ToString (@"hh\:mm\:ss"));
            return remoteView;
        }

        public void OnCreate ()
        {
        }

        public void OnDataSetChanged ()
        {
        }

        public void OnDestroy ()
        {
        }

        private async void FetchRecentEntries (int maxCount = 3)
        {
            var store = ServiceContainer.Resolve<IDataStore> ();
            var user = ServiceContainer.Resolve<AuthManager> ().User;

            // Group only items in the past 9 days
            var queryStartDate = Time.UtcNow - TimeSpan.FromDays (9);
            var query = store.Table<TimeEntryData> ()
                        .OrderBy (r => r.StartTime, false)
                        .Take (maxCount)
                        .Where (r => r.DeletedAt == null
                                && r.UserId == user.Id
                                && r.State != TimeEntryState.New
                                && r.StartTime >= queryStartDate);
            var entries = await query.QueryAsync ().ConfigureAwait (false);
            dataObject = entries;
        }

        public int Count
        {
            get {
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

        public void Dispose ()
        {
        }

        public IntPtr Handle
        {
            get {
                return (IntPtr)null;
            }
        }
    }

}
