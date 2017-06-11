using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;
using Timelapse.Database;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for TemplateChangedAndUpdate.xaml
    /// </summary>
    public partial class TemplateChangedAndUpdate : Window
    {
        private string actionAdd = "Add";
        private string actionDelete = "Delete";

        private Dictionary<string, Dictionary<string, string>> inImageOnly = new Dictionary<string, Dictionary<string, string>>();
        private Dictionary<string, Dictionary<string, string>> inTemplateOnly = new Dictionary<string, Dictionary<string, string>>();
        private List<ComboBox> comboBoxes = new List<ComboBox>();
        private List<int> actionRows = new List<int>();

        private TemplateSyncResults TemplateSyncResults { get; set; }

        public TemplateChangedAndUpdate(
            TemplateSyncResults templateSyncResults,
            Window owner)
        {
            this.InitializeComponent();
            this.Owner = owner;

            this.TemplateSyncResults = templateSyncResults;
            // Build the interface showing datalabels in terms of whether they can be added and renamed, added only, or deleted only.
            if (this.TemplateSyncResults.DataLabelsInTemplateButNotImageDatabase.Count > 0 || this.TemplateSyncResults.DataLabelsInImageButNotTemplateDatabase.Count > 0)
            {
                this.inImageOnly.Add(Constant.Control.Note, this.DictionaryFilterByType(this.TemplateSyncResults.DataLabelsInImageButNotTemplateDatabase, Constant.Control.Note));
                this.inTemplateOnly.Add(Constant.Control.Note, this.DictionaryFilterByType(this.TemplateSyncResults.DataLabelsInTemplateButNotImageDatabase, Constant.Control.Note));

                this.inImageOnly.Add(Constant.Control.Counter, this.DictionaryFilterByType(this.TemplateSyncResults.DataLabelsInImageButNotTemplateDatabase, Constant.Control.Counter));
                this.inTemplateOnly.Add(Constant.Control.Counter, this.DictionaryFilterByType(this.TemplateSyncResults.DataLabelsInTemplateButNotImageDatabase, Constant.Control.Counter));

                this.inImageOnly.Add(Constant.Control.FixedChoice, this.DictionaryFilterByType(this.TemplateSyncResults.DataLabelsInImageButNotTemplateDatabase, Constant.Control.FixedChoice));
                this.inTemplateOnly.Add(Constant.Control.FixedChoice, this.DictionaryFilterByType(this.TemplateSyncResults.DataLabelsInTemplateButNotImageDatabase, Constant.Control.FixedChoice));

                this.inImageOnly.Add(Constant.Control.Flag, this.DictionaryFilterByType(this.TemplateSyncResults.DataLabelsInImageButNotTemplateDatabase, Constant.Control.Flag));
                this.inTemplateOnly.Add(Constant.Control.Flag, this.DictionaryFilterByType(this.TemplateSyncResults.DataLabelsInTemplateButNotImageDatabase, Constant.Control.Flag));

                int row = 0;
                string[] types = { Constant.Control.Note, Constant.Control.Counter, Constant.Control.FixedChoice, Constant.Control.Flag };
                foreach (string type in types)
                {
                    // Changed items that can be renamed
                    int inTemplateCount = this.inTemplateOnly.ContainsKey(type) ? this.inTemplateOnly[type].Count : 0;
                    int inImageOnlyCount = this.inImageOnly.ContainsKey(type) ? this.inImageOnly[type].Count : 0;
              
                    if (inTemplateCount > 0 && inImageOnlyCount > 0)
                    {
                        // Iterated throught the datalabels that can be added or renamed
                        foreach (string datalabel in this.inImageOnly[type].Keys)
                        {
                            row++;
                            this.CreateRow(datalabel, type, row, false, this.actionAdd);
                        }
                        // Iterated throught the datalabels that can be added or renamed
                        foreach (string datalabel in this.inTemplateOnly[type].Keys)
                        {
                            row++;
                            this.CreateRow(datalabel, type, row, true, this.actionDelete);
                        }
                    }
                    else if (inTemplateCount > 0)
                    {
                        // Iterated throught the datalabels that can be only added 
                        foreach (string datalabel in this.inTemplateOnly[type].Keys)
                        {
                            row++;
                            this.CreateRow(datalabel, type, row, true, this.actionAdd);
                        }
                    }
                    else if (inImageOnlyCount > 0)
                    {
                        // Iterated throught the datalabels that can be only deleted 
                        foreach (string datalabel in this.inImageOnly[type].Keys)
                        {
                            row++;
                            this.CreateRow(datalabel, type, row, true, this.actionDelete);
                        }
                    }
                    if (inTemplateCount > 0 || inImageOnlyCount > 0)
                    {
                        row++;
                        this.AddSeparator(row);
                    }
                }
            }
            if (templateSyncResults.ControlSynchronizationWarnings.Count > 0)
            { 
                this.TextBlockDetails.Inlines.Add(new Run { FontWeight = FontWeights.Bold, Text = "Additional Warnings" });
                
                foreach (string warning in templateSyncResults.ControlSynchronizationWarnings)
                {
                    this.TextBlockDetails.Inlines.Add(Environment.NewLine);
                    this.TextBlockDetails.Inlines.Add(new Run { FontWeight = FontWeights.Normal, Text = warning });
                }
            }
        }

        // Position the window relative to its parent
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Utilities.SetDefaultDialogPosition(this);
            Utilities.TryFitWindowInWorkingArea(this);
        }

        // Get a subset of the dictionary filtered by the type of control
        private Dictionary<string, string> DictionaryFilterByType(Dictionary<string, string> dictionary, string controlType)
        {
            return dictionary.Where(i => (i.Value == controlType)).ToDictionary(i => i.Key, i => i.Value);
        }

        // Create a single row in the grid, which displays datalabels in terms of whether they can be added and renamed, added only, or deleted only.
        private void CreateRow(string datalabel, string type, int row, bool addOrDeleteOnly, string action)
        {
            // Create a new row
            RowDefinition rd = new RowDefinition();
            rd.Height = new GridLength(30);
            this.ActionGrid.RowDefinitions.Add(rd);
            this.actionRows.Add(row);

            // Type
            TextBlock textblockType = new TextBlock();
            textblockType.Text = type;
            textblockType.Margin = new Thickness(20, 0, 0, 0);
            textblockType.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(textblockType, 0);
            Grid.SetRow(textblockType, row);
            this.ActionGrid.Children.Add(textblockType);

            // Data label
            TextBlock textblockDataLabel = new TextBlock();
            textblockDataLabel.Text = datalabel;
            textblockDataLabel.Margin = new Thickness(10, 0, 0, 0);
            textblockDataLabel.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(textblockDataLabel, 1);
            Grid.SetRow(textblockDataLabel, row);
            this.ActionGrid.Children.Add(textblockDataLabel);

            // Add or Delete command without renaming
            if (addOrDeleteOnly)
            {
                Label labelActionDefaultAction = new Label();
                labelActionDefaultAction.Tag = rd;
                labelActionDefaultAction.Content = action;
                labelActionDefaultAction.Margin = new Thickness(10, 0, 0, 0);
                labelActionDefaultAction.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(labelActionDefaultAction, 2);
                Grid.SetRow(labelActionDefaultAction, row);
                this.ActionGrid.Children.Add(labelActionDefaultAction);
                return;
            }

            // Add command with renaming
            RadioButton radiobuttonActionDefaultAction = new RadioButton();
            radiobuttonActionDefaultAction.GroupName = datalabel;
            radiobuttonActionDefaultAction.Content = action;
            radiobuttonActionDefaultAction.Margin = new Thickness(10, 0, 0, 0);
            radiobuttonActionDefaultAction.VerticalAlignment = VerticalAlignment.Center;
            radiobuttonActionDefaultAction.IsChecked = true;
            Grid.SetColumn(radiobuttonActionDefaultAction, 2);
            Grid.SetRow(radiobuttonActionDefaultAction, row);
            this.ActionGrid.Children.Add(radiobuttonActionDefaultAction);

            // Combobox showing renaming possibilities

            ComboBox comboboxRenameMenu = new ComboBox();
            comboboxRenameMenu.Width = double.NaN;
            comboboxRenameMenu.Height = 25;
            comboboxRenameMenu.Margin = new Thickness(10, 0, 0, 0);
            comboboxRenameMenu.VerticalAlignment = VerticalAlignment.Center;
            comboboxRenameMenu.MinWidth = 150;
            comboboxRenameMenu.IsEnabled = false;
            foreach (string str in this.inTemplateOnly[type].Keys)
            {
                ComboBoxItem item = new ComboBoxItem();
                item.Content = str;
                item.IsEnabled = true;
                comboboxRenameMenu.Items.Add(item);
            }

            Grid.SetColumn(comboboxRenameMenu, 4);
            Grid.SetRow(comboboxRenameMenu, row);
            this.ActionGrid.Children.Add(comboboxRenameMenu);
            this.comboBoxes.Add(comboboxRenameMenu);

            comboboxRenameMenu.SelectionChanged += this.CbRenameMenu_SelectionChanged;

            RadioButton radiobuttonRenameAction = new RadioButton();
            radiobuttonRenameAction.GroupName = datalabel;
            radiobuttonRenameAction.Content = "Rename to";
            radiobuttonRenameAction.Margin = new Thickness(10, 0, 0, 0);
            radiobuttonRenameAction.VerticalAlignment = VerticalAlignment.Center;
            radiobuttonRenameAction.Tag = comboboxRenameMenu;
            Grid.SetColumn(radiobuttonRenameAction, 3);
            Grid.SetRow(radiobuttonRenameAction, row);
            this.ActionGrid.Children.Add(radiobuttonRenameAction);

            // Enable and disable the combobox depending upon which radiobutton is selected
            radiobuttonRenameAction.Checked += this.RbRenameAction_CheckChanged;
            radiobuttonRenameAction.Unchecked += this.RbRenameAction_CheckChanged;
        }

        // Enable or Disable the Rename comboboxdepending on the state of the Rename radio button
        private void RbRenameAction_CheckChanged(Object o, RoutedEventArgs a)
        {
            RadioButton rb = o as RadioButton;
            ComboBox cb = rb.Tag as ComboBox;
            cb.IsEnabled = (rb.IsChecked == true) ? true : false;
            this.ShowHideItemsAsNeeded();
        }

        // Check other combo box selected values to see if it matches the just-selected combobox data label item, 
        // and if so set it to empty
        private void CbRenameMenu_SelectionChanged(Object o, SelectionChangedEventArgs a)
        {
            ComboBox activeComboBox = o as ComboBox;
            if ((ComboBoxItem)activeComboBox.SelectedItem == null)
            {
                return;
            }
            ComboBoxItem selecteditem = (ComboBoxItem)activeComboBox.SelectedItem;
            string datalabelSelected = selecteditem.Content.ToString();
            foreach (ComboBox combobox in this.comboBoxes)
            {
                if (activeComboBox != combobox)
                {
                    if (combobox.SelectedItem != null)
                    {
                        ComboBoxItem cbi = combobox.SelectedItem as ComboBoxItem;
                        if (cbi.Content.ToString() == datalabelSelected)
                        {
                            combobox.SelectedIndex = -1;
                        }
                    }
                }
            }
            this.ShowHideItemsAsNeeded();
        }

        // For each row, if it contains an enabled rename combobox, then collect its currently selected datalabel (if any)
        // For other rows, if it is a 'Deleted' row, hide or show it depending if it matches one of the currently selected datalabels
        // Note that this is fragile, as it depends on various UI Elements being in various columns and row orders 
        // - eg., arranged by type with delete after renames.
        // Also, collect all the datalabels to add, delete and rename
        private void ShowHideItemsAsNeeded()
        {
            List<string> selectedDataLabels = new List<string>();

            foreach (int row in this.actionRows)
            {
                // Retrieve selected items, but only if the rename radio button is enabled and checked
                // retrieve selected items, but only if the rename radio button is checked
                UIElement uiComboBox = this.GetUIElement(row, 4);
                if (uiComboBox != null)
                {
                    ComboBox cb = uiComboBox as ComboBox;
                    if (cb != null && cb.IsEnabled == true)
                    {
                        ComboBoxItem cbi = cb.SelectedItem as ComboBoxItem;
                        if (cb.SelectedItem != null)
                        {
                            selectedDataLabels.Add(cbi.Content.ToString());
                        }
                        continue;
                    }
                }

                // If this is a Delete action row and a previously selected data label matches it, hide it. 
                Label labelAction = this.GetUIElement(row, 2) as Label;
                if (labelAction == null || labelAction.Content.ToString() != this.actionDelete)
                {
                    continue;
                }

                // Retrieve the data label
                string datalabel = String.Empty;
                TextBlock textblockDataLabel = this.GetUIElement(row, 1) as TextBlock;
                if (textblockDataLabel != null)
                {
                    this.ActionGrid.RowDefinitions[row].Height = selectedDataLabels.Contains(textblockDataLabel.Text) ? new GridLength(0) : new GridLength(30);
                }
            }
        }

        private void CollectItems()
        {
            GridLength activeGridHeight = new GridLength(30);

            foreach (int row in this.actionRows)
            {
                // Check if row is active
                if (this.ActionGrid.RowDefinitions[row].Height != activeGridHeight)
                {
                    continue;
                }

                // Retrieve the data label
                string datalabel = String.Empty;
                TextBlock textblockDataLabel = this.GetUIElement(row, 1) as TextBlock;
                if (textblockDataLabel != null)
                {
                    datalabel = textblockDataLabel.Text;
                }

                // Retrieve the command type
                // If this is a Delete action row and a previously selected data label matches it, hide it.
                Label labelAction = this.GetUIElement(row, 2) as Label;
                if (labelAction != null && labelAction.Content.ToString() == this.actionDelete)
                {
                    // System.Diagnostics.Debug.Print("Delete: " + datalabel);
                    this.TemplateSyncResults.DataLabelsToDelete.Add(datalabel);
                    continue;
                }

                // For Add actions, we need to first check to see if it has been renamed
                UIElement uiComboBox = this.GetUIElement(row, 4);
                if (uiComboBox != null)
                {
                    ComboBox cb = uiComboBox as ComboBox;
                    if (cb != null && cb.IsEnabled == true)
                    {
                        ComboBoxItem cbi = cb.SelectedItem as ComboBoxItem;
                        if (cb.SelectedItem != null)
                        {
                            // System.Diagnostics.Debug.Print("Renamed: " + datalabel + " to " + cbi.Content.ToString());
                            this.TemplateSyncResults.DataLabelsToRename.Add(new KeyValuePair<string, string>(datalabel, cbi.Content.ToString()));
                            continue;
                        }   
                    }
                }

                // If we arrived here, it must be an ACTION_ADD
                // System.Diagnostics.Debug.Print("Added: " + datalabel );
                this.TemplateSyncResults.DataLabelsToAdd.Add(datalabel);
            }
        }
        // Get the UI Element in the indicated row and column from the Action Grid.
        // returns null if no such element exists.
        private UIElement GetUIElement(int row, int column)
        {
            return this.ActionGrid.Children
                   .Cast<UIElement>()
                   .FirstOrDefault(e => Grid.GetRow(e) == row && Grid.GetColumn(e) == column);
        }

        // Create a grey line separator in the Action Grid
        private void AddSeparator(int row)
        {
            RowDefinition rd = new RowDefinition();
            rd.Height = new GridLength(1);
            this.ActionGrid.RowDefinitions.Add(rd);

            Rectangle rect = new Rectangle();
            rect.Fill = Brushes.Gray;
 
            Grid.SetRow(rect, this.ActionGrid.RowDefinitions.Count - 1);
            Grid.SetColumn(rect, 0);
            Grid.SetColumnSpan(rect, 5);
            this.ActionGrid.Children.Add(rect);
        }

        private void UseOldTemplate_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        private void UseNewTemplateButton_Click(object sender, RoutedEventArgs e)
        {
            this.CollectItems();
            this.DialogResult = true;
        }
    }
}