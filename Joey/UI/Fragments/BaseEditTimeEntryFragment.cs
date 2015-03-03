using System;
using System.Collections.Generic;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Text;
using Android.Text.Style;
using Android.Views;
using Android.Widget;
using Toggl.Joey.UI.Utils;
using Toggl.Joey.UI.Views;
using Toggl.Phoebe;
using Toggl.Phoebe.Data;
using Toggl.Phoebe.Data.Models;
using Toggl.Phoebe.Data.Utils;
using Toggl.Phoebe.Data.Views;
using Toggl.Phoebe.Logging;
using XPlatUtils;
using Fragment = Android.Support.V4.App.Fragment;
using MeasureSpec = Android.Views.View.MeasureSpec;

namespace Toggl.Joey.UI.Fragments
{
    public abstract class BaseEditTimeEntryFragment : Fragment
    {
        private static readonly string Tag = "BaseEditTimeEntryFragment";
        private const int TagMaxLength = 30;

        private readonly Handler handler = new Handler ();
        private PropertyChangeTracker propertyTracker;
        private TimeEntryModel model;
        private TimeEntryTagsView tagsView;
        private bool canRebind;
        private bool descriptionChanging;
        private bool autoCommitScheduled;
        private ViewGroup cont;

        protected BaseEditTimeEntryFragment ()
        {
        }

        protected BaseEditTimeEntryFragment (IntPtr jref, Android.Runtime.JniHandleOwnership xfer) : base (jref, xfer)
        {
        }

        protected TimeEntryModel TimeEntry
        {
            get { return model; }
            set {
                DiscardDescriptionChanges ();

                if (tagsView != null && (value == null || value.Id != tagsView.TimeEntryId)) {
                    tagsView.Updated -= OnTimeEntryTagsUpdated;
                    tagsView = null;
                }

                model = value;

                if (model != null && tagsView == null) {
                    tagsView = new TimeEntryTagsView (model.Id);
                    tagsView.Updated += OnTimeEntryTagsUpdated;
                }

                Rebind ();
                RebindTags ();
            }
        }

        protected bool CanRebind
        {
            get { return canRebind || model == null; }
        }

        protected abstract void ResetModel ();

        public override void OnStart ()
        {
            base.OnStart ();

            propertyTracker = new PropertyChangeTracker ();
            canRebind = true;

            Rebind ();
            RebindTags ();
        }

        public override void OnStop ()
        {
            base.OnStop ();

            canRebind = false;

            if (propertyTracker != null) {
                propertyTracker.Dispose ();
                propertyTracker = null;
            }
        }

        public override bool UserVisibleHint
        {
            get { return base.UserVisibleHint; }
            set {
                if (!value) {
                    CommitDescriptionChanges ();
                }
                base.UserVisibleHint = value;
            }
        }

        private void ResetTrackedObservables ()
        {
            if (propertyTracker == null) {
                return;
            }

            propertyTracker.MarkAllStale ();

            var entry = TimeEntry;
            if (entry != null) {
                propertyTracker.Add (entry, HandleTimeEntryPropertyChanged);

                if (entry.Project != null) {
                    propertyTracker.Add (entry.Project, HandleProjectPropertyChanged);

                    if (entry.Project.Client != null) {
                        propertyTracker.Add (entry.Project.Client, HandleClientPropertyChanged);
                    }
                }

                if (entry.Task != null) {
                    propertyTracker.Add (entry.Task, HandleTaskPropertyChanged);
                }
            }

            propertyTracker.ClearStale ();
        }

        private void HandleTimeEntryPropertyChanged (string prop)
        {
            if (prop == TimeEntryModel.PropertyProject
                    || prop == TimeEntryModel.PropertyTask
                    || prop == TimeEntryModel.PropertyState
                    || prop == TimeEntryModel.PropertyStartTime
                    || prop == TimeEntryModel.PropertyStopTime
                    || prop == TimeEntryModel.PropertyDescription
                    || prop == TimeEntryModel.PropertyIsBillable) {
                Rebind ();
            } else if (prop == TimeEntryModel.PropertyId) {
                ResetModel ();
            }
        }

        private void HandleProjectPropertyChanged (string prop)
        {
            if (prop == ProjectModel.PropertyClient
                    || prop == ProjectModel.PropertyName
                    || prop == ProjectModel.PropertyColor) {
                Rebind ();
            }
        }

        private void HandleTaskPropertyChanged (string prop)
        {
            if (prop == TaskModel.PropertyName) {
                Rebind ();
            }
        }

        private void HandleClientPropertyChanged (string prop)
        {
            if (prop == ClientModel.PropertyName) {
                Rebind ();
            }
        }

        protected virtual void Rebind ()
        {
            ResetTrackedObservables ();

            if (TimeEntry == null || !canRebind) {
                return;
            }

            var startTime = TimeEntry.StartTime;
            var useTimer = TimeEntry.StartTime == DateTime.MinValue;
            if (useTimer) {
                startTime = Time.Now;

                DurationTextView.Text = TimeSpan.Zero.ToString ();

                // Make sure that we display accurate time:
                handler.RemoveCallbacks (Rebind);
                handler.PostDelayed (Rebind, 5000);
            } else {
                startTime = TimeEntry.StartTime.ToLocalTime ();

                var duration = TimeEntry.GetDuration ();
                DurationTextView.Text = TimeSpan.FromSeconds ((long)duration.TotalSeconds).ToString ();

                if (TimeEntry.State == TimeEntryState.Running) {
                    handler.RemoveCallbacks (Rebind);
                    handler.PostDelayed (Rebind, 1000 - duration.Milliseconds);
                }
            }

            StartTimeEditText.Text = startTime.ToDeviceTimeString ();
            if (startTime.Date != Time.Now.Date) {
                DateTextView.Text = startTime.ToDeviceDateString ();
                DateTextView.Visibility = ViewStates.Visible;
            } else {
                DateTextView.Visibility = ViewStates.Invisible;
            }

            // Only update DescriptionEditText when content differs, else the user is unable to edit it
            if (!descriptionChanging && DescriptionEditText.Text != TimeEntry.Description) {
                DescriptionEditText.Text = TimeEntry.Description;
                DescriptionEditText.SetSelection (DescriptionEditText.Text.Length);
            }
            DescriptionEditText.SetHint (useTimer
                                         ? Resource.String.CurrentTimeEntryEditDescriptionHint
                                         : Resource.String.CurrentTimeEntryEditDescriptionPastHint);

            if (TimeEntry.StopTime.HasValue) {
                StopTimeEditText.Text = TimeEntry.StopTime.Value.ToLocalTime ().ToDeviceTimeString ();
                StopTimeEditText.Visibility = ViewStates.Visible;
            } else {
                StopTimeEditText.Text = Time.Now.ToDeviceTimeString ();
                if (TimeEntry.StartTime == DateTime.MinValue || TimeEntry.State == TimeEntryState.Running) {
                    StopTimeEditText.Visibility = ViewStates.Invisible;
                } else {
                    StopTimeEditText.Visibility = ViewStates.Visible;
                }
            }

            ProjectEditText.Text = TimeEntry.Project != null ? TimeEntry.Project.Name : String.Empty;
            BillableCheckBox.Checked = !TimeEntry.IsBillable;
            if (TimeEntry.IsBillable) {
                BillableCheckBox.SetText (Resource.String.CurrentTimeEntryEditBillableChecked);
            } else {
                BillableCheckBox.SetText (Resource.String.CurrentTimeEntryEditBillableUnchecked);
            }
            if (TimeEntry.Workspace == null || !TimeEntry.Workspace.IsPremium) {
                BillableCheckBox.Visibility = ViewStates.Gone;
            } else {
                BillableCheckBox.Visibility = ViewStates.Visible;
            }
        }

        private void OnTimeEntryTagsUpdated (object sender, EventArgs args)
        {
            RebindTags ();
        }

        protected virtual void RebindTags ()
        {
            List<String> tagList = new List<String> ();
            String t;

            if (tagsView == null || !canRebind) {
                return;
            }
            if (tagsView.Count == 0) {
                TagsEditText.Text = String.Empty;
                return;
            }

            foreach (String tagText in tagsView.Data) {
                if (tagText.Length > TagMaxLength) {
                    t = tagText.Substring (0, TagMaxLength - 1).Trim () + "…";
                } else {
                    t = tagText;
                }
                tagList.Add (t);
            }
            // The extra whitespace prevents the ImageSpans and the text they are over
            // to break at different positions, leaving zero linespacing on edge cases.
            var tags = new SpannableStringBuilder (String.Join (" ", tagList) + " ");

            int x = 0;
            foreach (String tagText in tagList) {
                tags.SetSpan (new ImageSpan (MakeTagChip (tagText)), x, x + tagText.Length, SpanTypes.ExclusiveExclusive);
                x = x + tagText.Length + 1;
            }
            TagsEditText.SetText (tags, EditText.BufferType.Spannable);
        }

        private BitmapDrawable MakeTagChip (String tagText)
        {
            var ctx = ServiceContainer.Resolve<Context> ();
            var Inflater = LayoutInflater.FromContext (ctx);
            var tagChipView = (TextView)Inflater.Inflate (Resource.Layout.TagViewChip, cont, false);

            tagChipView.Text = tagText.ToUpper ();
            int spec = MeasureSpec.MakeMeasureSpec (0, MeasureSpecMode.Unspecified);
            tagChipView.Measure (spec, spec);
            tagChipView.Layout (0, 0, tagChipView.MeasuredWidth, tagChipView.MeasuredHeight);

            var b = Bitmap.CreateBitmap (tagChipView.Width, tagChipView.Height, Bitmap.Config.Argb8888);

            var canvas = new Canvas (b);
            canvas.Translate (-tagChipView.ScrollX, -tagChipView.ScrollY);
            tagChipView.Draw (canvas);
            tagChipView.DrawingCacheEnabled = true;

            var cacheBmp = tagChipView.DrawingCache;
            var viewBmp = cacheBmp.Copy (Bitmap.Config.Argb8888, true);
            tagChipView.DestroyDrawingCache ();
            var bmpDrawable = new BitmapDrawable (Resources, viewBmp);
            bmpDrawable.SetBounds (0, 0, bmpDrawable.IntrinsicWidth, bmpDrawable.IntrinsicHeight);
            return bmpDrawable;
        }

        protected TextView DateTextView { get; private set; }

        protected TextView DurationTextView { get; private set; }

        protected EditText StartTimeEditText { get; private set; }

        protected EditText StopTimeEditText { get; private set; }

        protected EditText DescriptionEditText { get; private set; }

        protected EditText ProjectEditText { get; private set; }

        protected EditText TagsEditText { get; private set; }

        protected CheckBox BillableCheckBox { get; private set; }

        protected ImageButton DeleteImageButton { get; private set; }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle state)
        {
            var view = inflater.Inflate (Resource.Layout.EditTimeEntryFragment, container, false);
            cont = container;
            DateTextView = view.FindViewById<TextView> (Resource.Id.DateTextView).SetFont (Font.Roboto);
            DurationTextView = view.FindViewById<TextView> (Resource.Id.DurationTextView).SetFont (Font.Roboto);
            StartTimeEditText = view.FindViewById<EditText> (Resource.Id.StartTimeEditText).SetFont (Font.Roboto);
            StopTimeEditText = view.FindViewById<EditText> (Resource.Id.StopTimeEditText).SetFont (Font.Roboto);
            DescriptionEditText = view.FindViewById<EditText> (Resource.Id.DescriptionEditText).SetFont (Font.RobotoLight);
            ProjectEditText = view.FindViewById<EditText> (Resource.Id.ProjectEditText).SetFont (Font.RobotoLight);
            TagsEditText = view.FindViewById<EditText> (Resource.Id.TagsEditText).SetFont (Font.RobotoLight);
            BillableCheckBox = view.FindViewById<CheckBox> (Resource.Id.BillableCheckBox).SetFont (Font.RobotoLight);
            DeleteImageButton = view.FindViewById<ImageButton> (Resource.Id.DeleteImageButton);

            DurationTextView.Click += OnDurationTextViewClick;
            StartTimeEditText.Click += OnStartTimeEditTextClick;
            StopTimeEditText.Click += OnStopTimeEditTextClick;
            DescriptionEditText.TextChanged += OnDescriptionTextChanged;
            DescriptionEditText.EditorAction += OnDescriptionEditorAction;
            DescriptionEditText.FocusChange += OnDescriptionFocusChange;
            ProjectEditText.Click += OnProjectEditTextClick;
            TagsEditText.Click += OnTagsEditTextClick;
            BillableCheckBox.CheckedChange += OnBillableCheckBoxCheckedChange;
            DeleteImageButton.Click += OnDeleteImageButtonClick;

            return view;
        }

        private void OnDurationTextViewClick (object sender, EventArgs e)
        {
            if (TimeEntry == null) {
                return;
            }
            new ChangeTimeEntryDurationDialogFragment (TimeEntry).Show (FragmentManager, "duration_dialog");
        }

        private void OnStartTimeEditTextClick (object sender, EventArgs e)
        {
            if (TimeEntry == null) {
                return;
            }
            new ChangeTimeEntryStartTimeDialogFragment (TimeEntry).Show (FragmentManager, "time_dialog");
        }

        private void OnStopTimeEditTextClick (object sender, EventArgs e)
        {
            if (TimeEntry == null || TimeEntry.State == TimeEntryState.Running) {
                return;
            }
            new ChangeTimeEntryStopTimeDialogFragment (TimeEntry).Show (FragmentManager, "time_dialog");
        }

        private void OnDescriptionTextChanged (object sender, Android.Text.TextChangedEventArgs e)
        {
            // This can be called when the fragment is being restored, so the previous value will be
            // set miraculously. So we need to make sure that this is indeed the user who is changing the
            // value by only acting when the OnStart has been called.
            if (!canRebind) {
                return;
            }

            // Mark description as changed
            descriptionChanging = TimeEntry != null && DescriptionEditText.Text != TimeEntry.Description;

            // Make sure that we're commiting 1 second after the user has stopped typing
            CancelDescriptionChangeAutoCommit ();
            if (descriptionChanging) {
                ScheduleDescriptionChangeAutoCommit ();
            }
        }

        private void OnDescriptionFocusChange (object sender, View.FocusChangeEventArgs e)
        {
            if (!e.HasFocus) {
                CommitDescriptionChanges ();
            }
        }

        private void OnDescriptionEditorAction (object sender, TextView.EditorActionEventArgs e)
        {
            if (e.ActionId == Android.Views.InputMethods.ImeAction.Done) {
                CommitDescriptionChanges ();
            }
            e.Handled = false;
        }

        private void OnProjectEditTextClick (object sender, EventArgs e)
        {
            if (TimeEntry == null) {
                return;
            }

            new ChooseTimeEntryProjectDialogFragment (TimeEntry).Show (FragmentManager, "projects_dialog");
        }

        private void OnTagsEditTextClick (object sender, EventArgs e)
        {
            if (TimeEntry == null) {
                return;
            }

            new ChooseTimeEntryTagsDialogFragment (TimeEntry).Show (FragmentManager, "tags_dialog");
        }

        private void OnBillableCheckBoxCheckedChange (object sender, CompoundButton.CheckedChangeEventArgs e)
        {
            if (TimeEntry == null) {
                return;
            }

            var isBillable = !BillableCheckBox.Checked;
            if (TimeEntry.IsBillable != isBillable) {
                TimeEntry.IsBillable = isBillable;
                SaveTimeEntry ();
            }
        }

        private async void OnDeleteImageButtonClick (object sender, EventArgs e)
        {
            if (TimeEntry == null) {
                return;
            }

            await TimeEntry.DeleteAsync ();
            ResetModel ();

            Toast.MakeText (Activity, Resource.String.CurrentTimeEntryEditDeleteToast, ToastLength.Short).Show ();
        }

        private void AutoCommitDescriptionChanges ()
        {
            if (!autoCommitScheduled) {
                return;
            }
            autoCommitScheduled = false;
            CommitDescriptionChanges ();
        }

        private void ScheduleDescriptionChangeAutoCommit ()
        {
            if (autoCommitScheduled) {
                return;
            }

            autoCommitScheduled = true;
            handler.PostDelayed (AutoCommitDescriptionChanges, 1000);
        }

        private void CancelDescriptionChangeAutoCommit ()
        {
            if (!autoCommitScheduled) {
                return;
            }

            handler.RemoveCallbacks (AutoCommitDescriptionChanges);
            autoCommitScheduled = false;
        }

        private void CommitDescriptionChanges ()
        {
            if (TimeEntry != null && descriptionChanging) {
                if (TimeEntry.Description != DescriptionEditText.Text) {
                    TimeEntry.Description = DescriptionEditText.Text;
                    SaveTimeEntry ();
                }
            }
            descriptionChanging = false;
            CancelDescriptionChangeAutoCommit ();
        }

        private void DiscardDescriptionChanges ()
        {
            descriptionChanging = false;
            CancelDescriptionChangeAutoCommit ();
        }

        private async void SaveTimeEntry ()
        {
            var entry = TimeEntry;
            if (entry == null) {
                return;
            }

            try {
                await entry.SaveAsync ().ConfigureAwait (false);
            } catch (Exception ex) {
                var log = ServiceContainer.Resolve<ILogger> ();
                log.Warning (Tag, ex, "Failed to save model changes.");
            }
        }
    }
}
