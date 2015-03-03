using System;
using System.Collections.Generic;
using Toggl.Phoebe.Data.Models;

namespace Toggl.Phoebe
{
    public interface IWidgetUpdateService
    {
        List<WidgetSyncManager.WidgetEntryData> LastEntries { get; set; }

        string RunningEntryDuration { get; set; }

        bool IsUserLogged { get; set; }

        bool AppActivated { get; set; }

        bool AppOnBackground { get; set; }

        void ShowNewTimeEntryScreen (TimeEntryModel currentTimeEntry);

        Guid GetEntryIdStarted();
    }
}