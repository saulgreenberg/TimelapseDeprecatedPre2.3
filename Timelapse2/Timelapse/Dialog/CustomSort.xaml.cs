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
        private const string EmptyDisplay = "-- None --";
        private List<SortTerm> SortTermList;
        private string FileLabel = String.Empty;
        private string DateLabel = String.Empty;
        private FileDatabase database;

        public string SortTerms { get; set; }

        public CustomSort(FileDatabase database)
        {
            this.InitializeComponent();
            this.database = database;
        }

        // When the window is loaded, add SearchTerm controls to it
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Adjust this dialog window position 
            Dialogs.SetDefaultDialogPosition(this);
            Dialogs.TryFitDialogWindowInWorkingArea(this);

            // Get the sort terms. 
            SortTermList = Database.SortTerms.GetSortTerms(database);

            // We need the labels of the File and Date datalabels, as we will check to see if they are seleccted in the combo box
            foreach (SortTerm sortTerm in this.SortTermList)
            {
                if (sortTerm.DataLabel == Constant.DatabaseColumn.File)
                {
                    FileLabel = sortTerm.Label;
                }
                else if (sortTerm.DataLabel == Constant.DatabaseColumn.DateTime)
                {
                    DateLabel = sortTerm.Label;
                }
            }

            // Create the combo box entries showing the sort terms
            // As a side effect, PopulatePrimaryComboBox() invokes PrimaryComboBox_SelectionChanged, which then populates the secondary combo bo
            PopulatePrimaryComboBox(); 
        }

        #region Populate ComboBoxes
        // Populate the two combo boxes  with potential sort terms
        // We use the custom selection to get the field we need, but note that: 
        // - we add a None entry to the secondary combo box, allowing the user to clear the selection

        private void PopulatePrimaryComboBox()
        {
            // By default, we select sort by ID unless its over-ridden
            this.PrimaryComboBox.SelectedIndex = 0;

            foreach (SortTerm sortTerm in this.SortTermList)
            {
                this.PrimaryComboBox.Items.Add(sortTerm.Label);

                // If the current PrimarySort sort term matches the current item, then set it as selected
                if (sortTerm.DataLabel == database.ImageSet.GetSortTerm(0) || sortTerm.DataLabel == database.ImageSet.GetSortTerm(1))
                {
                    this.PrimaryComboBox.SelectedIndex = this.PrimaryComboBox.Items.Count - 1;
                }
            }
        }
        private void PopulateSecondaryComboBox()
        {
            this.SecondaryComboBox.Items.Clear();
            // Add a 'None' entry, as sorting on a second term is optional
            this.SecondaryComboBox.Items.Add(EmptyDisplay);
            this.SecondaryComboBox.SelectedIndex = 0;

            foreach (SortTerm sortTerm in this.SortTermList)
            {
                // If the current sort term is the one already selected in the primary combo box, skip it
                // as it doesn't make sense to sort again on the same term
                if (sortTerm.Label == (string)PrimaryComboBox.SelectedItem)
                {
                    continue;
                }
                this.SecondaryComboBox.Items.Add(sortTerm.Label);

                // If the current SecondarySort sort term matches the current item, then set it as selected.
                // Note that we check both terms for it, as File would be the 2nd term vs. the 1st term
                if (database.ImageSet.GetSortTerm(2) == sortTerm.DataLabel || database.ImageSet.GetSortTerm(3) == sortTerm.DataLabel)
                {
                    this.SecondaryComboBox.SelectedIndex = this.SecondaryComboBox.Items.Count - 1;
                }
            }
        }
        #endregion

        // Whenever the primary combobox changes, repopulated the secondary combo box to make sure it excludes the currently selected item
        private void PrimaryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            PopulateSecondaryComboBox();
        }

        #region Ok/Cancel buttons
        // Apply the selection if the Ok button is clicked
        private void OkButton_Click(object sender, RoutedEventArgs args)
        {
            string selectedPrimaryItem = (string)this.PrimaryComboBox.SelectedItem;
            string selectedSecondaryItem = (string)this.SecondaryComboBox.SelectedItem;
            string term0 = String.Empty;
            string term1 = String.Empty;
            string term2 = String.Empty;
            string term3 = String.Empty;

            foreach (SortTerm sortTerm in this.SortTermList)
            {
                if (selectedPrimaryItem == this.FileLabel)
                {
                    term0 = Constant.DatabaseColumn.RelativePath;
                    term1 = Constant.DatabaseColumn.File;
                }
                else if (selectedPrimaryItem == this.DateLabel)
                {
                    term0 = Constant.DatabaseColumn.DateTime;
                }
                else if (selectedPrimaryItem == sortTerm.Label)
                {
                    term0 = sortTerm.DataLabel;
                    term1 = String.Empty;
                    break;
                }
            }

            if (selectedSecondaryItem != EmptyDisplay)
            {
                foreach (SortTerm sortTerm in this.SortTermList)
                {
                    if (selectedSecondaryItem == FileLabel)
                    {
                        term2 = Constant.DatabaseColumn.RelativePath;
                        term3 = Constant.DatabaseColumn.File;
                        break;
                    }
                    else if (selectedSecondaryItem == DateLabel)
                    {
                        term2 = Constant.DatabaseColumn.DateTime;
                        term3 = String.Empty;
                        break;
                    }
                    else if (selectedSecondaryItem == sortTerm.Label)
                    {
                        term2 = sortTerm.DataLabel;
                        term3 = String.Empty;
                        break;
                    }
                }
            }
            // Create the sort term list from the individual terms
            this.SortTerms = String.Join(",", term0, term1, term2, term3);
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
