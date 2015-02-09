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
using Toggl.Phoebe;
using Toggl.Phoebe.Data;
using Toggl.Phoebe.Data.DataObjects;
using Toggl.Phoebe.Data.Models;
using Toggl.Phoebe.Net;
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
        private int runningProjectColor;
        public const string WidgetCommand = "command";
        public const string CommandInitial = "initial";
        public const string CommandActionButton = "actionButton";


        public override void OnStart (Intent intent, int startId)
        {
            base.OnStart (intent, startId);
            context = this;
            if (intent.Extras.ContainsKey (WidgetProvider.ExtraAppWidgetIds)) {
                appWidgetIds = intent.GetIntArrayExtra (WidgetProvider.ExtraAppWidgetIds);
            }
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
            if (timeEntryManager == null && IsLogged) {
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

        private PendingIntent LogInButtonIntent()
        {
            var loginIntent = new Intent (Intent.ActionMain)
            .AddCategory (Intent.CategoryLauncher)
            .AddFlags (ActivityFlags.NewTask)
            .SetComponent (
                new ComponentName (
                    ApplicationContext.PackageName,
                    "toggl.joey.ui.activities.LoginActivity"
                )
            );

            return PendingIntent.GetActivity (context, 0, loginIntent, PendingIntentFlags.UpdateCurrent);
        }

        private async void Pulse ()
        {
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
            var views = new RemoteViews (context.PackageName, Resource.Layout.keyguard_widget);

            if (CurrentState == TimeEntryState.Running) {
                views.SetInt (Resource.Id.WidgetActionButton, "setBackgroundColor", Resources.GetColor (Resource.Color.bright_red));
                views.SetInt (Resource.Id.WidgetActionButton, "setText", Resource.String.TimerStopButtonText);
                views.SetInt (Resource.Id.WidgetColorView, "setColorFilter", RunningProjectColor);
                views.SetViewVisibility (Resource.Id.WidgetRunningEntry, Android.Views.ViewStates.Visible);
                views.SetTextViewText (Resource.Id.WidgetRunningDescriptionTextView, CurrentDescription);
            } else {
                views.SetInt (Resource.Id.WidgetActionButton, "setBackgroundColor", Resources.GetColor (Resource.Color.bright_green));
                views.SetInt (Resource.Id.WidgetActionButton, "setText", Resource.String.TimerStartButtonText);
                views.SetViewVisibility (Resource.Id.WidgetRunningEntry, Android.Views.ViewStates.Invisible);
            }

            var adapterServiceIntent = new Intent (context, typeof (WidgetListViewService));
            adapterServiceIntent.PutExtra (AppWidgetManager.ExtraAppwidgetId, appWidgetIds[0]);
            adapterServiceIntent.SetData (Android.Net.Uri.Parse (adapterServiceIntent.ToUri (IntentUriType.Scheme)));

            views.SetRemoteAdapter (appWidgetIds[0], Resource.Id.WidgetRecentEntriesListView, adapterServiceIntent);

            var listItemIntent = new Intent (context, typeof (StartNewTimeEntryService.Receiver));
            listItemIntent.SetAction ("startEntry");
            listItemIntent.SetData (Android.Net.Uri.Parse (listItemIntent.ToUri (IntentUriType.Scheme)));
            var pendingIntent = PendingIntent.GetBroadcast (context, 0, listItemIntent, PendingIntentFlags.UpdateCurrent);
            views.SetPendingIntentTemplate (Resource.Id.WidgetRecentEntriesListView, pendingIntent);

            manager.NotifyAppWidgetViewDataChanged (appWidgetIds[0], Resource.Id.WidgetRecentEntriesListView);
            views.SetOnClickPendingIntent (Resource.Id.WidgetActionButton, ActionButtonIntent());
            views.SetTextViewText (Resource.Id.WidgetDuration, CurrentDuration);
            remoteViews = views;
        }

        private void LogInNotice()
        {
            EnsureAdapter();
            var views = new RemoteViews (context.PackageName, Resource.Layout.widget_login);
            views.SetOnClickPendingIntent (Resource.Id.WidgetLoginButton, LogInButtonIntent());
            remoteViews = views;
        }

        private async void FetchProjectColor()
        {
            var store = ServiceContainer.Resolve<IDataStore> ();
            var project = await store.Table<ProjectData> ()
                          .QueryAsync (r => r.Id == ActiveTimeEntryData.ProjectId);
            runningProjectColor = project.Count > 0 ?  project [0].Color : 0;
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

        private bool IsLogged
        {
            get {

                var authManager = ServiceContainer.Resolve<AuthManager> ();
                return authManager.IsAuthenticated;
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


        public WidgetListService (Context ctx, Intent intent)
        {
            context = ctx;
            FetchData ();
        }

        public long GetItemId (int position)
        {
            return position;
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
            remoteView.SetOnClickFillInIntent (Resource.Id.WidgetContinueImageButton, new Intent().PutExtra (FillIntentExtraKey, rowData.FillIntentBundle));
            remoteView.SetViewVisibility (Resource.Id.WidgetColorView, rowData.HasProject ? ViewStates.Visible: ViewStates.Gone);
            remoteView.SetTextViewText (Resource.Id.DescriptionTextView, rowData.Description);
            remoteView.SetTextViewText (Resource.Id.ProjectTextView, rowData.Project);
            remoteView.SetTextViewText (Resource.Id.DurationTextView, rowData.Duration.ToString (@"hh\:mm\:ss"));
            return remoteView;
        }

        public void OnCreate ()
        {
        }

        public void OnDataSetChanged ()
        {
            FetchData ();
        }

        public void OnDestroy ()
        {
        }

        private async void FetchData (int maxCount = 3)
        {
            var store = ServiceContainer.Resolve<IDataStore> ();
            var user = ServiceContainer.Resolve<AuthManager> ().User;
            var queryStartDate = Time.UtcNow - TimeSpan.FromDays (9);
            var query = store.Table<TimeEntryData> ()
                        .OrderBy (r => r.StartTime, false)
                        .Take (maxCount)
                        .Where (r => r.DeletedAt == null
                                && r.UserId == user.Id
                                && r.State != TimeEntryState.New
                                && r.StartTime >= queryStartDate);
            var entries = await query.QueryAsync ().ConfigureAwait (false);

            var bufferList = new List<ListEntryData> ();
            foreach (var entry in entries) {
                var project = await FetchProjectData (entry.ProjectId ?? Guid.Empty);

                var entryData = new ListEntryData();
                entryData.Id = entry.Id;
                entryData.Description = String.IsNullOrEmpty (entry.Description) ? "(no description)": entry.Description;
                entryData.Duration =  GetDuration (entry.StartTime, entry.StopTime ?? DateTime.Now);
                entryData.Project = String.IsNullOrEmpty (project.Name) ? "(no project)": project.Name;
                entryData.HasProject = String.IsNullOrEmpty (project.Name) ? false : true;
                entryData.ProjectColor = project.Color;
                entryData.State = entry.State;
                entryData.FillIntentBundle.PutString ("EntryId", entry.Id.ToString());

                bufferList.Add (entryData);
            }
            dataObject = bufferList;
        }

        private async Task<ProjectData> FetchProjectData (Guid projectId)
        {
            var store = ServiceContainer.Resolve<IDataStore> ();
            var project = await store.Table<ProjectData> ()
                          .QueryAsync (r => r.Id == projectId);

            if (projectId == Guid.Empty) {
                return new ProjectData(); // needs a better empty fallback
            }

            return project[0];
        }


        private TimeSpan GetDuration (DateTime startTime, DateTime stopTime)
        {
            if (startTime == DateTime.MinValue) {
                return TimeSpan.Zero;
            }

            var duration = stopTime - startTime;
            if (duration < TimeSpan.Zero) {
                duration = TimeSpan.Zero;
            }
            return duration;
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
    }

    public class ListEntryData
    {
        public Guid Id;
        public string Description;
        public bool HasProject;
        public string Project;
        public int ProjectColor;
        public TimeSpan Duration;
        public TimeEntryState State;
        public Bundle FillIntentBundle = new Bundle(); // For startEntryService.
    }
}
