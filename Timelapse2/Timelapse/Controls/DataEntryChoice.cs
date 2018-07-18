using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Timelapse.Database;

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
            : base(control, styleProvider, ControlContentStyle.ChoiceComboBox, ControlLabelStyle.DefaultLabel)
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
                // put empty choice at the end of the control below a separator for visual clarity
                // this.ContentControl.Items.Add(new Separator());
                cbi = new ComboBoxItem()
                {
                    Content = String.Empty
                };
                this.ContentControl.Items.Add(cbi);
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
