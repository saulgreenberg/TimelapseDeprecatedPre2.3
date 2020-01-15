using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Timelapse.Controls;
using Timelapse.Database;
using Timelapse.Dialog;
using Timelapse.Enums;
using Timelapse.Images;
using Timelapse.ImageSetLoadingPipeline;
using Timelapse.QuickPaste;
using Timelapse.Util;
using MessageBox = Timelapse.Dialog.MessageBox;

// Image Set Loading
namespace Timelapse
{
    public partial class TimelapseWindow : Window, IDisposable
    {
        // Prompt user to select a template.
        private bool TryGetTemplatePath(out string templateDatabasePath)
        {
            // Default the template selection dialog to the most recently opened database
            this.State.MostRecentImageSets.TryGetMostRecent(out string defaultTemplateDatabasePath);
            if (Utilities.TryGetFileFromUser(
                "Select a TimelapseTemplate.tdb file, which should be located in the root folder containing your images and videos",
                                             defaultTemplateDatabasePath,
                                             String.Format("Template files (*{0})|*{0}", Constant.File.TemplateDatabaseFileExtension),
                                             Constant.File.TemplateDatabaseFileExtension,
                                             out templateDatabasePath) == false)
            {
                return false;
            }

            string templateDatabaseDirectoryPath = Path.GetDirectoryName(templateDatabasePath);
            if (String.IsNullOrEmpty(templateDatabaseDirectoryPath))
            {
                return false;
            }
            return true;
        }

        // Load the specified database template and then the associated images. 
        // templateDatabasePath is the Fully qualified path to the template database file.
        // Returns true only if both the template and image database file are loaded (regardless of whether any images were loaded) , false otherwise
        private bool TryOpenTemplateAndBeginLoadFoldersAsync(string templateDatabasePath)
        {
            // Try to create or open the template database
            // First, check the file path length and notify the user the template couldn't be loaded because its path is too long
            if (Utilities.IsPathLengthTooLong(templateDatabasePath))
            {
                Dialogs.TemplatePathTooLongDialog(templateDatabasePath, this);
                return false;
            }
            // Second, check to see if we can actually open it.
            if (!TemplateDatabase.TryCreateOrOpen(templateDatabasePath, out this.templateDatabase))
            {
                // notify the user the template couldn't be loaded rather than silently doing nothing
                Dialogs.TemplateCouldNotBeLoadedDialog(templateDatabasePath, this);
                return false;
            }

            // The .tdb templateDatabase should now be loaded
            // Try to get the image database file path 
            // importImages will be true if its a new image database file, (meaning we should later ask the user to try to import some images)
            if (this.TrySelectDatabaseFile(templateDatabasePath, out string fileDatabaseFilePath, out bool importImages) == false)
            {
                // No image database file was selected
                return false;
            }

            // Check the file path length of the .ddb file and notify the user the ddb couldn't be loaded because its path is too long
            if (Utilities.IsPathLengthTooLong(fileDatabaseFilePath))
            {
                Dialogs.DatabasePathTooLongDialog(fileDatabaseFilePath, this);
                return false;
            }

            // Check the expected file path length of the backup files, and warn the user if backups may not be made because thier path is too long
            if (Utilities.IsBackupPathLengthTooLong(templateDatabasePath) || Utilities.IsBackupPathLengthTooLong(fileDatabaseFilePath))
            {
                Dialogs.BackupPathTooLongDialog(this);
            }

            // Before fully loading an existing image database, 
            // - upgrade the template tables if needed for backwards compatability (done automatically)
            // - compare the controls in the .tdb and .ddb template tables to see if there are any added or missing controls 
            TemplateSyncResults templateSyncResults = new Database.TemplateSyncResults();
            using (FileDatabase fileDB = FileDatabase.UpgradeDatabasesAndCompareTemplates(fileDatabaseFilePath, this.templateDatabase, templateSyncResults))
            {
                // A file database was available to open
                if (fileDB != null)
                {
                    if (templateSyncResults.ControlSynchronizationErrors.Count > 0 || (templateSyncResults.ControlSynchronizationWarnings.Count > 0 && templateSyncResults.SyncRequiredAsDataLabelsDiffer == false))
                    {
                        // There are unresolvable syncronization issues. Report them now as we cannot use this template.
                        // Depending on the user response, we either abort Timelapse or use the template found in the ddb file
                        Dialog.TemplateSynchronization templatesNotCompatibleDialog;

                        templatesNotCompatibleDialog = new Dialog.TemplateSynchronization(templateSyncResults.ControlSynchronizationErrors, templateSyncResults.ControlSynchronizationWarnings, this);
                        bool? result = templatesNotCompatibleDialog.ShowDialog();
                        if (result == false)
                        {
                            // user indicates exiting rather than continuing.
                            Application.Current.Shutdown();
                            return false;
                        }
                        else
                        {
                            templateSyncResults.UseTemplateDBTemplate = templatesNotCompatibleDialog.UseNewTemplate;
                            templateSyncResults.SyncRequiredAsChoiceMenusDiffer = templateSyncResults.ControlSynchronizationWarnings.Count > 0 ? true : false;
                        }
                    }
                    else if (templateSyncResults.SyncRequiredAsDataLabelsDiffer)
                    {
                        // If there are any new or missing columns, report them now
                        // Depending on the user response, set the useTemplateDBTemplate to signal whether we should: 
                        // - update the template and image data columns in the image database 
                        // - use the old template
                        TemplateChangedAndUpdate templateChangedAndUpdate = new TemplateChangedAndUpdate(templateSyncResults, this);
                        bool? result1 = templateChangedAndUpdate.ShowDialog();
                        templateSyncResults.UseTemplateDBTemplate = (result1 == true) ? true : false;
                    }
                    else if (templateSyncResults.SyncRequiredAsNonCriticalFieldsDiffer)
                    {
                        // Non critical differences in template, so these don't need reporting
                        templateSyncResults.UseTemplateDBTemplate = true;
                    }
                }
            }

            // At this point:
            // - for backwards compatability, all old databases will have been updated (if needed) to the current version standard
            // - we should have a valid template and image database loaded
            // - we know if the user wants to use the old or the new template
            // So lets load the database for real. The useTemplateDBTemplate signals whether to use the template stored in the DDB, or to use the TDB template.
            FileDatabase fileDatabase = FileDatabase.CreateOrOpen(fileDatabaseFilePath, this.templateDatabase, this.State.CustomSelectionTermCombiningOperator, templateSyncResults);

            // The next test is to test and syncronize (if needed) the default values stored in the fileDB table schema to those stored in the template
            Dictionary<string, string> columndefaultdict = fileDatabase.SchemaGetColumnsAndDefaultValues(Constant.DBTables.FileData);
            char[] quote = { '\'' };

            foreach (KeyValuePair<string, string> pair in columndefaultdict)
            {
                ControlRow row = this.templateDatabase.GetControlFromTemplateTable(pair.Key);
                if (row != null && pair.Value.Trim(quote) != row.DefaultValue)
                {
                    // If even one default is different between the schema default and the template default, update the entire file table.
                    fileDatabase.UpgradeFileDBSchemaDefaultsFromTemplate();
                    break;
                }
            }

            // Check to see if the root folder stored in the database is the same as the actual root folder. If not, ask the user if it should be changed.
            this.CheckAndCorrectRootFolder(fileDatabase);

            // Check to see if there are any missing folders as specified by the relative paths. For those missing, ask the user to try to locate those folders.
            this.CheckAndCorrectForMissingFolders(fileDatabase);

            // Generate and render the data entry controls, regardless of whether there are actually any files in the files database.
            this.dataHandler = new DataEntryHandler(fileDatabase);
            this.DataEntryControls.CreateControls(fileDatabase, this.dataHandler);
            this.SetUserInterfaceCallbacks();
            this.MarkableCanvas.DataEntryControls = this.DataEntryControls; // so the markable canvas can access the controls
            this.dataHandler.ClickableImagesGrid = this.MarkableCanvas.ClickableImagesGrid;
            this.dataHandler.MarkableCanvas = this.MarkableCanvas;

            this.Title = Constant.Defaults.MainWindowBaseTitle + " (" + Path.GetFileName(fileDatabase.FilePath) + ")";
            this.State.MostRecentImageSets.SetMostRecent(templateDatabasePath);
            this.RecentFileSets_Refresh();

            // Record the version number of the currently executing version of Timelapse only if its greater than the one already stored in the ImageSet Table.
            // This will indicate the latest timelapse version that is compatable with the database structure. 
            string currentVersionNumberAsString = VersionClient.GetTimelapseCurrentVersionNumber().ToString();
            if (VersionClient.IsVersion1GreaterThanVersion2(currentVersionNumberAsString, this.dataHandler.FileDatabase.ImageSet.VersionCompatability))
            {
                this.dataHandler.FileDatabase.ImageSet.VersionCompatability = currentVersionNumberAsString;
                this.dataHandler.FileDatabase.UpdateSyncImageSetToDatabase();
            }

            // If this is a new image database, try to load images (if any) from the folder...  
            if (importImages)
            {
                this.TryBeginImageFolderLoadAsync(this.FolderPath, this.FolderPath);
            }
            else
            {
                this.OnFolderLoadingCompleteAsync(false);
            }
            return true;
        }

        // Get the root folder name from the database, and check to see if its the same as the actual root folder.
        // If not, ask the user if he/she wants to update the database.
        public void CheckAndCorrectRootFolder(FileDatabase fileDatabase)
        {
            // Check the arguments for null 
            if (fileDatabase == null)
            {
                // this should not happen
                // System.Diagnostics.Debug.Print("The fielDatabase was null and it shouldn't be");
                TraceDebug.PrintStackTrace(1);
                // No-op
                return;
            }
            List<object> allRootFolderPaths = fileDatabase.GetDistinctValuesInColumn(Constant.DBTables.FileData, Constant.DatabaseColumn.Folder);
            if (allRootFolderPaths.Count < 1)
            {
                // System.Diagnostics.Debug.Print("Checking the root folder name in the database, but no entries were found. Perhaps the database is empty?");
                return;
            }

            // retrieve and compare the db and actual root folder path names. While there really should be only one entry in the allRootFolderPaths,
            // we still do a check in case there is more than one. If even one entry doesn't match, we use that entry to ask the user if he/she
            // wants to update the root folder to match the actual location of the root folder containing the template, data and image files.
            string actualRootFolderName = fileDatabase.FolderPath.Split(Path.DirectorySeparatorChar).Last();
            foreach (string databaseRootFolderName in allRootFolderPaths)
            {
                if (databaseRootFolderName.Equals(actualRootFolderName))
                {
                    continue;
                }
                else
                {
                    // We have at least one entry where there is a mismatch between the actual root folder and the stored root folder
                    // Consequently, ask the user if he/she wants to update the db entry 
                    Dialog.UpdateRootFolder renameRootFolderDialog;
                    renameRootFolderDialog = new Dialog.UpdateRootFolder(this, databaseRootFolderName, actualRootFolderName);
                    bool? result = renameRootFolderDialog.ShowDialog();
                    if (result == true)
                    {
                        ColumnTuple columnToUpdate = new ColumnTuple(Constant.DatabaseColumn.Folder, actualRootFolderName);
                        fileDatabase.UpdateFiles(columnToUpdate);
                    }
                    return;
                }
            }
        }

        // Get all the distinct relative folder paths and check to see if the folder exists.
        // If not, ask the user to try to locate each missing folder.
        public void CheckAndCorrectForMissingFolders(FileDatabase fileDatabase)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(fileDatabase, nameof(fileDatabase));

            List<object> allRelativePaths = fileDatabase.GetDistinctValuesInColumn(Constant.DBTables.FileData, Constant.DatabaseColumn.RelativePath);
            List<string> missingRelativePaths = new List<string>();
            foreach (string relativePath in allRelativePaths)
            {
                string path = Path.Combine(fileDatabase.FolderPath, relativePath);
                if (!Directory.Exists(path))
                {
                    missingRelativePaths.Add(relativePath);
                }
            }

            Cursor cursor = Mouse.OverrideCursor;
            // If there are multiple missing folders, it will generate multiple dialog boxes. Thus we explain what is going on.
            if (missingRelativePaths?.Count > 1)
            {
                Mouse.OverrideCursor = null;
                MessageBox messageBox = new MessageBox("Multiple image folders cannot be found", this, MessageBoxButton.OKCancel);
                messageBox.Message.Problem = "Timelapse could not locate the following image folders" + Environment.NewLine;
                foreach (string relativePath in missingRelativePaths)
                {
                    messageBox.Message.Problem += "\u2022 " + relativePath + Environment.NewLine;
                }
                messageBox.Message.Solution = "Selecting OK will raise additional dialog boxes, each asking you to locate a particular missing folder" + Environment.NewLine;
                messageBox.Message.Solution += "Selecting Cancel will still display the image's data, along with a 'missing' image placeholder";
                messageBox.Message.Icon = MessageBoxImage.Question;
                if (messageBox.ShowDialog() == false)
                {
                    Mouse.OverrideCursor = cursor;
                    return;
                }
                Mouse.OverrideCursor = cursor;
            }

            cursor = Mouse.OverrideCursor;
            Mouse.OverrideCursor = null;
            // Raise a dialog box for each image asking the user to locate the missing folder
            foreach (string relativePath in missingRelativePaths)
            {

                Dialog.FindMissingImageFolder findMissingImageFolderDialog;
                findMissingImageFolderDialog = new Dialog.FindMissingImageFolder(this, fileDatabase.FolderPath, relativePath);
                bool? result = findMissingImageFolderDialog.ShowDialog();
                if (result == true)
                {
                    ColumnTuple columnToUpdate = new ColumnTuple(Constant.DatabaseColumn.RelativePath, findMissingImageFolderDialog.NewFolderName);
                    ColumnTuplesWithWhere columnToUpdateWithWhere = new ColumnTuplesWithWhere(columnToUpdate, relativePath);
                    fileDatabase.UpdateFiles(columnToUpdateWithWhere);
                }
                else if (findMissingImageFolderDialog.CancelAll)
                {
                    // stop trying to locate missing folders
                    Mouse.OverrideCursor = cursor;
                    break;
                }
            }
            Mouse.OverrideCursor = cursor;
        }
        // END

        [HandleProcessCorruptedStateExceptions]
        // out parameters can't be used in anonymous methods, so a separate pointer to backgroundWorker is required for return to the caller
        private bool TryBeginImageFolderLoadAsync(string imageSetFolderPath, string selectedFolderPath)
        {
            List<FileInfo> filesToAdd = new List<FileInfo>();
            // Generate FileInfo list for every single image / video file in the folder path (including subfolders). These become the files to add to the database
            // PERFORMANCE - takes modest but noticable time to do if there are a huge number of files. 
            // TO DO: PUT THIS IN THE SHOW PROGRESS LOOP
            Util.FilesFoldersAndPaths.GetAllImageAndVideoFilesInFolderAndSubfolders(selectedFolderPath, filesToAdd);

            if (filesToAdd.Count == 0)
            {
                // No images were found in the root folder or subfolders, so there is nothing to do
                MessageBox messageBox = new MessageBox("No images or videos were found", this, MessageBoxButton.OK);
                messageBox.Message.Problem = "No images or videos were found in this folder or its subfolders:" + Environment.NewLine;
                messageBox.Message.Problem += "\u2022 " + selectedFolderPath + Environment.NewLine;
                messageBox.Message.Reason = "Neither the folder nor its sub-folders contain:" + Environment.NewLine;
                messageBox.Message.Reason += "\u2022 image files (ending in '.jpg') " + Environment.NewLine;
                messageBox.Message.Reason += "\u2022 video files (ending in '.avi or .mp4')";
                messageBox.Message.Solution = "Timelapse aborted the load operation." + Environment.NewLine;
                messageBox.Message.Hint = "Locate your template in a folder containing (or whose subfolders contain) image or video files ." + Environment.NewLine;
                messageBox.Message.Icon = MessageBoxImage.Exclamation;
                messageBox.ShowDialog();
                return false;
            }

            // Load all the files (matching allowable file types) found in the folder
            // Show image previews of the files to the user as they are individually loaded
            // Generally, Background worker examines each image, and extracts data from it which it stores in a data structure, which in turn is used to compose bulk database inserts. 
            // PERFORMANCE This is likely the place that the best performance increases can be gained by transforming its foreach loop into a Parallel.ForEach. 
            // Indeed, you will see commented out remnants of a Parallel.ForEach in the code where this was done, but using it introduced errors. 
#pragma warning disable CA2000 // Dispose objects before losing scope. Reason: Not required as Dispose on BackgroundWorker doesn't do anything
            BackgroundWorker backgroundWorker = new BackgroundWorker()
            {
                WorkerReportsProgress = true
            };
#pragma warning restore CA2000 // Dispose objects before losing scope

            // folderLoadProgress contains data to be used to provide feedback on the folder loading state
            FolderLoadProgress folderLoadProgress = new FolderLoadProgress(filesToAdd.Count)
            {
                TotalPasses = 2,
                CurrentPass = 1
            };

            backgroundWorker.DoWork += (ow, ea) =>
            {
                ImageSetLoader loader = new ImageSetLoader(imageSetFolderPath, filesToAdd, this.dataHandler, this.State);

                backgroundWorker.ReportProgress(0, folderLoadProgress);

                // If the DoWork delegate is async, this is considered finished before the actual image set is loaded.
                // Instead of an async DoWork and an await here, wait for the loading to finish.
                loader.LoadAsync(backgroundWorker.ReportProgress, folderLoadProgress, 500).Wait();
            };

            backgroundWorker.ProgressChanged += (o, ea) =>
            {
                // this gets called on the UI thread
                this.ImageSetPane.IsActive = true;
                if (folderLoadProgress.CurrentPass == 1 && folderLoadProgress.CurrentFile == 0)
                {
                    // skip the 0th file of the 1st pass, as there is not really much of interest to show
                    return;
                }
                string message = (folderLoadProgress.TotalPasses > 1) ? String.Format("Pass {0}/{1}{2}", folderLoadProgress.CurrentPass, folderLoadProgress.TotalPasses, Environment.NewLine) : String.Empty;
                if (folderLoadProgress.CurrentPass == 1 && folderLoadProgress.CurrentFile == folderLoadProgress.TotalFiles)
                {
                    message = String.Format("{0}Finalizing analysis of {1} files - could take several minutes ", message, folderLoadProgress.TotalFiles);
                }

                else
                {
                    string what = (folderLoadProgress.CurrentPass == 1) ? "Analyzing file" : "Adding files to database";
                    message = (folderLoadProgress.CurrentPass == 2 && folderLoadProgress.CurrentFile == 0)
                        ? String.Format("{0}{1} ...", message, what)
                        : String.Format("{0}{1} {2} of {3} ({4})", message, what, folderLoadProgress.CurrentFile, folderLoadProgress.TotalFiles, folderLoadProgress.CurrentFileName);
                }

                this.UpdateFolderLoadProgress(this.BusyCancelIndicator, folderLoadProgress.BitmapSource, ea.ProgressPercentage, message, false, false);
                this.StatusBar.SetCurrentFile(folderLoadProgress.CurrentFile);
                this.StatusBar.SetCount(folderLoadProgress.TotalFiles);
            };

            backgroundWorker.RunWorkerCompleted += async (o, ea) =>
            {
                // BackgroundWorker aborts execution on an exception and transfers it to completion for handling
                // If something went wrong rethrow the error so the user knows there's a problem.  Otherwise what would happen is either 
                //  1) some or all of the folder load file scan progress displays but no files get added to the database as the insert is skipped
                //  2) only some of the files get inserted and the rest are silently dropped
                // Both of these outcomes result in quite poor user experience and are best avoided.
                if (ea.Error != null)
                {
                    throw new FileLoadException("Folder loading failed unexpectedly.  See inner exception for details.", ea.Error);
                }

                // Show the file slider
                this.FileNavigatorSlider.Visibility = Visibility.Visible;

                this.OnFolderLoadingCompleteAsync(true);

                // Do some final things
                // Note that if the magnifier is enabled, we temporarily hide so it doesn't appear in the background 
                bool saveMagnifierState = this.MarkableCanvas.MagnifyingGlassEnabled;
                this.MarkableCanvas.MagnifyingGlassEnabled = false;
                this.StatusBar.SetMessage(folderLoadProgress.TotalFiles + " files are now loaded");
                this.MarkableCanvas.MagnifyingGlassEnabled = saveMagnifierState;

                // If we want to import old data from the ImageData.xml file, we can do it here...
                // Check to see if there is an ImageData.xml file in here. If there is, ask the user
                // if we want to load the data from that...
                if (File.Exists(Path.Combine(this.FolderPath, Constant.File.XmlDataFileName)))
                {
                    ImportImageSetXmlFile importLegacyXmlDialog = new ImportImageSetXmlFile(this);
                    bool? dialogResult = importLegacyXmlDialog.ShowDialog();
                    if (dialogResult == true)
                    {
                        ImageDataXml.Read(Path.Combine(this.FolderPath, Constant.File.XmlDataFileName), this.dataHandler.FileDatabase);
                        await this.FilesSelectAndShowAsync(this.dataHandler.FileDatabase.ImageSet.MostRecentFileID, this.dataHandler.FileDatabase.ImageSet.FileSelection).ConfigureAwait(true); // to regenerate the controls and markers for this image
                    }
                }
                this.BusyCancelIndicator.IsBusy = false; // Hide the busy indicator
            };

            // Set up the user interface to show feedback
            this.BusyCancelIndicator.IsBusy = true; // Display the busy indicator

            this.FileNavigatorSlider.Visibility = Visibility.Collapsed;
            // First feedback message
            this.UpdateFolderLoadProgress(GlobalReferences.BusyCancelIndicator, null, 0, String.Format("Initializing...{0}Analyzing and loading {1} files ", Environment.NewLine, filesToAdd.Count), false, false);
            this.StatusBar.SetMessage("Loading folders...");
            backgroundWorker.RunWorkerAsync();
            return true;
        }

        private void UpdateFolderLoadProgress(BusyCancelIndicator BusyCancelIndicator, BitmapSource bitmap, int percent, string message, bool isCancelEnabled, bool isIndeterminate)
        {
            if (bitmap != null)
            {
                this.MarkableCanvas.SetNewImage(bitmap, null);
            }

            // Check the arguments for null 
            ThrowIf.IsNullArgument(BusyCancelIndicator, nameof(BusyCancelIndicator));

            // Set it as a progressive or indeterminate bar
            BusyCancelIndicator.IsIndeterminate = isIndeterminate;

            // Set the progress bar position (only visible if determinate)
            BusyCancelIndicator.Percent = percent;

            // Update the text message
            BusyCancelIndicator.Message = message;

            // Update the cancel button to reflect the cancelEnabled argument
            BusyCancelIndicator.CancelButtonIsEnabled = isCancelEnabled;
            BusyCancelIndicator.CancelButtonText = isCancelEnabled ? "Cancel" : "Writing data...";
        }

        // Given the location path of the template,  return:
        // - true if a database file was specified
        // - databaseFilePath: the path to the data database file (or null if none was specified).
        // - importImages: true when the database file has just been created, which means images still have to be imported.
        private bool TrySelectDatabaseFile(string templateDatabasePath, out string databaseFilePath, out bool importImages)
        {
            importImages = false;

            string databaseFileName;
            string directoryPath = Path.GetDirectoryName(templateDatabasePath);
            string[] fileDatabasePaths = Directory.GetFiles(directoryPath, "*.ddb");
            if (fileDatabasePaths.Length == 1)
            {
                databaseFileName = Path.GetFileName(fileDatabasePaths[0]); // Get the file name, excluding the path
            }
            else if (fileDatabasePaths.Length > 1)
            {
                ChooseFileDatabaseFile chooseDatabaseFile = new ChooseFileDatabaseFile(fileDatabasePaths, templateDatabasePath, this);
                bool? result = chooseDatabaseFile.ShowDialog();
                if (result == true)
                {
                    databaseFileName = chooseDatabaseFile.SelectedFile;
                }
                else
                {
                    // User cancelled .ddb selection
                    databaseFilePath = null;
                    return false;
                }
            }
            else
            {
                // There are no existing .ddb files
                string templateDatabaseFileName = Path.GetFileName(templateDatabasePath);
                if (String.Equals(templateDatabaseFileName, Constant.File.DefaultTemplateDatabaseFileName, StringComparison.OrdinalIgnoreCase))
                {
                    databaseFileName = Constant.File.DefaultFileDatabaseFileName;
                }
                else
                {
                    databaseFileName = Path.GetFileNameWithoutExtension(templateDatabasePath) + Constant.File.FileDatabaseFileExtension;
                }
                importImages = true;
            }

            databaseFilePath = Path.Combine(directoryPath, databaseFileName);
            return true;
        }

        /// <summary>
        /// When folder loading has completed add callbacks, prepare the UI, set up the image set, and show the image.
        /// </summary>
        private async void OnFolderLoadingCompleteAsync(bool filesJustAdded)
        {
            this.ShowSortFeedback(true);

            // Show the image, hide the load button, and make the feedback panels visible
            this.ImageSetPane.IsActive = true;
            this.FileNavigatorSlider_EnableOrDisableValueChangedCallback(false);
            this.MarkableCanvas.Focus(); // We start with this having the focus so it can interpret keyboard shortcuts if needed. 

            // Adjust the visibility of the CopyPreviousValuesButton. Copyable controls will preview/highlight as one enters the CopyPreviousValuesButton
            this.CopyPreviousValuesButton.Visibility = Visibility.Visible;
            this.DataEntryControlPanel.IsVisible = true;

            // Show the File Player
            this.FilePlayer.Visibility = Visibility.Visible;

            // Set whether detections actually exist at this point.
            GlobalReferences.DetectionsExists = this.State.UseDetections ? this.dataHandler.FileDatabase.DetectionsExists() : false;

            // Get the QuickPasteXML from the database and populate the QuickPaste datastructure with it
            string xml = this.dataHandler.FileDatabase.ImageSet.QuickPasteXML;
            this.quickPasteEntries = QuickPasteOperations.QuickPasteEntriesFromXML(this.dataHandler.FileDatabase, xml);

            // if this is completion of an existing .ddb open, set the current selection and the image index to the ones from the previous session with the image set
            // also if this is completion of import to a new .ddb
            long mostRecentFileID = this.dataHandler.FileDatabase.ImageSet.MostRecentFileID;
            FileSelectionEnum fileSelection = this.dataHandler.FileDatabase.ImageSet.FileSelection;
            if (fileSelection == FileSelectionEnum.Folders)
            {
                // Compose a custom search term for the relative path
                this.dataHandler.FileDatabase.CustomSelection.SetRelativePathSearchTerm(this.dataHandler.FileDatabase.ImageSet.SelectedFolder);
            }
            if (filesJustAdded && (this.dataHandler.ImageCache.CurrentRow != Constant.DatabaseValues.InvalidRow && this.dataHandler.ImageCache.CurrentRow != Constant.DatabaseValues.InvalidRow))
            {
                // if this is completion of an add to an existing image set stay on the image, ideally, shown before the import
                if (this.dataHandler.ImageCache.Current != null)
                {
                    mostRecentFileID = this.dataHandler.ImageCache.Current.ID;
                }
                // This is heavier weight than desirable, but it's a one off.
                this.dataHandler.ImageCache.TryInvalidate(mostRecentFileID);
            }

            // PERFORMANCE - Initial but necessary Selection done in OnFolderLoadingComplete invoking this.FilesSelectAndShow to display selected image set 
            // PROGRESSBAR - Display a progress bar on this (and all other) calls to FilesSelectAndShow after a delay of (say) .5 seconds.
            await this.FilesSelectAndShowAsync(mostRecentFileID, fileSelection).ConfigureAwait(true);

            // match UX availability to file availability
            this.EnableOrDisableMenusAndControls();

            // Reset the folder list used to construct the Select Folders menu
            this.MenuItemSelectByFolder_ResetFolderList();

            // Whether to exclude DateTime and UTCOffset columns when exporting to a .csv file
            this.excludeDateTimeAndUTCOffsetWhenExporting = !this.IsUTCOffsetVisible();

            // Trigger updates to the datagrid pane, if its visible to the user.
            if (this.DataGridPane.IsVisible)
            {
                this.DataGridPane_IsActiveChanged(null, null);
            }
        }
    }
}
