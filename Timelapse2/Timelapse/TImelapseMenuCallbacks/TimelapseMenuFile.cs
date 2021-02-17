﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Timelapse.Controls;
using Timelapse.Database;
using Timelapse.Dialog;
using Timelapse.Enums;
using Timelapse.Util;
using DialogResult = System.Windows.Forms.DialogResult;
using SaveFileDialog = System.Windows.Forms.SaveFileDialog;

// File Menu Callbacks
namespace Timelapse
{
    public partial class TimelapseWindow : Window, IDisposable
    {
        #region File Submenu Opening 
        private void File_SubmenuOpening(object sender, RoutedEventArgs e)
        {
            this.FilePlayer_Stop(); // In case the FilePlayer is going
            this.RecentFileSets_Refresh();

            // Enable / disable various menu items depending on whether we are looking at the single image view or overview
            this.MenuItemExportThisImage.IsEnabled = this.IsDisplayingSingleImage();
            this.MenuItemExportSelectedImages.IsEnabled = this.IsFileDatabaseAvailable();
        }
        #endregion

        #region Loading image sets
        // Load template, images, and video files...
        private async void MenuItemLoadImages_Click(object sender, RoutedEventArgs e)
        {
            if (this.TryGetTemplatePath(out string templateDatabasePath))
            {
                Mouse.OverrideCursor = Cursors.Wait;
                this.StatusBar.SetMessage("Loading images, please wait...");
                await this.TryOpenTemplateAndBeginLoadFoldersAsync(templateDatabasePath).ConfigureAwait(true);
                this.StatusBar.SetMessage("Image set is now loaded.");
                Mouse.OverrideCursor = null;
            }
        }

        // Load a recently used image set
        private async void MenuItemRecentImageSet_Click(object sender, RoutedEventArgs e)
        {
            string recentDatabasePath = (string)((MenuItem)sender).ToolTip;
            Mouse.OverrideCursor = Cursors.Wait;
            this.StatusBar.SetMessage("Loading images, please wait...");
            bool result = await this.TryOpenTemplateAndBeginLoadFoldersAsync(recentDatabasePath).ConfigureAwait(true);
            if (result == false)
            {
                this.State.MostRecentImageSets.TryRemove(recentDatabasePath);
                this.RecentFileSets_Refresh();
            }
            this.StatusBar.SetMessage("Image set is now loaded.");
            Mouse.OverrideCursor = null;
        }

        // Add Images to Image Set 
        private void MenuItemAddImagesToImageSet_Click(object sender, RoutedEventArgs e)
        {
            if (this.ShowFolderSelectionDialog(this.FolderPath, out string folderPath))
            {
                Mouse.OverrideCursor = Cursors.Wait;
                this.StatusBar.SetMessage("Adding images, please wait...");
                if (false == this.TryBeginImageFolderLoad(this.FolderPath, folderPath))
                {
                    this.StatusBar.SetMessage("Aborted. Images were not added to the image set.");
                }
                this.StatusBar.SetMessage("Images added to the image set.");
                Mouse.OverrideCursor = null;
            }
        }
        #endregion

        #region Merging databases
        private async void MenuItemMergeDatabases_Click(object sender, RoutedEventArgs e)
        {
            if (this.State.SuppressMergeDatabasesPrompt == false)
            {
                if (Dialogs.MenuFileMergeDatabasesExplainedDialog(this) == false)
                {
                    return;
                }
            }
            // Get the location of the template, which also determines the root folder
            if (this.TryGetTemplatePath(out string templateDatabasePath) == false)
            {
                return;
            }

            // Set up progress indicators
            Mouse.OverrideCursor = Cursors.Wait;
            this.StatusBar.SetMessage("Creating a merged database, please wait...");
            IProgress<ProgressBarArguments> progress = progressHandler;
            this.BusyCancelIndicator.EnableForMerging(true);

            // Find all the .DDB files located in subfolders under the root folder (excluding backup folders)
            string startFolder = Path.GetDirectoryName(templateDatabasePath);
            List<string> allDDBFiles = new List<string>();
            FilesFolders.GetAllFilesInFoldersAndSubfoldersMatchingPattern(startFolder, "*" + Constant.File.FileDatabaseFileExtension, true, true, allDDBFiles);

            // Merge the found databases into a new (or replaced) TimelapseData_merged.ddb file located in the same folder as the template.
            // Note: .ddb files found in a Backup folder will be ignored
            ErrorsAndWarnings errorMessages = await MergeDatabases.TryMergeDatabasesAsync(templateDatabasePath, allDDBFiles, progress).ConfigureAwait(true);

            // Turn off progress indicators
            this.BusyCancelIndicator.EnableForSelection(false);
            Mouse.OverrideCursor = null;

            if (errorMessages.Errors.Count != 0 || errorMessages.Warnings.Count != 0)
            {
                // Merge databases: Show errors and/or warnings, if any.
                Dialogs.MenuFileMergeDatabasesErrorsAndWarningsDialog(this, errorMessages);
            }
            this.StatusBar.SetMessage(errorMessages.Errors.Count == 0 ? "Merged database created" : "Aborted creation of merged database");
        }
        #endregion

        #region Export/Import CSV file
        // Export data for this image set as a .csv file
        // Export data for this image set as a .csv file and preview in Excel 
        private void MenuItemExportCsv_Click(object sender, RoutedEventArgs e)
        {
            if (this.State.SuppressSelectedCsvExportPrompt == false &&
                this.DataHandler.FileDatabase.ImageSet.FileSelection != FileSelectionEnum.All)
            {
                // Export data for this image set as a.csv file, but confirm, as only a subset will be exported since a selection is active
                if (Dialogs.MenuFileExportCSVOnSelectionDialog(this) == false)
                {
                    return;
                }
            }

            // Generate the candidate file name/path 
            string csvFileName = Path.GetFileNameWithoutExtension(this.DataHandler.FileDatabase.FileName) + ".csv";

            // Get the selected filepath from the user
            if (false == Dialogs.TryGetFileFromUserUsingSaveFileDialog(
                "Export and save your data as a CSV file",
                csvFileName,
                String.Format("CSV files (*{0})|*{0}", Constant.File.CsvFileExtension),
                Constant.File.CsvFileExtension,
                out string selectedCSVFilePath))
            {
                // Abort, as file selection is cancelled
                this.StatusBar.SetMessage("Csv file export cancelled.");
                return;
            }

            if (File.Exists(selectedCSVFilePath) && new System.IO.FileInfo(selectedCSVFilePath).Attributes.HasFlag(System.IO.FileAttributes.ReadOnly))
            {
                // The file exists but its read only...
                Dialogs.FileCantOpen(GlobalReferences.MainWindow, selectedCSVFilePath, true);
                this.StatusBar.SetMessage("Csv file export cancelled.");
                return;
            }

            // Backup the csv file if it exists, as the export will overwrite it. 
            if (FileBackup.TryCreateBackup(this.FolderPath, selectedCSVFilePath))
            {
                this.StatusBar.SetMessage("Backup of csv file made.");
            }
            else
            {
                this.StatusBar.SetMessage("No csv file backup was made.");
            }

            try
            {
                if (false == CsvReaderWriter.ExportToCsv(this.DataHandler.FileDatabase, selectedCSVFilePath, this.excludeDateTimeAndUTCOffsetWhenExporting))
                {
                    Dialogs.FileCantOpen(GlobalReferences.MainWindow, selectedCSVFilePath, true);
                    return;
                }
            }
            catch (Exception exception)
            {
                // Can't write the spreadsheet file
                Dialogs.MenuFileCantWriteSpreadsheetFileDialog(this, selectedCSVFilePath, exception.GetType().FullName, exception.Message);
                return;
            }

            MenuItem mi = (MenuItem)sender;
            if (mi == this.MenuItemExportAsCsvAndPreview)
            {
                // Show the file in excel
                // Create a process that will try to show the file
                ProcessStartInfo processStartInfo = new ProcessStartInfo
                {
                    UseShellExecute = true,
                    RedirectStandardOutput = false,
                    FileName = selectedCSVFilePath
                };
                if (ProcessExecution.TryProcessStart(processStartInfo) == false)
                {
                    // Can't open excel
                    Dialogs.MenuFileCantOpenExcelDialog(this, selectedCSVFilePath);
                    return;
                }
            }
            else if (this.State.SuppressCsvExportDialog == false)
            {
                Dialogs.MenuFileCSVDataExportedDialog(this, selectedCSVFilePath);
            }
            this.StatusBar.SetMessage("Data exported to " + selectedCSVFilePath);
        }

        // Import data from a CSV file. Display instructions and error messages as needed.
        private async void MenuItemImportFromCsv_Click(object sender, RoutedEventArgs e)
        {
            if (this.State.SuppressCsvImportPrompt == false)
            {
                // Tell the user how importing CSV files work. Give them the opportunity to abort.
                if (Dialogs.MenuFileHowImportingCSVWorksDialog(this) == false)
                {
                    return;
                }
            }

            string csvFileName = Path.GetFileNameWithoutExtension(this.DataHandler.FileDatabase.FileName) + Constant.File.CsvFileExtension;
            if (Dialogs.TryGetFileFromUserUsingOpenFileDialog(
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
                    // Can't import CSV File
                    Dialogs.MenuFileCantImportCSVFileDialog(this, Path.GetFileName(csvFilePath), resultAndImportErrors.Item2);
                }
                else
                {
                    // Importing done.
                    Dialogs.MenuFileCSVFileImportedDialog(this, Path.GetFileName(csvFilePath));

                    // Reload the data
                    this.BusyCancelIndicator.IsBusy = true;
                    await this.FilesSelectAndShowAsync().ConfigureAwait(true);
                    this.BusyCancelIndicator.IsBusy = false;
                    this.StatusBar.SetMessage("CSV file imported.");
                }
            }
            catch (Exception exception)
            {
                // Can't import the .csv file
                Dialogs.MenuFileCantImportCSVFileDialog(this, Path.GetFileName(csvFilePath), exception.Message);
            }
        }
        #endregion

        #region Import recognition data
        private async void MenuItemImportDetectionData_Click(object sender, RoutedEventArgs e)
        {
            string jsonFileName = Constant.File.RecognitionJsonDataFileName;
            if (Dialogs.TryGetFileFromUserUsingOpenFileDialog(
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
            if (result)
            {
                // Only reset these if we actually imported some detections, as otherwise nothing has changed.
                GlobalReferences.DetectionsExists = this.State.UseDetections && this.DataHandler.FileDatabase.DetectionsExists();
                await this.FilesSelectAndShowAsync().ConfigureAwait(true);
            }

            // Hide the Busy indicator
            this.BusyCancelIndicator.IsBusy = false;

            string details = ComposeFolderDetails(foldersInDBListButNotInJSon, foldersInJsonButNotInDB, foldersInBoth);
            if (result == false)
            {
                // No matching folders in the DB and the detector
                Dialogs.MenuFileRecognitionDataNotImportedDialog(this, details);
            }
            else if (foldersInDBListButNotInJSon.Count > 0)
            {
                // Some folders missing - show which folder paths in the DB are not in the detector
                Dialogs.MenuFileRecognitionDataImportedOnlyForSomeFoldersDialog(this, details);
            }
            else
            {
                // Detections successfully imported message
                Dialogs.MenuFileDetectionsSuccessfulyImportedDialog(this, details);
            }
        }

        // Return a string that will be included in the message box invoked above that details the match (or mismatch) between the image set folder and recognition data folders
        private static string ComposeFolderDetails(List<string> foldersInDBListButNotInJSon, List<string> foldersInJsonButNotInDB, List<string> foldersInBoth)
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
        #endregion

        #region Export the current image or video _file
        private void MenuItemExportThisImage_Click(object sender, RoutedEventArgs e)
        {
            if (!this.DataHandler.ImageCache.Current.IsDisplayable(this.FolderPath))
            {
                // Can't export the currently displayed image as a file
                Dialogs.MenuFileCantExportCurrentImageDialog(this);
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
                        TracePrint.PrintMessage(String.Format("Copy of '{0}' to '{1}' failed. {2}", sourceFile, destFileName, exception.ToString()));
                        this.StatusBar.SetMessage(String.Format("Could not copy '{0}' for some reason.", sourceFile));
                    }
                }
            }
        }

        private void MenuItemExportAllSelectedImages_Click(object sender, RoutedEventArgs e)
        {
            ExportAllSelectedFiles exportAllSelectedFiles = new ExportAllSelectedFiles(this, this.DataHandler.FileDatabase);
            {
                exportAllSelectedFiles.ShowDialog();
            }
        }
        #endregion

        #region Rename the data file
        private void MenuItemRenameFileDatabaseFile_Click(object sender, RoutedEventArgs e)
        {
            RenameFileDatabaseFile renameFileDatabase = new RenameFileDatabaseFile(this.DataHandler.FileDatabase.FileName, this)
            {
                Owner = this
            };
            if (true == renameFileDatabase.ShowDialog())
            {
                this.DataHandler.FileDatabase.RenameFile(renameFileDatabase.NewFilename);
                this.StatusBar.SetMessage("Database file renamed");
            }
            else
            {
                this.StatusBar.SetMessage("Database file could not berenamed");
            }
        }
        #endregion

        #region Close image set / Exit Timelapse
        // Close Image Set
        public void MenuFileCloseImageSet_Click(object sender, RoutedEventArgs e)
        {
            this.CloseImageSet();
            this.StatusBar.SetMessage("Image set closed");
        }

        // Exit Timelapse
        private void MenuItemExit_Click(object sender, RoutedEventArgs e)
        {
            this.CloseImageSet();
            this.Close();
            Application.Current.Shutdown();
        }
        #endregion
    }
}
