using System;
using System.Collections.Generic;
using Foundation;
using Newtonsoft.Json;
using NotificationCenter;
using Toggl.Phoebe;
using Toggl.Phoebe.Data.Models;
using Toggl.Phoebe.Net;
using Toggl.Ross.ViewControllers;
using UIKit;
using XPlatUtils;

namespace Toggl.Ross.Data
{
    public class WidgetUpdateService : IWidgetUpdateService
    {
        public static string MillisecondsKey = "milliseconds_key";
        public static string TimeEntriesKey = "time_entries_key";
        public static string StartedEntryKey = "started_entry_key";
        public static string AppActiveEntryKey = "app_active_entry_key";
        public static string AppBackgroundEntryKey = "app_bg_entry_key";
        public static string IsUserLoggedKey = "is_logged_key";

        public static string TodayUrlPrefix = "today";
        public static string StartEntryUrlPrefix = "start";
        public static string ContinueEntryUrlPrefix = "continue";

        private NSUserDefaults nsUserDefaults;
        private UIViewController rootController;

        public NSUserDefaults UserDefaults
        {
            get {
                if (nsUserDefaults == null) {
<<<<<<< HEAD
                    nsUserDefaults = new NSUserDefaults ("group." + NSBundle.MainBundle.BundleIdentifier, NSUserDefaultsType.SuiteName);
=======
                    nsUserDefaults = new NSUserDefaults ("group.com.toggl.timer", NSUserDefaultsType.SuiteName);
>>>>>>> New app widget structure.
                }
                return nsUserDefaults;
            }
        }

        public WidgetUpdateService ()
        {
            // Update auth state from platform service.
            //
            // Phoebe services are initializated first,
            // if we try to update auth state from Phoebe
            // WidgetUpdateService still doesn't exists.

            var authManager = ServiceContainer.Resolve<AuthManager>();
            SetUserLogged (authManager.IsAuthenticated);
<<<<<<< HEAD
            SetAppActivated (true);
            SetAppOnBackground (false);
=======
>>>>>>> New app widget structure.
            rootController = UIApplication.SharedApplication.KeyWindow.RootViewController;
        }

        #region IWidgetUpdateService implementation

        public void SetLastEntries (List<WidgetSyncManager.WidgetEntryData> lastEntries)
        {
            if (lastEntries != null) {
                var json = JsonConvert.SerializeObject (lastEntries);
                UserDefaults.SetString (json, TimeEntriesKey);
                UpdateWidgetContent();
            }
        }

        public void SetRunningEntryDuration (string duration)
        {
            UserDefaults.SetString (duration, MillisecondsKey);
        }

        public void SetUserLogged (bool isLogged)
        {
            UserDefaults.SetBool (isLogged, IsUserLoggedKey);
            UpdateWidgetContent();
        }

<<<<<<< HEAD
        public void SetAppActivated (bool isActivated)
        {
            UserDefaults.SetBool (isActivated, AppActiveEntryKey);
            UpdateWidgetContent();
        }

        public void SetAppOnBackground (bool isBackground)
        {
            UserDefaults.SetBool (isBackground, AppBackgroundEntryKey);
            UpdateWidgetContent();
        }

        public void ShowNewTimeEntryScreen (TimeEntryModel currentTimeEntry)
        {
=======
        public void ShowNewTimeEntryScreen (TimeEntryModel currentTimeEntry)
        {
>>>>>>> New app widget structure.
            var topVCList = new List<UIViewController> (rootController.ChildViewControllers);
            if (topVCList.Count > 0) {
                // Get current VC's navigation
                var controllers = new List<UIViewController> (topVCList[0].NavigationController.ViewControllers);
                controllers.Add (new EditTimeEntryViewController (currentTimeEntry));
                if (ServiceContainer.Resolve<SettingsStore> ().ChooseProjectForNew) {
                    controllers.Add (new ProjectSelectionViewController (currentTimeEntry));
                }
                topVCList[0].NavigationController.SetViewControllers (controllers.ToArray (), true);
            }
        }

        public Guid GetEntryIdStarted ()
        {
            Guid entryId;
            Guid.TryParse (UserDefaults.StringForKey (StartedEntryKey), out entryId);
            return entryId;
        }

        public void UpdateWidgetContent()
        {
            var controller = NCWidgetController.GetWidgetController ();
            controller.SetHasContent (true, NSBundle.MainBundle.BundleIdentifier + ".today");
        }

        #endregion
    }
}

