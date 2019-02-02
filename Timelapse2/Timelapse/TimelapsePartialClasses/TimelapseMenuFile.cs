using System;
using System.Collections.Generic;
using System.ComponentModel;
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
        private void MenuItemLoadImages_Click(object sender, RoutedEventArgs e)
        {
            if (this.TryGetTemplatePath(out string templateDatabasePath))
            {
                Mouse.OverrideCursor = Cursors.Wait;
                this.TryOpenTemplateAndBeginLoadFoldersAsync(templateDatabasePath, out BackgroundWorker backgroundWorker);
                Mouse.OverrideCursor = null;
            }
        }

        // Load a recently used image set
        private void MenuItemRecentImageSet_Click(object sender, RoutedEventArgs e)
        {
            string recentDatabasePath = (string)((MenuItem)sender).ToolTip;
            Mouse.OverrideCursor = Cursors.Wait;
            if (this.TryOpenTemplateAndBeginLoadFoldersAsync(recentDatabasePath, out BackgroundWorker backgroundWorker) == false)
            {
                this.state.MostRecentImageSets.TryRemove(recentDatabasePath);
                this.RecentFileSets_Refresh();
            }
            Mouse.OverrideCursor = null;
        }

        // Add Images to Image Set 
        private void MenuItemAddImagesToImageSet_Click(object sender, RoutedEventArgs e)
        {
            if (this.ShowFolderSelectionDialog(out IEnumerable<string> folderPaths))
            {
                Mouse.OverrideCursor = Cursors.Wait;
                this.TryBeginImageFolderLoadAsync(folderPaths, out BackgroundWorker backgroundWorker);
                Mouse.OverrideCursor = null;
            }
        }

        // Export data for this image set as a .csv file
        // Export data for this image set as a .csv file and preview in Excel 
        private void MenuItemExportCsv_Click(object sender, RoutedEventArgs e)
        {
            if (this.state.SuppressSelectedCsvExportPrompt == false &&
                this.dataHandler.FileDatabase.ImageSet.FileSelection != FileSelectionEnum.All)
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
                    this.state.SuppressSelectedCsvExportPrompt = messageBox.DontShowAgain.IsChecked.Value;
                }
            }

            // Generate the file names/path
            string csvFileName = Path.GetFileNameWithoutExtension(this.dataHandler.FileDatabase.FileName) + ".csv";
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

            CsvReaderWriter csvWriter = new CsvReaderWriter();
            try
            {
                CsvReaderWriter.ExportToCsv(this.dataHandler.FileDatabase, csvFilePath, this.excludeDateTimeAndUTCOffsetWhenExporting);
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
                // Show the file in excel
                // Create a process that will try to show the file
                Process process = new Process();

                process.StartInfo.UseShellExecute = true;
                process.StartInfo.RedirectStandardOutput = false;
                process.StartInfo.FileName = csvFilePath;
                process.Start();
            }
            else if (this.state.SuppressCsvExportDialog == false)
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
                    this.state.SuppressCsvExportDialog = csvExportInformation.DontShowAgain.IsChecked.Value;
                }
            }
            this.StatusBar.SetMessage("Data exported to " + csvFileName);
        }

        // Import data from a .csv file
        private void MenuItemImportFromCsv_Click(object sender, RoutedEventArgs e)
        {
            if (this.state.SuppressCsvImportPrompt == false)
            {
                MessageBox messageBox = new MessageBox("How importing .csv data works", this, MessageBoxButton.OKCancel);
                messageBox.Message.What = "Importing data from a .csv (comma separated value) file follows the rules below." + Environment.NewLine;
                messageBox.Message.What += "Otherwise your Timelapse data may become corrupted.";
                messageBox.Message.Reason = "Timelapse requires the .csv file follow a specific format and processes  its data a specific way.";
                messageBox.Message.Solution = "\u2022 Only modify and import a .csv file previously exported by Timelapse." + Environment.NewLine;
                messageBox.Message.Solution += "\u2022 Do not change Folder, RelativePath, or File as those fields uniquely identify a file" + Environment.NewLine;
                messageBox.Message.Solution += "\u2022 Do not change Date or Time as those columns are ignored (change DateTime instead, if it exists)" + Environment.NewLine;
                messageBox.Message.Solution += "\u2022 Do not change column names" + Environment.NewLine;
                messageBox.Message.Solution += "\u2022 Do not add or delete rows (those changes will be ignored)" + Environment.NewLine;
                messageBox.Message.Solution += "\u2022 Restrict modifications as follows:" + Environment.NewLine;
                messageBox.Message.Solution += String.Format("    \u2022 If it's in the csv file, DateTime must be in '{0}' format{1}", Constant.Time.DateTimeDatabaseFormat, Environment.NewLine);
                messageBox.Message.Solution += String.Format("    \u2022 If it's in the csv file, UtcOffset must be a floating point number between {0} and {1}, inclusive{2}", DateTimeHandler.ToDatabaseUtcOffsetString(Constant.Time.MinimumUtcOffset), DateTimeHandler.ToDatabaseUtcOffsetString(Constant.Time.MinimumUtcOffset), Environment.NewLine);
                messageBox.Message.Solution += "    \u2022 Counter data must be zero or a positive integer" + Environment.NewLine;
                messageBox.Message.Solution += "    \u2022 Flag data must be 'true' or 'false'" + Environment.NewLine;
                messageBox.Message.Solution += "    \u2022 FixedChoice data must be a string that exactly matches one of the FixedChoice menu options, or empty." + Environment.NewLine;
                messageBox.Message.Solution += "    \u2022 Note data to any string, including empty.";
                messageBox.Message.Result = "Timelapse will create a backup .ddb file in the Backups folder, and will then try its best.";
                messageBox.Message.Hint = "\u2022 After you import, check your data. If it is not what you expect, restore your data by using that backup file." + Environment.NewLine;
                messageBox.Message.Hint += "\u2022 If you check don't show this message this dialog can be turned back on via the Options menu.";
                messageBox.Message.Icon = MessageBoxImage.Warning;
                messageBox.DontShowAgain.Visibility = Visibility.Visible;

                bool? proceeed = messageBox.ShowDialog();
                if (proceeed != true)
                {
                    return;
                }

                if (messageBox.DontShowAgain.IsChecked.HasValue)
                {
                    this.state.SuppressCsvImportPrompt = messageBox.DontShowAgain.IsChecked.Value;
                }
            }

            string csvFileName = Path.GetFileNameWithoutExtension(this.dataHandler.FileDatabase.FileName) + Constant.File.CsvFileExtension;
            if (Utilities.TryGetFileFromUser(
                                 "Select a .csv file to merge into the current image set",
                                 Path.Combine(this.dataHandler.FileDatabase.FolderPath, csvFileName),
                                 String.Format("Comma separated value files (*{0})|*{0}", Constant.File.CsvFileExtension), 
                                 Constant.File.CsvFileExtension,
                                 out string csvFilePath) == false)
            {
                return;
            }

            // Create a backup database file
            if (FileBackup.TryCreateBackup(this.FolderPath, this.dataHandler.FileDatabase.FileName))
            {
                this.StatusBar.SetMessage("Backup of data file made.");
            }
            else
            {
                this.StatusBar.SetMessage("No data file backup was made.");
            }

            CsvReaderWriter csvReader = new CsvReaderWriter();
            try
            {
                if (CsvReaderWriter.TryImportFromCsv(csvFilePath, this.dataHandler.FileDatabase, out List<string> importErrors) == false)
                {
                    MessageBox messageBox = new MessageBox("Can't import the .csv file.", this);
                    messageBox.Message.Icon = MessageBoxImage.Error;
                    messageBox.Message.Problem = String.Format("The file {0} could not be read.", csvFilePath);
                    messageBox.Message.Reason = "The .csv file is not compatible with the current image set.";
                    messageBox.Message.Solution = "Check that:" + Environment.NewLine;
                    messageBox.Message.Solution += "\u2022 The first row of the .csv file is a header line." + Environment.NewLine;
                    messageBox.Message.Solution += "\u2022 The column names in the header line match the database." + Environment.NewLine;
                    messageBox.Message.Solution += "\u2022 Choice values use the correct case." + Environment.NewLine;
                    messageBox.Message.Solution += "\u2022 Counter values are numbers." + Environment.NewLine;
                    messageBox.Message.Solution += "\u2022 Flag values are either 'true' or 'false'.";
                    messageBox.Message.Result = "Either no data was imported or invalid parts of the .csv were skipped.";
                    messageBox.Message.Hint = "The errors encountered were:";
                    foreach (string importError in importErrors)
                    {
                        messageBox.Message.Hint += "\u2022 " + importError;
                    }
                    messageBox.ShowDialog();
                }
            }
            catch (Exception exception)
            {
                MessageBox messageBox = new MessageBox("Can't import the .csv file.", this);
                messageBox.Message.Icon = MessageBoxImage.Error;
                messageBox.Message.Problem = String.Format("The file {0} could not be opened.", csvFilePath);
                messageBox.Message.Reason = "Most likely the file is open in another program.";
                messageBox.Message.Solution = "If the file is open in another program, close it.";
                messageBox.Message.Result = String.Format("{0}: {1}", exception.GetType().FullName, exception.Message);
                messageBox.Message.Hint = "Is the file open in Excel?";
                messageBox.ShowDialog();
            }
            // Reload the data table
            this.FilesSelectAndShow(false);
            this.StatusBar.SetMessage(".csv file imported.");
        }

        // Export the current image or video _file
        private void MenuItemExportThisImage_Click(object sender, RoutedEventArgs e)
        {
            if (!this.dataHandler.ImageCache.Current.IsDisplayable())
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
            string sourceFile = this.dataHandler.ImageCache.Current.FileName;

            // Set up a Folder Browser with some instructions
            SaveFileDialog dialog = new SaveFileDialog()
            {
                Title = "Export a copy of the currently displayed file",
                Filter = String.Format("*{0}|*{0}", Path.GetExtension(this.dataHandler.ImageCache.Current.FileName)),
                FileName = sourceFile,
                OverwritePrompt = true
            };

            // Display the Folder Browser dialog
            DialogResult result = dialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                // Set the source and destination file names, including the complete path
                string sourcePath = this.dataHandler.ImageCache.Current.GetFilePath(this.FolderPath);
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
                    Utilities.PrintFailure(String.Format("Copy of '{0}' to '{1}' failed. {2}", sourceFile, destFileName, exception.ToString()));
                    this.StatusBar.SetMessage(String.Format("Copy failed with {0} in MenuItemExportThisImage_Click.", exception.GetType().Name));
                }
            }
        }

        // Rename the data file
        private void MenuItemRenameFileDatabaseFile_Click(object sender, RoutedEventArgs e)
        {
            RenameFileDatabaseFile renameFileDatabase = new RenameFileDatabaseFile(this.dataHandler.FileDatabase.FileName, this)
            {
                Owner = this
            };
            bool? result = renameFileDatabase.ShowDialog();
            if (result == true)
            {
                this.dataHandler.FileDatabase.RenameFile(renameFileDatabase.NewFilename);
            }
        }

        // Close Image Set
        private void MenuFileCloseImageSet_Click(object sender, RoutedEventArgs e)
        {
            // if we are actually viewing any files
            if (this.IsFileDatabaseAvailable())
            {
                // persist image set properties if an image set has been opened
                if (this.dataHandler.FileDatabase.CurrentlySelectedFileCount > 0)
                {
                    this.Window_Closing(null, null);
                    // revert to custom selections to all 
                    if (this.dataHandler.FileDatabase.ImageSet.FileSelection == FileSelectionEnum.Custom)
                    {
                        this.dataHandler.FileDatabase.ImageSet.FileSelection = FileSelectionEnum.All;
                    }
                    if (this.dataHandler.ImageCache != null && this.dataHandler.ImageCache.Current != null)
                    {
                        this.dataHandler.FileDatabase.ImageSet.MostRecentFileID = this.dataHandler.ImageCache.Current.ID;
                    }

                    // write image set properties to the database
                    this.dataHandler.FileDatabase.SyncImageSetToDatabase();

                    // ensure custom filter operator is synchronized in state for writing to user's registry
                    this.state.CustomSelectionTermCombiningOperator = this.dataHandler.FileDatabase.CustomSelection.TermCombiningOperator;
                }
                // discard the image set 
                if (this.dataHandler.ImageCache != null)
                {
                    this.dataHandler.ImageCache.Dispose();
                }
                if (this.dataHandler != null)
                {
                    this.dataHandler.Dispose();
                }
                this.dataHandler = null;
                this.templateDatabase = null;
                this.DataEntryControlPanel.IsVisible = false;
            }
            // Clear the data grid
            this.DataGrid.ItemsSource = null;

            // Reset the UX 
            this.state.Reset();
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
    }
}
