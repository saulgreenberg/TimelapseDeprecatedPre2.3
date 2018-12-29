using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Database;
using Timelapse.Enums;

namespace Timelapse.Controls
{
    // A note lays out a stack panel containing
    // - a label containing the descriptive label) 
    // - an editable textbox (containing the content) at the given width
    public class DataEntryNote : DataEntryControl<AutocompleteTextBox, Label>
    {
        /// <summary>Gets  the content of the note</summary>
        public override string Content
        {
            get { return this.ContentControl.Text; }
        }

        public bool ContentChanged { get; set; }

        public override bool ContentReadOnly
        {
            get { return this.ContentControl.IsReadOnly; }
            set { this.ContentControl.IsReadOnly = value; }
        }

        public DataEntryNote(ControlRow control, List<string> autocompletions, DataEntryControls styleProvider) :
            base(control, styleProvider, ControlContentStyleEnum.NoteTextBox, ControlLabelStyleEnum.DefaultLabel)
        {
            // Now configure the various elements
            this.ContentControl.Autocompletions = autocompletions;
            this.ContentChanged = false;
        }

        public override void SetContentAndTooltip(string value)
        {
            // If the value is null, an ellipsis will be drawn in the checkbox (see Checkbox style)
            // Used to signify the indeterminate state in no or multiple selections in the overview.
            if (value == null)
            {
                this.ContentControl.Text = Constant.Unicode.Ellipsis;
                this.ContentControl.ToolTip = "Edit to change the " + this.Label + " for all selected images";
                return;
            }
            // Otherwise, the note will be set to the provided value 
            // If the value to be empty, we just make it the same as the tooltip so something meaningful is displayed..
            this.ContentChanged = this.ContentControl.Text != value;
            this.ContentControl.Text = value;
            this.ContentControl.ToolTip = String.IsNullOrEmpty(value) ? value : this.LabelControl.ToolTip;
        }
    }
}