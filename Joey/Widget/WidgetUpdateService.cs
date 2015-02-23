using System;
using System.Collections.Generic;
using Android.Appwidget;
using Android.Content;
using Toggl.Phoebe;
using Toggl.Phoebe.Data.Models;
using Toggl.Phoebe.Net;
using XPlatUtils;

namespace Toggl.Joey.Widget
{
    public class WidgetUpdateService : IWidgetUpdateService
    {
        private Context ctx;

        public WidgetUpdateService (Context ctx)
        {
            this.ctx = ctx;

            // Update auth state from platform service.
            //
            // Phoebe services are initializated first,
            // if we try to update auth state from Phoebe
            // WidgetUpdateService still doesn't exists.

            var authManager = ServiceContainer.Resolve<AuthManager>();
            IsUserLogged = authManager.IsAuthenticated;
        }

        #region IWidgetUpdateService implementation

        private List<WidgetSyncManager.WidgetEntryData> lastEntries;

        public List<WidgetSyncManager.WidgetEntryData> LastEntries
        {
            get {
                if (lastEntries == null) {
                    lastEntries = new List<WidgetSyncManager.WidgetEntryData>();
                }
                return lastEntries;
            } set {
                lastEntries = value;
                UpdateWidgetContent (WidgetProvider.RefreshListAction);
            }
        }

        private string runningEntryDuration;

        public string RunningEntryDuration
        {
            get {
                return runningEntryDuration;
            } set {
                if ( string.Compare (runningEntryDuration, value, StringComparison.Ordinal) == 0) {
                    return;
                }

                runningEntryDuration = value;
                UpdateWidgetContent (WidgetProvider.RefreshTimeAction);
            }
        }

        private bool isUserLogged;

        public bool IsUserLogged
        {
            get {
                return isUserLogged;
            } set {
                if (isUserLogged == value) {
                    return;
                }

                isUserLogged = value;
                UpdateWidgetContent (AppWidgetManager.ActionAppwidgetUpdate);
            }
        }

        public void ShowNewTimeEntryScreen ( TimeEntryModel currentTimeEntry)
        {

        }

        public Guid GetEntryIdStarted ()
        {
            return new Guid();
        }

        public void UpdateWidgetContent (string action)
        {
            var intent = new Intent (ctx, typeof (WidgetProvider));
            intent.SetAction (action);

            var widgetManager = AppWidgetManager.GetInstance (ctx);
            int[] appWidgetIds = widgetManager.GetAppWidgetIds (new ComponentName (ctx, Java.Lang.Class.FromType (typeof (WidgetProvider))));
            intent.PutExtra (AppWidgetManager.ExtraAppwidgetIds, appWidgetIds);

            ctx.SendBroadcast (intent);
        }

        #endregion
    }
}

