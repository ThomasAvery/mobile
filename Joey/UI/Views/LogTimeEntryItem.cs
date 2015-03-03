using System;
using System.Drawing;
using System.Linq;
using Android.Content;
using Android.Content.Res;
using Android.Graphics.Drawables;
using Android.Util;
using Android.Views;
using Android.Widget;

namespace Toggl.Joey.UI.Views
{
    public class LogTimeEntryItem : ViewGroup
    {
        private View colorView;
        private TextView projectTextView;
        private TextView clientTextView;
        private TextView taskTextView;
        private TextView descriptionTextView;
        private ImageButton continueImageButton;
        private View continueButtonSeparator;
        private TextView durationTextView;
        private ImageView billableIcon;
        private ImageView tagsIcon;

        private Drawable topFadeDrawable;
        private int topFadeWidth;
        private Rectangle topFadeRect;

        private Drawable bottomFadeDrawable;
        private int bottomFadeWidth;
        private Rectangle bottomFadeRect;

        private View view;

        public LogTimeEntryItem (Context context, IAttributeSet attrs) : base (context, attrs)
        {
            view = LayoutInflater.FromContext (context).Inflate (Resource.Layout.LogTimeEntryListItem, this, true);
            Initialize ();
        }

        public LogTimeEntryItem (Context context, IAttributeSet attrs, int defStyle) : base (context, attrs, defStyle)
        {
            view = LayoutInflater.FromContext (context).Inflate (Resource.Layout.LogTimeEntryListItem, this, true);
            Initialize ();
        }

        private void Initialize ()
        {
            this.DescendantFocusability = DescendantFocusability.BlockDescendants;
            view.SetBackgroundDrawable (Resources.GetDrawable (Resource.Drawable.MainListButton));
            colorView = FindViewById (Resource.Id.ColorView);
            projectTextView = view.FindViewById<TextView> (Resource.Id.ProjectTextView);
            clientTextView = view.FindViewById<TextView> (Resource.Id.ClientTextView);
            taskTextView = view.FindViewById<TextView> (Resource.Id.TaskTextView);
            descriptionTextView = view.FindViewById<TextView> (Resource.Id.DescriptionTextView);
            continueImageButton = view.FindViewById<ImageButton> (Resource.Id.ContinueImageButton);
            continueButtonSeparator = view.FindViewById<View> (Resource.Id.ContinueButtonSeparator);
            durationTextView = view.FindViewById<TextView> (Resource.Id.DurationTextView);
            billableIcon = view.FindViewById<ImageView> (Resource.Id.BillableIcon);
            tagsIcon = view.FindViewById<ImageView> (Resource.Id.TagsIcon);

            ReplaceDrawable (ref topFadeDrawable, ref topFadeWidth, MakeFadeDrawable ());
            ReplaceDrawable (ref bottomFadeDrawable, ref bottomFadeWidth, MakeFadeDrawable ());
        }

        private FadeDrawable MakeFadeDrawable ()
        {
            var width = (int)TypedValue.ApplyDimension (ComplexUnitType.Dip, 20, Resources.DisplayMetrics);

            var d = new FadeDrawable (width);
            d.SetStateColor (new[] { Android.Resource.Attribute.StatePressed }, Android.Graphics.Color.Rgb (245, 245, 245));
            d.SetStateColor (new[] { Android.Resource.Attribute.StateActivated }, Android.Graphics.Color.Rgb (222, 222, 222));
            d.SetStateColor (new int[] { }, Android.Graphics.Color.White);
            return d;
        }

        protected override void OnMeasure (int widthMeasureSpec, int heightMeasureSpec)
        {
            int widthUsed = 0;
            int heightUsed = 0;
            int widthSize = MeasureSpec.GetSize (widthMeasureSpec);

            MeasureChildWithMargins (colorView, widthMeasureSpec, widthUsed, heightMeasureSpec, heightUsed);
            heightUsed += GetMeasuredHeightWithMargins (colorView);

            MeasureChildWithMargins (projectTextView, widthMeasureSpec, widthUsed, heightMeasureSpec, heightUsed);
            MeasureChildWithMargins (descriptionTextView, widthMeasureSpec, widthUsed, heightMeasureSpec, heightUsed);
            MeasureChildWithMargins (clientTextView, widthMeasureSpec, widthUsed, heightMeasureSpec, heightUsed);
            MeasureChildWithMargins (taskTextView, widthMeasureSpec, widthUsed, heightMeasureSpec, heightUsed);
            MeasureChildWithMargins (continueButtonSeparator, widthMeasureSpec, widthUsed, heightMeasureSpec, heightUsed);
            MeasureChildWithMargins (continueImageButton, widthMeasureSpec, widthUsed, heightMeasureSpec, heightUsed);
            MeasureChildWithMargins (durationTextView, widthMeasureSpec, widthUsed, heightMeasureSpec, heightUsed);
            MeasureChildWithMargins (billableIcon, widthMeasureSpec, widthUsed, heightMeasureSpec, heightUsed);
            MeasureChildWithMargins (tagsIcon, widthMeasureSpec, widthUsed, heightMeasureSpec, heightUsed);

            int heightSize = heightUsed + PaddingTop + PaddingBottom;
            SetMeasuredDimension (widthSize, heightSize);
        }

        protected override void OnLayout (bool changed, int l, int t, int r, int b)
        {
            int paddingLeft = PaddingLeft;
            int currentTop = PaddingTop;

            LayoutView (colorView, paddingLeft, currentTop, colorView.MeasuredWidth, colorView.MeasuredHeight);
            paddingLeft += GetWidthWithMargins (colorView);

            int durationBar = r - continueImageButton.MeasuredWidth;
            LayoutView (continueButtonSeparator, durationBar, currentTop, continueButtonSeparator.MeasuredWidth, continueButtonSeparator.MeasuredHeight);
            LayoutView (continueImageButton, durationBar, currentTop, continueImageButton.MeasuredWidth, continueImageButton.MeasuredHeight);
            int secondLineMark = durationBar;
            durationBar -= durationTextView.MeasuredWidth;
            LayoutView (durationTextView, durationBar, currentTop, durationTextView.MeasuredWidth, durationTextView.MeasuredHeight);

            if (billableIcon.Visibility == ViewStates.Visible) {
                durationBar -= billableIcon.MeasuredWidth;
                LayoutView (billableIcon, durationBar, currentTop, billableIcon.MeasuredWidth, billableIcon.MeasuredHeight);
            }
            if (tagsIcon.Visibility == ViewStates.Visible) {
                durationBar -= tagsIcon.MeasuredWidth;
                LayoutView (tagsIcon, durationBar, currentTop, tagsIcon.MeasuredWidth, tagsIcon.MeasuredHeight);
            }

            int usableWidthFirstLine = durationBar - paddingLeft;
            int firstWidth = 0;

            if (clientTextView.Text != String.Empty) {
                firstWidth = GetFirstElementWidth (usableWidthFirstLine, clientTextView.MeasuredWidth);
                LayoutView (clientTextView, paddingLeft, currentTop, firstWidth, clientTextView.MeasuredHeight);
            }

            LayoutView (projectTextView, paddingLeft + firstWidth, currentTop, GetSecondElementWidth (usableWidthFirstLine, firstWidth, projectTextView.MeasuredWidth), projectTextView.MeasuredHeight);
            topFadeRect = new Rectangle (
                usableWidthFirstLine + paddingLeft, currentTop,
                0, Math.Max (projectTextView.MeasuredHeight, clientTextView.MeasuredHeight)
            );

            int usableWidthSecondLine = secondLineMark - paddingLeft;

            if (taskTextView.Text != String.Empty) {
                LayoutView (
                    taskTextView,
                    paddingLeft,
                    currentTop,
                    GetFirstElementWidth (usableWidthSecondLine, taskTextView.MeasuredWidth),
                    taskTextView.MeasuredHeight
                );
                LayoutView (
                    descriptionTextView,
                    paddingLeft + GetFirstElementWidth (usableWidthSecondLine, taskTextView.MeasuredWidth),
                    currentTop,
                    GetSecondElementWidth (usableWidthSecondLine, taskTextView.MeasuredWidth, descriptionTextView.MeasuredWidth),
                    descriptionTextView.MeasuredHeight
                );
            } else {
                LayoutView (
                    descriptionTextView,
                    paddingLeft,
                    currentTop,
                    GetSecondElementWidth (usableWidthSecondLine, taskTextView.MeasuredWidth, descriptionTextView.MeasuredWidth),
                    descriptionTextView.MeasuredHeight
                );
            }

            bottomFadeRect = new Rectangle (
                secondLineMark, currentTop + durationTextView.MeasuredHeight,
                0, Math.Max (taskTextView.MeasuredHeight, descriptionTextView.MeasuredHeight) - durationTextView.MeasuredHeight
            );

            ConfigureDrawables ();
        }

        protected override void DispatchDraw (Android.Graphics.Canvas canvas)
        {
            base.DispatchDraw (canvas);

            // Draw gradients on-top of others
            if (topFadeDrawable != null) {
                topFadeDrawable.Draw (canvas);
            }
            if (bottomFadeDrawable != null) {
                bottomFadeDrawable.Draw (canvas);
            }
        }

        private int GetFirstElementWidth (int usable, int first)
        {
            return first > usable ? usable : first;
        }

        private int GetSecondElementWidth (int usable, int first, int second)
        {
            int firstActual = GetFirstElementWidth (usable, first);
            if (firstActual == usable) {
                return 0;
            } else if (usable < firstActual + second) {
                return usable - firstActual;
            } else {
                return second;
            }
        }

        private void LayoutView (View view, int left, int top, int width, int height)
        {
            var margins = (MarginLayoutParams)view.LayoutParameters;
            int leftWithMargins = left + margins.LeftMargin;
            int topWithMargins = top + margins.TopMargin;

            view.Layout (leftWithMargins, topWithMargins,
                         leftWithMargins + width, topWithMargins + height);
        }

        private int GetWidthWithMargins (View child)
        {
            MarginLayoutParams lp = (MarginLayoutParams)child.LayoutParameters;
            return child.Width + lp.LeftMargin + lp.RightMargin;
        }

        private int GetHeightWithMargins (View child)
        {
            MarginLayoutParams lp = (MarginLayoutParams)child.LayoutParameters;
            return child.MeasuredHeight + lp.TopMargin + lp.BottomMargin;
        }

        private int GetMeasuredWidthWithMargins (View child)
        {
            MarginLayoutParams lp = (MarginLayoutParams)child.LayoutParameters;
            return child.MeasuredWidth + lp.LeftMargin + lp.RightMargin;
        }

        private int GetMeasuredHeightWithMargins (View child)
        {
            MarginLayoutParams lp = (MarginLayoutParams)child.LayoutParameters;
            return child.MeasuredHeight + lp.TopMargin + lp.BottomMargin;
        }

        public override LayoutParams GenerateLayoutParams (IAttributeSet attrs)
        {
            return new MarginLayoutParams (Context, attrs);
        }

        protected override LayoutParams GenerateDefaultLayoutParams ()
        {
            return new MarginLayoutParams (LayoutParams.WrapContent, LayoutParams.WrapContent);
        }

        private void ConfigureDrawables ()
        {
            if (topFadeDrawable != null) {
                topFadeDrawable.SetBounds (
                    topFadeRect.X - topFadeWidth,
                    topFadeRect.Y,
                    topFadeRect.X,
                    topFadeRect.Y + topFadeRect.Height
                );
            }

            if (bottomFadeDrawable != null) {
                bottomFadeDrawable.SetBounds (
                    bottomFadeRect.X - bottomFadeWidth,
                    bottomFadeRect.Y,
                    bottomFadeRect.X,
                    bottomFadeRect.Y + bottomFadeRect.Height
                );
            }
        }

        public override void InvalidateDrawable (Drawable drawable)
        {
            if (drawable == topFadeDrawable || drawable == bottomFadeDrawable) {
                Invalidate ();
            } else {
                base.InvalidateDrawable (drawable);
            }
        }

        private void ReplaceDrawable (ref Drawable field, ref int width, Drawable value)
        {
            if (field == value) {
                return;
            }

            if (field != null) {
                field.Callback = null;
                UnscheduleDrawable (field);
            }

            field = value;
            if (field != null) {
                field.Callback = this;
                if (field.IsStateful) {
                    field.SetState (GetDrawableState ());
                }

                field.SetVisible (Visibility == ViewStates.Visible, true);

                width = field.IntrinsicWidth;
            } else {
                width = 0;
            }

            ConfigureDrawables ();
            Invalidate ();
        }

        protected override void DrawableStateChanged ()
        {
            base.DrawableStateChanged ();
            if (topFadeDrawable != null && topFadeDrawable.IsStateful) {
                topFadeDrawable.SetState (GetDrawableState ());
            }
            if (bottomFadeDrawable != null && bottomFadeDrawable.IsStateful) {
                bottomFadeDrawable.SetState (GetDrawableState ());
            }
        }

        public override ViewStates Visibility
        {
            get { return base.Visibility; }
            set {
                base.Visibility = value;
                if (topFadeDrawable != null) {
                    topFadeDrawable.SetVisible (Visibility == ViewStates.Visible, false);
                }
                if (bottomFadeDrawable != null) {
                    bottomFadeDrawable.SetVisible (Visibility == ViewStates.Visible, false);
                }
            }
        }

        protected override void OnAttachedToWindow ()
        {
            base.OnAttachedToWindow ();
            if (topFadeDrawable != null) {
                topFadeDrawable.SetVisible (Visibility == ViewStates.Visible, false);
            }
            if (bottomFadeDrawable != null) {
                bottomFadeDrawable.SetVisible (Visibility == ViewStates.Visible, false);
            }
        }

        protected override void OnDetachedFromWindow ()
        {
            base.OnDetachedFromWindow ();
            if (topFadeDrawable != null) {
                topFadeDrawable.SetVisible (false, false);
            }
            if (bottomFadeDrawable != null) {
                bottomFadeDrawable.SetVisible (false, false);
            }
        }
    }
}

