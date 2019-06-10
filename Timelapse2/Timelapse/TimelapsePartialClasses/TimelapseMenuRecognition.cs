using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Timelapse.Controls;
using Timelapse.Database;
using Timelapse.Dialog;
using Timelapse.Util;
using MessageBox = Timelapse.Dialog.MessageBox;

namespace Timelapse
{
    public partial class TimelapseWindow : Window, IDisposable
    {
        private void Recognition_SubmenuOpening(object sender, RoutedEventArgs e)
        {
            // Not sure if we need this
        }

        // Import the recognition data from a well-formed csv file. 
       private void MenuItemImportRecognitionData_Click(object sender, RoutedEventArgs e)
        {
            // Ask the user for the file name containing the recognition data
            string csvFileName = Constant.File.RecognitionDataFileName;
            if (Utilities.TryGetFileFromUser(
                      "Select a .csv file that contains the recognition data. It will be merged into the current image set",
                      Path.Combine(this.dataHandler.FileDatabase.FolderPath, csvFileName),
                      String.Format("Comma separated value files (*{0})|*{0}", Constant.File.CsvFileExtension),
                      Constant.File.CsvFileExtension,
                      out string csvFilePath) == false)
            {
                return;
            }
            Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
            // Create a backup database file
            if (FileBackup.TryCreateBackup(this.FolderPath, this.dataHandler.FileDatabase.FileName))
            {
                this.StatusBar.SetMessage("Backup of data file made.");
            }
            else
            {
                this.StatusBar.SetMessage("No data file backup was made.");
            }

            // Try to import the recognition data

            bool result = CsvReaderWriter.TryImportRecognitionDataFromCsv(csvFilePath, this.dataHandler.FileDatabase, out List<string> importErrors);
            Mouse.OverrideCursor = null;
            if (result == false)
            {
                MessageBox messageBox = new MessageBox("The recognition data could not be imported.", this);
                messageBox.Message.Icon = MessageBoxImage.Error;
                messageBox.Message.Problem = String.Format("The recognition data could not be imported from ", csvFilePath);
                messageBox.Message.Reason = "The errors encountered were:" + Environment.NewLine;
                foreach (string importError in importErrors)
                {
                    messageBox.Message.Reason += Environment.NewLine + "\u2022 " + importError;
                }
                messageBox.ShowDialog();
            }
            else
            {
                // PERFORMANCE - This reselect can be avoided by updating the data fields in the table as we read the recognition data. 
                this.FilesSelectAndShow(this.dataHandler.ImageCache.Current.ID, this.dataHandler.FileDatabase.ImageSet.FileSelection, true);
            }
        }

        // Set various options of the image recognition detector
        private void MenuItemDetectorOptions_Click(object sender, RoutedEventArgs e)
        {
            DetectorOptions detectorOptions = new DetectorOptions(this.state, this.dataHandler.FileDatabase, this);
            detectorOptions.ShowDialog();
            // redisplay the file as the options may change how bounding boxes should be displayed
            if (this.dataHandler != null)
            { 
                this.FileShow(this.dataHandler.ImageCache.CurrentRow, true);
            }
        }
    }
}
