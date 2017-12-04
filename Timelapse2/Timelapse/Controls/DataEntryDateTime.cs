using System;
using System.Windows.Controls;
using Timelapse.Database;
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
            base(control, styleProvider, ControlContentStyle.DateTimeBox, ControlLabelStyle.DefaultLabel)
        {
            // configure the various elements
            DataEntryHandler.Configure(this.ContentControl, null);
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
