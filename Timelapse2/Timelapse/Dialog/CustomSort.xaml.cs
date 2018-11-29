using System;
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
        private const string FileRelativePathDisplayName = "File name (including relative path)";
        private const string EmptyDisplay = "-- None --";

        private FileDatabase database;

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

            // Create the sort term combo box entries
            // Note that we use the custom selection for convenience, as it gets the fields we need. 
            // Note that it also skips hidden fields, and only shows DateTime vs. Date and Time
            // However, we have to special case File/Relative Path (we do it on relative path), as it is actually two search terms.
            // We also have to delete one of the DateTimes, as custom selection creates two of them

            PopulatePrimaryComboBox();
            PopulateSecondaryComboBox();
        }

        #region Populate ComboBoxes
        // Populate the two combo boxes  with potential sort terms
        // We use the custom selection to get the field we need, but note that: 
        // - it skips hidden fields, 
        // - it only shows DateTime vs. Date and Time
        // - we have to add Id as it is not included in the custom selection list
        // - we have to special case the File/RelativePath terms (we do it on relative path), as we want to combine both as a single search term.
        // - as custom selection generates two DateTimes, we have to delete one of them
        private void PopulatePrimaryComboBox()
        {
            // By default, we select sort by ID unless its over-ridden
            this.PrimaryComboBox.Items.Add(Constant.DatabaseColumn.ID);
            this.PrimaryComboBox.SelectedIndex = 0;

            bool datetimeAdded = false;
            foreach (SearchTerm searchTerm in this.database.CustomSelection.SearchTerms)
            {
                string databaseColumnName = searchTerm.DataLabel;
                string displayName = (searchTerm.DataLabel == Constant.DatabaseColumn.RelativePath) ? FileRelativePathDisplayName : searchTerm.Label;

                // Don't display File and UtcOffset
                if (databaseColumnName == Constant.DatabaseColumn.File || databaseColumnName == Constant.DatabaseColumn.UtcOffset)
                {
                    continue;
                }

                // Skip one of the DateTime fields as custom selection creates two of them
                if (databaseColumnName == Constant.DatabaseColumn.DateTime)
                {
                    if (datetimeAdded == true)
                    {
                        continue;
                    }
                    datetimeAdded = true;
                }
                this.PrimaryComboBox.Items.Add(displayName);

                // If the current PrimarySort sort term matches the current item, then set it as selected
                if (database.PrimarySortTerm1 == searchTerm.DataLabel)
                {
                    this.PrimaryComboBox.SelectedIndex = this.PrimaryComboBox.Items.Count - 1;
                }
            }
        }
        private void PopulateSecondaryComboBox()
        {
            this.SecondaryComboBox.Items.Clear();

            // By default, we don't sort on a secondary term, unless its over-ridden
            this.SecondaryComboBox.Items.Add(EmptyDisplay);
            this.SecondaryComboBox.SelectedIndex = 0;

            // Add the Id unless its already selected as the primary sort choice
            if (Constant.DatabaseColumn.ID != (string)PrimaryComboBox.SelectedItem)
            { 
                this.SecondaryComboBox.Items.Add(Constant.DatabaseColumn.ID);
            }

            bool datetimeAdded = false;
            foreach (SearchTerm searchTerm in this.database.CustomSelection.SearchTerms)
            {
                string databaseColumnName = searchTerm.DataLabel;
                string displayName = (searchTerm.DataLabel == Constant.DatabaseColumn.RelativePath) ? FileRelativePathDisplayName : searchTerm.Label;

                // Don't display File and UtcOffset
                if (databaseColumnName == Constant.DatabaseColumn.File || databaseColumnName == Constant.DatabaseColumn.UtcOffset)
                {
                    continue;
                }

                // If the item is already selected in the primary combo box, we don't display it in the secondary combo box
                if (displayName == (string)PrimaryComboBox.SelectedItem)
                {
                    if ((string)this.SecondaryComboBox.SelectedItem == databaseColumnName)
                    {
                        this.SecondaryComboBox.SelectedIndex = 0;
                    }
                    continue;
                }

                // Skip one of the DateTime fields as custom selection creates two of them
                if (databaseColumnName == Constant.DatabaseColumn.DateTime)
                {
                    if (datetimeAdded == true)
                    {
                        continue;
                    }
                    datetimeAdded = true;
                }
                this.SecondaryComboBox.Items.Add(displayName);

                // If the current SecondarySort sort term matches the current item, then set it as selected
                if (database.SecondarySortTerm1 == searchTerm.DataLabel)
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
            string primaryTerm1 = String.Empty;
            string primaryTerm2 = String.Empty;
            string secondaryTerm1 = String.Empty;
            string secondaryTerm2 = String.Empty;

            if (selectedPrimaryItem == Constant.DatabaseColumn.ID)
            {
                primaryTerm1 = Constant.DatabaseColumn.ID;
            }
            else
            {
                foreach (SearchTerm searchTerm in this.database.CustomSelection.SearchTerms)
                {
                    if (selectedPrimaryItem == searchTerm.Label)
                    {
                        primaryTerm1 = searchTerm.DataLabel;
                        break;
                    }
                    else if (selectedPrimaryItem == FileRelativePathDisplayName)
                    {
                        primaryTerm1 = Constant.DatabaseColumn.RelativePath;
                        primaryTerm2 = Constant.DatabaseColumn.File;
                        break;
                    }
                }
            }

            if (selectedSecondaryItem != EmptyDisplay)
            {
                if (selectedSecondaryItem == Constant.DatabaseColumn.ID)
                {
                    secondaryTerm1 = Constant.DatabaseColumn.ID;
                }
                else
                {
                    foreach (SearchTerm searchTerm in this.database.CustomSelection.SearchTerms)
                    {
                        if (selectedSecondaryItem == searchTerm.Label)
                        {
                            secondaryTerm1 = searchTerm.DataLabel;
                            break;
                        }
                        else if (selectedSecondaryItem == FileRelativePathDisplayName)
                        {
                            secondaryTerm1 = Constant.DatabaseColumn.RelativePath;
                            secondaryTerm2 = Constant.DatabaseColumn.File;
                            break;
                        }
                    }
                }
            }
            this.database.PrimarySortTerm1 = primaryTerm1;
            this.database.PrimarySortTerm2 = primaryTerm2;
            this.database.SecondarySortTerm1 = secondaryTerm1;
            this.database.SecondarySortTerm2 = secondaryTerm2;
            // System.Diagnostics.Debug.Print(String.Format("{0}, {1}, {2}, {3}", primaryTerm1, primaryTerm2, secondaryTerm1, secondaryTerm2));
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
