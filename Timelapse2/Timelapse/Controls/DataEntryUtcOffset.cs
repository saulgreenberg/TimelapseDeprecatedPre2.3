using System;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Database;
using Timelapse.Enums;

namespace Timelapse.Controls
{
    public class DataEntryUtcOffset : DataEntryControl<UtcOffsetUpDown, Label>
    {
        // Return the TopLeft corner of the content control as a point
        public override Point TopLeft
        {
            get { return this.ContentControl.PointToScreen(new Point(0, 0)); }
        }
        public override UIElement GetContentControl
        {
            // We return the textbox part of the content control, as otherwise focus does not work properly when we try to set it on the content control
            get { return (UIElement)this.ContentControl.Template.FindName("PART_TextBox", this.ContentControl); }
        }

        public override bool IsContentControlEnabled
        {
            get { return this.ContentControl.IsEnabled; }
        }

        public override string Content
        {
            get { return this.ContentControl.Text; }
        }

        public override bool ContentReadOnly
        {
            get { return this.ContentControl.IsReadOnly; }
            set { this.ContentControl.IsReadOnly = value; }
        }

        public DataEntryUtcOffset(ControlRow control, DataEntryControls styleProvider) :
            base(control, styleProvider, ControlContentStyleEnum.UTCOffsetBox, ControlLabelStyleEnum.DefaultLabel)
        {
            // Callback to change the look of the control whenever it gets the focus
            this.ContentControl.GotKeyboardFocus += ContentControl_GotKeyboardFocus;
            this.ContentControl.LostKeyboardFocus += ContentControl_LostKeyboardFocus;
            // configure the various elements
        }

        #region Event Handlers
        // Highlight the border whenever the control gets the keyboard focus
        private void ContentControl_GotKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
        {
            this.ContentControl.BorderThickness = new Thickness(Constant.Control.BorderThicknessHighlight);
            this.ContentControl.BorderBrush = Constant.Control.BorderColorNormal;
        }

        private void ContentControl_LostKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
        {
            this.ContentControl.BorderThickness = new Thickness(Constant.Control.BorderThicknessNormal);
            this.ContentControl.BorderBrush = Constant.Control.BorderColorNormal;
        }
        #endregion

        #region Setting Content and Tooltip
        public override void SetContentAndTooltip(string value)
        {
            if (value == null)
            {
                if (this.ContentControl.Template.FindName("PART_TextBox", this.ContentControl) is Xceed.Wpf.Toolkit.WatermarkTextBox textBox)
                {
                    textBox.Text = (value != null) ? value : Constant.Unicode.Ellipsis;
                }
            }
            else
            {
                // Hack: The content control doesn't update if the value hasn't changed.
                // However, this means that if we are displaying the ellipses in the textbox, it won't 
                // redisplay the old value on selection or navigation if it hasn't actually changed
                // So we set it twice: the first time with a different value to guarantee that it has changed, and the second time with the
                // desired value ot actually display it
                double hours = double.Parse(value);
                this.ContentControl.Value = TimeSpan.FromHours(hours + 1);
                this.ContentControl.Value = TimeSpan.FromHours(hours);
            }
            this.ContentControl.ToolTip = value ?? "Edit to change the " + this.Label + " for the selected image";
        }
        #endregion

        #region Visual Effects and Popup Previews
        // Flash the content area of the control
        public override void FlashContentControlValue()
        {
            // UtcOffset is never copyable or a candidate for quickpaste, so we do nothing
        }

        public override void ShowPreviewControlValue(string value)
        {
            // UtcOffset is never copyable or a candidate for quickpaste, so we do nothing
        }
        public override void HidePreviewControlValue()
        {
            // UtcOffset is never copyable or a candidate for quickpaste, so we do nothing
        }

        public override void FlashPreviewControlValue()
        {
            // UtcOffset is never copyable or a candidate for quickpaste, so we do nothing
        }
        #endregion
    }
}
