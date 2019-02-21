﻿using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Timelapse.Database;
using Timelapse.Enums;
using Xceed.Wpf.Toolkit;

namespace Timelapse.Controls
{
    public class DataEntryDateTime : DataEntryControl<DateTimePicker, Label>
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

        public DataEntryNote DateControl { get; set; }
        public DataEntryNote TimeControl { get; set; }

        public DataEntryDateTime(ControlRow control, DataEntryControls styleProvider) :
            base(control, styleProvider, ControlContentStyleEnum.DateTimeBox, ControlLabelStyleEnum.DefaultLabel)
        {
            // configure the various elements
            DataEntryHandler.Configure(this.ContentControl, null);
            this.ContentControl.GotKeyboardFocus += ContentControl_GotKeyboardFocus;
            this.ContentControl.LostKeyboardFocus += ContentControl_LostKeyboardFocus;
        }



        #region Event Handlers

        // Highlight the border whenever the control gets the keyboard focus
        private void ContentControl_GotKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
        {
            this.ContentControl.BorderThickness = new Thickness(Constant.Control.BorderThicknessHighlight);
            this.ContentControl.BorderBrush = Constant.Control.BorderColorHighlight;
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
            if (this.ContentControl.Template.FindName("PART_TextBox", this.ContentControl) is Xceed.Wpf.Toolkit.WatermarkTextBox textBox)
            {
                textBox.Text = value ?? Constant.Unicode.Ellipsis;
            }
            else
            {
                this.ContentControl.Text = value;
            }
            this.ContentControl.ToolTip = value ?? "Edit to change the " + this.Label + " for the selected image";
        }
        #endregion

        #region Visual Effects and Popup Previews
        // Flash the content area of the control
        public override void FlashContentControl()
        {
            // DateTime is never copyable or a candidate for quickpaste, so we do nothing
        }

        public override void ShowPreviewControlValue(string value)
        {
            // DateTime is never copyable or a candidate for quickpaste, so we do nothing
        }
        public override void HidePreviewControlValue()
        {
            // DateTime is never copyable or a candidate for quickpaste, so we do nothing
        }

        public override void FlashPreviewControlValue()
        {
            // DateTime is never copyable or a candidate for quickpaste, so we do nothing
        }
        #endregion
    }
}
