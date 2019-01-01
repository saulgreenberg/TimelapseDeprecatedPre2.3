using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Timelapse.Database;
using Timelapse.Enums;

namespace Timelapse.Controls
{
    // A FixedChoice comprises a stack panel containing
    // - a label containing the descriptive label) 
    // - a combobox (containing the content) at the given width
    public class DataEntryChoice : DataEntryControl<ComboBox, Label>
    {
        /// <summary>Gets or sets the content of the choice.</summary>
        public override string Content
        {
            get { return this.ContentControl.Text; }
        }

        public override bool ContentReadOnly
        {
            get { return this.ContentControl.IsReadOnly; }
            set { this.ContentControl.IsReadOnly = value; }
        }

        public DataEntryChoice(ControlRow control, DataEntryControls styleProvider)
            : base(control, styleProvider, ControlContentStyleEnum.ChoiceComboBox, ControlLabelStyleEnum.DefaultLabel)
        {
            // The behaviour of the combobox
            this.ContentControl.Focusable = true;
            this.ContentControl.IsEditable = false;
            this.ContentControl.IsTextSearchEnabled = true;

            // Callback used to allow Enter to select the highlit item
            this.ContentControl.PreviewKeyDown += this.ContentCtl_PreviewKeyDown;

            // Add items to the combo box. If we have an  EmptyChoiceItem, then  add an 'empty string' to the end 
            List<string> choiceList = control.GetChoices(out bool includesEmptyChoice);
            ComboBoxItem cbi;
            foreach (string choice in choiceList)
            {
                cbi = new ComboBoxItem()
                {
                    Content = choice
                };
                this.ContentControl.Items.Add(cbi);
            }
            if (includesEmptyChoice)
            {
                // put empty choice at the beginning of the control below a separator for visual clarity
                cbi = new ComboBoxItem()
                {
                    Content = String.Empty
                };
                this.ContentControl.Items.Insert(0, cbi);
            }
            // We include an invisible ellipsis menu item. This allows us to display an ellipsis in the combo box text field
            // when multiple images with different values are selected
            cbi = new ComboBoxItem()
            {
                Content = Constant.Unicode.Ellipsis
            };
            this.ContentControl.Items.Insert(0, cbi);
            ((ComboBoxItem)this.ContentControl.Items[0]).Visibility = System.Windows.Visibility.Collapsed;     
            this.ContentControl.SelectedIndex = 0;
        }

        // Users may want to use the text search facility on the combobox, where they type the first letter and then enter
        // For some reason, it wasn't working on pressing 'enter' so this handler does that.
        // Whenever a return or enter is detected on the combobox, it finds the highlight item (i.e., that is highlit from the text search)
        // and sets the combobox to that value.
        private void ContentCtl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return || e.Key == Key.Enter)
            {
                ComboBox comboBox = sender as ComboBox;
                for (int i = 0; i < comboBox.Items.Count; i++)
                {
                    ComboBoxItem comboBoxItem = (ComboBoxItem)comboBox.ItemContainerGenerator.ContainerFromIndex(i);
                    if (comboBoxItem != null && comboBoxItem.IsHighlighted)
                    {
                        comboBox.SelectedValue = comboBoxItem.Content.ToString();
                    }
                }
            }
            else if (e.Key == Key.Up || e.Key == Key.Right || e.Key == Key.Down  || e.Key == Key.Left)
            {
                // Because we have inserted an invisible ellipses into the list, we have to skip over it when a 
                // user navigates the combobox with the keyboard using the arrow keys
                ComboBox comboBox = sender as ComboBox;
                
                if ((e.Key == Key.Up || e.Key == Key.Left) && (comboBox.SelectedIndex == 1 || comboBox.SelectedIndex == -1))
                {
                    // If the user tries to navigate to the ellisis at the beginning of the list, keep it on the first valid item
                    if (comboBox.SelectedIndex == -1)
                    {
                        comboBox.SelectedIndex = 1;
                    }
                    e.Handled = true;
                } 
                else if ((e.Key == Key.Down || e.Key == Key.Right) && (comboBox.SelectedIndex == comboBox.Items.Count - 1 || comboBox.SelectedIndex == -1))
                {
                    // If the user tries to navigate beyond the end of the list, keep it on the last valid item
                    // But the -1 should only be triggered to go back to the beginning
                    if (comboBox.SelectedIndex == -1)
                    {
                        comboBox.SelectedIndex = 1; 
                    }
                    e.Handled = true;
                }
            }
        }

        public override void ShowPreviewControlValue(string value)
        {
            // Create the popup overlay
            if (this.PopupPreview == null)
            {
                // We want to expose the arrow on the choice menu, so subtract its width and move the horizontal offset over
                double arrowWidth = 20;
                double width = this.ContentControl.Width - arrowWidth;
                double horizontalOffset = -arrowWidth / 2;

                // Padding is used to align the text so it begins at the same spot as the control's text
                Thickness padding = new Thickness(5.5, 6.5, 0, 0);

                this.PopupPreview = this.CreatePopupPreview(this.ContentControl, padding, width, horizontalOffset);
            }
            // Show the popup
            this.ShowPopupPreview(value);
        }
        public override void HidePreviewControlValue()
        {
            this.HidePopupPreview();
        }

        // Set the Control's Content and Tooltip to the provided value
        public override void SetContentAndTooltip(string value)
        {
            // If the value is null, an ellipsis will be drawn in the checkbox (see Checkbox style)
            // Used to signify the indeterminate state in no or multiple selections in the overview.
            if (value == null)
            {
                this.ContentControl.Text = Constant.Unicode.Ellipsis;
                this.ContentControl.ToolTip = "Select an item to change the " + this.Label + " for all selected images";
                return;
            }
            this.ContentControl.Text = value;
            this.ContentControl.ToolTip = String.IsNullOrEmpty(value) ? value : this.LabelControl.ToolTip;
        }
    }
}
