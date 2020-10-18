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
    public partial class MissingFoldersLocateFolders : Window
    {
        #region Public Properties
        public Dictionary<string, string> FinalFolderLocations
        {
            get
            {
                Dictionary<string, string> finalFolderLocations = new Dictionary<string, string>();
                foreach (Tuple<string, string, string, bool> tuple in observableCollection)
                {
                    if (tuple.Item4 == true)
                    {
                        finalFolderLocations.Add(tuple.Item2, tuple.Item3);
                    }
                }
                return finalFolderLocations;
            }
        }
        #endregion

        private readonly string RootPath;
        private ObservableCollection<Tuple<string, string, string, bool>> observableCollection; // A tuple defining the contents of the datagrid
        private IList<DataGridCellInfo> selectedRowTuple; // Will contain the tuple of the row corresponding to the selected cell

        #region Constructor, Loaded and AutoGeneratedColumns
        public MissingFoldersLocateFolders(Window owner, string rootPath, Dictionary<string, string> missingFoldersAndLikelyLocations)
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
            // Create a collection comprising: folder name, expected location as a relative path, and the possbile new location 
            // and bind it to the data grid
            this.observableCollection = new ObservableCollection<Tuple<string, string, string, bool>>();
            foreach (KeyValuePair<string, string> pair in missingFoldersAndLikelyLocations)
            {
                // if the likely location is empty, ensure that the Use flag is not checked as there is no folder to replace it with.
                bool isLikelyLocationAvailable = false == String.IsNullOrWhiteSpace(missingFoldersAndLikelyLocations[pair.Key]);
                this.observableCollection.Add(new Tuple<string, string, string, bool>(Path.GetFileName(pair.Key), pair.Key, missingFoldersAndLikelyLocations[pair.Key], isLikelyLocationAvailable));
            }
            this.DataGrid.ItemsSource = observableCollection;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dialogs.TryPositionAndFitDialogIntoWindow(this);

            // Get rid of those ugly empty cell headers atop the Locate/View columns
            this.DataGrid.Columns[0].HeaderStyle = CreateEmptyHeaderStyle();
            this.DataGrid.Columns[1].HeaderStyle = CreateEmptyHeaderStyle();
        }

        // Create the datagrid column headers
        private void MatchDataGrid_AutoGeneratedColumns(object sender, EventArgs e)
        {
            this.DataGrid.Columns[2].Header = "Folder name";
            this.DataGrid.Columns[2].Width = new DataGridLength(1, DataGridLengthUnitType.Auto);
            this.DataGrid.Columns[3].Header = "Expected old location";
            this.DataGrid.Columns[3].Width = new DataGridLength(2, DataGridLengthUnitType.Star);
            this.DataGrid.Columns[4].Header = "Possible new location";
            this.DataGrid.Columns[4].Width = new DataGridLength(2, DataGridLengthUnitType.Star);
            this.DataGrid.Columns[5].Header = "Use?";
            this.DataGrid.Columns[5].Width = new DataGridLength(2, DataGridLengthUnitType.Auto);
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
            this.selectedRowTuple = e.AddedCells;
        }

        // Determine if the user clicked the View or Locate cell, and take the appropriate action
        private void MatchDataGrid_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (this.selectedRowTuple == null || this.selectedRowTuple.Count == 0 || this.selectedRowTuple[0].Item == null)
            {
                return;
            }
            int selectedColumn = this.selectedRowTuple[0].Column.DisplayIndex;
            string missingFolderName = this.GetFolderNameFromSelection();
            string possibleLocation = GetPossibleLocationFromSelection();
            Tuple<string, string, string, bool> rowValues;
            ObservableCollection<Tuple<string, string, string, bool>> obsCollection;
            bool isLikelyLocationAvailable;
            switch (selectedColumn)
            {
                case 0:
                    // Locate the folder via a dialog
                    string newLocation = Dialogs.LocateRelativePathUsingOpenFileDialog(this.RootPath, missingFolderName);
                    if (newLocation == null)
                    {
                        return;
                    }
                    rowValues = (Tuple<string, string, string, bool>)this.selectedRowTuple[0].Item;
                    // We need to update the datagrid with the new value. 
                    // To keep it simple,  just rebuild the observable collection and rebind it
                    obsCollection = new ObservableCollection<Tuple<string, string, string, bool>>();
                    foreach (Tuple<string, string, string, bool> row in this.observableCollection)
                    {
                        if (row != rowValues)
                        {
                            obsCollection.Add(row);
                        }
                        else
                        {
                            // if the likely location is empty, ensure that the Use flag is unchecked as there is no folder to replace it with. Otherwise automatically check it
                            isLikelyLocationAvailable = false == String.IsNullOrWhiteSpace(newLocation);
                            obsCollection.Add(new Tuple<string, string, string, bool>(rowValues.Item1, rowValues.Item2, newLocation, isLikelyLocationAvailable));
                        }
                    }
                    this.observableCollection = obsCollection;
                    this.DataGrid.ItemsSource = this.observableCollection;
                    break;
                case 1:
                    Util.ProcessExecution.TryProcessStartUsingFileExplorer(Path.Combine(this.RootPath, possibleLocation));
                    break;
                case 5:
                    rowValues = (Tuple<string, string, string, bool>)this.selectedRowTuple[0].Item;
                    // We need to update the datagrid with the new value. 
                    // To keep it simple,  just rebuild the observable collection and rebind it
                    obsCollection = new ObservableCollection<Tuple<string, string, string, bool>>();
                    foreach (Tuple<string, string, string, bool> row in this.observableCollection)
                    {
                        if (row != rowValues)
                        {
                            obsCollection.Add(row);
                        }
                        else
                        {
                            // if the likely location is empty, ensure that the Use flag is unchecked as there is no folder to replace it with. Otherwise keep its state
                            isLikelyLocationAvailable = false == String.IsNullOrWhiteSpace(rowValues.Item3);
                            obsCollection.Add(new Tuple<string, string, string, bool>(rowValues.Item1, rowValues.Item2, rowValues.Item3, isLikelyLocationAvailable && !rowValues.Item4));
                        }
                    }
                    this.observableCollection = obsCollection;
                    this.DataGrid.ItemsSource = this.observableCollection;
                    break;

                default:
                    return;
            }
        }
        #endregion

        #region Helper methods
        private string GetFolderNameFromSelection()
        {
            Tuple<string, string, string, bool> tuple = (Tuple<string, string, string, bool>)this.selectedRowTuple[0].Item;
            if (tuple != null)
            {
                return tuple.Item1;
            }
            else
            {
                return String.Empty;
            }
        }

        private string GetPossibleLocationFromSelection()
        {
            Tuple<string, string, string, bool> tuple = (Tuple<string, string, string, bool>)this.selectedRowTuple[0].Item;
            if (tuple != null)
            {
                return tuple.Item3;
            }
            else
            {
                return String.Empty;
            }
        }
        #endregion

        #region Styles
        // A ColumnHeader style that appears (more or less) empty
        private static Style CreateEmptyHeaderStyle()
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
}


