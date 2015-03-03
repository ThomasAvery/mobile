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
        private Context context;

        public WidgetUpdateService (Context ctx)
        {
            this.context = ctx;

            // Update auth state from platform service.
            //
            // Phoebe services are initializated first,
            // if we try to update auth state from Phoebe
            // WidgetUpdateService still doesn't exists.

            var authManager = ServiceContainer.Resolve<AuthManager>();
            IsUserLogged = authManager.IsAuthenticated;
        }

        #region IWidgetUpdateService implementation

        public bool AppActivated { get; set; }

        public bool AppOnBackground { get;  set; }

        public string RunningEntryDuration { get; set; }

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
                WidgetProvider.RefreshWidget (context, WidgetProvider.RefreshListAction);
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
                WidgetProvider.RefreshWidget (context, WidgetProvider.RefreshCompleteAction);
            }
        }

        public void ShowNewTimeEntryScreen ( TimeEntryModel currentTimeEntry)
        {
        }

        public Guid GetEntryIdStarted ()
        {
            return new Guid();
        }

        #endregion
    }
}

