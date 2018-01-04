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

        // If value is null, then show and ellipsis. If its a number, show that. Otherwise blank.
        public override void SetContentAndTooltip(string value)
        {
            // This is a hacky approach, but it works.
            // To explain, the IntegerUpDown control supplied with the WPFToolkit only allows numbers. 
            // As we want it to also show both ellipsis and blanks, we have to coerce it to show those.
            // Ideally, we should modify the IntegerUpDown control to allow ellipsis and blanks instead of these hacks.

            // Hack: Access the textbox portion of the IntegerUpDown, so we can write directly into it if needed.
            WatermarkTextBox textBox = (WatermarkTextBox)this.ContentControl.Template.FindName("PART_TextBox", this.ContentControl);

            // A null value indicates we should show the ellipsis symbol in the textbox. 
            // Note that this will not work if we have a null value and a null textbox, but I don't think that case will arise as
            // null textboxes would ever only happen on startup, which is never in an overview mode.
            if (value == null)
            {
                if (textBox != null)
                {
                    textBox.Text = Constant.Unicode.Ellipsis;
                    // System.Diagnostics.Debug.Print("1 Value null, Textbox1 not null: Ellipsis, ");
                }
                // We really need an else statement to somehow coerce it to put in an ellipsis later (if its null), 
                // but I can't do it without changing the IntegerUpDown class
                // else
                // {
                //   System.Diagnostics.Debug.Print("1 Value null, Textbox1 null ");
                // }
            }
            else
            {
                value = value.Trim();
                // The value is non-null, so its either a number or blank.
                // If its a number, just set it to that number
                if (int.TryParse(value, out int intvalue))
                {
                    if (textBox != null)
                    {
                        textBox.Text = intvalue.ToString();
                        // System.Diagnostics.Debug.Print("Intvalue, textbox not null, value is '" + value + "'");
                    }
                    this.ContentControl.Value = intvalue;
                    // System.Diagnostics.Debug.Print("Intvalue: " + value);
                }
                else
                {
                    // If its not a number, blank out the text
                    this.ContentControl.Text = String.Empty;
                    if (textBox != null)
                    {
                        textBox.Text = value;
                        // System.Diagnostics.Debug.Print("2 Value not null and not int, textbox not null, value is '" + value + "'");
                    }
                    // else
                    // {
                    //    System.Diagnostics.Debug.Print("2 Value not null and not int, textbox is null, value is '" + value + "'");
                    // }
                }
            }
            this.ContentControl.ToolTip = value ?? "Edit to change the " + this.Label + " for all selected images";
        }
    }
}
