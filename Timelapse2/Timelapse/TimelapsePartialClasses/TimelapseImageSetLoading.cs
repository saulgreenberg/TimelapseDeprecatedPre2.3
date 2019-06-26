using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Timelapse.Controls;
using Timelapse.Database;
using Timelapse.Dialog;
using Timelapse.Enums;
using Timelapse.Images;
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
            this.state.MostRecentImageSets.TryGetMostRecent(out string defaultTemplateDatabasePath);
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
        private bool TryOpenTemplateAndBeginLoadFoldersAsync(string templateDatabasePath, out BackgroundWorker backgroundWorker)
        {
            backgroundWorker = null;

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
            FileDatabase fileDatabase = FileDatabase.CreateOrOpen(fileDatabaseFilePath, this.templateDatabase, this.state.CustomSelectionTermCombiningOperator, templateSyncResults);

            // The next test is to test and syncronize (if needed) the default values stored in the fileDB table schema to those stored in the template
            Dictionary<string, string> columndefaultdict = fileDatabase.GetColumnsAndDefaultValuesFromSchema(Constant.DatabaseTable.FileData);
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

            this.Title = Constant.MainWindowBaseTitle + " (" + Path.GetFileName(fileDatabase.FilePath) + ")";
            this.state.MostRecentImageSets.SetMostRecent(templateDatabasePath);
            this.RecentFileSets_Refresh();

            // Record the version number of the currently executing version of Timelapse only if its greater than the one already stored in the ImageSet Table.
            // This will indicate the latest timelapse version that is compatable with the database structure. 
            string currentVersionNumberAsString = VersionClient.GetTimelapseCurrentVersionNumber().ToString();
            if (VersionClient.IsVersion1GreaterThanVersion2(currentVersionNumberAsString, this.dataHandler.FileDatabase.ImageSet.VersionCompatability))
            {
                this.dataHandler.FileDatabase.ImageSet.VersionCompatability = currentVersionNumberAsString;
                this.dataHandler.FileDatabase.SyncImageSetToDatabase();
            }

            // If this is a new image database, try to load images (if any) from the folder...  
            if (importImages)
            {
                this.TryBeginImageFolderLoadAsync(this.FolderPath, true, out backgroundWorker);
            }
            else
            {
                this.OnFolderLoadingComplete(false);
            }
            return true;
        }

        // Get the root folder name from the database, and check to see if its the same as the actual root folder.
        // If not, ask the user if he/she wants to update the database.
        public void CheckAndCorrectRootFolder(FileDatabase fileDatabase)
        {
            List<object> allRootFolderPaths = fileDatabase.GetDistinctValuesInColumn(Constant.DatabaseTable.FileData, Constant.DatabaseColumn.Folder);
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
            List<object> allRelativePaths = fileDatabase.GetDistinctValuesInColumn(Constant.DatabaseTable.FileData, Constant.DatabaseColumn.RelativePath);
            List<string> missingRelativePaths = new List<string>();
            foreach (string relativePath in allRelativePaths)
            {
                string path = Path.Combine(fileDatabase.FolderPath, relativePath);
                if (!Directory.Exists(path))
                {
                    missingRelativePaths.Add(relativePath);
                }
            }

            // If there are multiple missing folders, it will generate multiple dialog boxes. Thus we explain what is going on.
            if (missingRelativePaths?.Count > 1)
            {
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
                    return;
                }
            }

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
            }
        }
        // END

        [HandleProcessCorruptedStateExceptions]
        // out parameters can't be used in anonymous methods, so a separate pointer to backgroundWorker is required for return to the caller
        private bool TryBeginImageFolderLoadAsync(string imageFolderPath, bool isInitialImageSetLoading, out BackgroundWorker externallyVisibleWorker)
        {
            // We check for different things if this is the initial first-time load of an image set vs. adding files after the database has been created.
            if (isInitialImageSetLoading)
            {
                // We are loading images from the root folder for the first time
                bool isImagesInRootFolder = Util.FilesFoldersAndPaths.CheckFolderForAtLeastOneImageOrVideoFiles(this.FolderPath);
                List<string> subFolderPaths = new List<string>();
                Util.FilesFoldersAndPaths.GetAllFoldersContainingAnImageOrVideo(imageFolderPath, subFolderPaths);

                // The else-ifs are stubs in case we want to modify the code to ask the user if she wants to load only images in the root folder, 
                // or to include subfolders as well.
                if (subFolderPaths.Count == 0)
                {
                    // No images were found in the root folder or subfolders
                    MessageBox messageBox = new MessageBox("No images or videos were found", this, MessageBoxButton.OK);
                    messageBox.Message.Problem = "No images or videos were found in this folder or its subfolders:" + Environment.NewLine;
                    messageBox.Message.Problem += "\u2022 " + imageFolderPath + Environment.NewLine;
                    messageBox.Message.Reason = "Neither the folder nor its sub-folders contain:" + Environment.NewLine;
                    messageBox.Message.Reason += "\u2022 image files (ending in '.jpg') " + Environment.NewLine;
                    messageBox.Message.Reason += "\u2022 video files (ending in '.avi or .mp4')";
                    messageBox.Message.Solution = "Timelapse aborted the load operation." + Environment.NewLine;
                    messageBox.Message.Hint = "Locate your template in a folder containing (or whose subfolders contain) image or video files ." + Environment.NewLine;
                    messageBox.Message.Icon = MessageBoxImage.Exclamation;
                    messageBox.ShowDialog();
                    externallyVisibleWorker = null;
                    return false;
                }
                else if (subFolderPaths.Contains(imageFolderPath) && subFolderPaths.Count() == 1)
                {
                    // Images found in root folder only
                    // System.Diagnostics.Debug.Print("Images in root folder only");
                }
                else if (subFolderPaths.Contains(imageFolderPath) && subFolderPaths.Count() > 1)
                {
                    // Images found in root folder and sub folders
                    // System.Diagnostics.Debug.Print("Images in root folder and subfolders");
                }
                else if (subFolderPaths.Contains(imageFolderPath) == false && subFolderPaths.Count() > 0)
                {
                    // Images found in subfolders only
                    // System.Diagnostics.Debug.Print("Images in sub folders only");
                }
            }
            else
            {
                // We are adding images to an already existing database
                bool isImagesInRootFolder = Util.FilesFoldersAndPaths.CheckFolderForAtLeastOneImageOrVideoFiles(this.FolderPath);
                List<string> subFolderPaths = new List<string>();
                Util.FilesFoldersAndPaths.GetAllFoldersContainingAnImageOrVideo(imageFolderPath, subFolderPaths);
                if (subFolderPaths.Count == 0)
                {
                    // No images were found in the root folder or subfolders
                    MessageBox messageBox = new MessageBox("No images or videos were found", this, MessageBoxButton.OK);
                    messageBox.Message.Problem = "No images or videos were found in this folder or its subfolders:" + Environment.NewLine;
                    messageBox.Message.Problem += "\u2022 " + imageFolderPath + Environment.NewLine;
                    messageBox.Message.Reason = "Neither the folder nor its sub-folders contain:" + Environment.NewLine;
                    messageBox.Message.Reason += "\u2022 image files (ending in '.jpg') " + Environment.NewLine;
                    messageBox.Message.Reason += "\u2022 video files (ending in '.avi or .mp4')";
                    messageBox.Message.Solution = "Timelapse did not add any images or files." + Environment.NewLine;
                    messageBox.Message.Hint = "Try adding a folder that contains (or whose subfolders contain) image or video files ." + Environment.NewLine;
                    messageBox.Message.Icon = MessageBoxImage.Exclamation;
                    messageBox.ShowDialog();
                    externallyVisibleWorker = null;
                    return false;
                }
            }

            List<FileInfo> filesToAdd = Util.FilesFoldersAndPaths.GetAllImageAndVideoFilesInFolderAndSubfolders(imageFolderPath);
           
            // Load all the files (matching allowable file types) found in the folder
            // Show image previews of the files to the user as they are individually loaded
            BackgroundWorker backgroundWorker = new BackgroundWorker()
            {
                WorkerReportsProgress = true
            };

            // folderLoadProgress contains data to be used to provide feedback on the folder loading state
            FolderLoadProgress folderLoadProgress = new FolderLoadProgress(filesToAdd.Count)
            {
                TotalPasses = 2,
                CurrentPass = 1
            };

            backgroundWorker.DoWork += (ow, ea) =>
            {
                // First pass: Examine files to extract their basic properties and build a list of files not already in the database
                //
                // Todd found the following. With dark calculations enabled:
                // Profiling of a 1000 image load on quad core, single 80+MB/s capable SSD shows the following:en
                // - one thread:   100% normalized execution time, 35% CPU, 16MB/s disk (100% normalized time = 1 minute 58 seconds)
                // - two threads:   55% normalized execution time, 50% CPU, 17MB/s disk (6.3% normalized time with dark checking skipped)
                // - three threads: 46% normalized execution time, 70% CPU, 20MB/s disk
                // This suggests memory bound operation due to image quality calculation.  The overhead of displaying preview images is fairly low; 
                // normalized time is about 5% with both dark checking and previewing skipped.
                //
                // For now, try to get at least two threads as that captures most of the benefit from parallel operation.  Video loading may be more CPU bound 
                // due to initial frame rendering and benefit from additional threads.  This requires further investigation.  It may also be desirable to reduce 
                // the pixel stride in image quality calculation, which would increase CPU load.
                //
                // With dark calculations disabled:
                // The bottleneck's the SQL insert though using more than four threads (or possibly more threads than the number of physical processors as the 
                // test machine was quad core) results in slow progress on the first 20 files or so, making the optimum number of loading threads complex as it
                // depends on amortizing startup lag across faster progress on the remaining import.  As this is comparatively minor relative to SQL (at least
                // for O(10,000) files for now, just default to four threads in the disabled case.
                //
                // Note: the UI thread is free during loading.  So if loading's going slow the user can switch off dark checking asynchronously to speed up 
                // loading.
                //
                // A sequential partitioner is used as this keeps the preview images displayed to the user in pretty much the same order as they're named,
                // which is less confusing than TPL's default partitioning where the displayed image jumps back and forth through the image set.  Pulling files
                // nearly sequentially may also offer some minor disk performance benefit.                
                List<ImageRow> filesToInsert = new List<ImageRow>();
                TimeZoneInfo imageSetTimeZone = this.dataHandler.FileDatabase.ImageSet.GetSystemTimeZone();
                DateTime utcNow = new DateTime();
                DateTime lastUpdateTime = DateTime.UtcNow - Constant.ThrottleValues.LoadingImageDisplayInterval;  // so the first update check will refresh 

                // SaulXXX There is a bug in the Parallel.ForEach somewhere when initially loading files, where it may occassionally duplicate an entry and skip a nearby image.
                // It also occassion produces an SQLite Database locked error. 
                // While I have kept the call here in case we eventually track down this bug, I have reverted to foreach
                // Parallel.ForEach(new SequentialPartitioner<FileInfo>(filesToAdd), Utilities.GetParallelOptions(this.state.ClassifyDarkImagesWhenLoading ? 2 : 4), (FileInfo fileInfo) =>
                int filesProcessed = 0;
                foreach (FileInfo fileInfo in filesToAdd)
                {
                    utcNow = DateTime.UtcNow;
                    // As GetOrCreateFile inserts the file date as the Date/Time, a later call will examine the bitmap metadata, and - if it exists - over-write the file date  
                    if (this.dataHandler.FileDatabase.GetOrCreateFile(fileInfo, out ImageRow file))
                    {
                        // The image is already in the database. So skip it.
                        // Even so, provide feedback every LoadingImageDisplayInterval
                        if (utcNow - lastUpdateTime >= Constant.ThrottleValues.LoadingImageDisplayInterval)
                        {
                            folderLoadProgress.BitmapSource = Constant.ImageValues.FileAlreadyLoaded.Value;
                            folderLoadProgress.CurrentFile = filesProcessed;
                            folderLoadProgress.CurrentFileName = file.File;
                            int percentProgress = (int)(100.0 * filesProcessed / (double)filesToAdd.Count);
                            backgroundWorker.ReportProgress(percentProgress, folderLoadProgress);

                            lastUpdateTime = utcNow;
                            filesProcessed++;
                            continue;
                        }
                    }

                    // The image is not in the database, so add it.
                    filesProcessed++;
                    BitmapSource bitmapSource = null;
                    try
                    {
                        // Create the bitmap and determine its quality
                        // avoid ImageProperties.LoadImage() here as the create exception needs to surface to set the image quality to corrupt
                        // framework bug: WriteableBitmap.Metadata returns null rather than metatada offered by the underlying BitmapFrame, so 
                        // retain the frame and pass its metadata to TryUseImageTaken().
                        bitmapSource = file.LoadBitmap(this.FolderPath, ImageDisplayIntentEnum.TransientLoading, out bool isCorruptOrMissing);

                        // Check if the ImageQuality is corrupt or missing, and if so set it to unknown
                        if (isCorruptOrMissing)
                        {
                            file.ImageQuality = FileSelectionEnum.Unknown;
                        }

                        if (this.state.ClassifyDarkImagesWhenLoading == true && bitmapSource != Constant.ImageValues.Corrupt.Value)
                        {
                            // Dark Image Classification during loading if the automatically classify dark images option is set  
                            // Bug: invoking GetImageQuality here (i.e., on initial image loading ) would sometimes crash the system on older machines/OS, 
                            // likely due to some threading issue that I can't debug.
                            // This is caught by GetImageQuality, where it signals failure by returning ImageSelection.Corrupted
                            // As this is a non-deterministic failure (i.e., there may be nothing wrong with the image), we try to resolve this failure by restarting the loop.
                            // We will do try this at most MAX_RETRIES per image, after which we will just skip it and set the ImageQuality to Ok.
                            // Yup, its a hack, and there is a small chance that the failure may overwrite some vital memory, but not sure what else to do besides banging my head against the wall.
                            const int MAX_RETRIES = 3;
                            int retries_attempted = 0;
                            file.ImageQuality = bitmapSource.AsWriteable().GetImageQuality(this.state.DarkPixelThreshold, this.state.DarkPixelRatioThreshold);

                            // We don't check videos for darkness, so set it as Unknown.
                            if (file.IsVideo)
                            {
                                file.ImageQuality = FileSelectionEnum.Unknown;
                            }
                            else
                            {
                                while (file.ImageQuality == FileSelectionEnum.Unknown && retries_attempted < MAX_RETRIES)
                                {
                                    // See what images were retried
                                    TraceDebug.PrintMessage("Retrying dark image classification : " + retries_attempted.ToString() + " " + fileInfo);
                                    retries_attempted++;
                                    file.ImageQuality = bitmapSource.AsWriteable().GetImageQuality(this.state.DarkPixelThreshold, this.state.DarkPixelRatioThreshold);
                                }
                                if (retries_attempted == MAX_RETRIES && file.ImageQuality == FileSelectionEnum.Unknown)
                                {
                                    // We've reached the maximum number of retires. Give up, and just set the image quality (perhaps incorrectly) to ok
                                    file.ImageQuality = FileSelectionEnum.Unknown;
                                }
                                // Try to update the datetime (which is currently the file date) with the metadata date time the image was taken instead
                                // We only do this for files, as videos do not have these metadata fields
                                file.TryReadDateTimeOriginalFromMetadata(this.FolderPath, imageSetTimeZone);
                            }
                        }
                    }
                    catch (Exception exception)
                    {
                        // We couldn't manage the image for whatever reason, so mark it as corrupted.
                        TraceDebug.PrintMessage(String.Format("Load of {0} failed as it's likely corrupted, in TryBeginImageFolderLoadAsync. {1}", file.File, exception.ToString()));
                        bitmapSource = Constant.ImageValues.Corrupt.Value;
                        file.ImageQuality = FileSelectionEnum.Unknown;
                    }

                    int filesPendingInsert;
                    // lock (filesToInsert) // SAULXXX Parallel.ForEach
                    // {
                    filesToInsert.Add(file);
                    filesPendingInsert = filesToInsert.Count;
                    // }

                    // Display progress on feedback after a given time interval
                    if (utcNow - lastUpdateTime >= Constant.ThrottleValues.LoadingImageDisplayInterval)
                    {
                        // lock (folderLoadProgress) // SAULXXX Parallel.ForEach
                        // {
                        if (file.IsVideo)
                            {
                                // TODO When Video thumbnails are available, we can use those.
                                // Show a stock video bitmap image as they won't be retrieved fast enough anyways
                                folderLoadProgress.BitmapSource = Constant.ImageValues.BlankVideo512.Value;
                            }
                            else if (bitmapSource != null)
                            {
                                // if file was already loaded for dark checking, use the resulting bitmap
                                folderLoadProgress.BitmapSource = bitmapSource;
                            }
                            else
                            {
                                // otherwise, load the file for display
                                folderLoadProgress.BitmapSource = file.LoadBitmap(this.FolderPath, ImageDisplayIntentEnum.TransientLoading, out bool isCorruptOrMissing);
                            }
                            folderLoadProgress.CurrentFile = filesToInsert.Count;
                            folderLoadProgress.CurrentFileName = file.File;
                            int percentProgress = (int)(100.0 * filesToInsert.Count / (double)filesToAdd.Count);
                            backgroundWorker.ReportProgress(percentProgress, folderLoadProgress);
                            lastUpdateTime = utcNow;
                            // Uncomment this to see how many images (if any) are not skipped
                            //  Console.Write(" ");
                        // }
                    }
                    else
                    {
                        // Uncomment this to see how many images (if any) are skipped
                         // Console.WriteLine(" ");
                         // Console.WriteLine("Skipped");
                        folderLoadProgress.BitmapSource = null;
                    }
                    // }); // SAULXXX Parallel.ForEach
                }

                utcNow = DateTime.UtcNow;
                lastUpdateTime = DateTime.UtcNow - Constant.ThrottleValues.LoadingImageDisplayInterval;  // so the first update check will refresh 
                // Second pass: Update database
                filesToInsert = filesToInsert.OrderBy(file => Path.Combine(file.RelativePath, file.File)).ToList();
                folderLoadProgress.CurrentPass = 2;
                this.dataHandler.FileDatabase.AddFiles(filesToInsert, (ImageRow file, int fileIndex) =>
                {
                    utcNow = DateTime.UtcNow;
                    if (utcNow - lastUpdateTime >= Constant.ThrottleValues.LoadingImageDisplayInterval)
                    { 
                        // skip reloading images to display as the user's already seen them import
                        folderLoadProgress.BitmapSource = null;
                        folderLoadProgress.CurrentFile = fileIndex;
                        folderLoadProgress.CurrentFileName = file.File;
                        int percentProgress = (int)(100.0 * fileIndex / (double)filesToInsert.Count);
                        backgroundWorker.ReportProgress(percentProgress, folderLoadProgress);
                        lastUpdateTime = utcNow;
                    }
                });
            };
            backgroundWorker.ProgressChanged += (o, ea) =>
            {
                // this gets called on the UI thread
                this.ImageSetPane.IsActive = true;
                string message = (folderLoadProgress.TotalPasses > 1) ? String.Format("Pass {0}/{1}: ", folderLoadProgress.CurrentPass, folderLoadProgress.TotalPasses) : String.Empty;
                message = String.Format("{0} Analyzing file {1} of {2} ({3})", message, folderLoadProgress.CurrentFile, folderLoadProgress.TotalFiles, folderLoadProgress.CurrentFileName);

                this.UpdateFolderLoadProgress(folderLoadProgress.BitmapSource, ea.ProgressPercentage, message);
                this.StatusBar.SetCurrentFile(folderLoadProgress.CurrentFile);
                this.StatusBar.SetCount(folderLoadProgress.TotalFiles);
            };
            backgroundWorker.RunWorkerCompleted += (o, ea) =>
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

                this.OnFolderLoadingComplete(true);

                // Finally, tell the user how many images were loaded, etc.
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
                        this.FilesSelectAndShow(this.dataHandler.FileDatabase.ImageSet.MostRecentFileID, this.dataHandler.FileDatabase.ImageSet.FileSelection, true); // to regenerate the controls and markers for this image
                    }
                }
                this.BusyIndicator.IsBusy = false; // Hide the busy indicator
            };

            // Set up the user interface to show feedback
            this.BusyIndicator.IsBusy = true; // Display the busy indicator

            this.FileNavigatorSlider.Visibility = Visibility.Collapsed;
            this.UpdateFolderLoadProgress(null, 0, "Loading folders...");
            this.StatusBar.SetMessage("Loading folders...");
            backgroundWorker.RunWorkerAsync();
            externallyVisibleWorker = backgroundWorker;
            return true;
        }

        private void UpdateFolderLoadProgress(BitmapSource bitmap, int percent, string message)
        {
            if (bitmap != null)
            {
                this.MarkableCanvas.SetNewImage(bitmap, null);
            }

            ProgressBar bar = Utilities.GetVisualChild<ProgressBar>(this.BusyIndicator);
            TextBlock textmessage = Utilities.GetVisualChild<TextBlock>(this.BusyIndicator);
            if (bar != null)
            {
                bar.Value = percent;
            }
            if (textmessage != null)
            {
                textmessage.Text = message; // "Analyzing data from : " + folderLoadProgress.CurrentFile + "/" + folderLoadProgress.TotalFiles + " files";
            }
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
        private void OnFolderLoadingComplete(bool filesJustAdded)
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
            FilePlayer.Visibility = Visibility.Visible;

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
                mostRecentFileID = this.dataHandler.ImageCache.Current.ID;
                // This is heavier weight than desirable, but it's a one off.
                this.dataHandler.ImageCache.TryInvalidate(mostRecentFileID);
            }
            // PERFORMANCE - Initial but necessary Selection done in OnFolderLoadingComplete invoking this.FilesSelectAndShow to display selected image set 
            this.FilesSelectAndShow(mostRecentFileID, fileSelection);

            // match UX availability to file availability
            this.EnableOrDisableMenusAndControls();

            // Reset the folder list used to construct the Select Folders menu
            this.MenuItemSelectByFolder_ResetFolderList();

            // Whether to exclude DateTime and UTCOffset columns when exporting to a .csv file
            this.excludeDateTimeAndUTCOffsetWhenExporting = !this.IsUTCOffsetVisible();
            
            // Trigger updates to the datagrid pane, if its visible to the user.
            if (this.DataGridPane.IsVisible)
            {
                DataGridPane_IsActiveChanged(null, null);
            }
        }
    }
}
