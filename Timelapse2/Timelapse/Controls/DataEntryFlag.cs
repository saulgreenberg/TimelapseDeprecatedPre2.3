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
            get { return ((bool)this.ContentControl.IsChecked) ? Constant.Boolean.True : Constant.Boolean.False; }
        }

        public override bool ContentReadOnly
        {
            get { return !this.ContentControl.IsEnabled; }
            set { this.ContentControl.IsEnabled = !value; }
        }

        public DataEntryFlag(ControlRow control, DataEntryControls styleProvider)
            : base(control, styleProvider, ControlContentStyle.FlagCheckBox, ControlLabelStyle.DefaultLabel)
        {
        }

        public override void SetContentAndTooltip(string value)
        {
            value = value.ToLower();
            this.ContentControl.IsChecked = (value == Constant.Boolean.True) ? true : false;
            this.ContentControl.ToolTip = (value != String.Empty) ? value : this.LabelControl.ToolTip;
        }
    }
}
