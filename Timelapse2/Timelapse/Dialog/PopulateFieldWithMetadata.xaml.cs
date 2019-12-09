using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Database;
using Timelapse.ExifTool;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    /// <summary>
    /// This dialog displays a list of available data fields (currently the note, date and time fields), and 
    /// a list of metadata found in the current image. It asks the user to select one from each.
    /// The user can then populate the selected data field with the corresponding metadata value from that image for all images.
    /// </summary>
    public partial class PopulateFieldWithMetadata : Window, IDisposable
    {
        private bool clearIfNoMetadata;
        private readonly FileDatabase database;
        private string dataFieldLabel;
        private bool dataFieldSelected;
        private Dictionary<string, ImageMetadata> metadataDictionary;
        private readonly Dictionary<string, string> dataLabelByLabel;
        private readonly string filePath;
        private string metadataFieldName;

        private bool metadataFieldSelected;
        private bool noMetadataAvailable;
        private ExifToolWrapper exifTool;

        public PopulateFieldWithMetadata(FileDatabase database, string filePath)
        {
            this.InitializeComponent();
            this.clearIfNoMetadata = false;
            this.database = database;
            this.dataFieldLabel = String.Empty;
            this.dataFieldSelected = false;
            this.dataLabelByLabel = new Dictionary<string, string>();
            this.filePath = filePath;
            this.metadataFieldName = String.Empty;
            this.metadataFieldSelected = false;
            this.noMetadataAvailable = true;
        }

        // After the interface is loaded, 
        // - Load the metadata into the data grid
        // - Load the names of the note controls into the listbox
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dialogs.SetDefaultDialogPosition(this);
            Dialogs.TryFitDialogWindowInWorkingArea(this);

            this.lblImageName.Content = Path.GetFileName(this.filePath);
            this.lblImageName.ToolTip = this.lblImageName.Content;

            // Construct a list showing the available note fields
            foreach (ControlRow control in this.database.Controls)
            {
                if (control.Type == Constant.Control.Note)
                {
                    this.dataLabelByLabel.Add(control.Label, control.DataLabel);
                    this.DataFields.Items.Add(control.Label);
                }
            }
            // Show the metadata of the current image, depending on the kind of tool selected
            this.MetadataToolType_Checked(null, null);

            // Add callbacks to the radio buttons here, so they are not invoked when the window is loaded.
            this.MetadataExtractorRB.Checked += this.MetadataToolType_Checked;
            this.ExifToolRB.Checked += this.MetadataToolType_Checked;
        }

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

        #region MetadataExtractor-specific methods
        // Retrieve and show a single image's metadata in the datagrid
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
                metadataList.Add(new Tuple<string, string, string, string>(metadata.Key, metadata.Value.Directory, metadata.Value.Name, metadata.Value.Value));
            }
            this.dataGrid.ItemsSource = metadataList;
        }
        #endregion

        #region ExifTool-specific methods
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

            // In order to populate the metadataDictionary and datagrid , we have to unpack the ExifTool dictionary, recreate the dictionary, and create a list containing four values
            List<Tuple<string, string, string, string>> metadataList = new List<Tuple<string, string, string, string>>();
            foreach (KeyValuePair<string, string> metadata in exifDictionary)
            {
                this.metadataDictionary.Add(metadata.Key, new Timelapse.Util.ImageMetadata(String.Empty, metadata.Key, metadata.Value));
                metadataList.Add(new Tuple<string, string, string, string>(metadata.Key, String.Empty, metadata.Key, metadata.Value));
            }
            this.dataGrid.ItemsSource = metadataList;
        }
        #endregion

        // Label the column headers
        private void Datagrid_AutoGeneratedColumns(object sender, EventArgs e)
        {
            this.dataGrid.Columns[0].Header = "Key";
            this.dataGrid.Columns[1].Header = "Metadata kind";
            this.dataGrid.Columns[2].Header = "Metadata name";
            this.dataGrid.Columns[3].Header = "Example value from current file";
            this.dataGrid.SortByColumnAscending(2);
            this.dataGrid.Columns[0].Visibility = Visibility.Collapsed;
            this.dataGrid.Columns[1].Visibility = Visibility.Collapsed;
            // this.dataGrid.Columns[1].Width = 130;
        }

        // The user has selected a row. Get the metadata from that row, and make it the selected metadata.
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
                this.PopulateButton.IsEnabled = this.dataFieldSelected && this.metadataFieldSelected;
            }
            else
            {
                this.MetadataDisplayText.Content = String.Empty;
                // Note that metadata name may still has spaces in it. We will have to strip it out and check it to make sure its an acceptable data label
                this.metadataFieldSelected = false;
                this.PopulateButton.IsEnabled = this.dataFieldSelected && this.metadataFieldSelected;
            }
        }

        // Listbox Callback indicating the user has selected a data field. 
        private void NoteFieldsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.DataFields.SelectedItem != null)
            {
                this.DataField.Content = this.DataFields.SelectedItem as string;
                this.dataFieldLabel = this.DataFields.SelectedItem as string;
                this.dataFieldSelected = true;
            }
            this.PopulateButton.IsEnabled = this.dataFieldSelected && this.metadataFieldSelected;
        }

        // Populate the database with the metadata for the selected note field
        private void Populate()
        {
            // This list will hold key / value pairs that will be bound to the datagrid feedback, 
            // which is the way to make those pairs appear in the data grid during background worker progress updates
            ObservableCollection<KeyValuePair<string, string>> keyValueList = new ObservableCollection<KeyValuePair<string, string>>();
            bool? metadataExtractorRBIsChecked = this.MetadataExtractorRB.IsChecked;

            // Update the UI to show the feedback datagrid, 
            this.PopulatingMessage.Text = "Populating '" + this.DataField.Content + "' from each file's '" + this.MetadataDisplayText.Content + "' metadata ";
            this.PopulateButton.Visibility = Visibility.Collapsed; // Hide the populate button, as we are now in the act of populating things
            this.ClearIfNoMetadata.Visibility = Visibility.Collapsed; // Hide the checkbox button for the same reason
            this.PrimaryPanel.Visibility = Visibility.Collapsed;  // Hide the various panels to reveal the feedback datagrid
            this.DataFields.Visibility = Visibility.Collapsed;
            this.FeedbackPanel.Visibility = Visibility.Visible;
            this.PanelHeader.Visibility = Visibility.Collapsed;
            this.ToolSelectionPanel.Visibility = Visibility.Collapsed;

#pragma warning disable CA2000 // Dispose objects before losing scope. Reason: Not required as Dispose on BackgroundWorker doesn't do anything
            BackgroundWorker backgroundWorker = new BackgroundWorker() { WorkerReportsProgress = true };
#pragma warning restore CA2000 // Dispose objects before losing scope
            backgroundWorker.DoWork += (ow, ea) =>
            {
                // this runs on the background thread; its written as an anonymous delegate
                // We need to invoke this to allow updates on the UI
                this.Dispatcher.Invoke(new Action(() =>
                {
                }));

                // For each row in the database, get the image filename and try to extract the chosen metadata value.
                // If we can't decide if we want to leave the data field alone or to clear it depending on the state of the isClearIfNoMetadata (set via the checkbox)
                // Report progress as needed.
                // This tuple list will hold the id, key and value that we will want to update in the database
                string dataLabelToUpdate = this.dataLabelByLabel[this.dataFieldLabel];
                List<ColumnTuplesWithWhere> imagesToUpdate = new List<ColumnTuplesWithWhere>();
                TimeZoneInfo imageSetTimeZone = this.database.ImageSet.GetSystemTimeZone();
                int progress = 0;

                double totalImages = this.database.CurrentlySelectedFileCount;
                Dictionary<string, ImageMetadata> metadata = new Dictionary<string, ImageMetadata>();
                for (int imageIndex = 0; imageIndex < this.database.CurrentlySelectedFileCount; ++imageIndex)
                {

                    ImageRow image = this.database.FileTable[imageIndex];
                    if (metadataExtractorRBIsChecked == true)
                    {   // MetadataExtractor specific code
                        metadata = ImageMetadataDictionary.LoadMetadata(image.GetFilePath(this.database.FolderPath));
                    }
                    else
                    {
                        // ExifTool specific code - note that we transform results into the same dictionary structure used by the MetadataExtractor
                        string[] tags = { this.metadataFieldName };
                        metadata.Clear();
                        Dictionary<string, string> exifData = this.exifTool.FetchExifFrom(image.GetFilePath(this.database.FolderPath), tags);
                        if (exifData.ContainsKey(tags[0]))
                        {
                            metadata.Add(tags[0], new Timelapse.Util.ImageMetadata(String.Empty, tags[0], exifData[tags[0]]));
                        }
                    }
                    progress = Convert.ToInt32(imageIndex / totalImages * 100.0);
                    backgroundWorker.ReportProgress(progress, new FeedbackMessage(String.Format("{0}/{1} images. Processing ", imageIndex, totalImages), image.File));

                    if (metadata.ContainsKey(this.metadataFieldName) == false)
                    {
                        if (this.clearIfNoMetadata)
                        {
                            // Clear the data field if there is no metadata...
                            if (dataLabelToUpdate == Constant.DatabaseColumn.DateTime)
                            {
                                image.SetDateTimeOffsetFromFileInfo(this.database.FolderPath);
                                imagesToUpdate.Add(image.GetDateTimeColumnTuples());
                                keyValueList.Add(new KeyValuePair<string, string>(image.File, "No metadata found - date/time reread from file"));
                            }
                            else
                            {
                                List<ColumnTuple> clearField = new List<ColumnTuple>() { new ColumnTuple(this.dataLabelByLabel[this.dataFieldLabel], String.Empty) };
                                imagesToUpdate.Add(new ColumnTuplesWithWhere(clearField, image.ID));
                                keyValueList.Add(new KeyValuePair<string, string>(image.File, "No metadata found - data field is cleared"));
                            }
                        }
                        else
                        {
                            keyValueList.Add(new KeyValuePair<string, string>(image.File, "No metadata found - data field remains unaltered"));
                        }

                        continue;
                    }

                    string metadataValue = metadata[this.metadataFieldName].Value;
                    ColumnTuplesWithWhere imageUpdate;
                    if (dataLabelToUpdate == Constant.DatabaseColumn.DateTime)
                    {
                        if (DateTimeHandler.TryParseMetadataDateTaken(metadataValue, imageSetTimeZone, out DateTimeOffset metadataDateTime))
                        {
                            image.SetDateTimeOffset(metadataDateTime);
                            imageUpdate = image.GetDateTimeColumnTuples();
                            keyValueList.Add(new KeyValuePair<string, string>(image.File, metadataValue));
                        }
                        else
                        {
                            keyValueList.Add(new KeyValuePair<string, string>(image.File, String.Format("'{0}' - data field remains unaltered - not a valid date/time.", metadataValue)));
                            continue;
                        }
                    }
                    else
                    {
                        imageUpdate = new ColumnTuplesWithWhere(new List<ColumnTuple>() { new ColumnTuple(dataLabelToUpdate, metadataValue) }, image.ID);
                        keyValueList.Add(new KeyValuePair<string, string>(image.File, metadataValue));
                    }
                    imagesToUpdate.Add(imageUpdate);

                    if (imageIndex % Constant.ThrottleValues.SleepForImageRenderInterval == 0)
                    {
                        Thread.Sleep(Constant.ThrottleValues.RenderingBackoffTime); // Put in a short delay every now and then, as otherwise the UI may not update.
                    }
                }
                backgroundWorker.ReportProgress(progress, new FeedbackMessage("Writing the data...", "Please wait..."));
                this.database.UpdateFiles(imagesToUpdate);
                backgroundWorker.ReportProgress(progress, new FeedbackMessage("Done", String.Empty));
            };
            backgroundWorker.ProgressChanged += (o, ea) =>
            {
                // Update the progress bar with a message
                FeedbackMessage message = (FeedbackMessage)ea.UserState;
                this.UpdateMetadataLoadProgress(ea.ProgressPercentage, message.Message + message.FileName);
            };
            backgroundWorker.RunWorkerCompleted += (o, ea) =>
            {
                // Show the results
                this.FeedbackGrid.ItemsSource = keyValueList;
                this.btnCancel.Content = "Done"; // Change the Cancel button to Done, but inactivate it as we don't want the operation to be cancellable (due to worries about database corruption)
                this.btnCancel.IsEnabled = true;
                this.BusyIndicator.IsBusy = false;
                this.PopulatingMessage.Text = "Populated '" + this.DataField.Content + "' from each file's '" + this.MetadataDisplayText.Content + "' metadata as follows."; //this.dataFieldLabel
                if (this.exifTool != null)
                {
                    this.exifTool.Stop();
                }
            };
            this.BusyIndicator.IsBusy = true;

            // Set up the user interface to show the progress bar
            backgroundWorker.RunWorkerAsync();
        }

        private void UpdateMetadataLoadProgress(int percent, string message)
        {
            ProgressBar bar = Utilities.GetVisualChild<ProgressBar>(this.BusyIndicator);
            TextBlock textmessage = Utilities.GetVisualChild<TextBlock>(this.BusyIndicator);
            if (bar != null)
            {
                bar.Value = percent;
            }
            if (textmessage != null)
            {
                textmessage.Text = message;
            }
        }

        // Ensures that the columns will have appropriate header names. Can't be set directly in code otherwise
        private void FeedbackDatagrid_AutoGeneratedColumns(object sender, EventArgs e)
        {
            this.FeedbackGrid.Columns[0].Header = "Image Name";
            this.FeedbackGrid.Columns[1].Header = "The Metadata Value for " + this.metadataFieldName;
        }

        private void PopulateButton_Click(object sender, RoutedEventArgs e)
        {
            this.Populate();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = ((string)this.btnCancel.Content == "Cancel") ? false : true;
        }

        // This checkbox sets the state as to whether the data field should be cleared or left alone if there is no metadata
        private void ClearIfNoMetadata_Checked(object sender, RoutedEventArgs e)
        {
            this.clearIfNoMetadata = (this.ClearIfNoMetadata.IsChecked == true) ? true : false;
        }

        // Classes that tracks our progress as we load the images
        // These are needed to make the background worker update correctly.
        private class FeedbackMessage
        {
            public string FileName { get; set; }
            public string Message { get; set; }

            public FeedbackMessage(string message, string fileName)
            {
                this.FileName = fileName;
                this.Message = message;
            }
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
    }
}
