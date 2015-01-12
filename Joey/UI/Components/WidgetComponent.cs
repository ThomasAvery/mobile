
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
using Toggl.Phoebe.Data;
using XPlatUtils;
using Toggl.Phoebe.Data.Models;

namespace Toggl.Joey.UI.Components
{
    public class WidgetComponent
    {

        private bool isRunning = false;
        private TextView currentDuration;
        private Button timerButton;
        private ActiveTimeEntryManager timeEntryManager;

        private void Initialize ()
        {
            if (timeEntryManager == null) {
                timeEntryManager = ServiceContainer.Resolve<ActiveTimeEntryManager> ();
            }
        }

        public WidgetState GetRunningDuration()
        {
            Initialize();
            var wState = new WidgetState ();
            var activeTimeEntryData = timeEntryManager.Active;

            wState.Description = activeTimeEntryData.Description;
            wState.IsRunning = activeTimeEntryData.State;
            wState.StartTime = activeTimeEntryData.StartTime;

            return wState;
        }
    }

    public class WidgetState
    {
        public DateTime StartTime;
        public TimeEntryState IsRunning;
        public string Description;
    }
}

