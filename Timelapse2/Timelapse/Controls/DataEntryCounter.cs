using System;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Database;
using Xceed.Wpf.Toolkit;

namespace Timelapse.Controls
{
    // A counter comprises a stack panel containing
    // - a radio button containing the descriptive label
    // - an editable textbox (containing the content) at the given width
    public class DataEntryCounter : DataEntryControl<IntegerUpDown, RadioButton>
    {
        // Holds the DataLabel of the previously clicked counter control across all counters
        private static string previousControlDataLabel = String.Empty;

        /// <summary>Gets or sets the content of the counter.</summary>
        public override string Content
        {
            get { return this.ContentControl.Text; }
        }

        public override bool ContentReadOnly
        {
            get { return this.ContentControl.IsReadOnly; }
            set { this.ContentControl.IsReadOnly = value; }
        }

        public bool IsSelected
        {
            get { return this.LabelControl.IsChecked.HasValue ? (bool)this.LabelControl.IsChecked : false; }
        }

        public DataEntryCounter(ControlRow control, DataEntryControls styleProvider) :
            base(control, styleProvider, ControlContentStyle.CounterTextBox, ControlLabelStyle.CounterButton)
        {
            // Configure the various elements if needed
            // Assign all counters to a single group so that selecting a new counter deselects any currently selected counter
            this.LabelControl.GroupName = "DataEntryCounter";
            this.LabelControl.Click += this.LabelControl_Click;
            this.ContentControl.Width += 18; // to account for the width of the spinner
            this.ContentControl.PreviewKeyDown += ContentControl_PreviewKeyDown;
            this.ContentControl.PreviewTextInput += ContentControl_PreviewTextInput;
        }

        #region Event Handlers
        // Behaviour: enable the counter textbox for editing
        // SAULXX The textbox in the IntegerUpDown is, for some unknown reason, disabled and thus disallows text input.
        // This hack seems to fix it. 
        //  A better solution is to find out where it is being disabled and fix it there.
        private void ContentControl_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (this.ContentControl.Template.FindName("PART_TextBox", this.ContentControl) is Xceed.Wpf.Toolkit.WatermarkTextBox textBox)
            {
                textBox.IsReadOnly = false;
            }
        }

        // Behaviour: Ignore any non-numeric input (but backspace delete etc work just fine)
        private void ContentControl_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            if (int.TryParse(e.Text, out int number) == false)
            {
                e.Handled = true;
            }
        }

        // Behaviour: If the currently clicked counter is deselected, it will be selected and all other counters will be deselected,
        // If the currently clicked counter is selected, it will be deselected along with all other counters will be deselected,
        private void LabelControl_Click(object sender, RoutedEventArgs e)
        {
            if (previousControlDataLabel == null)
            {
                // System.Diagnostics.Debug.Print("1 - " + previousControl + " : " + this.DataLabel);
                this.LabelControl.IsChecked = true;
                previousControlDataLabel = this.DataLabel;
            }
            else if (previousControlDataLabel == this.DataLabel)
            {
                // System.Diagnostics.Debug.Print("1 - " + previousControl + " : " + this.DataLabel);
                this.LabelControl.IsChecked = false;
                previousControlDataLabel = String.Empty;
            }
            else
            {
                // System.Diagnostics.Debug.Print("1 - " + previousControl + " : " + this.DataLabel);
                this.LabelControl.IsChecked = true;
                previousControlDataLabel = this.DataLabel;
            }
        }
        #endregion

        public override void SetContentAndTooltip(string value)
        {
            // This if clause should always be true, but just in case...
            if (this.ContentControl.Template.FindName("PART_TextBox", this.ContentControl) is Xceed.Wpf.Toolkit.WatermarkTextBox textBox)
            {
                textBox.Text = value ?? Constant.Unicode.Ellipsis;
            }
            else
            {
                this.ContentControl.Text = value;
            }
            this.ContentControl.ToolTip = value ?? "Edit to change the " + this.Label + " for all selected images";
        }
    }
}
