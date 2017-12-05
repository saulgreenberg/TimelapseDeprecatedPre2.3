using System;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Database;

namespace Timelapse.Controls
{
    // A flag comprises a stack panel containing
    // - a label containing the descriptive label) 
    // - checkbox (the content) at the given width
    public class DataEntryFlag : DataEntryControl<CheckBox, Label>
    {
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
            : base(control, styleProvider, ControlContentStyle.FlagCheckBox, ControlLabelStyle.DefaultLabel)
        {
        }

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
    }
}
