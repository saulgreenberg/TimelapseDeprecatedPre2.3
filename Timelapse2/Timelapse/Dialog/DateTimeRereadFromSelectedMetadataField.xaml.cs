﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Controls;
using Timelapse.Database;
using Timelapse.ExifTool;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    /// <summary>
    /// This dialog displays a list of metadata data fields in the selected file containing parsable dates and times. It asks the user to select one .
    /// The user can then update the datetime field with the corresponding metadata value from that image for all images. 
    /// Files that do not have that metadata field are skipped.
    /// </summary>
    public partial class DateTimeRereadFromSelectedMetadataField : BusyableDialogWindow, IDisposable
    {
        #region Private Variables
        private readonly FileDatabase fileDatabase;
        private readonly string filePath;

        private ExifToolWrapper exifTool;

        private readonly Dictionary<string, string> dataLabelByLabel;
        private bool dataFieldSelected;

        private Dictionary<string, ImageMetadata> metadataDictionary;
        private string metadataFieldName;
        private bool metadataFieldSelected;
        private bool noMetadataAvailable;

        // Tracks whether any changes to the data or database are made
        private bool IsAnyDataUpdated;
        #endregion

        #region Constructor, Loaded
        public DateTimeRereadFromSelectedMetadataField(Window owner, FileDatabase fileDatabase, string filePath) : base(owner)
        {
            ThrowIf.IsNullArgument(fileDatabase, nameof(fileDatabase));

            this.InitializeComponent();
            this.fileDatabase = fileDatabase;
            this.filePath = filePath;

            // Store various states which will eventually be reset by the user
            this.dataLabelByLabel = new Dictionary<string, string>();
            this.dataFieldSelected = true;

            this.metadataFieldName = String.Empty;
            this.metadataFieldSelected = false;
            this.noMetadataAvailable = true;
        }

        // After the interface is loaded, 
        // - Load the metadata into the data grid
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Set up a progress handler that will update the progress bar
            this.InitalizeProgressHandler(this.BusyCancelIndicator);

            // Set up the initial UI and values
            this.lblImageName.Content = Path.GetFileName(this.filePath);
            this.lblImageName.ToolTip = this.lblImageName.Content;

            // Show the metadata of the current image, depending on the kind of tool selected
            this.MetadataToolType_Checked(null, null);

            // Add callbacks to the radio buttons. We do it here so they are not invoked when the window is loaded.
            this.MetadataExtractorRB.Checked += this.MetadataToolType_Checked;
            this.ExifToolRB.Checked += this.MetadataToolType_Checked;
        }
        #endregion

        #region Autogenerated Columns
        // This datagrid will indicate, for the selected metadata, the metadata values assigned to the selected field
        private void FeedbackDatagrid_AutoGeneratedColumns(object sender, EventArgs e)
        {
            this.FeedbackGrid.Columns[0].Header = "File Name";
            this.FeedbackGrid.Columns[1].Header = "The Metadata Value for " + this.metadataFieldName;
        }

        // This datagrid will hold all the metadata available on a file
        private void AvailableMetadataDatagrid_AutoGeneratedColumns(object sender, EventArgs e)
        {
            this.AvailableMetadataDataGrid.Columns[0].Header = "Key";
            this.AvailableMetadataDataGrid.Columns[1].Header = "Metadata kind";
            this.AvailableMetadataDataGrid.Columns[2].Header = "Metadata name";
            this.AvailableMetadataDataGrid.Columns[3].Header = "Metadata's date/time value from the current file";
            this.AvailableMetadataDataGrid.SortByColumnAscending(2);
            this.AvailableMetadataDataGrid.Columns[0].Visibility = Visibility.Collapsed;
            this.AvailableMetadataDataGrid.Columns[1].Visibility = Visibility.Collapsed;
        }
        #endregion

        #region Closing and Disposing
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            this.DialogResult = this.Token.IsCancellationRequested || this.IsAnyDataUpdated;
        }

        // To follow design pattern in  CA1001 Types that own disposable fields should be disposable
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // dispose managed resources
                if (this.exifTool != null)
                {
                    this.exifTool.Dispose();
                }
            }
            // free native resources
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion

        #region MetadataExtractor-specific methods
        // Retrieve and show a single image's metadata in the datagrid, although filter out any metadata whose value is not a valid date.
        private void MetadataExtractorShowImageMetadata()
        {
            this.metadataDictionary = ImageMetadataDictionary.LoadMetadata(this.filePath);
            // If there is no metadata, this is an easy way to inform the user
            if (this.metadataDictionary.Count == 0)
            {
                this.metadataDictionary.Add("Empty", new Timelapse.Util.ImageMetadata("Empty", "No metadata found in the currently displayed image", "Navigate to a displayable image"));
                this.noMetadataAvailable = true;
            }
            else
            {
                this.noMetadataAvailable = false;
            }

            // In order to populate the datagrid, we have to unpack the dictionary as a list containing four values
            List<Tuple<string, string, string, string>> metadataList = new List<Tuple<string, string, string, string>>();
            foreach (KeyValuePair<string, ImageMetadata> metadata in this.metadataDictionary)
            {
                if (DateTime.TryParse(metadata.Value.Value.ToString(), out DateTime dateTime))
                {
                    metadataList.Add(new Tuple<string, string, string, string>(metadata.Key, metadata.Value.Directory, metadata.Value.Name, metadata.Value.Value));
                }
            }
            this.AvailableMetadataDataGrid.ItemsSource = metadataList;
        }
        #endregion

        #region ExifTool-specific methods
        // Retrieve and show a single image's metadata in the datagrid, although filter out any metadata whose value is not a valid date.
        private void ExifToolShowImageMetadata()
        {
            // Clear the dictionary so we get fresh contents
            this.metadataDictionary.Clear();

            // Start the exifTool process if its not already started
            if (this.exifTool == null)
            {
                this.exifTool = new ExifToolWrapper();
                this.exifTool.Start();
            }

            // Fetch the exif data using ExifTool
            Dictionary<string, string> exifDictionary = this.exifTool.FetchExifFrom(this.filePath);

            // If there is no metadata, inform the user by setting bogus dictionary values which will appear on the grid
            if (exifDictionary.Count == 0)
            {
                this.metadataDictionary.Add("Empty", new Timelapse.Util.ImageMetadata("Empty", "No metadata found in the currently displayed image", "Navigate to a displayable image"));
                this.noMetadataAvailable = true;
            }
            else
            {
                this.noMetadataAvailable = false;
            }

            // In order to populate the metadataDictionary and datagrid, we have to unpack the ExifTool dictionary, recreate the dictionary, and create a list containing four values
            List<Tuple<string, string, string, string>> metadataList = new List<Tuple<string, string, string, string>>();
            foreach (KeyValuePair<string, string> metadata in exifDictionary)
            {
                // We only collect metadata for those fields whose value appears to have a valid date.
                if (DateTime.TryParse(metadata.Value, out DateTime dateTime))
                {
                    this.metadataDictionary.Add(metadata.Key, new Timelapse.Util.ImageMetadata(String.Empty, metadata.Key, metadata.Value));
                    metadataList.Add(new Tuple<string, string, string, string>(metadata.Key, String.Empty, metadata.Key, metadata.Value));
                }
            }
            this.AvailableMetadataDataGrid.ItemsSource = metadataList;
        }
        #endregion

        #region Do the work: Populate the database 
        // Populate the database with the metadata for the selected note field
        private async Task<ObservableCollection<KeyValuePair<string, string>>> PopulateAsync(bool? metadataExtractorRBIsChecked)
        {
            // This list will hold key / value pairs that will be bound to the datagrid feedback, 
            // which is the way to make those pairs appear in the data grid during background worker progress updates
            ObservableCollection<KeyValuePair<string, string>> keyValueList = new ObservableCollection<KeyValuePair<string, string>>();

            return await Task.Run(() =>
            {
                // For each row in the database, get the image filename and try to extract the chosen metadata value.
                // Report progress as needed.
                // This tuple list will hold the id, key and value that we will want to update in the database
                List<ColumnTuplesWithWhere> imagesToUpdate = new List<ColumnTuplesWithWhere>();
                TimeZoneInfo imageSetTimeZone = this.fileDatabase.ImageSet.GetSystemTimeZone();
                int percentDone = 0;

                double totalImages = this.fileDatabase.CountAllCurrentlySelectedFiles;
                Dictionary<string, ImageMetadata> metadata = new Dictionary<string, ImageMetadata>();
                List<ImageRow> filesToAdjust = new List<ImageRow>();

                // Start up the progress bar, so it shows something as even small data sets will have a delay in it.
                this.Progress.Report(new ProgressBarArguments(percentDone, "Initializing...", true, false));
                Thread.Sleep(Constant.ThrottleValues.RenderingBackoffTime);  // Allows the UI thread to update every now and then

                for (int imageIndex = 0; imageIndex < totalImages; ++imageIndex)
                {
                    // Provide feedback if the operation was cancelled during the database update
                    if (Token.IsCancellationRequested == true)
                    {
                        keyValueList.Clear();
                        keyValueList.Add(new KeyValuePair<string, string>("Cancelled", "No changes were made"));
                        return keyValueList;
                    }

                    ImageRow image = this.fileDatabase.FileTable[imageIndex];
                    if (metadataExtractorRBIsChecked == true)
                    {   // MetadataExtractor specific code
                        metadata = ImageMetadataDictionary.LoadMetadata(image.GetFilePath(this.fileDatabase.FolderPath));
                    }
                    else
                    {
                        // ExifTool specific code - note that we transform results into the same dictionary structure used by the MetadataExtractor
                        string[] tags = { this.metadataFieldName };
                        metadata.Clear();
                        Dictionary<string, string> exifData = this.exifTool.FetchExifFrom(image.GetFilePath(this.fileDatabase.FolderPath), tags);
                        if (exifData.ContainsKey(tags[0]))
                        {
                            metadata.Add(tags[0], new Timelapse.Util.ImageMetadata(String.Empty, tags[0], exifData[tags[0]]));
                        }
                    }

                    if (this.ReadyToRefresh())
                    {
                        percentDone = Convert.ToInt32(imageIndex / totalImages * 100.0);
                        this.Progress.Report(new ProgressBarArguments(percentDone, String.Format("{0}/{1} images. Processing {2}", imageIndex, totalImages, image.File), true, false));
                        Thread.Sleep(Constant.ThrottleValues.RenderingBackoffTime);  // Allows the UI thread to update every now and then
                    }

                    if (metadata.ContainsKey(this.metadataFieldName) == false)
                    {
                        // System.Diagnostics.Debug.Print(String.Format("{0}: No metadata", image.File));
                        continue;
                    }

                    string metadataValue = metadata[this.metadataFieldName].Value;
                    ColumnTuplesWithWhere imageUpdate;
                    if (DateTimeHandler.TryParseMetadataDateTaken(metadataValue, imageSetTimeZone, out DateTimeOffset metadataDateTime))
                    {
                        image.SetDateTimeOffset(metadataDateTime);
                        imageUpdate = image.GetDateTimeColumnTuples();
                        keyValueList.Add(new KeyValuePair<string, string>(image.File, metadataValue));
                    }
                    else
                    {
                        keyValueList.Add(new KeyValuePair<string, string>(image.File, String.Format("Data field unchanged - '{0}' is not a valid date/time.", metadataValue)));
                        continue;
                    }
                    imagesToUpdate.Add(imageUpdate);
                }
                this.IsAnyDataUpdated = true;
                this.Progress.Report(new ProgressBarArguments(100, String.Format("Writing metadata for {0} files. Please wait...", totalImages), false, true));
                Thread.Sleep(Constant.ThrottleValues.RenderingBackoffTime);  // Allows the UI thread to update every now and then
                this.fileDatabase.UpdateFiles(imagesToUpdate);
                return keyValueList;
            }, this.Token).ConfigureAwait(true);
        }
        #endregion

        #region Callbacks to select and assign metadata fields
        // Datagrid Callack where the user has selected a row. Get the metadata from that row, and make it the selected metadata.
        // Also enable/disable UI controls as needed
        private void Datagrid_SelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
        {
            // If there's no metadata, then just bail
            if (this.noMetadataAvailable == true)
            {
                return;
            }
            IList<DataGridCellInfo> selectedcells = e.AddedCells;

            // Make sure there are actually some selected cells
            if (selectedcells == null || selectedcells.Count == 0)
            {
                return;
            }

            // We should only have a single selected cell, so just grab the first one
            DataGridCellInfo di = selectedcells[0];

            // the selected item is the entire row, where the format returned is [MetadataName , MetadataValue] 
            // Parse out the metadata name
            String[] s = di.Item.ToString().Split(',');  // Get the "[Metadataname" portion before the ','
            this.metadataFieldName = s[0].Substring(1);              // Remove the leading '['
            if (this.metadataDictionary.ContainsKey(this.metadataFieldName))
            {
                this.MetadataDisplayText.Content = this.metadataDictionary[this.metadataFieldName].Name;
                // Note that metadata name may still has spaces in it. We will have to strip it out and check it to make sure its an acceptable data label
                this.metadataFieldSelected = true;
                this.StartDoneButton.IsEnabled = this.dataFieldSelected && this.metadataFieldSelected;
            }
            else
            {
                this.MetadataDisplayText.Content = String.Empty;
                // Note that metadata name may still has spaces in it. We will have to strip it out and check it to make sure its an acceptable data label
                this.metadataFieldSelected = false;
                this.StartDoneButton.IsEnabled = this.dataFieldSelected && this.metadataFieldSelected;
            }
        }
        #endregion

        #region Checkbox callbacks
        // Checkbox callback sets which metadata tool should be used
        private void MetadataToolType_Checked(object sender, RoutedEventArgs e)
        {
            if (this.MetadataExtractorRB.IsChecked == true)
            {
                this.MetadataExtractorShowImageMetadata();
            }
            else
            {
                this.ExifToolShowImageMetadata();
            }
        }
        #endregion

        #region Button callbacks
        private async void Start_Click(object sender, RoutedEventArgs e)
        {
            bool? metadataExtractorRBIsChecked = this.MetadataExtractorRB.IsChecked;

            // Update the UI to show the feedback datagrid, 
            this.PopulatingMessage.Text = "Scanning and updating 'DateTime' from each file's '" + this.MetadataDisplayText.Content + "' metadata ";
            this.CancelButton.IsEnabled = false;
            this.CancelButton.Visibility = Visibility.Hidden;
            this.StartDoneButton.Content = "_Done";
            this.StartDoneButton.Click -= this.Start_Click;
            this.StartDoneButton.Click += this.Done_Click;
            this.StartDoneButton.IsEnabled = false;
            this.BusyCancelIndicator.IsBusy = true;
            this.WindowCloseButtonIsEnabled(false);

            this.PrimaryPanel.Visibility = Visibility.Collapsed;  // Hide the various panels to reveal the feedback datagrid
            this.FeedbackPanel.Visibility = Visibility.Visible;
            this.PanelHeader.Visibility = Visibility.Collapsed;
            this.ToolSelectionPanel.Visibility = Visibility.Collapsed;
            this.WindowCloseButtonIsEnabled(false);

            // This call does all the actual populating...
            ObservableCollection<KeyValuePair<string, string>> keyValueList = await this.PopulateAsync(metadataExtractorRBIsChecked).ConfigureAwait(true);

            // Update the UI to its final state
            this.FeedbackGrid.ItemsSource = keyValueList;
            this.StartDoneButton.IsEnabled = true;
            this.BusyCancelIndicator.IsBusy = false;
            this.WindowCloseButtonIsEnabled(true);

            this.FeedbackGrid.ItemsSource = keyValueList;

            if (this.Token.IsCancellationRequested)
            {
                this.PopulatingMessage.Text = "Cancelled: 'DateTime' is unchanged.";
            }
            else
            {
                this.PopulatingMessage.Text = "Updated 'DateTime' from each file's '" + this.MetadataDisplayText.Content + "' metadata as follows.";
            }
            if (this.exifTool != null)
            {
                this.exifTool.Stop();
            }

        }
        private void Done_Click(object sender, RoutedEventArgs e)
        {

            // We return true if the database was altered but also if there was a cancellation, as a cancelled operation
            // may have changed the FileTable (but not database) date entries. Returning true will reset them, as a FileSelectAndShow will be done.
            // Kinda hacky as it expects a certain behaviour of the caller, but it works.
            this.DialogResult = this.Token.IsCancellationRequested || this.IsAnyDataUpdated;
        }
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = this.Token.IsCancellationRequested || this.IsAnyDataUpdated;
        }
        #endregion
    }
}
