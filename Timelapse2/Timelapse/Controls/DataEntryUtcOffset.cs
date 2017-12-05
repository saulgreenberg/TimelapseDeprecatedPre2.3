using System;
using System.Windows.Controls;
using Timelapse.Database;
using Timelapse.Util;

namespace Timelapse.Controls
{
    public class DataEntryUtcOffset : DataEntryControl<UtcOffsetUpDown, Label>
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

        public DataEntryUtcOffset(ControlRow control, DataEntryControls styleProvider) :
            base(control, styleProvider, ControlContentStyle.UTCOffsetBox, ControlLabelStyle.DefaultLabel)
        {
            // configure the various elements
        }

        public override void SetContentAndTooltip(string value)
        {
            if (value == null)
            {
                if (this.ContentControl.Template.FindName("PART_TextBox", this.ContentControl) is Xceed.Wpf.Toolkit.WatermarkTextBox textBox)
                {
                   textBox.Text = (value != null) ? value : Constant.Unicode.Ellipsis;
                   System.Diagnostics.Debug.Print("Ellipses");
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
    }
}
