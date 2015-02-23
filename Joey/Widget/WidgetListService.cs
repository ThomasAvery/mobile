using System;
using System.Collections.Generic;
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Widget;
using Toggl.Phoebe;
using XPlatUtils;

namespace Toggl.Joey.Widget
{
    [Service (Exported = false, Permission = Android.Manifest.Permission.BindRemoteviews)]
    public class WidgetListViewService : RemoteViewsService
    {
        public override IRemoteViewsFactory OnGetViewFactory (Intent intent)
        {
            return new WidgetListService (ApplicationContext);
        }
    }

    public class WidgetListService : Java.Lang.Object, RemoteViewsService.IRemoteViewsFactory
    {
        public const string FillIntentExtraKey = "listItemAction";
        private readonly List<WidgetSyncManager.WidgetEntryData> itemList = new  List<WidgetSyncManager.WidgetEntryData> ();
        private Context context;

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

        public WidgetListService (Context ctx)
        {
            context = ctx;
        }

        public long GetItemId (int position)
        {
            return position;
        }

        public void OnCreate ()
        {
            itemList.AddRange (UpdateService.LastEntries);
        }

        public void OnDestroy ()
        {
        }

        public RemoteViews GetViewAt (int position)
        {
            var remoteView = new RemoteViews (context.PackageName, Resource.Layout.widget_list_item);
            var rowData = itemList [position];

            // set if is running
            if (rowData.IsRunning) {
                remoteView.SetImageViewResource (Resource.Id.WidgetContinueImageButton, Resource.Drawable.IcWidgetStop);
            } else {
                remoteView.SetImageViewResource (Resource.Id.WidgetContinueImageButton, Resource.Drawable.IcWidgetPlay);
            }

            // set color
            remoteView.SetInt (Resource.Id.WidgetColorView, "setColorFilter", Color.ParseColor (rowData.Color));
            remoteView.SetOnClickFillInIntent (Resource.Id.WidgetContinueImageButton, ConstructFillIntent (rowData));

            // set content
            remoteView.SetTextViewText (
                Resource.Id.DescriptionTextView,
                String.IsNullOrWhiteSpace (rowData.Description) ?  "(no description)" : rowData.Description);
            remoteView.SetTextViewText (
                Resource.Id.ProjectTextView,
                String.IsNullOrWhiteSpace (rowData.ProjectName) ?  "(no project)" : rowData.ProjectName);
            remoteView.SetTextViewText (Resource.Id.DurationTextView, rowData.TimeValue);

            return remoteView;
        }

        private Intent ConstructFillIntent (WidgetSyncManager.WidgetEntryData entryData)
        {
            var intentBundle = new Bundle();
            intentBundle.PutString ("EntryId", entryData.Id);
            return new Intent().PutExtra (FillIntentExtraKey, intentBundle);
        }

        public void OnDataSetChanged ()
        {
            itemList.Clear ();
            itemList.AddRange (UpdateService.LastEntries);
        }

        public int Count
        {
            get {
                return itemList.Count;
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