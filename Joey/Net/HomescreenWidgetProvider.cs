using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Toggl.Joey.UI.Activities;
using Toggl.Joey.UI.Components;
using System.Timers;

namespace Toggl.Joey.Net
{

    [BroadcastReceiver]
    [IntentFilter (new string [] { "android.appwidget.action.APPWIDGET_UPDATE" })]
    [MetaData ("android.appwidget.provider", Resource = "@xml/widget_info")]

    public class HomescreenWidgetProvider : AppWidgetProvider
    {
        public void OnReceive(Context context, Intent intent) {
            base.OnReceive(context, intent);
        }
       
        public override void OnUpdate(Context context, AppWidgetManager appWidgetManager, int[] appWidgetIds)
        {
            context.StartService (new Intent (context, typeof (WidgetService)));

            Timer timer = new Timer();

            timer.ScheduleAtFixedRate(
            new TimerTask() {
            public void run() {
                RunOnUiThread(new Runnable() {
                    public void run() {
                        imageView.setImageBitmap(bitmap);
                    }
                });
            }
            }, 5000, 5000);

            WidgetService m = new WidgetService();

//            if(widget == null){
//                Initialize();
//            }
//            var wState = widget.GetRunningDuration();

//            for(int i=0; i < appWidgetIds.Length; i++) {
//                int appWidgetId = appWidgetIds[i];

//                RemoteViews views = new RemoteViews(context.PackageName, Resource.Layout.homescreen_widget);
//                Console.WriteLine("OnUpdate");

//                var serviceIntent = new Intent (context, typeof (WidgetService));
//                serviceIntent.PutExtra ("command", WidgetService.CommandStart);
//                PendingIntent pendingIntent = PendingIntent.GetService (context, 0, serviceIntent, 0);
//                pendingIntent.Send();
//                appWidgetManager.UpdateAppWidget (appWidgetId, views);
//            }
        } 
    }
}
