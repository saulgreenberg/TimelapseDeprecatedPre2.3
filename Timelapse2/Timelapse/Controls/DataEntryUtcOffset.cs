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
            if (this.ContentControl.Template.FindName("PART_TextBox", this.ContentControl) is Xceed.Wpf.Toolkit.WatermarkTextBox textBox)
            {
                textBox.Text = value ?? Constant.Unicode.Ellipsis;
            }
            else
            {
                this.ContentControl.Value = DateTimeHandler.ParseDatabaseUtcOffsetString(value);
            }
            this.ContentControl.ToolTip = value ?? "Edit to change the " + this.Label + " for the selected image";
        }
    }
}
