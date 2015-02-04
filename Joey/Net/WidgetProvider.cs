using System;
using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.OS;
using Android.Widget;
using Toggl.Phoebe.Data;

namespace Toggl.Joey.Net
{
    [BroadcastReceiver]
    [IntentFilter (new string [] { "android.appwidget.action.APPWIDGET_UPDATE" })]
    [MetaData ("android.appwidget.provider", Resource = "@xml/widget_info")]

    public class WidgetProvider : AppWidgetProvider
    {
        private Context context;
        public static readonly string ExtraAppWidgetIds = "appWidgetIds";

        public override void OnUpdate (Context context, AppWidgetManager appWidgetManager, int[] appWidgetIds)
        {
            this.context = context;
            var serviceIntent = new Intent (context, typeof (WidgetService));
            serviceIntent.SetAction (WidgetService.CommandInitial);
            var serviceBundle = new Bundle ();
            serviceBundle.PutIntArray (ExtraAppWidgetIds, appWidgetIds);
            serviceIntent.PutExtras (serviceBundle);
            context.StartService (serviceIntent);
        }
    }
}
