﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Timelapse.Database;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for PopulateFieldsWithMetadataOnLoad.xaml
    /// </summary>
    public partial class PopulateFieldsWithMetadataOnLoad : Window
    {
        #region Private variables
        // Passed in parameters
        private string FilePath;
        private readonly FileDatabase FileDatabase;
        #endregion

        #region Public variables
        // Return a data structure containing a list of key value pairs that define the selected metadata and data label
        public MetadataOnLoad MetadataOnLoad = new MetadataOnLoad();
        #endregion

        #region Initialization
        public PopulateFieldsWithMetadataOnLoad(Window owner, FileDatabase fileDatabase, string filePath)
        {
            InitializeComponent();
            this.FilePath = filePath;
            this.FileDatabase = fileDatabase;
            this.Owner = owner;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dialogs.TryPositionAndFitDialogIntoWindow(this);
            Mouse.OverrideCursor = null;

            this.MetadataGrid.viewModel.FilePath = this.FilePath;

            // Construct a dictionary of the available note fields as labels|datalabels
            // and a list of only the note field labels which will be used to populate the ComboBoxes in the datagrid
            Dictionary<string, string> collectLabels = new Dictionary<string, string>();
            foreach (ControlRow control in this.FileDatabase.Controls)
            {
                if (control.Type == Constant.Control.Note)
                {
                    collectLabels.Add(control.DataLabel, control.Label);
                }
            }
            // Setting DictDataLabel_Label will result in desired side effects in the MetadataGrid user control
            this.MetadataGrid.DictDataLabel_Label = collectLabels;
            this.MetadataGrid.SelectedMetadata.CollectionChanged += this.SelectedMetadata_CollectionChanged;
        }

        // This datagrid will indicate, for the selected metadata, the metadata values assigned to the selected field
        private void FeedbackDatagrid_AutoGeneratedColumns(object sender, System.EventArgs e)
        {
            this.FeedbackGrid.Columns[0].Header = "File Name";
            this.FeedbackGrid.Columns[1].Header = "Metadata name";
            this.FeedbackGrid.Columns[2].Header = "Metadata Value";
        }
        #endregion

        #region Change Notifications
        private void SelectedMetadata_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // Enable or disable the Populate button to match if any items are in the selectedMetadataList 
            this.DoneButton.IsEnabled = (this.MetadataGrid.SelectedMetadata != null && this.MetadataGrid.SelectedMetadata.Count > 0);
        }
        #endregion

        #region Button callbacks
        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            string filter = String.Format("Images and videos (*{0};*{1};*{2};*{3})|*{0};*{1};*{2};*{3}", Constant.File.JpgFileExtension, Constant.File.AviFileExtension, Constant.File.Mp4FileExtension, Constant.File.ASFFileExtension);
            if (Dialogs.TryGetFileFromUserUsingOpenFileDialog("Select a typical file to inspect", ".", filter, Constant.File.JpgFileExtension, out string filePath) == true)
            {
                this.FilePath = filePath;
                this.MetadataGrid.viewModel.FilePath = this.FilePath;
                this.MetadataGrid.Refresh();
            }
        }
        private void Done_Click(object sender, RoutedEventArgs e)
        {
            this.MetadataOnLoad.SelectedMetadata = this.MetadataGrid.SelectedMetadata.ToList();
            this.MetadataOnLoad.MetadataToolSelected = this.MetadataGrid.MetadataToolSelected;
            this.MetadataOnLoad.ExifTool = this.MetadataGrid.ExifTool;
            this.DialogResult = true;
        }
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
        #endregion
    }
}

