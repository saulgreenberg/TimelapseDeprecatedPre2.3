﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Timelapse.Dialog
{
    /// <summary>
    /// This dialog deals with missing folders, i.e. folders that are not found at their relative paths.
    /// It is given a dictionary of old vs. new folder names, and based on that it displays a table of rows, each showing 
    /// - A Locate button
    /// - a View button
    /// - a folder's name, 
    /// - a relative path to an expected old location of that folder,
    /// - a relative path to a possible new location of that folder
    /// - a checkbox indicating whether that new location should be used
    /// - whether the new location should be used
    /// The user can then check each location (via View) and find a new location (via Locate) if needed
    /// - return true: the new locations with the 'Use' checkbox checked will be returned
    /// - return false: cancel all attempts to find the locaton of missing folders.
    /// </summary>
    public partial class MissingFoldersLocateAllFolders : Window
    {
        public Dictionary<string, string> FinalFolderLocations
        {
            get
            {
                Dictionary<string, string> finalFolderLocations = new Dictionary<string, string>();
                foreach (MissingFolderRow tuple in observableCollection)
                {
                    if (tuple.Use == true)
                    {
                        // finalFolderLocations.Add(tuple.ExpectedOldLocation, tuple.PossibleNewLocation);
                        finalFolderLocations.Add(tuple.ExpectedOldLocation, "JUNK");
                    }
                }
                return finalFolderLocations;
            }
        }
        private readonly string RootPath;
        private ObservableCollection<MissingFolderRow> observableCollection; // A tuple defining the contents of the datagrid
        private IList<DataGridCellInfo> selectedRowValues; // Will contain the tuple of the row corresponding to the selected cell

        #region Constructor, Loaded and AutoGeneratedColumns
        public MissingFoldersLocateAllFolders(Window owner, string rootPath, Dictionary<string, string> missingFoldersAndLikelyLocations)
        {
            InitializeComponent();

            if (missingFoldersAndLikelyLocations == null)
            {
                // Nothing to do. Abort
                this.DialogResult = false;
                return;
            }
            this.Owner = owner;
            this.RootPath = rootPath;
            this.observableCollection = new ObservableCollection<MissingFolderRow>();
            this.EnsureCheckboxValue();
            foreach (KeyValuePair<string, string> pair in missingFoldersAndLikelyLocations)
            {
                List<string> possibleNewLocations = new List<string>
                {
                    missingFoldersAndLikelyLocations[pair.Key],
                };
                MissingFolderRow row = new MissingFolderRow(Path.GetFileName(pair.Key), pair.Key, possibleNewLocations, false);
                this.observableCollection.Add(row);
            }
            this.DataGrid.ItemsSource = this.observableCollection;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dialogs.TryPositionAndFitDialogIntoWindow(this);

            // Get rid of those ugly empty cell headers atop the Locate/View columns
            this.DataGrid.Columns[0].HeaderStyle = CreateEmptyHeaderStyle();
            this.DataGrid.Columns[1].HeaderStyle = CreateEmptyHeaderStyle();

            // Bind each combobox, and select the first item in each Combobox
            int rowIndex = 0;
            foreach (MissingFolderRow mfr in this.observableCollection)
            {
                DataGridRow dataGridRow = (DataGridRow)this.DataGrid.ItemContainerGenerator
                                               .ContainerFromIndex(rowIndex);
                ComboBox cb = Util.VisualChildren.GetVisualChild<ComboBox>(dataGridRow, "Part_Combo");
                cb.ItemsSource = mfr.PossibleNewLocation;
                cb.SelectedIndex = 0;
                rowIndex++;
            }
            this.SetInitialCheckboxValue();
        }
        #endregion

        #region Button callbacks
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        private void UseNewLocations_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }
        #endregion

        #region DataGrid callbacks
        // Remember the tuple of the selected row
        private void MatchDataGrid_SelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
        {
            this.selectedRowValues = e.AddedCells;
        }

        // Determine if the user clicked the View or Locate cell, and take the appropriate action
        private void MatchDataGrid_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (this.selectedRowValues == null || this.selectedRowValues.Count == 0 || this.selectedRowValues[0].Item == null)
            {
                return;
            }
            int selectedColumn = this.selectedRowValues[0].Column.DisplayIndex;
            string missingFolderName = this.GetFolderNameFromSelection();
            // string missingFolderName = this.selectedRowTuple


            string possibleLocation = GetPossibleLocationFromSelection();
            MissingFolderRow rowValues;
            int rowIndex = 0;
            switch (selectedColumn)
            {
                case 0:
                    // Locate the folder via a dialog
                    string newLocation = Dialogs.LocateRelativePathUsingOpenFileDialog(this.RootPath, missingFolderName);
                    if (newLocation == null)
                    {
                        return;
                    }

                    // We need to update the datagrid with the new value. 
                    rowValues = (MissingFolderRow)this.selectedRowValues[0].Item;
                    rowIndex = 0;
                    foreach (MissingFolderRow row in this.observableCollection)
                    {
                        if (row == rowValues)
                        {
                            // We are on the selected row
                            // Rebuild its combobox items so it has the latest user-provided location as the first selected item
                            DataGridRow dataGridRow = (DataGridRow)this.DataGrid.ItemContainerGenerator
                                               .ContainerFromIndex(rowIndex);
                            if (dataGridRow == null) continue;
                            ComboBox comboBox = Util.VisualChildren.GetVisualChild<ComboBox>(dataGridRow, "Part_Combo");
                            if (comboBox == null) continue;
   
                            // Rebuild the list with the new position at the beginning and selected.
                            List<string> newList = new List<string>();
                            newList.Add(newLocation);
                            foreach (string item in row.PossibleNewLocation)
                            {
                                if (false == String.Equals(item, newLocation))
                                {
                                    newList.Add(item);
                                }
                            }
                            row.PossibleNewLocation = newList;

                            comboBox.ItemsSource = newList;
                            comboBox.SelectedIndex = 0;
                            this.EnsureCheckboxValue();
                        }
                        rowIndex++;
                    }
                    break;
                case 1:
                    Util.ProcessExecution.TryProcessStartUsingFileExplorer(Path.Combine(this.RootPath, possibleLocation));
                    break;
                default:
                    return;
            }
        }


        private void Checkbox_CheckChanged(object sender, RoutedEventArgs e)
        {
            this.EnsureCheckboxValue();
        }

        private void SetInitialCheckboxValue()
        {
            int rowIndex = 0;
            CheckBox checkBox;
            ComboBox comboBox;
            foreach (MissingFolderRow mfr in this.observableCollection)
            {
                DataGridRow dataGridRow = (DataGridRow)this.DataGrid.ItemContainerGenerator
                   .ContainerFromIndex(rowIndex);
                if (null == dataGridRow) continue;
                checkBox = Util.VisualChildren.GetVisualChild<CheckBox>(dataGridRow, "Part_Checkbox");
                if (null == checkBox) continue;
                comboBox = Util.VisualChildren.GetVisualChild<ComboBox>(dataGridRow, "Part_Combo");
                if (null == comboBox) continue;
                if (String.IsNullOrEmpty((string)comboBox.SelectedItem) || comboBox.Items.Count != 1)
                {
                    checkBox.IsChecked = false;
                }
                else
                {
                    checkBox.IsChecked = true;
                }
                System.Diagnostics.Debug.Print(mfr.Use.ToString());
                rowIndex++;
            }
        }

        private void EnsureCheckboxValue()
        {
            int rowIndex = 0;
            CheckBox checkBox;
            ComboBox comboBox;
            foreach (MissingFolderRow mfr in this.observableCollection)
            {
                DataGridRow dataGridRow = (DataGridRow)this.DataGrid.ItemContainerGenerator
                   .ContainerFromIndex(rowIndex);
                if (null == dataGridRow) continue;
                checkBox = Util.VisualChildren.GetVisualChild<CheckBox>(dataGridRow, "Part_Checkbox");
                if (null == checkBox) continue; 
                comboBox = Util.VisualChildren.GetVisualChild<ComboBox>(dataGridRow, "Part_Combo");
                if (null == comboBox) continue;
                if (String.IsNullOrEmpty((string)comboBox.SelectedItem))
                {
                    checkBox.IsChecked = false;
                }
                System.Diagnostics.Debug.Print(mfr.Use.ToString());
                rowIndex++;
            }
        }

        private void SetCheckboxValue(CheckBox checkBox)
        {
            int rowIndex = 0;
            ComboBox comboBox;
            System.Diagnostics.Debug.Print("Here " + (checkBox.IsChecked == true).ToString());
            foreach (MissingFolderRow mfr in this.observableCollection)
            {
                DataGridRow dataGridRow = (DataGridRow)this.DataGrid.ItemContainerGenerator
                   .ContainerFromIndex(rowIndex);
                if (Util.VisualChildren.GetVisualChild<CheckBox>(dataGridRow, "Part_Checkbox") == checkBox)
                {
                    comboBox = Util.VisualChildren.GetVisualChild<ComboBox>(dataGridRow, "Part_Combo");
                    if (String.IsNullOrEmpty((string)comboBox.SelectedItem))
                    {
                        checkBox.IsChecked = false;
                    }
                }
                System.Diagnostics.Debug.Print(mfr.Use.ToString());
                rowIndex++;
            }
        }
        #endregion

        #region Helper methods
        private string GetFolderNameFromSelection()
        {
            MissingFolderRow mfr = (MissingFolderRow)this.selectedRowValues[0].Item;
            return mfr.FolderName;
        }

        private string GetPossibleLocationFromSelection()
        {
            MissingFolderRow mfr = (MissingFolderRow)this.selectedRowValues[0].Item;
            return mfr.PossibleNewLocation[0];
        }
        #endregion

        #region Styles
        // A ColumnHeader style that appears (more or less) empty
        private Style CreateEmptyHeaderStyle()
        {
            Style headerStyle = new Style
            {
                TargetType = typeof(DataGridColumnHeader)//sets target type as DataGrid row
            };

            Setter setterBackground = new Setter
            {
                Property = DataGridColumnHeader.BackgroundProperty,
                Value = new SolidColorBrush(Colors.White)
            };

            Setter setterBorder = new Setter
            {
                Property = DataGridColumnHeader.BorderThicknessProperty,
                Value = new Thickness(0, 0, 0, 1)
            };

            headerStyle.Setters.Add(setterBackground);
            headerStyle.Setters.Add(setterBorder);
            return headerStyle;
        }
        #endregion

    }

    class MissingFolderRow
    {
        public string FolderName { get; set; }
        public string ExpectedOldLocation { get; set; }
        public List<string> PossibleNewLocation { get; set; }
        public bool Use { get; set; }

        public MissingFolderRow(string folderName, string expectedOldLocation, List<string> possibleNewLocation, bool use)
        {
            this.FolderName = folderName;
            this.ExpectedOldLocation = expectedOldLocation;
            this.PossibleNewLocation = possibleNewLocation;
            this.Use = use;
        }
    }
}


