using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Timelapse.Database;
using Timelapse.Enums;
using Timelapse.Util;

namespace Timelapse.Controls
{
    // A flag comprises a stack panel containing
    // - a label containing the descriptive label) 
    // - checkbox (the content) at the given width
    public class DataEntryFlag : DataEntryControl<CheckBox, Label>
    {
        // Return the TopLeft corner of the content control as a point
        public override Point TopLeft
        {
            get { return this.ContentControl.PointToScreen(new Point(0, 0)); }
        }
        public override UIElement GetContentControl
        {
            get { return this.ContentControl; }
        }

        public override bool IsContentControlEnabled
        {
            get { return this.ContentControl.IsEnabled; }
        }

        /// <summary>Gets or sets the Content of the Note</summary>
        public override string Content
        {
            get { return ((bool)this.ContentControl.IsChecked) ? Constant.BooleanValue.True : Constant.BooleanValue.False; }
        }

        public override bool ContentReadOnly
        {
            get { return false; }
            set { }
            // SAULXXX: Not sure why the original code below isn't working. The issue is that when we close and re-open an image set, 
            // the newly created content control for the flag seems to be set to IsEnabled is false, but I can't track down why that change happens.
            // However, since flags and DeleteFlag is always writeable, we can just fake it
            // get { return !this.ContentControl.IsEnabled; }
            // set { this.ContentControl.IsEnabled = !value; }
        }

        public DataEntryFlag(ControlRow control, DataEntryControls styleProvider)
            : base(control, styleProvider, ControlContentStyleEnum.FlagCheckBox, ControlLabelStyleEnum.DefaultLabel)
        {
            // Callback used to allow Enter to select the highlit item
            this.ContentControl.PreviewKeyDown += this.ContentControl_PreviewKeyDown;
        }

        #region Event Handlers
        // Ignore these navigation key events, as otherwise they act as tabs which does not conform to how we navigate
        // between other control types
        private void ContentControl_PreviewKeyDown(object sender, KeyEventArgs keyEvent)
        {
            if (keyEvent.Key == Key.Right || keyEvent.Key == Key.Left || keyEvent.Key == Key.PageUp || keyEvent.Key == Key.PageDown)
            {
                // the right/left arrow keys normally cycle through the menu items.
                // However, we want to retain the arrow keys - as well as the PageUp/Down keys - for cycling through the image.
                // So we mark the event as handled, and we cycle through the images anyways.
                // Note that redirecting the event to the main window, while prefered, won't work
                // as the main window ignores the arrow keys if the focus is set to a control.
                keyEvent.Handled = true;
                GlobalReferences.MainWindow.Handle_PreviewKeyDown(keyEvent, true);
            }
            else if (keyEvent.Key == Key.Up || keyEvent.Key == Key.Down)
            {
                // Ignore as it otherwise handled as a tab
                keyEvent.Handled = true;
            }
        }
        #endregion

        #region Setting Content and Tooltip
        public override void SetContentAndTooltip(string value)
        {
            // If the value is null, an ellipsis will be drawn in the checkbox (see Checkbox style)
            // Used to signify the indeterminate state in no or multiple selections in the overview.
            if (value == null)
            {
                this.ContentControl.IsChecked = null;
                this.ContentControl.ToolTip = "Click to change the " + this.Label + " for all selected images";
                return;
            }

            // Otherwise, the checkbox will be checked depending on whether the value is true or false,
            // and the tooltip will be set to true or false. 
            value = value.ToLower();
            this.ContentControl.IsChecked = (value == Constant.BooleanValue.True) ? true : false;
            this.ContentControl.ToolTip = this.LabelControl.ToolTip;
        }
        #endregion

        #region Visual Effects and Popup Previews
        // Flash the content area of the control
        public override void FlashContentControl()
        {
            Border border = (Border)this.ContentControl.Template.FindName("checkBoxBorder", this.ContentControl);
            if (border != null)
            {
                border.Background = new SolidColorBrush(Colors.White);
                border.Background.BeginAnimation(SolidColorBrush.ColorProperty, this.GetColorAnimation());
            }
        }

        protected override Popup CreatePopupPreview(Control control, Thickness padding, double width, double horizontalOffset)
        {
            Style style = (Style)this.ContentControl.FindResource(ControlContentStyleEnum.FlagCheckBox.ToString());

            // Creatre a textblock and align it so the text is exactly at the same position as the control's text
            CheckBox popupText = new CheckBox
            {
                Width = width,
                Height = control.Height,
                Padding = padding,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Background = Constant.Control.QuickPasteFieldHighlightBrush,
                Foreground = Brushes.Green,
                FontStyle = FontStyles.Italic,
                Style = style
            };

            Border border = new Border
            {
                BorderBrush = Brushes.Green,
                BorderThickness = new Thickness(0),
                Child = popupText,
                Width = 17,
                Height = 17,
                CornerRadius = new CornerRadius(2),
            };

            Popup popup = new Popup
            {
                Width = width,
                Height = control.Height,
                VerticalAlignment = VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Left,
                Placement = PlacementMode.Center,
                VerticalOffset = 0,
                HorizontalOffset = horizontalOffset,
                PlacementTarget = control,
                IsOpen = false,
                Child = border,
                AllowsTransparency = true,
                Opacity = 0
            };
            return popup;
        }
        public override void ShowPreviewControlValue(string value)
        {
            // Create the popup overlay
            if (this.PopupPreview == null)
            {
                // We want to shrink the width a bit, as its otherwise a bit wide
                double widthCorrection = 0;
                double width = this.ContentControl.Width - widthCorrection;
                double horizontalOffset = 0;

                // Padding is used to align the text so it begins at the same spot as the control's text
                Thickness padding = new Thickness(0, 0, 0, 0);

                this.PopupPreview = this.CreatePopupPreview(this.ContentControl, padding, width, horizontalOffset);
            }
            // Convert the true/false to a checkmark or none, then show the Popup
            bool check = value.ToLower() == Constant.BooleanValue.True;
            this.ShowPopupPreview(check);
        }
        protected void ShowPopupPreview(bool value)
        {
            Border border = (Border)this.PopupPreview.Child;
            CheckBox popupText = (CheckBox)border.Child;
            popupText.IsChecked = value;
            this.PopupPreview.IsOpen = true;
            Border cbborder = (Border)popupText.Template.FindName("checkBoxBorder", popupText);
            if (cbborder != null)
            {
                cbborder.Background = Constant.Control.QuickPasteFieldHighlightBrush;
            }
        }

        public override void HidePreviewControlValue()
        {
            if (this.PopupPreview == null || this.PopupPreview.Child == null)
            {
                // There is no popupPreview being displayed, so there is nothing to hide.
                return;
            }
            Border border = (Border)this.PopupPreview.Child;
            CheckBox popupText = (CheckBox)border.Child;
            popupText.IsChecked = false;
            this.PopupPreview.IsOpen = false;
        }

        public override void FlashPreviewControlValue()
        {
            this.FlashPopupPreview();
        }

        protected override void FlashPopupPreview()
        {
            if (this.PopupPreview == null || this.PopupPreview.Child == null)
            {
                return;
            }

            // Get the TextBlock
            Border border = (Border)this.PopupPreview.Child;
            CheckBox popupText = (CheckBox)border.Child;

            // Animate the color from white back to its current color
            ColorAnimation animation;
            animation = new ColorAnimation()
            {
                From = Colors.White,
                AutoReverse = false,
                Duration = new Duration(TimeSpan.FromSeconds(.6)),
                EasingFunction = new ExponentialEase()
                {
                    EasingMode = EasingMode.EaseIn
                },
            };

            // Get it all going
            popupText.Background.BeginAnimation(SolidColorBrush.ColorProperty, animation);
        }
        #endregion
    }
}
