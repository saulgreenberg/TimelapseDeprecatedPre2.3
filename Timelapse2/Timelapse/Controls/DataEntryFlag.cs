using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Timelapse.Database;
using Timelapse.Enums;

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
            this.ContentControl.PreviewKeyDown += ContentControl_PreviewKeyDown;
        }

        #region Event Handlers
        // Ignore these navigation key events, as otherwise they act as tabs which does not conform to how we navigate
        // between other control types
        private void ContentControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Right || e.Key == Key.Left)
            {
                e.Handled = true;
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
        public override void FlashContentControlValue()
        {
            Border border = (Border)this.ContentControl.Template.FindName("checkBoxBorder", this.ContentControl);
            if (border != null)
            {
                border.Background.BeginAnimation(SolidColorBrush.ColorProperty, this.GetColorAnimation());
            }
        }

        public override void ShowPreviewControlValue(string value)
        {
            // Create the popup overlay
            if (this.PopupPreview == null)
            {
                // We want to shring the width a bit, as its otherwise a bit wide
                double widthCorrection = 2;
                double width = this.ContentControl.Width - widthCorrection;
                double horizontalOffset = -widthCorrection / 2;

                // Padding is used to align the text so it begins at the same spot as the control's text
                Thickness padding = new Thickness(2.5, 3, 0, 0);

                this.PopupPreview = this.CreatePopupPreview(this.ContentControl, padding, width, horizontalOffset);
            }
            // Convert the true/false to a checkmark or none, then show the Popup
            string check = (value.ToLower() == Constant.BooleanValue.True) ? "\u2713" : String.Empty;
            this.ShowPopupPreview(check);
        }
        public override void HidePreviewControlValue()
        {
            this.HidePopupPreview();
        }

        public override void FlashPreviewControlValue()
        {
            this.FlashPopupPreview();
        }
        #endregion
    }
}
