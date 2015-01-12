
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Appwidget;
using Toggl.Joey.UI.Components;
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
        private ComponentName widgetComponent;
        public static readonly string CommandStart = "start";
        public static readonly string CommandStop = "stop";

        public override void OnStart (Intent intent, int startId)
        {
            var command = intent.GetStringExtra("command");
            context = this;
//            switch (command) {
//                case CommandStop:
//                    StopRunningTimeEntry();
//                case CommandStart:
//
//            }
//            if(command == "start"){
//                StartLastTimeEntry();
//            }
            Pulse();
        }

        private async void SendMessages()
        {
            await Task.Delay(1000);
        }


        private void EnsureAdapter()
        {
            if (timeEntryManager == null) {
                timeEntryManager = ServiceContainer.Resolve<ActiveTimeEntryManager> ();
            }

//            if (remoteViews == null) {
             RefreshViews();
//            }

//            if(manager == null) {
                manager = AppWidgetManager.GetInstance (this);
//            }

//            if(widgetComponent == null) {
            widgetComponent = new ComponentName(this, "Toggl.Joey.Net.HomescreenWidgetProvider");
//            }
            Console.WriteLine("widgetComponent: {0}", widgetComponent);
        }

        private void AttachEvents()
        {
//            remoteViews.SetOnClickPendingIntent (Resource.Id.WidgetActionButton, pendingIntent);
        }

        private async void Pulse ()
        {
            Console.WriteLine("Pulse");
            EnsureAdapter();
            manager.UpdateAppWidget(widgetComponent, remoteViews);
            await Task.Delay (TimeSpan.FromMilliseconds (1000));
            Pulse();
        }

        private void StartLastTimeEntry()
        {
            EnsureAdapter();
            var activeTimeEntryData = timeEntryManager.Active;
            Console.WriteLine("starting last entry");
        }

        private void StopRunningTimeEntry()
        {
            EnsureAdapter();
            var activeTimeEntryData = timeEntryManager.Active;
            activeTimeEntryData.StopTime = DateTime.Now;
            Console.WriteLine("stopping last entry");
        }

        private TimeEntryState CurrentState()
        {
            return timeEntryManager.Active.State;
        }

        private void RefreshViews ()
        {
            EnsureAdapter();
            Console.WriteLine("refreshing views");
            RemoteViews views = new RemoteViews(context.PackageName, Resource.Layout.homescreen_widget);
            var state = CurrentState();
            if(state == TimeEntryState.Running) {
                Console.WriteLine("Is running");
                views.SetInt(Resource.Id.WidgetActionButton, "background", Color.Red);
            } else {
                Console.WriteLine("NOt running");
                views.SetInt(Resource.Id.WidgetActionButton, "background", Color.LightGreen);
            }
            Console.WriteLine("duration:  {0}", CurrentDuration());
            views.SetTextViewText (Resource.Id.WidgetDuration, CurrentDuration());
            remoteViews = views;
        }

        private string CurrentDuration()
        {
            if (CurrentState() != TimeEntryState.Running) {
                return "00:00:00";
            }
            var activeTE = timeEntryManager.Active;
            var d = DateTime.Now - activeTE.StartTime;
            return String.Format("{0}:{1}:{2}", d.Hours, d.Minutes, d.Seconds);
        }

        public override IBinder OnBind (Intent intent)
        {
            return null;
        }
    }
}

