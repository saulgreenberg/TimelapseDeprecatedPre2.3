using System;
using System.Collections.Generic;
using System.Windows.Controls;
using Timelapse.Database;

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
            base(control, styleProvider, ControlContentStyle.NoteTextBox, ControlLabelStyle.DefaultLabel)
        {
            // Now configure the various elements
            this.ContentControl.Autocompletions = autocompletions;
            this.ContentChanged = false;
        }

        public override void SetContentAndTooltip(string value)
        {
            this.ContentChanged = this.ContentControl.Text != value;
            this.ContentControl.Text = value;
            this.ContentControl.ToolTip = (value != String.Empty) ? value : this.LabelControl.ToolTip;
        }
    }
}