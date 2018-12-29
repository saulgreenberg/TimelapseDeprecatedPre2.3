using System;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Database;
using Timelapse.Enums;
using Xceed.Wpf.Toolkit;

namespace Timelapse.Controls
{
    public class DataEntryDateTime : DataEntryControl<DateTimePicker, Label>
    {
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
    }
}
