using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Timelapse.Database;
using Timelapse.Enums;
using Xceed.Wpf.Toolkit;

namespace Timelapse.Controls
{
    // A counter comprises a stack panel containing
    // - a radio button containing the descriptive label
    // - an editable textbox (containing the content) at the given width
    public class DataEntryCounter : DataEntryControl<IntegerUpDown, RadioButton>
    {
        // Return the TopLeft corner of the content control as a point
        public override Point TopLeft
        {
            get { return this.ContentControl.PointToScreen(new Point(0, 0)); }
        }
        public override UIElement GetContentControl
        {
            get { return this.ContentControl; }
        }

        public override bool IsContentControlEnabled
        {
            get { return this.ContentControl.IsEnabled; }
        }

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
            base(control, styleProvider, ControlContentStyleEnum.CounterTextBox, ControlLabelStyleEnum.CounterButton)
        {
            // Configure the various elements if needed
            // Assign all counters to a single group so that selecting a new counter deselects any currently selected counter
            this.LabelControl.GroupName = "DataEntryCounter";
            this.LabelControl.Click += this.LabelControl_Click;
            this.ContentControl.Width += 18; // to account for the width of the spinner
            this.ContentControl.PreviewKeyDown += ContentControl_PreviewKeyDown;
            this.ContentControl.PreviewTextInput += ContentControl_PreviewTextInput;
            this.ContentControl.GotKeyboardFocus += ContentControl_GotKeyboardFocus;
            this.ContentControl.LostKeyboardFocus += ContentControl_LostKeyboardFocus;
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
                this.LabelControl.IsChecked = true;
                previousControlDataLabel = this.DataLabel;
            }
            else if (previousControlDataLabel == this.DataLabel)
            {
                this.LabelControl.IsChecked = false;
                previousControlDataLabel = String.Empty;
            }
            else
            {
                this.LabelControl.IsChecked = true;
                previousControlDataLabel = this.DataLabel;
            }
            // Also set the keyboard focus to this control 
            Keyboard.Focus(this.ContentControl);
        }

        // Behaviour: Highlight the border and make the text caret appear whenever the control gets the keyboard focus
        private void ContentControl_GotKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
        {
            this.ContentControl.BorderThickness = new Thickness(Constant.Control.BorderThicknessHighlight);
            this.ContentControl.BorderBrush = Constant.Control.BorderColorHighlight;
            if (this.ContentControl.Template.FindName("PART_TextBox", this.ContentControl) is Xceed.Wpf.Toolkit.WatermarkTextBox textBox)
            {
                textBox.IsReadOnlyCaretVisible = true;
            }
        }

        // Behaviour: Revert the border whenever the control loses the keyboard focus
        private void ContentControl_LostKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
        {
            this.ContentControl.BorderThickness = new Thickness(Constant.Control.BorderThicknessNormal);
            this.ContentControl.BorderBrush = Constant.Control.BorderColorNormal;
        }
        #endregion

        #region Setting Content and Tooltip
        // Changing a counter value does not trigger a ValueChanged event if the values are the same.
        // which means multiple images may not be updated even if other images have the same value.
        // To get around this, we set a bogus value and then the real value, which means that the
        // ValueChanged event will be triggered. Inefficient, but seems to work.
        public void SetBogusCounterContentAndTooltip()
        {
            SetContentAndTooltip(int.MaxValue.ToString());
        }

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
                    }
                    this.ContentControl.Value = intvalue;
                }
                else
                {
                    // If its not a number, blank out the text
                    this.ContentControl.Text = String.Empty;
                    if (textBox != null)
                    {
                        textBox.Text = value;
                    }
                }
            }
            this.ContentControl.ToolTip = value ?? "Edit to change the " + this.Label + " for all selected images";
        }
        #endregion

        #region Visual Effects and Popup Previews
        // Flash the content area of the control
        public override void FlashContentControl()
        {
            TextBox contentHost = (TextBox)this.ContentControl.Template.FindName("PART_TextBox", this.ContentControl);
            if (contentHost != null)
            {
                contentHost.Background = new SolidColorBrush(Colors.White);
                contentHost.Background.BeginAnimation(SolidColorBrush.ColorProperty, this.GetColorAnimation());
            }
        }

        public override void ShowPreviewControlValue(string value)
        {
            // Create the popup overlay
            if (this.PopupPreview == null)
            {
                // We want to expose the up/down controls, so subtract its width and move the horizontal offset over
                double integerUpDownWidth = 16;
                double width = this.ContentControl.Width - integerUpDownWidth;
                double horizontalOffset = -integerUpDownWidth / 2;

                // Padding is used to align the text so it begins at the same spot as the control's text
                Thickness padding = new Thickness(7, 5.5, 0, 0);

                this.PopupPreview = this.CreatePopupPreview(this.ContentControl, padding, width, horizontalOffset);
            }
            // Show the popup
            this.ShowPopupPreview(value);
        }
        public override void HidePreviewControlValue()
        {
            if (this.PopupPreview != null)
            { 
                this.HidePopupPreview();
            }
        }

        public override void FlashPreviewControlValue()
        {
            this.FlashPopupPreview();
        }
        #endregion
    }
}
