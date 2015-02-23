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

        void SetAppActivated (bool isActivated);

        void SetAppOnBackground (bool isBackground);

        void ShowNewTimeEntryScreen (TimeEntryModel currentTimeEntry);

        Guid GetEntryIdStarted();
    }
}