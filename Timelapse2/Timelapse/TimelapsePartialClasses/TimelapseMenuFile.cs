using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Timelapse.Controls;
using Timelapse.Database;
using Timelapse.Dialog;
using Timelapse.Enums;
using Timelapse.Util;
using DialogResult = System.Windows.Forms.DialogResult;
using MessageBox = Timelapse.Dialog.MessageBox;
using SaveFileDialog = System.Windows.Forms.SaveFileDialog;

// File Menu Callbacks
namespace Timelapse
{
    public partial class TimelapseWindow : Window, IDisposable
    {
        // File Submenu Opening 
        private void File_SubmenuOpening(object sender, RoutedEventArgs e)
        {
            this.FilePlayer_Stop(); // In case the FilePlayer is going
            this.RecentFileSets_Refresh();

            // Enable / disable various menu items depending on whether we are looking at the single image view or overview
            this.MenuItemExportThisImage.IsEnabled = this.IsDisplayingSingleImage();
        }

        // Load template, images, and video files...
        private async void MenuItemLoadImages_Click(object sender, RoutedEventArgs e)
        {
            if (this.TryGetTemplatePath(out string templateDatabasePath))
            {
                Mouse.OverrideCursor = Cursors.Wait;
                await this.TryOpenTemplateAndBeginLoadFoldersAsync(templateDatabasePath).ConfigureAwait(true);
                Mouse.OverrideCursor = null;
            }
        }

        // Load a recently used image set
        private async void MenuItemRecentImageSet_Click(object sender, RoutedEventArgs e)
        {
            string recentDatabasePath = (string)((MenuItem)sender).ToolTip;
            Mouse.OverrideCursor = Cursors.Wait;
            bool result = await this.TryOpenTemplateAndBeginLoadFoldersAsync(recentDatabasePath).ConfigureAwait(true);
            if (result == false)
            {
                this.State.MostRecentImageSets.TryRemove(recentDatabasePath);
                this.RecentFileSets_Refresh();
            }
            Mouse.OverrideCursor = null;
        }

        // Add Images to Image Set 
        private void MenuItemAddImagesToImageSet_Click(object sender, RoutedEventArgs e)
        {
            if (this.ShowFolderSelectionDialog(this.FolderPath, out string folderPath))
            {
                Mouse.OverrideCursor = Cursors.Wait;
                this.TryBeginImageFolderLoad(this.FolderPath, folderPath);
                Mouse.OverrideCursor = null;
            }
        }

        // Import Detection data
        private async void MenuItemImportDetectionData_Click(object sender, RoutedEventArgs e)
        {
            string jsonFileName = Constant.File.RecognitionJsonDataFileName;
            if (Utilities.TryGetFileFromUser(
                      "Select a .json file that contains the recognition data. It will be merged into the current image set",
                      Path.Combine(this.DataHandler.FileDatabase.FolderPath, jsonFileName),
                      String.Format("JSon files (*{0})|*{0}", Constant.File.JsonFileExtension),
                      Constant.File.JsonFileExtension,
                      out string jsonFilePath) == false)
            {
                return;
            }
            List<string> foldersInDBListButNotInJSon = new List<string>();
            List<string> foldersInJsonButNotInDB = new List<string>();
            List<string> foldersInBoth = new List<string>();

            // Show the Busy indicator
            this.BusyCancelIndicator.IsBusy = true;

            // Load the detections
            bool result = await this.DataHandler.FileDatabase.PopulateDetectionTablesAsync(jsonFilePath, foldersInDBListButNotInJSon, foldersInJsonButNotInDB, foldersInBoth).ConfigureAwait(true);

            // Hide the Busy indicator
            //this.BusyCancelIndicator.IsBusy = false;

            if (result)
            {
                // Only reset these if we actually imported some detections, as otherwise nothing has changed.
                GlobalReferences.DetectionsExists = this.State.UseDetections ? this.DataHandler.FileDatabase.DetectionsExists() : false;
                await this.FilesSelectAndShowAsync().ConfigureAwait(true);
            }
            this.BusyCancelIndicator.IsBusy = false;

            if (result == false)
            {
                // No matching folders in the DB and the detector
                MessageBox messageBox = new MessageBox("Recognition data not imported.", this);
                messageBox.Message.Problem = "No recognition information was imported, as none of its image folder paths were found in your Database file." + Environment.NewLine;
                messageBox.Message.Problem += "Thus no recognition information could be assigned to your images.";
                messageBox.Message.Reason = "The recognizer may have been run on a folder containing various image sets, each in a sub-folder. " + Environment.NewLine;
                messageBox.Message.Reason += "For example, if the recognizer was run on 'AllFolders/Camera1/' but your template and database is in 'Camera1/'," + Environment.NewLine;
                messageBox.Message.Reason += "the folder paths won't match, since AllFolders/Camera1/ \u2260 Camera1/.";
                messageBox.Message.Solution = "Microsoft provides a program to extract a subset of recognitions in the Recognition file" + Environment.NewLine;
                messageBox.Message.Solution += "that you can use to extract recognitions matching your sub-folder: " + Environment.NewLine;
                messageBox.Message.Solution += "  http://aka.ms/cameratraps-detectormismatch";
                messageBox.Message.Result = "Recognition information was not imported.";
                messageBox.Message.Details = this.ComposeFolderDetails(foldersInDBListButNotInJSon, foldersInJsonButNotInDB, foldersInBoth);
                messageBox.ShowDialog();
            }
            else if (foldersInDBListButNotInJSon.Count > 0)
            {
                // Some folders missing - show which folder paths in the DB are not in the detector
                MessageBox messageBox = new MessageBox("Recognition data imported for only some of your folders.", this);
                messageBox.Message.Icon = MessageBoxImage.Information;
                messageBox.Message.Problem = "Some of the sub-folders in your image set's Database file have no corresponding entries in the Recognition file." + Environment.NewLine;
                messageBox.Message.Problem += "While not an error, we just wanted to bring it to your attention.";
                messageBox.Message.Reason = "This could happen if you have added, moved, or renamed the folders since supplying the originals to the recognizer:" + Environment.NewLine;
                messageBox.Message.Result = "Recognition data will still be imported for the other folders.";
                messageBox.Message.Hint = "You can also view which images are missing recognition data by choosing" + Environment.NewLine;
                messageBox.Message.Hint += "'Select|Custom Selection...' and checking the box titled 'Show all files with no recognition data'";
                messageBox.Message.Details = this.ComposeFolderDetails(foldersInDBListButNotInJSon, foldersInJsonButNotInDB, foldersInBoth);
                messageBox.ShowDialog();
            }
            else
            {
                // Detections successfully imported message
                MessageBox messageBox = new MessageBox("Recognitions imported.", this);
                messageBox.Message.Icon = MessageBoxImage.Information;
                messageBox.Message.Result = "Recognition data imported. You can select images matching particular recognitions by choosing 'Select|Custom Selection...'";
                messageBox.Message.Hint = "You can also view which images (if any) are missing recognition data by choosing" + Environment.NewLine;
                messageBox.Message.Hint += "'Select|Custom Selection...' and checking the box titled 'Show all files with no recognition data'";
                messageBox.Message.Details = this.ComposeFolderDetails(foldersInDBListButNotInJSon, foldersInJsonButNotInDB, foldersInBoth);
                messageBox.ShowDialog();
            }
        }

        private string ComposeFolderDetails(List<string> foldersInDBListButNotInJSon, List<string> foldersInJsonButNotInDB, List<string> foldersInBoth)
        {
            string folderDetails = String.Empty;
            if (foldersInDBListButNotInJSon.Count == 0 && foldersInJsonButNotInDB.Count == 0)
            {
                // All folders match, so don't show any details.
                return folderDetails;
            }

            // At this point, there is a mismatch, so we should show something.
            if (foldersInBoth.Count > 0)
            {
                folderDetails += foldersInBoth.Count.ToString() + " of your folders had matching recognition data:" + Environment.NewLine;
                foreach (string folder in foldersInBoth)
                {
                    folderDetails += "\u2022 " + folder + Environment.NewLine;
                }
                folderDetails += Environment.NewLine;
            }

            if (foldersInDBListButNotInJSon.Count > 0)
            {
                folderDetails += foldersInDBListButNotInJSon.Count.ToString() + " of your folders had no matching recognition data:" + Environment.NewLine;
                foreach (string folder in foldersInDBListButNotInJSon)
                {
                    folderDetails += "\u2022 " + folder + Environment.NewLine;
                }
                folderDetails += Environment.NewLine;
            }
            if (foldersInJsonButNotInDB.Count > 0)
            {
                folderDetails += "The recognition file also included " + foldersInJsonButNotInDB.Count.ToString() + " other ";
                folderDetails += (foldersInJsonButNotInDB.Count == 1) ? "folder" : "folders";
                folderDetails += " not found in your folders:" + Environment.NewLine;
                foreach (string folder in foldersInJsonButNotInDB)
                {
                    folderDetails += "\u2022 " + folder + Environment.NewLine;
                }
            }
            return folderDetails;
        }

        // Export data for this image set as a .csv file
        // Export data for this image set as a .csv file and preview in Excel 
        private void MenuItemExportCsv_Click(object sender, RoutedEventArgs e)
        {
            if (this.State.SuppressSelectedCsvExportPrompt == false &&
                this.DataHandler.FileDatabase.ImageSet.FileSelection != FileSelectionEnum.All)
            {
                MessageBox messageBox = new MessageBox("Exporting to a .csv file on a selected view...", this, MessageBoxButton.OKCancel);
                messageBox.Message.What = "Only a subset of your data will be exported to the .csv file.";
                messageBox.Message.Reason = "As your selection (in the Selection menu) is not set to view 'All', ";
                messageBox.Message.Reason += "only data for these selected files will be exported. ";
                messageBox.Message.Solution = "If you want to export just this subset, then " + Environment.NewLine;
                messageBox.Message.Solution += "\u2022 click Okay" + Environment.NewLine + Environment.NewLine;
                messageBox.Message.Solution += "If you want to export data for all your files, then " + Environment.NewLine;
                messageBox.Message.Solution += "\u2022 click Cancel," + Environment.NewLine;
                messageBox.Message.Solution += "\u2022 select 'All Files' in the Selection menu, " + Environment.NewLine;
                messageBox.Message.Solution += "\u2022 retry exporting your data as a .csv file.";
                messageBox.Message.Hint = "If you check don't show this message this dialog can be turned back on via the Options menu.";
                messageBox.Message.Icon = MessageBoxImage.Warning;
                messageBox.DontShowAgain.Visibility = Visibility.Visible;

                bool? exportCsv = messageBox.ShowDialog();
                if (exportCsv != true)
                {
                    return;
                }

                if (messageBox.DontShowAgain.IsChecked.HasValue)
                {
                    this.State.SuppressSelectedCsvExportPrompt = messageBox.DontShowAgain.IsChecked.Value;
                }
            }

            // Generate the file names/path
            string csvFileName = Path.GetFileNameWithoutExtension(this.DataHandler.FileDatabase.FileName) + ".csv";
            string csvFilePath = Path.Combine(this.FolderPath, csvFileName);

            // Backup the csv file if it exists, as the export will overwrite it. 
            if (FileBackup.TryCreateBackup(this.FolderPath, csvFileName))
            {
                this.StatusBar.SetMessage("Backup of csv file made.");
            }
            else
            {
                this.StatusBar.SetMessage("No csv file backup was made.");
            }

            try
            {
                CsvReaderWriter.ExportToCsv(this.DataHandler.FileDatabase, csvFilePath, this.excludeDateTimeAndUTCOffsetWhenExporting);
            }
            catch (IOException exception)
            {
                // Can't write the spreadsheet file
                MessageBox messageBox = new MessageBox("Can't write the spreadsheet file.", this);
                messageBox.Message.Icon = MessageBoxImage.Error;
                messageBox.Message.Problem = "The following file can't be written: " + csvFilePath;
                messageBox.Message.Reason = "You may already have it open in Excel or another application.";
                messageBox.Message.Solution = "If the file is open in another application, close it and try again.";
                messageBox.Message.Hint = String.Format("{0}: {1}", exception.GetType().FullName, exception.Message);
                messageBox.ShowDialog();
                return;
            }

            MenuItem mi = (MenuItem)sender;
            if (mi == this.MenuItemExportAsCsvAndPreview)
            {
                try
                {
                    // Show the file in excel
                    // Create a process that will try to show the file
                    using (Process process = new Process())
                    {
                        process.StartInfo.UseShellExecute = true;
                        process.StartInfo.RedirectStandardOutput = false;
                        process.StartInfo.FileName = csvFilePath;
                        process.Start();
                    }
                }
                catch
                {
                    // Can't open excel
                    MessageBox messageBox = new MessageBox("Can't open Excel.", this);
                    messageBox.Message.Icon = MessageBoxImage.Error;
                    messageBox.Message.Problem = "Excel could not be opened to display " + csvFilePath;
                    messageBox.Message.Solution = "Try again, or just manually start Excel and open the .csv file ";
                    messageBox.ShowDialog();
                    return;
                }
            }
            else if (this.State.SuppressCsvExportDialog == false)
            {
                // since the exported file isn't shown give the user some feedback about the export operation
                MessageBox csvExportInformation = new MessageBox("Data exported.", this);
                csvExportInformation.Message.What = "The selected files were exported to " + csvFileName;
                csvExportInformation.Message.Result = String.Format("This file is overwritten every time you export it (backups can be found in the {0} folder).", Constant.File.BackupFolder);
                csvExportInformation.Message.Hint = "\u2022 You can open this file with most spreadsheet programs, such as Excel." + Environment.NewLine;
                csvExportInformation.Message.Hint += "\u2022 If you make changes in the spreadsheet file, you will need to import it to see those changes." + Environment.NewLine;
                csvExportInformation.Message.Hint += "\u2022 If you check don't show this message again you can still see the name of the .csv file in the status bar at the lower right corner of the main Carnassial window.  This dialog can also be turned back on through the Options menu.";
                csvExportInformation.Message.Icon = MessageBoxImage.Information;
                csvExportInformation.DontShowAgain.Visibility = Visibility.Visible;

                bool? result = csvExportInformation.ShowDialog();
                if (result.HasValue && result.Value && csvExportInformation.DontShowAgain.IsChecked.HasValue)
                {
                    this.State.SuppressCsvExportDialog = csvExportInformation.DontShowAgain.IsChecked.Value;
                }
            }
            this.StatusBar.SetMessage("Data exported to " + csvFileName);
        }

        // Import data from a CSV file. Display instructions and error messages as needed.
        private async void MenuItemImportFromCsv_Click(object sender, RoutedEventArgs e)
        {
            if (this.State.SuppressCsvImportPrompt == false)
            {
                MessageBox messageBox = new MessageBox("How importing .csv data works", this, MessageBoxButton.OKCancel);
                messageBox.Message.What = "Importing data from a .csv (comma separated value) file follows the rules below.";
                messageBox.Message.Reason = "\u2022 The first row in the CSV file must comprise column Headers that match the DataLabels in the .tdb template file." + Environment.NewLine;
                messageBox.Message.Reason += "\u2022 The column Header 'File' must be included." + Environment.NewLine;
                messageBox.Message.Reason += "\u2022 Subsequent rows defines the data for each File ." + Environment.NewLine;
                messageBox.Message.Reason += "\u2022 Column data should match the Header type. In particular," + Environment.NewLine;
                messageBox.Message.Reason += "  \u2022\u2022 File values should define name of the file you want to update." + Environment.NewLine;
                messageBox.Message.Reason += "  \u2022\u2022 Counter values must be blank or a positive integer. " + Environment.NewLine;
                messageBox.Message.Reason += "  \u2022\u2022 Flag and DeleteFlag values must be 'true' or 'false'." + Environment.NewLine;
                messageBox.Message.Reason += "  \u2022\u2022 FixedChoice values should be a string that exactly matches one of the FixedChoice menu options, or empty. ";
                messageBox.Message.Result = "Image values for identified files will be updated, except for values relating to a File's location or its dates / times.";
                messageBox.Message.Hint = "\u2022 Your CSV file columns can be a subset of your template's DataLabels." + Environment.NewLine;
                messageBox.Message.Hint += "\u2022 Warning will be generated for non-matching CSV fields, which you can then fix." + Environment.NewLine;
                messageBox.Message.Hint += "\u2022 If you check 'Don't show this message again' this dialog can be turned back on via the Options menu.";
                messageBox.Message.Icon = MessageBoxImage.Warning;
                messageBox.DontShowAgain.Visibility = Visibility.Visible;

                bool? proceeed = messageBox.ShowDialog();
                if (proceeed != true)
                {
                    return;
                }

                if (messageBox.DontShowAgain.IsChecked.HasValue)
                {
                    this.State.SuppressCsvImportPrompt = messageBox.DontShowAgain.IsChecked.Value;
                }
            }

            string csvFileName = Path.GetFileNameWithoutExtension(this.DataHandler.FileDatabase.FileName) + Constant.File.CsvFileExtension;
            if (Utilities.TryGetFileFromUser(
                                 "Select a .csv file to merge into the current image set",
                                 Path.Combine(this.DataHandler.FileDatabase.FolderPath, csvFileName),
                                 String.Format("Comma separated value files (*{0})|*{0}", Constant.File.CsvFileExtension),
                                 Constant.File.CsvFileExtension,
                                 out string csvFilePath) == false)
            {
                return;
            }

            // Create a backup database file
            if (FileBackup.TryCreateBackup(this.FolderPath, this.DataHandler.FileDatabase.FileName))
            {
                this.StatusBar.SetMessage("Backup of data file made.");
            }
            else
            {
                this.StatusBar.SetMessage("No data file backup was made.");
            }

            try
            {
                // Show the Busy indicator
                this.BusyCancelIndicator.IsBusy = true;

                Tuple<bool, List<string>> resultAndImportErrors;
                resultAndImportErrors = await CsvReaderWriter.TryImportFromCsv(csvFilePath, this.DataHandler.FileDatabase).ConfigureAwait(true);

                this.BusyCancelIndicator.IsBusy = false;

                if (resultAndImportErrors.Item1 == false)
                {
                    MessageBox messageBox = new MessageBox("Can't import the .csv file.", this);
                    messageBox.Message.Icon = MessageBoxImage.Error;
                    messageBox.Message.Problem = String.Format("The file {0} could not be read.", Path.GetFileName(csvFilePath));
                    messageBox.Message.Reason = "The .csv file is not compatible with the current image set.";
                    messageBox.Message.Solution = "Check that:" + Environment.NewLine;
                    messageBox.Message.Solution += "\u2022 The first row of the .csv file is a header line." + Environment.NewLine;
                    messageBox.Message.Solution += "\u2022 The column names in the header line match the database." + Environment.NewLine;
                    messageBox.Message.Solution += "\u2022 Choice and ImageQuality values are in that DataLabel's Choice list." + Environment.NewLine;
                    messageBox.Message.Solution += "\u2022 Counter values are numbers or blanks." + Environment.NewLine;
                    messageBox.Message.Solution += "\u2022 Flag and DeleteFlag values are either 'true' or 'false'.";
                    messageBox.Message.Result = "Importing of data from the CSV file was aborted. No changes were made.";
                    messageBox.Message.Hint = "Change your CSV file to fix the errors below and try again.";
                    foreach (string importError in resultAndImportErrors.Item2)
                    {
                        messageBox.Message.Hint += Environment.NewLine + "\u2022 " + importError;
                    }
                    messageBox.ShowDialog();
                }
                else
                {
                    // Importing done.
                    MessageBox messageBox = new MessageBox("CSV file imported", this);
                    messageBox.Message.Icon = MessageBoxImage.Information;
                    messageBox.Message.What = String.Format("The file {0} was successfully imported.", Path.GetFileName(csvFilePath));
                    messageBox.Message.Hint = "\u2022 Check your data. If it is not what you expect, restore your data by using latest backup file in " + Constant.File.BackupFolder + ".";
                    messageBox.ShowDialog();

                    // Reload the data
                    this.BusyCancelIndicator.IsBusy = true;
                    await this.FilesSelectAndShowAsync().ConfigureAwait(true);
                    this.BusyCancelIndicator.IsBusy = false;
                    this.StatusBar.SetMessage("CSV file imported.");
                }
            }
            catch (Exception exception)
            {
                MessageBox messageBox = new MessageBox("Can't import the .csv file.", this);
                messageBox.Message.Icon = MessageBoxImage.Error;
                messageBox.Message.Problem = String.Format("The file {0} could not be opened.", Path.GetFileName(csvFilePath));
                messageBox.Message.Reason = "Most likely the file is open in another program. The technical reason is:" + Environment.NewLine;
                messageBox.Message.Reason += exception.Message;
                messageBox.Message.Solution = "If the file is open in another program, close it.";
                //messageBox.Message.Result = String.Format("{0}: {1}", exception.GetType().FullName, exception.Message);
                messageBox.Message.Result = "Importing of data from the CSV file was aborted. No changes were made.";
                messageBox.Message.Hint = "Is the file open in Excel?";
                messageBox.ShowDialog();
            }
        }

        // Export the current image or video _file
        private void MenuItemExportThisImage_Click(object sender, RoutedEventArgs e)
        {
            if (!this.DataHandler.ImageCache.Current.IsDisplayable(this.FolderPath))
            {
                MessageBox messageBox = new MessageBox("Can't export this file!", this);
                messageBox.Message.Icon = MessageBoxImage.Error;
                messageBox.Message.Problem = "Timelapse can't export the currently displayed file.";
                messageBox.Message.Reason = "It is likely a corrupted or missing file.";
                messageBox.Message.Solution = "Make sure you have navigated to, and are displaying, a valid file before you try to export it.";
                messageBox.ShowDialog();
                return;
            }
            // Get the file name of the current image 
            string sourceFile = this.DataHandler.ImageCache.Current.File;

            // Set up a Folder Browser with some instructions
            using (SaveFileDialog dialog = new SaveFileDialog()
            {
                Title = "Export a copy of the currently displayed file",
                Filter = String.Format("*{0}|*{0}", Path.GetExtension(this.DataHandler.ImageCache.Current.File)),
                FileName = sourceFile,
                OverwritePrompt = true
            })
            {
                // Display the Folder Browser dialog
                DialogResult result = dialog.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    // Set the source and destination file names, including the complete path
                    string sourcePath = this.DataHandler.ImageCache.Current.GetFilePath(this.FolderPath);
                    string destFileName = dialog.FileName;

                    // Try to copy the source file to the destination, overwriting the destination file if it already exists.
                    // And giving some feedback about its success (or failure) 
                    try
                    {
                        File.Copy(sourcePath, destFileName, true);
                        this.StatusBar.SetMessage(sourceFile + " copied to " + destFileName);
                    }
                    catch (Exception exception)
                    {
                        TraceDebug.PrintMessage(String.Format("Copy of '{0}' to '{1}' failed. {2}", sourceFile, destFileName, exception.ToString()));
                        this.StatusBar.SetMessage(String.Format("Copy failed with {0} in MenuItemExportThisImage_Click.", exception.GetType().Name));
                    }
                }
            }
        }

        // Rename the data file
        private void MenuItemRenameFileDatabaseFile_Click(object sender, RoutedEventArgs e)
        {
            RenameFileDatabaseFile renameFileDatabase = new RenameFileDatabaseFile(this.DataHandler.FileDatabase.FileName, this)
            {
                Owner = this
            };
            bool? result = renameFileDatabase.ShowDialog();
            if (result == true)
            {
                this.DataHandler.FileDatabase.RenameFile(renameFileDatabase.NewFilename);
            }
        }

        // Close Image Set
        private void MenuFileCloseImageSet_Click(object sender, RoutedEventArgs e)
        {
            // if we are actually viewing any files
            if (this.IsFileDatabaseAvailable())
            {
                // persist image set properties if an image set has been opened
                if (this.DataHandler.FileDatabase.CountAllCurrentlySelectedFiles > 0)
                {
                    this.Window_Closing(null, null);
                    // revert to custom selections to all 
                    if (this.DataHandler.FileDatabase.ImageSet.FileSelection == FileSelectionEnum.Custom)
                    {
                        this.DataHandler.FileDatabase.ImageSet.FileSelection = FileSelectionEnum.All;
                    }
                    if (this.DataHandler.ImageCache != null && this.DataHandler.ImageCache.Current != null)
                    {
                        this.DataHandler.FileDatabase.ImageSet.MostRecentFileID = this.DataHandler.ImageCache.Current.ID;
                    }

                    // write image set properties to the database
                    this.DataHandler.FileDatabase.UpdateSyncImageSetToDatabase();

                    // ensure custom filter operator is synchronized in state for writing to user's registry
                    this.State.CustomSelectionTermCombiningOperator = this.DataHandler.FileDatabase.CustomSelection.TermCombiningOperator;
                }
                // discard the image set 
                if (this.DataHandler.ImageCache != null)
                {
                    this.DataHandler.ImageCache.Dispose();
                }
                if (this.DataHandler != null)
                {
                    this.DataHandler.Dispose();
                }
                this.DataHandler = null;
                this.templateDatabase = null;
                this.DataEntryControlPanel.IsVisible = false;
            }

            // As we are starting afresh, no detections should now exists.
            GlobalReferences.DetectionsExists = false;

            // Clear the data grid
            this.DataGrid.ItemsSource = null;

            // Reset the UX 
            this.State.Reset();
            this.MarkableCanvas.ZoomOutAllTheWay();
            this.FileNavigatorSliderReset();
            this.EnableOrDisableMenusAndControls();
            this.CopyPreviousValuesButton.Visibility = Visibility.Collapsed;
            this.DataEntryControlPanel.IsVisible = false;
            this.FilePlayer.Visibility = Visibility.Collapsed;
            this.InstructionPane.IsActive = true;
            this.DataGridSelectionsTimer.Stop();
            this.lastControlWithFocus = null;
            this.QuickPasteWindowTerminate();
        }


        // Exit Timelapse
        private void MenuItemExit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
            Application.Current.Shutdown();
        }

        private async void MenuItemMergeDatabases_Click(object sender, RoutedEventArgs e)
        {
            if (this.State.SuppressMergeDatabasesPrompt == false)
            {
                // Show a message that explains how merging databases works, and its constraints
                MessageBox messageBox = new MessageBox("Merge Databases.", this, MessageBoxButton.OKCancel);
                messageBox.Message.Icon = MessageBoxImage.Question;
                messageBox.Message.Title = "Merge Databases Explained.";
                messageBox.Message.What = "Merging databases works as follows. Timelapse will:" + Environment.NewLine;
                messageBox.Message.What += "\u2022 ask you to locate a root folder containing a template (a.tdb file)," + Environment.NewLine;
                messageBox.Message.What += String.Format("\u2022 create a new database (.ddb) file in that folder, called {0},{1}", Constant.File.MergedFileName, Environment.NewLine);
                messageBox.Message.What += "\u2022 search for other database (.ddb) files in that folder's sub-folders, " + Environment.NewLine;
                messageBox.Message.What += "\u2022 try to merge all data found in those found databases into the new database.";
                messageBox.Message.Details = "\u2022 All databases must be based on the same template, otherwise the merge will fail." + Environment.NewLine;
                messageBox.Message.Details += "\u2022 Databases found in the Backup folders are ignored." + Environment.NewLine;
                messageBox.Message.Details += "\u2022 Detections and Classifications (if any) are merged; categories are taken from the first database found with detections." + Environment.NewLine;
                messageBox.Message.Details += "\u2022 The merged database is independent of the found databases: updates will not propagate between them." + Environment.NewLine;
                messageBox.Message.Details += "\u2022 The merged database is a normal Timelapse database, which you can open and use as expected.";
                messageBox.Message.Hint = "Press Ok to continue with the merge, otherwise Cancel.";
                messageBox.DontShowAgain.Visibility = Visibility.Visible;
                messageBox.ShowDialog();
                if (messageBox.DialogResult == false)
                {
                    return;
                }

                if (messageBox.DontShowAgain.IsChecked.HasValue)
                {
                    this.State.SuppressMergeDatabasesPrompt = messageBox.DontShowAgain.IsChecked.Value;
                }
            }
            // Get the location of the template, which also determines the root folder
            if (this.TryGetTemplatePath(out string templateDatabasePath) == false)
            {
                return;
            }

            // Set up progress indicators
            Mouse.OverrideCursor = Cursors.Wait;
            IProgress<ProgressBarArguments> progress = progressHandler as IProgress<ProgressBarArguments>;
            this.EnableBusyCancelIndicatorForSelection(true);

            // Find all the .DDB files located in subfolders under the root folder (excluding backup folders)
            string startFolder = Path.GetDirectoryName(templateDatabasePath);
            List<string> allDDBFiles = new List<string>();
            FilesFoldersAndPaths.RecursivelyFindFilesWithPattern(startFolder, "*" + Constant.File.FileDatabaseFileExtension, true, allDDBFiles);

            // Merge the found databases into a new (or replaced) TimelapseData_merged.ddb file located in the same folder as the template.
            // Note: .ddb files found in a Backup folder will be ignored
            ErrorsAndWarnings errorMessages = await MergeDatabases.TryMergeDatabasesAsync(templateDatabasePath, allDDBFiles, progress).ConfigureAwait(true);

            // Turn off progress indicators
            this.EnableBusyCancelIndicatorForSelection(false);
            Mouse.OverrideCursor = null;

            // Show errors and/or warnings, if any.
            if (errorMessages.Errors.Count != 0 || errorMessages.Warnings.Count != 0)
            {
                MessageBox messageBox = new MessageBox("Merge Databases Results.", this);
                messageBox.Message.Icon = MessageBoxImage.Error;
                if (errorMessages.Errors.Count != 0)
                {
                    messageBox.Message.Title = "Merge Databases Failed.";
                    messageBox.Message.What = "The merged database could not be created for the following reasons:";
                }
                else if (errorMessages.Warnings.Count != 0)
                {
                    messageBox.Message.Title = "Merge Databases Left Out Some Files.";
                    messageBox.Message.What = "The merged database left out some files for the following reasons:";
                }

                if (errorMessages.Errors.Count != 0)
                {
                    messageBox.Message.What += String.Format("{0}{0}Errors:", Environment.NewLine);
                    foreach (string error in errorMessages.Errors)
                    {
                        messageBox.Message.What += String.Format("{0}\u2022 {1},", Environment.NewLine, error);
                    }
                }
                if (errorMessages.Warnings.Count != 0)
                {
                    messageBox.Message.What += String.Format("{0}{0}Warnings:", Environment.NewLine);
                }
                foreach (string warning in errorMessages.Warnings)
                {
                    messageBox.Message.What += String.Format("{0}\u2022 {1},", Environment.NewLine, warning);
                }
                messageBox.ShowDialog();
            }
        }

        #region Progess handler / Progress bar updates 
        // Set up a progress handler that will update the progress bar
        readonly Progress<ProgressBarArguments> progressHandler = new Progress<ProgressBarArguments>(value =>
        {
            // Update the progress bar
            UpdateProgressBar(GlobalReferences.BusyCancelIndicator, value.PercentDone, value.Message, value.CancelMessage, value.IsCancelEnabled, value.IsIndeterminate);
        });

        static private void UpdateProgressBar(BusyCancelIndicator busyCancelIndicator, int percent, string message, string cancelMessage, bool isCancelEnabled, bool isIndeterminate)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // Code to run on the GUI thread.
                // Check the arguments for null 
                ThrowIf.IsNullArgument(busyCancelIndicator, nameof(busyCancelIndicator));

                // Set it as a progressive or indeterminate bar
                busyCancelIndicator.IsIndeterminate = isIndeterminate;

                // Set the progress bar position (only visible if determinate)
                busyCancelIndicator.Percent = percent;

                // Update the text message
                busyCancelIndicator.Message = message;

                // Update the cancel button to reflect the cancelEnabled argument
                busyCancelIndicator.CancelButtonIsEnabled = isCancelEnabled;
                busyCancelIndicator.CancelButtonText = isCancelEnabled ? "Cancel" : cancelMessage;
            });
        }
        #endregion
    }
}
