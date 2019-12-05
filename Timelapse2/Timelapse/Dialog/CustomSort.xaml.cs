using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Database;

namespace Timelapse.Dialog
{
    /// <summary>
    /// A dialog allowing a user to create a custom sort by choosing primary and secondary data fields.
    /// </summary>
    public partial class CustomSort : Window
    {
        private List<SortTerm> sortTermList;
        private string fileDisplayLabel = String.Empty;
        private string dateDisplayLabel = String.Empty;
        private readonly FileDatabase database;

        public SortTerm SortTerm1 { get; set; }
        public SortTerm SortTerm2 { get; set; }

        public CustomSort(FileDatabase database)
        {
            this.InitializeComponent();
            this.database = database;
            this.SortTerm1 = new SortTerm();
            this.SortTerm2 = new SortTerm();
        }

        // When the window is loaded, add SearchTerm controls to it
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Adjust this dialog window position 
            Dialogs.SetDefaultDialogPosition(this);
            Dialogs.TryFitDialogWindowInWorkingArea(this);

            // Get the sort terms. 
            this.sortTermList = Database.SortTerms.GetSortTerms(this.database.CustomSelection.SearchTerms);

            // We need the labels of the File and Date datalabels, as we will check to see if they are seleccted in the combo box
            foreach (SortTerm sortTerm in this.sortTermList)
            {
                if (sortTerm.DataLabel == Constant.DatabaseColumn.File)
                {
                    this.fileDisplayLabel = sortTerm.DisplayLabel;
                }
                else if (sortTerm.DataLabel == Constant.DatabaseColumn.DateTime)
                {
                    this.dateDisplayLabel = sortTerm.DisplayLabel;
                }
            }

            // Create the combo box entries showing the sort terms
            // As a side effect, PopulatePrimaryComboBox() invokes PrimaryComboBox_SelectionChanged, which then populates the secondary combo bo
            this.PopulatePrimaryUIElements();
        }

        #region Populate ComboBoxes
        // Populate the two combo boxes  with potential sort terms
        // We use the custom selection to get the field we need, but note that: 
        // - we add a None entry to the secondary combo box, allowing the user to clear the selection
        private void PopulatePrimaryUIElements()
        {
            // Populate the Primary combo box with choices
            // By default, we select sort by ID unless its over-ridden
            this.PrimaryComboBox.SelectedIndex = 0;
            SortTerm sortTermDB = this.database.ImageSet.GetSortTerm(0); // Get the 1st sort term from the database
            foreach (SortTerm sortTerm in this.sortTermList)
            {
                this.PrimaryComboBox.Items.Add(sortTerm.DisplayLabel);

                // If the current PrimarySort sort term matches the current item, then set it as selected
                // if (sortTerm.DataLabel == database.ImageSet.GetSortTerm(0) || sortTerm.DataLabel == database.ImageSet.GetSortTerm(1

                if (sortTerm.DataLabel == sortTermDB.DataLabel)
                {
                    this.PrimaryComboBox.SelectedIndex = this.PrimaryComboBox.Items.Count - 1;
                }
            }

            // Set the radio buttons to the default values
            this.PrimaryAscending.IsChecked = sortTermDB.IsAscending == Constant.BooleanValue.True;
            this.PrimaryDescending.IsChecked = sortTermDB.IsAscending == Constant.BooleanValue.False;
        }
        private void PopulateSecondaryUIElements()
        {
            // Populate the Secondary combo box with choices
            // By default, we select "None' unless its over-ridden
            this.SecondaryComboBox.Items.Clear();
            // Add a 'None' entry, as sorting on a second term is optional
            this.SecondaryComboBox.Items.Add(Constant.SortTermValues.NoneDisplayLabel);
            this.SecondaryComboBox.SelectedIndex = 0;

            SortTerm sortTermDB = this.database.ImageSet.GetSortTerm(1); // Get the 2nd sort term from the database
            foreach (SortTerm sortTerm in this.sortTermList)
            {
                // If the current sort term is the one already selected in the primary combo box, skip it
                // as it doesn't make sense to sort again on the same term
                if (sortTerm.DisplayLabel == (string)this.PrimaryComboBox.SelectedItem)
                {
                    continue;
                }
                this.SecondaryComboBox.Items.Add(sortTerm.DisplayLabel);

                // If the current SecondarySort sort term matches the current item, then set it as selected.
                // Note that we check both terms for it, as File would be the 2nd term vs. the 1st term
                if (sortTermDB.DataLabel == sortTerm.DataLabel)
                {
                    this.SecondaryComboBox.SelectedIndex = this.SecondaryComboBox.Items.Count - 1;
                }
            }
            // Set the radio buttons to the default values
            this.SecondaryAscending.IsChecked = sortTermDB.IsAscending == Constant.BooleanValue.True;
            this.SecondaryDescending.IsChecked = sortTermDB.IsAscending == Constant.BooleanValue.False;
        }
        #endregion

        // Whenever the primary combobox changes, repopulated the secondary combo box to make sure it excludes the currently selected item
        private void PrimaryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            this.PopulateSecondaryUIElements();
        }

        #region Ok/Cancel buttons
        // Apply the selection if the Ok button is clicked
        private void OkButton_Click(object sender, RoutedEventArgs args)
        {
            string selectedPrimaryItem = (string)this.PrimaryComboBox.SelectedItem;
            string selectedSecondaryItem = (string)this.SecondaryComboBox.SelectedItem;

            foreach (SortTerm sortTerm in this.sortTermList)
            {
                if (selectedPrimaryItem == this.fileDisplayLabel)
                {
                    this.SortTerm1.DataLabel = Constant.DatabaseColumn.File;
                    this.SortTerm1.DisplayLabel = this.fileDisplayLabel;
                    this.SortTerm1.ControlType = String.Empty;
                }
                else if (selectedPrimaryItem == this.dateDisplayLabel)
                {
                    this.SortTerm1.DataLabel = Constant.DatabaseColumn.DateTime;
                    this.SortTerm1.DisplayLabel = this.dateDisplayLabel;
                    this.SortTerm1.ControlType = String.Empty;
                }
                else if (selectedPrimaryItem == sortTerm.DisplayLabel)
                {
                    this.SortTerm1.DataLabel = sortTerm.DataLabel;
                    this.SortTerm1.DisplayLabel = sortTerm.DisplayLabel;
                    this.SortTerm1.ControlType = sortTerm.ControlType;
                }
                this.SortTerm1.IsAscending = (this.PrimaryAscending.IsChecked == true) ? Constant.BooleanValue.True : Constant.BooleanValue.False;
            }

            if (selectedSecondaryItem != Constant.SortTermValues.NoneDisplayLabel)
            {
                foreach (SortTerm sortTerm in this.sortTermList)
                {
                    if (selectedSecondaryItem == this.fileDisplayLabel)
                    {
                        this.SortTerm2.DataLabel = Constant.DatabaseColumn.File;
                        this.SortTerm2.DisplayLabel = this.fileDisplayLabel;
                        this.SortTerm2.ControlType = String.Empty;
                        this.SortTerm2.IsAscending = (this.SecondaryAscending.IsChecked == true) ? Constant.BooleanValue.True : Constant.BooleanValue.False;
                    }
                    else if (selectedSecondaryItem == this.dateDisplayLabel)
                    {
                        this.SortTerm2.DataLabel = Constant.DatabaseColumn.DateTime;
                        this.SortTerm2.DisplayLabel = this.dateDisplayLabel;
                        this.SortTerm2.ControlType = String.Empty;
                        this.SortTerm2.IsAscending = (this.SecondaryAscending.IsChecked == true) ? Constant.BooleanValue.True : Constant.BooleanValue.False;
                    }
                    else if (selectedSecondaryItem == sortTerm.DisplayLabel)
                    {
                        this.SortTerm2.DataLabel = sortTerm.DataLabel;
                        this.SortTerm2.DisplayLabel = sortTerm.DisplayLabel;
                        this.SortTerm2.ControlType = sortTerm.ControlType;
                        this.SortTerm2.IsAscending = (this.SecondaryAscending.IsChecked == true) ? Constant.BooleanValue.True : Constant.BooleanValue.False;
                    }
                }
            }
            this.DialogResult = true;
        }

        // Cancel - exit the dialog without doing anythikng.
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
        #endregion
    }
}
