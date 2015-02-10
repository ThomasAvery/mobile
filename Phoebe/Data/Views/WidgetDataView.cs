using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Android.OS;
using Toggl.Phoebe.Data.DataObjects;
using Toggl.Phoebe.Net;
using XPlatUtils;

namespace Toggl.Phoebe.Data.Views
{
    public class WidgetDataView
    {

        private List<ListEntryData> dataObject;
        private ListEntryData activeTimeEntry;
        private readonly int maxCount = 3;
        private UserData userData;
        private DateTime queryStartDate;
        private bool hasRunning;

        public async void Load()
        {
            if (IsLoading) {
                return;
            }
            IsLoading = true;

            try {
                userData = ServiceContainer.Resolve<AuthManager> ().User;
                var store = ServiceContainer.Resolve<IDataStore> ();
                queryStartDate = Time.UtcNow - TimeSpan.FromDays (9);
                var recentEntries = await store.Table<TimeEntryData> ()
                                    .OrderBy (r => r.StartTime, false)
                                    .Take (maxCount)
                                    .Where (r => r.DeletedAt == null
                                            && r.UserId == userData.Id
                                            && r.State != TimeEntryState.New
                                            && r.StartTime >= queryStartDate)
                                    .QueryAsync()
                                    .ConfigureAwait (false);

                dataObject = new List<ListEntryData> ();
                foreach (var entry in recentEntries) {
                    var entryData = await ConvertToListEntryData (entry);
                    dataObject.Add (entryData);
                }

                var runningEntry = await store.Table<TimeEntryData> ()
                                   .Where (r => r.DeletedAt == null
                                           && r.UserId == userData.Id
                                           && r.State == TimeEntryState.Running)
                                   .QueryAsync()
                                   .ConfigureAwait (false);

                if (runningEntry.Count > 0) {
                    activeTimeEntry = await ConvertToListEntryData (runningEntry[0]);
                    hasRunning = true;
                } else {
                    activeTimeEntry = null;
                    hasRunning = false;
                }

            } finally {
                IsLoading = false;
            }
        }

        private async Task<ListEntryData> ConvertToListEntryData (TimeEntryData entry)
        {
            var project = await FetchProjectData (entry.ProjectId ?? Guid.Empty);
            var entryData = new ListEntryData();
            entryData.Id = entry.Id;
            entryData.Description = String.IsNullOrEmpty (entry.Description) ? "(no description)": entry.Description;
            entryData.Duration =  GetDuration (entry.StartTime, entry.StopTime ?? DateTime.Now);
            entryData.Project = String.IsNullOrEmpty (project.Name) ? "(no project)": project.Name;
            entryData.HasProject = String.IsNullOrEmpty (project.Name) ? false : true;
            entryData.ProjectColor = project.Color;
            entryData.State = entry.State;
            entryData.FillIntentBundle.PutString ("EntryId", entry.Id.ToString());

            return entryData;
        }
        public List<ListEntryData> Data
        {
            get {
                return dataObject;
            }
        }

        public ListEntryData Active
        {
            get {
                return activeTimeEntry;
            }
        }

        public bool HasRunning
        {
            get {
                return hasRunning;
            }
        }


        public bool IsLoading { get; private set; }

        private async Task<ProjectData> FetchProjectData (Guid projectId)
        {
            var store = ServiceContainer.Resolve<IDataStore> ();
            var project = await store.Table<ProjectData> ()
                          .QueryAsync (r => r.Id == projectId);

            if (projectId == Guid.Empty) {
                return new ProjectData();
            }

            return project[0];
        }

        private TimeSpan GetDuration (DateTime startTime, DateTime stopTime)
        {
            if (startTime == DateTime.MinValue) {
                return TimeSpan.Zero;
            }

            var duration = stopTime - startTime;
            if (duration < TimeSpan.Zero) {
                duration = TimeSpan.Zero;
            }
            return duration;
        }
    }

    public class ListEntryData
    {
        public Guid Id;

        public string Description;

        public bool HasProject;

        public string Project;

        public int ProjectColor;

        public TimeSpan Duration;

        public TimeEntryState State;

        public Bundle FillIntentBundle = new Bundle(); // For startEntryService.
    }
}

