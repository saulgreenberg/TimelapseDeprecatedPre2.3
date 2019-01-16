using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Speech.Synthesis;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Timelapse.Controls;
using Timelapse.Database;
using Timelapse.Dialog;
using Timelapse.Enums;
using Timelapse.EventArguments;
using Timelapse.Images;
using Timelapse.QuickPaste;
using Timelapse.Util;
using Xceed.Wpf.AvalonDock.Controls;
using Xceed.Wpf.AvalonDock.Layout;
using MessageBox = Timelapse.Dialog.MessageBox;

namespace Timelapse
{
    /// <summary>
    /// main window for Timelapse
    /// </summary>
    public partial class TimelapseWindow : Window, IDisposable
    {
        #region Variables and Properties
        private DataEntryHandler dataHandler;
        private bool disposed;
        private bool excludeDateTimeAndUTCOffsetWhenExporting = false;  // Whether to exclude the DateTime and UTCOffset when exporting to a .csv file
        private List<MarkersForCounter> markersOnCurrentFile = null;   // Holds a list of all markers for each counter on the current file
        private string mostRecentFileAddFolderPath;
        private SpeechSynthesizer speechSynthesizer;                    // Enables speech feedback
        public TimelapseState state;                                    // Status information concerning the state of the UI
        private TemplateDatabase templateDatabase;                      // The database that holds the template
        private IInputElement lastControlWithFocus = null;              // The last control (data, copyprevious button, or FileNavigatorSlider) that had the focus, so we can reset it

        private List<QuickPasteEntry> quickPasteEntries;              // 0 or more custum paste entries that can be created or edited by the user
        private QuickPasteWindow quickPasteWindow = null;

        // Timer for periodically updating images as the ImageNavigator slider is being used
        private DispatcherTimer timerFileNavigator;

        // Timer used to AutoPlay images via MediaControl buttons
        DispatcherTimer FilePlayerTimer = new DispatcherTimer { };

        DispatcherTimer DataGridSelectionsTimer = new DispatcherTimer { };

        private string FolderPath
        {
            get { return this.dataHandler.FileDatabase.FolderPath; }
        }
        #endregion

        #region Main
        public TimelapseWindow()
        {
            AppDomain.CurrentDomain.UnhandledException += this.OnUnhandledException;
            this.InitializeComponent();

            // Register MarkableCanvas callbacks
            this.MarkableCanvas.PreviewMouseDown += new MouseButtonEventHandler(this.MarkableCanvas_PreviewMouseDown);
            this.MarkableCanvas.MouseEnter += new MouseEventHandler(this.MarkableCanvas_MouseEnter);
            this.MarkableCanvas.MarkerEvent += new EventHandler<MarkerEventArgs>(this.MarkableCanvas_RaiseMarkerEvent);
            this.MarkableCanvas.ClickableImagesGrid.DoubleClick += ClickableImagesGrid_DoubleClick;
            this.MarkableCanvas.ClickableImagesGrid.SelectionChanged += ClickableImagesGrid_SelectionChanged;
            this.MarkableCanvas.SwitchedToClickableImagesGridEventAction += SwitchedToClickableImagesGrid;
            this.MarkableCanvas.SwitchedToSingleImageViewEventAction += SwitchedToSingleImagesView;

            // Save/restore the focus whenever we leave / enter the control grid (which contains controls pluse the copy previous button, or the file navigator
            this.ControlGrid.MouseEnter += FocusRestoreOn_MouseEnter;
            this.ControlGrid.MouseLeave += FocusSaveOn_MouseLeave;

            // Set the window's title
            this.Title = Constant.MainWindowBaseTitle;

            // Create the speech synthesiser
            this.speechSynthesizer = new SpeechSynthesizer();

            // Recall user's state from prior sessions
            this.state = new TimelapseState();
            this.state.ReadSettingsFromRegistry();
            this.MarkableCanvas.SetBookmark(this.state.BookmarkScale, this.state.BookmarkTranslation);

            this.MenuItemAudioFeedback.IsChecked = this.state.AudioFeedback;
            this.MenuItemClassifyDarkImagesWhenLoading.IsChecked = this.state.ClassifyDarkImagesWhenLoading;

            // Populate the most recent image set list
            this.RecentFileSets_Refresh();

            // Timer to force the image to update to the current slider position when the user pauses while dragging the  slider 
            this.timerFileNavigator = new DispatcherTimer()
            {
                Interval = this.state.Throttles.DesiredIntervalBetweenRenders
            };
            this.timerFileNavigator.Tick += this.TimerFileNavigator_Tick;

            // Callback to ensure Video AutoPlay stops when the user clicks on it
            this.FileNavigatorSlider.PreviewMouseDown += this.ContentControl_MouseDown;
            this.FileNavigatorSliderReset();

            // Timer activated / deactivated by Video Autoplay media control buttons
            FilePlayerTimer.Tick += FilePlayerTimer_Tick;

            this.DataGridSelectionsTimer.Tick += DataGridSelectionsTimer_Tick;
            this.DataGridSelectionsTimer.Interval = Constant.ThrottleValues.DataGridTimerInterval;

            // Get the window and its size from its previous location
            // SAULXX: Note that this is actually redundant, as if AvalonLayout_TryLoad succeeds it will do it again.
            // Maybe integrate this call with that?
            this.Top = this.state.TimelapseWindowPosition.Y;
            this.Left = this.state.TimelapseWindowPosition.X;
            this.Height = this.state.TimelapseWindowPosition.Height;
            this.Width = this.state.TimelapseWindowPosition.Width;

            // Mute the harmless 'System.Windows.Data Error: 4 : Cannot find source for binding with reference' (I think its from Avalon dock)
            System.Diagnostics.PresentationTraceSources.DataBindingSource.Switch.Level = System.Diagnostics.SourceLevels.Critical;
        }
        #endregion

        #region Window Loading, Closing, and Disposing

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Abort if some of the required dependencies are missing
            if (Dependencies.AreRequiredBinariesPresent(Constant.ApplicationName, Assembly.GetExecutingAssembly()) == false)
            {
                Dependencies.ShowMissingBinariesDialog(Constant.ApplicationName);
                Application.Current.Shutdown();
            }

            // Check for updates at least once a day
            if (DateTime.Now.Year != this.state.MostRecentCheckForUpdates.Year ||
                DateTime.Now.Month != this.state.MostRecentCheckForUpdates.Month ||
                DateTime.Now.Day != this.state.MostRecentCheckForUpdates.Day)
            {
                VersionClient updater = new VersionClient(this, Constant.ApplicationName, Constant.LatestVersionFileNameXML);
                updater.TryGetAndParseVersion(false);
                this.state.MostRecentCheckForUpdates = DateTime.UtcNow;
            }
            if (this.state.FirstTimeFileLoading)
            {
                // Load the previously saved layout. If there is none, TryLoad will default to a reasonable layout and window size/position.
                this.AvalonLayout_TryLoad(Constant.AvalonLayoutTags.LastUsed);
                this.state.FirstTimeFileLoading = false;
            }

            if (!Util.Utilities.CheckAndGetLangaugeAndCulture(out string language, out string culturename, out string displayname))
            {
                this.HelpDocument.WarningRegionLanguage = displayname;
            }
            this.DataEntryControlPanel.IsVisible = false; // this.DataEntryControlPanel.IsFloating;
            this.InstructionPane.IsActive = true;
        }

        private void Window_LocationChanged(object sender, EventArgs e)
        {
            this.FindBoxVisibility(false);
        }

        // On exiting, save various attributes so we can use recover them later
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            this.FilePlayer_Stop();


            if ((this.dataHandler != null) &&
                (this.dataHandler.FileDatabase != null) &&
                (this.dataHandler.FileDatabase.CurrentlySelectedFileCount > 0))
            {
                // save image set properties to the database
                if (this.dataHandler.FileDatabase.ImageSet.FileSelection == FileSelectionEnum.Custom)
                {
                    // don't save custom selections, revert to All 
                    this.dataHandler.FileDatabase.ImageSet.FileSelection = FileSelectionEnum.All;
                }
                
                // sync image set properties
                if (this.MarkableCanvas != null)
                {
                    this.dataHandler.FileDatabase.ImageSet.MagnifyingGlassEnabled = this.MarkableCanvas.MagnifyingGlassEnabled;
                }

                // Persist the current ID in the database image set, so we can go back to that image when restarting timelapse
                if (this.dataHandler.ImageCache != null && this.dataHandler.ImageCache.Current != null)
                {
                    this.dataHandler.FileDatabase.ImageSet.MostRecentFileID = this.dataHandler.ImageCache.Current.ID;
                }

                this.dataHandler.FileDatabase.SyncImageSetToDatabase();

                // ensure custom filter operator is synchronized in state for writing to user's registry
                this.state.CustomSelectionTermCombiningOperator = this.dataHandler.FileDatabase.CustomSelection.TermCombiningOperator;

                // Check if we should delete the DeletedFiles folder, and if so do it.
                // Note that we can only do this if we know where the DeletedFolder is,
                // i.e. because the datahandler and datahandler.FileDatabae is not null
                // That is why its in this if statement.
                if (this.state.DeleteFolderManagement != DeleteFolderManagementEnum.ManualDelete)
                {
                    this.DeleteTheDeletedFilesFolderIfNeeded();
                }
            }

            // persist user specific state to the registry
            if (this.Top > -10 && this.Left > -10)
            {
                this.state.TimelapseWindowPosition = new Rect(new Point(this.Left, this.Top), new Size(this.Width, this.Height));
            }
            this.state.TimelapseWindowSize = new Size(this.Width, this.Height);

            // Save the layout only if we are really closing Timelapse and the DataEntryControlPanel is visible, as otherwise it would be hidden
            // the next time Timelapse is started
            if (sender != null && this.DataEntryControlPanel.IsVisible == true)
            {
                this.AvalonLayout_TrySave(Constant.AvalonLayoutTags.LastUsed);
            }
            else if (sender != null)
            {
                // If the data entry control panel is not visible, we should do a reduced layut save i.e.,
                // where we save ony the position and size of the main window and whether its maximized
                // This is useful for the situation where:
                // - the user has opened timelapse but not loaded an image set
                // - they moved/resized/ maximized the window
                // - they exited without loading an image set.
                // On reload, it will show the timelapse window at the former place/size/maximize state
                // The catch is that if there is a flaoting data entry window, that window will appear at its original place, i.e., where it was when
                // last used to analyze an image set. That is, it may be in an awkward position as it is not moved relative to the timelapse window. 
                // There is no real easy solution for that, except to make the (floating) data entry window always visible on loading (which I don't really want to do). But I don't expect it to be a big problem.
                this.AvalonLayout_TrySaveWindowPositionAndSizeAndMaximizeState(Constant.AvalonLayoutTags.LastUsed);
            }

            // persist user specific state to the registry
            // note that we have to set the bookmark scale and transform in the state, as it is not done elsewhere
            this.state.BookmarkScale = this.MarkableCanvas.GetBookmarkScale();
            this.state.BookmarkTranslation = this.MarkableCanvas.GetBookmarkTranslation();
            this.state.WriteSettingsToRegistry();

            // Clear the CustomPasteEntries from the ImageSet table and save its state
            this.quickPasteEntries = null;
        }

        private void DeleteTheDeletedFilesFolderIfNeeded()
        {
            string deletedFolderPath = Path.Combine(this.dataHandler.FileDatabase.FolderPath, Constant.File.DeletedFilesFolder);
            int howManyDeletedFiles = Directory.Exists(deletedFolderPath) ? Directory.GetFiles(deletedFolderPath).Length : 0;

            // If there are no files, there is nothing to delete
            if (howManyDeletedFiles <= 0)
            {
                return;
            }

            // We either have auto deletion, or ask the user. Check both cases.
            // If its auto deletion, then set the flag to delete
            bool deleteTheDeletedFolder = (this.state.DeleteFolderManagement == DeleteFolderManagementEnum.AutoDeleteOnExit) ? true : false;

            // if its ask the user, then set the flag according to the response
            if (this.state.DeleteFolderManagement == DeleteFolderManagementEnum.AskToDeleteOnExit)
            {
                Dialog.DeleteDeleteFolder deleteDeletedFolders = new Dialog.DeleteDeleteFolder(howManyDeletedFiles)
                {
                    Owner = this
                };
                 deleteTheDeletedFolder = deleteDeletedFolders.ShowDialog() == true ? true : false;
            }
            if (deleteTheDeletedFolder == true)
            {
                Directory.Delete(deletedFolderPath, true);
            }
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }

            if (disposing)
            {
                if (this.dataHandler != null)
                {
                    this.dataHandler.Dispose();
                }
                this.speechSynthesizer.Dispose();
            }
            this.disposed = true;
        }
        // If we get an exception that wasn't handled, show a dialog asking the user to send the bug report to us.
        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject.ToString().Contains("System.IO.PathTooLongException"))
            {
                Dialogs.ShowFilePathTooLongDialog(e, this);
            }
            else
            {
                Utilities.ShowExceptionReportingDialog("Timelapse", e, this);
            }
        }
        #endregion

        #region Image Set Loading
        // Prompt user to select a template.
        private bool TryGetTemplatePath(out string templateDatabasePath)
        {
            // Default the template selection dialog to the most recently opened database
            this.state.MostRecentImageSets.TryGetMostRecent(out string defaultTemplateDatabasePath);
            if (Utilities.TryGetFileFromUser("Select a TimelapseTemplate.tdb file, which should be located in the root folder containing your images and videos",
                                             defaultTemplateDatabasePath,
                                             String.Format("Template files (*{0})|*{0}", Constant.File.TemplateDatabaseFileExtension), Constant.File.TemplateDatabaseFileExtension,
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
                Dialogs.ShowTemplatePathTooLongDialog(templateDatabasePath, this);
                return false;
            }
            // Second, check to see if we can actually open it.
            if (!TemplateDatabase.TryCreateOrOpen(templateDatabasePath, out this.templateDatabase))
            {
                // notify the user the template couldn't be loaded rather than silently doing nothing
                Dialogs.ShowTemplateCouldNotBeLoaded(templateDatabasePath, this);
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
                Dialogs.ShowDatabasePathTooLongDialog(fileDatabaseFilePath, this);
                return false;
            }

            // Check the expected file path length of the backup files, and warn the user if backups may not be made because thier path is too long
            if (Utilities.IsBackupPathLengthTooLong(templateDatabasePath) || Utilities.IsBackupPathLengthTooLong(fileDatabaseFilePath))
            {
                Dialogs.ShowBackupPathTooLongDialog(this);
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
                        TemplateChangedAndUpdate templateChangedAndUpdate = new TemplateChangedAndUpdate(
                            templateSyncResults, this);
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

            // Check to see if the root folder stored in the database is the same as the actual root folder. If not, ask the user if it should be changed.
            this.CheckAndCorrectRootFolder(fileDatabase);

            // Check to see if there are any missing folders as specified by the relative paths. For thos missing, as the user to try to locate those folders.
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
                this.TryBeginImageFolderLoadAsync(new List<string>() { this.FolderPath }, out backgroundWorker);
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
                //  System.Diagnostics.Debug.Print("Checking the root folder name in the database, but no entries were found. Perhaps the database is empty?");
                return;
            }

            // retrieve and compare the db and actual root folder path names. While there really should be only one entry in the allRootFolderPaths,
            // we still do a check in case there is more than one. If even one entry doesn't match, we use that entry to ask the user if he/she
            // wants to update the root folder to match the actual location of the root folder containing the template, data and image files.
            string actualRootFolderName = fileDatabase.FolderPath.Split(Path.DirectorySeparatorChar).Last();
            foreach (string dbRootFolderName in allRootFolderPaths)
            {
                if (dbRootFolderName.Equals(actualRootFolderName)) continue;
                else
                {
                    // We have at least one entry where there is a mismatch between the actual root folder and the stored root folder
                    // Consequently, ask the user if he/she wants to update the db entry 
                    Dialog.UpdateRootFolder renameRootFolderDialog;
                    renameRootFolderDialog = new Dialog.UpdateRootFolder(this, dbRootFolderName, actualRootFolderName);
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
        private bool TryBeginImageFolderLoadAsync(IEnumerable<string> imageFolderPaths, out BackgroundWorker externallyVisibleWorker)
        {
            List<FileInfo> filesToAdd = new List<FileInfo>();
            foreach (string imageFolderPath in imageFolderPaths)
            {
                DirectoryInfo imageFolder = new DirectoryInfo(imageFolderPath);
                foreach (string extension in new List<string>() { Constant.File.AviFileExtension, Constant.File.Mp4FileExtension, Constant.File.JpgFileExtension })
                {
                    filesToAdd.AddRange(imageFolder.GetFiles("*" + extension));
                }
            }
            filesToAdd = filesToAdd.OrderBy(file => file.FullName).ToList();

            if (filesToAdd.Count == 0)
            {
                externallyVisibleWorker = null;

                // no images were found in folder; see if user wants to try again
                MessageBox messageBox = new MessageBox("Select a folder containing images or videos", this, MessageBoxButton.YesNo);
                messageBox.Message.Problem = "Select a folder containing images or videos, as there aren't any images or videos in the folder:" + Environment.NewLine;
                messageBox.Message.Problem += "\u2022 " + this.FolderPath + Environment.NewLine;
                messageBox.Message.Reason = "\u2022 This folder has no JPG files in it (files ending in '.jpg'), and" + Environment.NewLine;
                messageBox.Message.Reason += "\u2022 This folder has no AVI files in it (files ending in '.avi'), and" + Environment.NewLine;
                messageBox.Message.Reason += "\u2022 This folder has no MP4 files in it (files ending in '.mp4'), or" + Environment.NewLine;
                messageBox.Message.Reason += "\u2022 The images / videos may be located in a subfolder to this one.";
                messageBox.Message.Solution = "Select a folder containing images (files with a '.jpg' suffix) and/or" + Environment.NewLine;
                messageBox.Message.Solution += "videos ('.avi' or '.mp4' files)." + Environment.NewLine;
                messageBox.Message.Icon = MessageBoxImage.Question;
                if (messageBox.ShowDialog() == false)
                {
                    return false;
                }

                if (this.ShowFolderSelectionDialog(out IEnumerable<string> folderPaths))
                {
                    return this.TryBeginImageFolderLoadAsync(folderPaths, out externallyVisibleWorker);
                }

                // exit if user changed their mind about trying again
                return false;
            }

            // Load all the files (matching allowable file types) found in the folder
            // Show image previews of the files to the user as they are individually loaded
            BackgroundWorker backgroundWorker = new BackgroundWorker()
            {
                WorkerReportsProgress = true
            };

            FolderLoadProgress folderLoadProgress = new FolderLoadProgress(filesToAdd.Count);
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
                DateTime previousImageRender = DateTime.UtcNow - this.state.Throttles.DesiredIntervalBetweenRenders;

                // SaulXXX There is a bug in the Parallel.ForEach somewhere when initially loading files, where it may occassionally duplicate an entry and skip a nearby image.
                // It also occassion produces an SQLite Database locked error. 
                // While I have kept the call here in case we eventually track down this bug, I have reverted to foreach
                // Parallel.ForEach(new SequentialPartitioner<FileInfo>(filesToAdd), Utilities.GetParallelOptions(this.state.ClassifyDarkImagesWhenLoading ? 2 : 4), (FileInfo fileInfo) =>
                int filesProcessed = 0;
                foreach (FileInfo fileInfo in filesToAdd)
                {
                    // Note that calling GetOrCreateFile inserts the the creation date as the Date/Time. 
                    // We should ensure that a later call examines the bitmap metadata, and - if that metadata date exists - over-writes the creation date  
                    if (this.dataHandler.FileDatabase.GetOrCreateFile(fileInfo, out ImageRow file))
                    {
                        // the database already has an entry for this file so skip it
                        // feedback is displayed, albeit fleetingly unless a large number of images are skipped.
                        folderLoadProgress.BitmapSource = Constant.ImageValues.FileAlreadyLoaded.Value;
                        folderLoadProgress.CurrentFile = filesProcessed;
                        folderLoadProgress.CurrentFileName = file.FileName;

                        int percentProgress = (int)(100.0 * filesProcessed / (double)filesToAdd.Count);
                        backgroundWorker.ReportProgress(percentProgress, folderLoadProgress);
                        filesProcessed++;
                        continue;
                    }
                    filesProcessed++;

                    BitmapSource bitmapSource = null;
                    try
                    {
                        if (this.state.ClassifyDarkImagesWhenLoading == false)
                        {
                            file.ImageQuality = FileSelectionEnum.Ok;
                        }
                        else
                        {
                            // Create the bitmap and determine its quality
                            // avoid ImageProperties.LoadImage() here as the create exception needs to surface to set the image quality to corrupt
                            // framework bug: WriteableBitmap.Metadata returns null rather than metatada offered by the underlying BitmapFrame, so 
                            // retain the frame and pass its metadata to TryUseImageTaken().
                            bitmapSource = file.LoadBitmap(this.FolderPath, ImageDisplayIntentEnum.TransientLoading);

                            // Set the ImageQuality to corrupt if the returned bitmap is the corrupt image, otherwise set it to its Ok/Dark setting
                            if (bitmapSource == Constant.ImageValues.Corrupt.Value)
                            {
                                file.ImageQuality = FileSelectionEnum.Corrupted;
                            }
                            else
                            {

                                // Dark Image Classification during loading
                                // One Timelapse option is to have it automatically classify dark images when loading 
                                // If its set, check to see if its a Dark or Okay image.
                                // However, invoking GetImageQuality here (i.e., on initial image loading ) would sometimes crash the system on older machines/OS, 
                                // likley due to some threading issue that I can't debug.
                                // This is caught by GetImageQuality, where it signals failure by returning ImageSelection.Corrupted
                                // As this is a non-deterministic failure (i.e., there may be nothing wrong with the image), we try to resolve this failure by restarting the loop.
                                // We will do try this at most MAX_RETRIES per image, after which we will just skip it and set the ImageQuality to Ok.
                                // Yup, its a hack, and there is a small chance that the failure may overwrite some vital memory, but not sure what else to do besides banging my head against the wall.
                                const int MAX_RETRIES = 3;
                                int retries_attempted = 0;
                                file.ImageQuality = bitmapSource.AsWriteable().GetImageQuality(this.state.DarkPixelThreshold, this.state.DarkPixelRatioThreshold);
                                // We don't check videos for darkness, so set it as ok.
                                if (file.IsVideo)
                                {
                                    file.ImageQuality = FileSelectionEnum.Ok;
                                }
                                else
                                {
                                    while (file.ImageQuality == FileSelectionEnum.Corrupted && retries_attempted < MAX_RETRIES)
                                    {
                                        // See what images were retried
                                        Utilities.PrintFailure("Retrying dark image classification : " + retries_attempted.ToString() + " " + fileInfo);
                                        retries_attempted++;
                                        file.ImageQuality = bitmapSource.AsWriteable().GetImageQuality(this.state.DarkPixelThreshold, this.state.DarkPixelRatioThreshold);
                                    }
                                    if (retries_attempted == MAX_RETRIES && file.ImageQuality == FileSelectionEnum.Corrupted)
                                    {
                                        // We've reached the maximum number of retires. Give up, and just set the image quality (perhaps incorrectly) to ok
                                        file.ImageQuality = FileSelectionEnum.Ok;
                                    }
                                }
                            }
                        }
                        // Try to update the datetime (which is currently the creation date) with the metadata date time tthe image was taken instead
                        // SAULXXX Note: This fails on video reading when we try to read the dark threshold. Check into it....
                        file.TryReadDateTimeOriginalFromMetadata(this.FolderPath, imageSetTimeZone);
                    }
                    catch (Exception exception)
                    {
                        // We couldn't manage the image for whatever reason, so mark it as corrupted.
                        Utilities.PrintFailure(String.Format("Load of {0} failed as it's likely corrupted, in TryBeginImageFolderLoadAsync. {1}", file.FileName, exception.ToString()));
                        bitmapSource = Constant.ImageValues.Corrupt.Value;
                        file.ImageQuality = FileSelectionEnum.Corrupted;
                    }

                    int filesPendingInsert;
                    lock (filesToInsert)
                    {
                        filesToInsert.Add(file);
                        filesPendingInsert = filesToInsert.Count;
                    }

                    // If the throttle interval isn't reached then skip showing the image.
                    DateTime utcNow = DateTime.UtcNow;
                    if ((this.state.SuppressThrottleWhenLoading == true) ||
                    (utcNow - previousImageRender > this.state.Throttles.DesiredIntervalBetweenRenders))
                    {
                        lock (folderLoadProgress)
                        {
                            // if file was already loaded for dark checking use the resulting bitmap
                            // otherwise, load the file for display
                            if (bitmapSource != null)
                            {
                                folderLoadProgress.BitmapSource = bitmapSource;
                            }
                            else
                            {
                                folderLoadProgress.BitmapSource = file.LoadBitmap(this.FolderPath, ImageDisplayIntentEnum.TransientLoading);
                            }
                            folderLoadProgress.CurrentFile = filesToInsert.Count;
                            folderLoadProgress.CurrentFileName = file.FileName;

                            int percentProgress = (int)(100.0 * filesToInsert.Count / (double)filesToAdd.Count);
                            backgroundWorker.ReportProgress(percentProgress, folderLoadProgress);
                            previousImageRender = utcNow;
                            // Uncomment this to see how many images (if any) are not skipped
                            //  Console.Write(" ");
                        }
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


                // Second pass: Update database
                filesToInsert = filesToInsert.OrderBy(file => Path.Combine(file.RelativePath, file.FileName)).ToList();
                this.dataHandler.FileDatabase.AddFiles(filesToInsert, (ImageRow file, int fileIndex) =>
                {
                    // skip reloading images to display as the user's already seen them import
                    folderLoadProgress.BitmapSource = null;
                    folderLoadProgress.CurrentFile = fileIndex;
                    folderLoadProgress.CurrentFileName = file.FileName;
                    int percentProgress = (int)(100.0 * fileIndex / (double)filesToInsert.Count);
                    backgroundWorker.ReportProgress(percentProgress, folderLoadProgress);
                });
            };
            backgroundWorker.ProgressChanged += (o, ea) =>
            {
                this.ImageSetPane.IsActive = true;
                // this gets called on the UI thread
                this.UpdateFolderLoadProgress(folderLoadProgress.BitmapSource, ea.ProgressPercentage, folderLoadProgress.GetMessage());
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

                // hide the feedback panel, and show the file slider
                this.FeedbackControl.Visibility = Visibility.Collapsed;
                this.FileNavigatorSlider.Visibility = Visibility.Visible;

                this.OnFolderLoadingComplete(true);

                // Finally, tell the user how many images were loaded, etc.
                // Note that if the magnifier is enabled, we temporarily hide so it doesn't appear in the background 
                bool saveMagnifierState = this.MarkableCanvas.MagnifyingGlassEnabled;
                this.MarkableCanvas.MagnifyingGlassEnabled = false;
                this.MaybeShowFileCountsDialog(true, this);
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
                        this.SelectFilesAndShowFile(this.dataHandler.FileDatabase.ImageSet.MostRecentFileID, this.dataHandler.FileDatabase.ImageSet.FileSelection, true); // to regenerate the controls and markers for this image
                    }
                }
            };

            // Update UI for import
            this.FeedbackControl.Visibility = Visibility.Visible;
            this.FileNavigatorSlider.Visibility = Visibility.Collapsed;
            this.UpdateFolderLoadProgress(null, 0, "Folder loading beginning...");
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
            this.FeedbackControl.Message.Content = message;
            this.FeedbackControl.ProgressBar.Value = percent;
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
            if (filesJustAdded && (this.dataHandler.ImageCache.CurrentRow != Constant.DatabaseValues.InvalidRow && this.dataHandler.ImageCache.CurrentRow != Constant.DatabaseValues.InvalidRow))
            {
                // if this is completion of an add to an existing image set stay on the image, ideally, shown before the import
                mostRecentFileID = this.dataHandler.ImageCache.Current.ID;
                // This is heavier weight than desirable, but it's a one off.
                this.dataHandler.ImageCache.TryInvalidate(mostRecentFileID);
            }
            this.SelectFilesAndShowFile(mostRecentFileID, fileSelection);

            // match UX availability to file availability
            this.EnableOrDisableMenusAndControls();

            // Whether to exclude DateTime and UTCOffset columns when exporting to a .csv file
            this.excludeDateTimeAndUTCOffsetWhenExporting = !this.IsUTCOffsetVisible();

            // Trigger updates to the datagrid pane, if its visible to the user.
            if (this.DataGridPane.IsVisible)
            {
                DataGridPane_IsActiveChanged(null, null);
            }
        }
        #endregion

        #region Enabling or Disabling Menus and Controls
        private void EnableOrDisableMenusAndControls()
        {
            bool imageSetAvailable = this.IsFileDatabaseAvailable();
            bool filesSelected = (imageSetAvailable && this.dataHandler.FileDatabase.CurrentlySelectedFileCount > 0) ? true : false;

            // Depending upon whether images exist in the data set,
            // enable / disable menus and menu items as needed

            // File menu
            this.MenuItemAddFilesToImageSet.IsEnabled = imageSetAvailable;
            this.MenuItemLoadFiles.IsEnabled = !imageSetAvailable;
            this.MenuItemRecentImageSets.IsEnabled = !imageSetAvailable;
            this.MenuItemExportThisImage.IsEnabled = filesSelected;
            this.MenuItemExportAsCsvAndPreview.IsEnabled = filesSelected;
            this.MenuItemExportAsCsv.IsEnabled = filesSelected;
            this.MenuItemImportFromCsv.IsEnabled = filesSelected;
            this.MenuItemRenameFileDatabaseFile.IsEnabled = filesSelected;
            this.MenuFileCloseImageSet.IsEnabled = imageSetAvailable;

            // Edit menu
            this.MenuItemEdit.IsEnabled = filesSelected;
            this.MenuItemDeleteCurrentFile.IsEnabled = filesSelected;
            // this.MenuItemAdvancedImageSetOptions.IsEnabled = imagesExist; SAULXXX: I don't think we need this anymore, as there is now a date correction option that does this. Remove it from the XAML as well, and delete that dialog?

            // Options menu
            // always enable at top level when an image set exists so that image set advanced options are accessible
            this.MenuItemOptions.IsEnabled = true; // imageSetAvailable;
            //this.MenuItemAdvancedDeleteDuplicates.IsEnabled = filesSelected; // DEPRACATED
            this.MenuItemAudioFeedback.IsEnabled = filesSelected;
            this.MenuItemMagnifyingGlass.IsEnabled = imageSetAvailable;
            this.MenuItemDisplayMagnifyingGlass.IsChecked = imageSetAvailable && this.dataHandler.FileDatabase.ImageSet.MagnifyingGlassEnabled;
            this.MenuItemImageCounts.IsEnabled = filesSelected;
            this.MenuItemDialogsOnOrOff.IsEnabled = filesSelected;
            this.MenuItemAdvancedTimelapseOptions.IsEnabled = filesSelected;

            // View menu
            this.MenuItemView.IsEnabled = filesSelected;

            // Select menu
            this.MenuItemSelect.IsEnabled = filesSelected;

            // Sort menu
            this.MenuItemSort.IsEnabled = filesSelected;

            // Windows menu is always enabled

            // Enablement state of the various other UI components.
            this.ControlsPanel.IsEnabled = filesSelected;  // If images don't exist, the user shouldn't be allowed to interact with the control tray
            this.FileNavigatorSlider.IsEnabled = filesSelected;
            this.MarkableCanvas.IsEnabled = filesSelected;
            this.MarkableCanvas.MagnifyingGlassEnabled = filesSelected && this.dataHandler.FileDatabase.ImageSet.MagnifyingGlassEnabled;

            if (filesSelected == false)
            {
                this.ShowFile(Constant.DatabaseValues.InvalidRow);
                this.StatusBar.SetMessage("Image set is empty.");
                this.StatusBar.SetCurrentFile(0);
                this.StatusBar.SetCount(0);
            }
        }

        // Enable or disable the various menu items that allow images to be manipulated
        private void EnableImageManipulationMenus(bool enable)
        {
            this.MenuItemZoomIn.IsEnabled = enable;
            this.MenuItemZoomOut.IsEnabled = enable;
            this.MenuItemViewDifferencesCycleThrough.IsEnabled = enable;
            this.MenuItemViewDifferencesCombined.IsEnabled = enable;
            this.MenuItemDisplayMagnifyingGlass.IsEnabled = enable;
            this.MenuItemMagnifyingGlassIncrease.IsEnabled = enable;
            this.MenuItemMagnifyingGlassDecrease.IsEnabled = enable;
            this.MenuItemBookmarkSavePanZoom.IsEnabled = enable;
            this.MenuItemBookmarkSetPanZoom.IsEnabled = enable;
            this.MenuItemBookmarkDefaultPanZoom.IsEnabled = enable;
        }
        #endregion

        #region File Selection

        // Simplest form: It doesn't force an update if the file index hasn't changed
        private void SelectFilesAndShowFile()
        {
            SelectFilesAndShowFile(false);
        }
        private void SelectFilesAndShowFile(bool forceUpdate)
        {
            if (this.dataHandler == null || this.dataHandler.FileDatabase == null)
            {
                Utilities.PrintFailure("SelectFilesAndShowFile: Expected a file database to be available.");
            }
            this.SelectFilesAndShowFile(this.dataHandler.FileDatabase.ImageSet.FileSelection, forceUpdate);
        }

        private void SelectFilesAndShowFile(FileSelectionEnum selection, bool forceUpdate)
        {
            long fileID = Constant.DatabaseValues.DefaultFileID;
            if (this.dataHandler != null && this.dataHandler.ImageCache != null && this.dataHandler.ImageCache.Current != null)
            {
                fileID = this.dataHandler.ImageCache.Current.ID;
            }
            this.SelectFilesAndShowFile(fileID, selection, forceUpdate);
        }

        // Basic form doesn't force an update
        private void SelectFilesAndShowFile(long imageID, FileSelectionEnum selection)
        {
            SelectFilesAndShowFile(imageID, selection, false);
        }

        private void SelectFilesAndShowFile(long imageID, FileSelectionEnum selection, bool forceUpdate)
        {
            // change selection
            // if the data grid is bound the file database automatically updates its contents on SelectFiles()
            if (this.dataHandler == null)
            {
                Utilities.PrintFailure("SelectFilesAndShowFile() should not be reachable with a null data handler.  Is a menu item wrongly enabled?");
                return;
            }
            if (this.dataHandler.FileDatabase == null)
            {
                Utilities.PrintFailure("SelectFilesAndShowFile() should not be reachable with a null database.  Is a menu item wrongly enabled?");
                return;
            }

            // We set forceUpdate to true because at least one imagequality status has changed
            // and we want the correct image to be shown
            if (CheckAndUpdateImageQualityForMissingFiles())
            {
                forceUpdate = true;
            }

            // Select the files according to the given selection
            this.dataHandler.FileDatabase.SelectFiles(selection);

            // explain to user if their selection has gone empty and change to all files
            if ((this.dataHandler.FileDatabase.CurrentlySelectedFileCount < 1) && (selection != FileSelectionEnum.All))
            {
                // These cases are reached when 
                // 1) datetime modifications result in no files matching a custom selection
                // 2) all files which match the selection get deleted
                MessageBox messageBox = new MessageBox("Resetting selection to All files (no files currently match the current selection)", this);
                messageBox.Message.Icon = MessageBoxImage.Information;
                messageBox.Message.Result = "The 'All files' selection will be applied, where all files in your image set are displayed.";

                switch (selection)
                {
                    case FileSelectionEnum.Corrupted:
                        messageBox.Message.Problem = "Corrupted files were previously selected but no files are currently corrupted, so nothing can be shown.";
                        messageBox.Message.Reason = "No files have their 'ImageQuality' field set to Corrupted.";
                        messageBox.Message.Hint = "If you have files you think should be marked as 'Corrupted', set their 'ImageQuality' field to 'Corrupted' and then reselect corrupted files.";
                        break;

                    case FileSelectionEnum.Custom:
                        messageBox.Message.Problem = "No files currently match the custom selection so nothing can be shown.";
                        messageBox.Message.Reason = "No files match the criteria set in the current Custom selection.";
                        messageBox.Message.Hint = "Create a different custom selection and apply it view the matching files.";
                        break;
                    case FileSelectionEnum.Dark:
                        messageBox.Message.Problem = "Dark files were previously selected but no files are currently dark so nothing can be shown.";
                        messageBox.Message.Reason = "No files have their 'ImageQuality' field set to Dark.";
                        messageBox.Message.Hint = "If you have files you think should be marked as 'Dark', set their 'ImageQuality' field to 'Dark' and then reselect dark files.";
                        break;
                    case FileSelectionEnum.Missing:
                        messageBox.Message.Problem = "Missing files were previously selected. However, none of the files are marked as missing, so nothing can be shown.";
                        messageBox.Message.Reason = "No files have their 'ImageQuality' field set to Missing.";
                        messageBox.Message.Hint = "If you have files that you think should be marked as 'Missing' (i.e., whose images are no longer available as shown by the displayed graphic), set their 'ImageQuality' field to 'Missing' and then reselect 'Missing' files.";
                        break;
                    case FileSelectionEnum.MarkedForDeletion:
                        messageBox.Message.Problem = "Files marked for deletion were previously selected but no files are currently marked so nothing can be shown.";
                        messageBox.Message.Reason = "No files have their 'Delete?' field checked.";
                        messageBox.Message.Hint = "If you have files you think should be marked for deletion, check their 'Delete?' field and then reselect files marked for deletion.";
                        break;
                    case FileSelectionEnum.Ok:
                        messageBox.Message.Problem = "Ok files were previously selected but no files are currently OK so nothing can be shown.";
                        messageBox.Message.Reason = "No files have their 'ImageQuality' field set to Ok.";
                        messageBox.Message.Hint = "If you have files you think should be marked as 'Ok', set their 'ImageQuality' field to 'Ok' and then reselect Ok files.";
                        break;
                    default:
                        throw new NotSupportedException(String.Format("Unhandled selection {0}.", selection));
                }
                this.StatusBar.SetMessage("Resetting selection to All files.");
                messageBox.ShowDialog();

                selection = FileSelectionEnum.All;
                this.dataHandler.FileDatabase.SelectFiles(selection);
            }

            // Change the selection to reflect what the user selected. Update the menu state accordingly
            // Set the checked status of the radio button menu items to the selection.
            string status;
            switch (selection)
            {
                case FileSelectionEnum.All:
                    status = "All files";
                    break;
                case FileSelectionEnum.Corrupted:
                    status = "Corrupted files";
                    break;
                case FileSelectionEnum.Custom:
                    status = "Custom selection";
                    break;
                case FileSelectionEnum.Dark:
                    status = "Dark files";
                    break;
                case FileSelectionEnum.MarkedForDeletion:
                    status = "Files marked for deletion";
                    break;
                case FileSelectionEnum.Missing:
                    status = "Missing files";
                    break;
                case FileSelectionEnum.Ok:
                    status = "Light and Okay files";
                    break;
                default:
                    throw new NotSupportedException(String.Format("Unhandled file selection {0}.", selection));
            }
            // Show feedback of the status description in both the status bar and the data entry control panel title
            this.StatusBar.SetView(status);
            this.DataEntryControlPanel.Title = "Data entry for " + status;

            // Display the specified file or, if it's no longer selected, the next closest one
            // Showfile() handles empty image sets, so those don't need to be checked for here.
            // After a selection changes, set the slider to represent the index and the count of the current selection
            this.FileNavigatorSlider_EnableOrDisableValueChangedCallback(false);
            this.FileNavigatorSlider.Maximum = this.dataHandler.FileDatabase.CurrentlySelectedFileCount;  // Reset the slider to the size of images in this set
            if (this.FileNavigatorSlider.Maximum <= 50)
            {
                this.FileNavigatorSlider.IsSnapToTickEnabled = true;
                this.FileNavigatorSlider.TickFrequency = 1.0;
            }
            else
            {
                this.FileNavigatorSlider.IsSnapToTickEnabled = false;
                this.FileNavigatorSlider.TickFrequency = 0.02 * this.FileNavigatorSlider.Maximum;
            }

            // SAULXXX Reset the clickable grid selection after every change in the selection
            if (this.IsDisplayingMultipleImagesInOverview())
            {
                this.MarkableCanvas.ClickableImagesGrid.SelectInitialCellOnly();
            }
            //this.ShowFile(this.dataHandler.FileDatabase.GetFileOrNextFileIndex(imageID), forceUpdate);
            // Always force an update after a selection
            this.ShowFile(this.dataHandler.FileDatabase.GetFileOrNextFileIndex(imageID), true);

            // Update the status bar accordingly
            this.StatusBar.SetCurrentFile(this.dataHandler.ImageCache.CurrentRow + 1);  // We add 1 because its a 0-based list
            this.StatusBar.SetCount(this.dataHandler.FileDatabase.CurrentlySelectedFileCount);
            this.FileNavigatorSlider_EnableOrDisableValueChangedCallback(true);
            this.dataHandler.FileDatabase.ImageSet.FileSelection = selection;    // Remember the current selection
        }

        // Check every file to see if:
        // - file exists but ImageQuality is Missing, or
        // - file does not exist but ImageQuality is anything other than missing
        // If there is a mismatch, change the ImageQuality to reflect the Files' actual status.
        // The downfall is that prior ImageQuality information will be lost if a change is made
        // Another issue is that the current version only checks the currently selected files vs. all files
        public bool CheckAndUpdateImageQualityForMissingFiles()
        {
            string filepath = String.Empty;
            string message = String.Empty;
            List<ColumnTuplesWithWhere> imagesToUpdate = new List<ColumnTuplesWithWhere>();
            ColumnTuplesWithWhere imageUpdate;

            // Get all files, regardless of the selection
            FileTable allFiles = this.dataHandler.FileDatabase.GetAllFiles();
            foreach (ImageRow image in allFiles)
            {
                filepath = Path.Combine(this.FolderPath, image.RelativePath, image.FileName);
                if (File.Exists(filepath) && image.ImageQuality == FileSelectionEnum.Missing)
                {
                    // The File exists but image quality is set to missing. Reset it to OK
                    // Note that the file may be corrupt, dark, etc., but we don't check for that.
                    // SAULXXX Perhaps we should?
                    image.ImageQuality = FileSelectionEnum.Ok;
                    imageUpdate = new ColumnTuplesWithWhere(new List<ColumnTuple>() { new ColumnTuple(Constant.DatabaseColumn.ImageQuality, image.ImageQuality.ToString()) }, image.ID);
                    imagesToUpdate.Add(imageUpdate);
                    System.Diagnostics.Debug.Print("Restored " + filepath);
                }
                else if (File.Exists(filepath) == false && image.ImageQuality != FileSelectionEnum.Missing)
                {
                    // The File does not exist anymore, but the image quality is not set to missing. Reset it to Missing
                    // Note that this could lose information,  as the file may be marked as corrupt or dark, etc., but we don't check for that.
                    // SAULXXX Not sure how to fix this, except to separate image quality information into other columns.
                    message = "Missing " + filepath;
                    image.ImageQuality = FileSelectionEnum.Missing;
                    imageUpdate = new ColumnTuplesWithWhere(new List<ColumnTuple>() { new ColumnTuple(Constant.DatabaseColumn.ImageQuality, image.ImageQuality.ToString()) }, image.ID);
                    imagesToUpdate.Add(imageUpdate);
                    System.Diagnostics.Debug.Print("Missing " + filepath);
                }
            }
            if (imagesToUpdate.Count > 0)
            {
                this.dataHandler.FileDatabase.UpdateFiles(imagesToUpdate);
                return true;
            }
            return false;
        }
        #endregion

        #region Control Callbacks
        /// <summary>
        /// Add user interface event handler callbacks for (possibly invisible) controls
        /// </summary>
        private void SetUserInterfaceCallbacks()
        {
            // Add data entry callbacks to all editable controls. When the user changes an image's attribute using a particular control,
            // the callback updates the matching field for that image in the database.
            DataEntryNote date = null;
            DataEntryDateTime dateTime = null;
            DataEntryNote time = null;
            foreach (KeyValuePair<string, DataEntryControl> pair in this.DataEntryControls.ControlsByDataLabel)
            {
                string controlType = this.dataHandler.FileDatabase.FileTableColumnsByDataLabel[pair.Key].ControlType;
                switch (controlType)
                {
                    case Constant.Control.Counter:
                        DataEntryCounter counter = (DataEntryCounter)pair.Value;
                        counter.ContentControl.PreviewMouseDown += this.ContentControl_MouseDown;
                        counter.ContentControl.PreviewKeyDown += this.ContentCtl_PreviewKeyDown;
                        counter.Container.MouseEnter += this.CounterControl_MouseEnter;
                        counter.Container.MouseLeave += this.CounterControl_MouseLeave;
                        counter.LabelControl.Click += this.CounterControl_Click;
                        break;
                    case Constant.Control.Flag:
                    case Constant.DatabaseColumn.DeleteFlag:
                        DataEntryFlag flag = (DataEntryFlag)pair.Value;
                        flag.ContentControl.PreviewMouseDown += this.ContentControl_MouseDown;
                        flag.ContentControl.PreviewKeyDown += this.ContentCtl_PreviewKeyDown;
                        break;
                    case Constant.Control.FixedChoice:
                    case Constant.DatabaseColumn.ImageQuality:
                        DataEntryChoice choice = (DataEntryChoice)pair.Value;
                        choice.ContentControl.PreviewMouseDown += this.ContentControl_MouseDown;
                        choice.ContentControl.PreviewKeyDown += this.ContentCtl_PreviewKeyDown;
                        break;
                    case Constant.Control.Note:
                    case Constant.DatabaseColumn.Date:
                    case Constant.DatabaseColumn.File:
                    case Constant.DatabaseColumn.Folder:
                    case Constant.DatabaseColumn.RelativePath:
                    case Constant.DatabaseColumn.Time:
                        DataEntryNote note = (DataEntryNote)pair.Value;
                        note.ContentControl.PreviewMouseDown += this.ContentControl_MouseDown;
                        note.ContentControl.PreviewKeyDown += this.ContentCtl_PreviewKeyDown;
                        if (controlType == Constant.DatabaseColumn.Date)
                        {
                            date = note;
                        }
                        if (controlType == Constant.DatabaseColumn.Time)
                        {
                            time = note;
                        }
                        break;
                    case Constant.DatabaseColumn.DateTime:
                        dateTime = (DataEntryDateTime)pair.Value;
                        dateTime.ContentControl.PreviewMouseDown += this.ContentControl_MouseDown;
                        dateTime.ContentControl.PreviewKeyDown += this.ContentCtl_PreviewKeyDown;
                        break;
                    case Constant.DatabaseColumn.UtcOffset:
                        DataEntryUtcOffset utcOffset = (DataEntryUtcOffset)pair.Value;
                        utcOffset.ContentControl.PreviewMouseDown += this.ContentControl_MouseDown;
                        utcOffset.ContentControl.PreviewKeyDown += this.ContentCtl_PreviewKeyDown;
                        break;
                    default:
                        Utilities.PrintFailure(String.Format("Unhandled control type '{0}' in SetUserInterfaceCallbacks.", controlType));
                        break;
                }
            }

            // if needed, link date and time controls to datetime control
            if (dateTime != null && date != null)
            {
                dateTime.DateControl = date;
            }
            if (dateTime != null && time != null)
            {
                dateTime.TimeControl = time;
            }
        }

        // This preview callback is used by all controls on receipt of a mouse down, 
        // to ensure the FilePlayer is stopped when the user clicks into it
        private void ContentControl_MouseDown(object sender, RoutedEventArgs e)
        {
            FilePlayer_Stop(); // In case the FilePlayer is going
        }

        /// <summary>
        /// This preview callback is used by all controls to reset the focus.
        /// Whenever the user hits enter over the control, set the focus back to the top-level
        /// </summary>
        /// <param name="sender">source of the event</param>
        /// <param name="eventArgs">event information</param>
        private void ContentCtl_PreviewKeyDown(object sender, KeyEventArgs eventArgs)
        {
            if (eventArgs.Key == Key.Enter)
            {
                this.TrySetKeyboardFocusToMarkableCanvas(false, eventArgs);
                eventArgs.Handled = true;
                FilePlayer_Stop(); // In case the FilePlayer is going
            }
            // The 'empty else' means don't check to see if a textbox or control has the focus, as we want to reset the focus elsewhere
        }

        /// <summary>Preview callback for counters, to ensure that we only accept numbers</summary>
        /// <param name="sender">the event source</param>
        /// <param name="e">event information</param>
        private void CounterCtl_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = (Utilities.IsDigits(e.Text) || String.IsNullOrWhiteSpace(e.Text)) ? false : true;
            this.OnPreviewTextInput(e);
            FilePlayer_Stop(); // In case the FilePlayer is going
        }

        /// <summary>Click callback: When the user selects a counter, refresh the markers, which will also readjust the colors and emphasis</summary>
        /// <param name="sender">the event source</param>
        /// <param name="e">event information</param>
        private void CounterControl_Click(object sender, RoutedEventArgs e)
        {
            this.MarkableCanvas_UpdateMarkers();
            FilePlayer_Stop(); // In case the FilePlayer is going
        }

        /// <summary>Highlight the markers associated with a counter when the mouse enters it</summary>
        private void CounterControl_MouseEnter(object sender, MouseEventArgs e)
        {
            Panel panel = (Panel)sender;
            this.state.MouseOverCounter = ((DataEntryCounter)panel.Tag).DataLabel;
            this.MarkableCanvas_UpdateMarkers();
        }

        /// <summary>Remove marker highlighting associated with a counter when the mouse leaves it</summary>
        private void CounterControl_MouseLeave(object sender, MouseEventArgs e)
        {
            // Recolor the marks
            this.state.MouseOverCounter = null;
            this.MarkableCanvas_UpdateMarkers();
        }

        // Move the focus (usually because of tabbing or shift-tab)
        // It cycles between the data entry controls and the CopyPrevious button 
        private void MoveFocusToNextOrPreviousControlOrCopyPreviousButton(bool moveToPreviousControl)
        {
            // identify the currently selected control
            // if focus is currently set to the canvas this defaults to the first or last control, as appropriate
            int currentControl = moveToPreviousControl ? this.DataEntryControls.Controls.Count : -1;
            Type type;

            IInputElement focusedElement = FocusManager.GetFocusedElement(this);
            if (focusedElement != null)
            {
                type = focusedElement.GetType();

                if (Constant.Control.KeyboardInputTypes.Contains(type))
                {
                    if (DataEntryHandler.TryFindFocusedControl(focusedElement, out DataEntryControl focusedControl))
                    {
                        int index = 0;
                        foreach (DataEntryControl control in this.DataEntryControls.Controls)
                        {
                            if (Object.ReferenceEquals(focusedControl, control))
                            {
                                // We found it, so no need to look further
                                currentControl = index;
                                break;
                            }
                            ++index;
                        }
                    }
                }
            }

            // move to the next or previous control as available
            Func<int, int> incrementOrDecrement;
            if (moveToPreviousControl)
            {
                incrementOrDecrement = (int index) => { return --index; };
            }
            else
            {
                incrementOrDecrement = (int index) => { return ++index; };
            }

            for (currentControl = incrementOrDecrement(currentControl);
                 currentControl > -1 && currentControl < this.DataEntryControls.Controls.Count;
                 currentControl = incrementOrDecrement(currentControl))
            {
                DataEntryControl control = this.DataEntryControls.Controls[currentControl];
                if (control.ContentReadOnly == false)
                {
                    this.lastControlWithFocus = control.Focus(this);
                    return;
                }
            }
            // if we've gone thorugh all the controls and couldn't set the focus, then we must be at the beginning or at the end.
            if (this.CopyPreviousValuesButton.IsEnabled)
            {
                // So set the focus to the Copy PreviousValuesButton, unless it is disabled.
                this.CopyPreviousValuesButton.Focus();
                this.lastControlWithFocus = this.CopyPreviousValuesButton;
                this.CopyPreviousValuesSetEnableStatePreviewsAndGlowsAsNeeded();
            }
            else
            {
                // Skip the CopyPreviousValuesButton, as it is disabled.
                DataEntryControl candidateControl = (moveToPreviousControl) ? this.DataEntryControls.Controls.Last() : this.DataEntryControls.Controls.First();
                if (moveToPreviousControl)
                {
                    // Find the LAST control
                    foreach (DataEntryControl control in this.DataEntryControls.Controls)
                    {
                        if (control.ContentReadOnly == false)
                        {
                            candidateControl = control;
                        }
                    }
                }
                else
                {
                    // Find the FIRST control
                    foreach (DataEntryControl control in this.DataEntryControls.Controls)
                    {
                        if (control.ContentReadOnly == false)
                        {
                            candidateControl = control;
                            break;
                        }
                    }
                }
                if (candidateControl != null)
                {
                    this.lastControlWithFocus = candidateControl.Focus(this);
                }
            }
        }

        // When the Control Grid size changes, reposition the CopyPrevious Button depending on the width/height ratio
        private void ControlGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Height + 150 > e.NewSize.Width) // We include 150, as otherwise it will bounce around as repositioning the button changes the size
            {
                // Place the button at the bottom right of the grid
                Grid.SetRow(this.CopyPreviousValuesButton, 1);
                Grid.SetColumn(this.CopyPreviousValuesButton, 0);
                this.CopyPreviousValuesButton.HorizontalAlignment = HorizontalAlignment.Stretch;
                this.CopyPreviousValuesButton.VerticalAlignment = VerticalAlignment.Top;
                this.CopyPreviousValuesButton.Margin = new Thickness(0, 5, 0, 5);
            }
            else
            {
                // Place the button at the right of the grid
                Grid.SetRow(this.CopyPreviousValuesButton, 0);
                Grid.SetColumn(this.CopyPreviousValuesButton, 1);
                this.CopyPreviousValuesButton.HorizontalAlignment = HorizontalAlignment.Right;
                this.CopyPreviousValuesButton.VerticalAlignment = VerticalAlignment.Stretch;
                this.CopyPreviousValuesButton.Margin = new Thickness(5, 0, 5, 0);
            }
        }
        #endregion

        #region Recent File Sets
        /// <summary>
        /// Update the list of recent databases displayed under File -> Recent Databases.
        /// </summary>
        private void RecentFileSets_Refresh()
        {
            // remove image sets which are no longer present from the most recently used list
            // probably overkill to perform this check on every refresh rather than once at application launch, but it's not particularly expensive
            List<string> invalidPaths = new List<string>();
            foreach (string recentImageSetPath in this.state.MostRecentImageSets)
            {
                if (File.Exists(recentImageSetPath) == false)
                {
                    invalidPaths.Add(recentImageSetPath);
                }
            }

            foreach (string path in invalidPaths)
            {
                bool result = this.state.MostRecentImageSets.TryRemove(path);
                if (!result)
                {
                    Utilities.PrintFailure(String.Format("Removal of image set '{0}' no longer present on disk unexpectedly failed.", path));
                }
            }

            // Enable the menu only when there are items in it and only if the load menu is also enabled (i.e., that we haven't loaded anything yet)
            this.MenuItemRecentImageSets.IsEnabled = this.state.MostRecentImageSets.Count > 0 && this.MenuItemLoadFiles.IsEnabled;
            this.MenuItemRecentImageSets.Items.Clear();

            // add menu items most recently used image sets
            int index = 1;
            foreach (string recentImageSetPath in this.state.MostRecentImageSets)
            {
                // Create a menu item for each path
                MenuItem recentImageSetItem = new MenuItem();
                recentImageSetItem.Click += this.MenuItemRecentImageSet_Click;
                recentImageSetItem.Header = String.Format("_{0} {1}", index++, recentImageSetPath);
                recentImageSetItem.ToolTip = recentImageSetPath;
                this.MenuItemRecentImageSets.Items.Add(recentImageSetItem);
            }
        }
        #endregion

        #region Folder Selection Dialogs
        // Open a dialog where the user selects one or more folders that contain the image set(s)
        private bool ShowFolderSelectionDialog(out IEnumerable<string> folderPaths)
        {
            CommonOpenFileDialog folderSelectionDialog = new CommonOpenFileDialog()
            {
                Title = "Select one or more folders ...",
                DefaultDirectory = this.mostRecentFileAddFolderPath ?? this.FolderPath,
                IsFolderPicker = true,
                Multiselect = true
            };
            folderSelectionDialog.InitialDirectory = folderSelectionDialog.DefaultDirectory;
            folderSelectionDialog.FolderChanging += this.FolderSelectionDialog_FolderChanging;
            if (folderSelectionDialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                folderPaths = folderSelectionDialog.FileNames;

                // remember the parent of the selected folder path to save the user clicks and scrolling in case images from additional 
                // directories are added
                this.mostRecentFileAddFolderPath = Path.GetDirectoryName(folderPaths.First());
                return true;
            }

            folderPaths = null;
            return false;
        }

        /// <summary>
        /// File menu helper function:
        /// </summary>
        private void FolderSelectionDialog_FolderChanging(object sender, CommonFileDialogFolderChangeEventArgs e)
        {
            // require folders to be loaded be either the same folder as the .tdb and .ddb or subfolders of it
            if (e.Folder.StartsWith(this.FolderPath, StringComparison.OrdinalIgnoreCase) == false)
            {
                e.Cancel = true;
            }
        }
        #endregion

        #region Slider Event Handlers and related

        private void FileNavigatorSlider_DragStarted(object sender, DragStartedEventArgs args)
        {
            FilePlayer_Stop(); // In case the FilePlayer is going
            this.timerFileNavigator.Start(); // The timer forces an image display update to the current slider position if the user pauses longer than the timer's interval. 
            this.state.FileNavigatorSliderDragging = true;
        }

        private void FileNavigatorSlider_DragCompleted(object sender, DragCompletedEventArgs args)
        {
            FilePlayer_Stop(); // In case the FilePlayer is going
            this.state.FileNavigatorSliderDragging = false;
            this.ShowFile(this.FileNavigatorSlider);
            this.timerFileNavigator.Stop();
        }


        private void FileNavigatorSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> args)
        {
            // since the minimum value is 1 there's a value change event during InitializeComponent() to ignore
            if (this.state == null)
            {
                return;
            }

            // Stop the timer, but restart it if we are dragging
            this.timerFileNavigator.Stop();
            if (this.state.FileNavigatorSliderDragging == true)
            {
                this.timerFileNavigator.Interval = this.state.Throttles.DesiredIntervalBetweenRenders; // Throttle values may have changed, so we reset it just in case.
                this.timerFileNavigator.Start();
            }

            DateTime utcNow = DateTime.UtcNow;
            if ((this.state.FileNavigatorSliderDragging == false) || (utcNow - this.state.MostRecentDragEvent > this.timerFileNavigator.Interval))
            {
                this.ShowFile(this.FileNavigatorSlider);
                this.state.MostRecentDragEvent = utcNow;
                this.FileNavigatorSlider.AutoToolTipContent = this.dataHandler.ImageCache.Current.FileName;
            }
        }
        private void FileNavigatorSlider_EnableOrDisableValueChangedCallback(bool enableCallback)
        {
            if (enableCallback)
            {
                this.FileNavigatorSlider.ValueChanged += new RoutedPropertyChangedEventHandler<double>(this.FileNavigatorSlider_ValueChanged);
            }
            else
            {
                this.FileNavigatorSlider.ValueChanged -= new RoutedPropertyChangedEventHandler<double>(this.FileNavigatorSlider_ValueChanged);
            }
        }

        // Reset are usually done to disable the FileNavigator when there is no image set to display.
        private void FileNavigatorSliderReset()
        {
            bool filesSelected = (this.IsFileDatabaseAvailable() && this.dataHandler.FileDatabase.CurrentlySelectedFileCount > 0) ? true : false;

            this.timerFileNavigator.Stop();
            this.FileNavigatorSlider_EnableOrDisableValueChangedCallback(filesSelected);
            this.FileNavigatorSlider.IsEnabled = filesSelected;
            this.FileNavigatorSlider.Maximum = filesSelected ? this.dataHandler.FileDatabase.CurrentlySelectedFileCount : 0;
        }

        // Timer callback that forces image update to the current slider position. Invoked as the user pauses dragging the image slider 
        private void TimerFileNavigator_Tick(object sender, EventArgs e)
        {
            this.timerFileNavigator.Stop();
            this.ShowFile(this.FileNavigatorSlider);
            this.FileNavigatorSlider.AutoToolTipContent = this.dataHandler.ImageCache.Current.FileName;

        }

        // Create a semi-transparent visible blue border around the slider when it has the focus. Its semi-transparent to mute it somewhat...
        private void FileNavigatorSlider_GotFocus(object sender, RoutedEventArgs e)
        {
            SolidColorBrush brush = Constant.Control.BorderColorHighlight.Clone();
            brush.Opacity = .5;
            this.AutoToolTipSliderBorder.BorderBrush = brush;
        }

        private void FileNavigatorSlider_LostFocus(object sender, RoutedEventArgs e)
        {
            this.AutoToolTipSliderBorder.BorderBrush = Brushes.Transparent;
        }
        #endregion

        #region DataGridPane activation
        // Update the datagrid (including its binding) to show the currently selected images whenever it is made visible. 
        public void DataGridPane_IsActiveChanged(object sender, EventArgs e)
        {
            // Because switching to the data grid generates a scroll event, we need to ignore it as it will 
            // otherwise turn off the data grid timer
            this.DataGridPane_IsActiveChanged(false);
        }
        public void DataGridPane_IsActiveChanged(bool forceUpdate)
        {
            // Don't update anything if we don't have any files to display
            if (this.dataHandler == null || this.dataHandler.FileDatabase == null)
            {
                this.DataGrid.ItemsSource = null;
                return;
            }

            if (forceUpdate || this.DataGridPane.IsActive || this.DataGridPane.IsFloating || this.DataGridPane.IsVisible)
            {
                this.dataHandler.FileDatabase.BindToDataGrid(this.DataGrid, null);
            }
            this.DataGridSelectionsTimer_Reset();
        }
        #endregion

        #region Showing images
        // ShowFile is invoked here from a 1-based slider, so we need to correct it to the 0-base index
        // By default, don't force the update
        private void ShowFile(Slider fileNavigatorSlider)
        {
            this.ShowFile((int)fileNavigatorSlider.Value - 1, true, false);
        }

        // ShowFile is invoked from elsewhere than from the slider. 
        // By default, don't force the update
        private void ShowFile(int fileIndex)
        {
            this.ShowFile(fileIndex, false, false);
        }

        // ShowFile is invoked from elsewhere than from the slider. 
        // The argument specifies whether we should force the update
        private void ShowFile(int fileIndex, bool forceUpdate)
        {
            this.ShowFile(fileIndex, false, forceUpdate);
        }

        // Show the image in the specified row, but only if its a different image.
        private void ShowFile(int fileIndex, bool isInSliderNavigation, bool forceUpdate)
        {
            // If there is no image set open, or if there is no image to show, then show an image indicating the empty image set.
            if (this.IsFileDatabaseAvailable() == false || this.dataHandler.FileDatabase.CurrentlySelectedFileCount < 1)
            {
                this.MarkableCanvas.SetNewImage(Constant.ImageValues.NoFilesAvailable.Value, null);
                this.markersOnCurrentFile = null;
                this.MarkableCanvas_UpdateMarkers();
                this.MarkableCanvas.SwitchToImageView();

                // We could invalidate the cache here, but it will be reset anyways when images are loaded. 
                if (this.dataHandler != null)
                {
                    this.dataHandler.IsProgrammaticControlUpdate = false;
                }

                // We also need to do a bit of cleanup of UI elements that make no sense when there are no images to show.
                this.QuickPasteWindowHide();
                return;
            }

            // If we are already showing the desired file, and if we are not forcing an update, 
            // then abort as there is no need to redisplay the image.
            if (this.dataHandler.ImageCache.CurrentRow == fileIndex && forceUpdate == false)
            {
                return;
            }

            // Reset the Clickable Images Grid to the current image
            // SAULXX: COULD SET FOLDER PATH AND FILEDATABASE ON LOAD, BUT MAY BE BETTER TO JUST KEEP ON DOING IT HERE
            this.MarkableCanvas.ClickableImagesGrid.FolderPath = this.FolderPath;
            this.MarkableCanvas.ClickableImagesGrid.FileTableStartIndex = fileIndex;
            this.MarkableCanvas.ClickableImagesGrid.FileTable = this.dataHandler.FileDatabase.Files;


            // for the bitmap caching logic below to work this should be the only place where code in TimelapseWindow moves the image enumerator
            if (this.dataHandler.ImageCache.TryMoveToFile(fileIndex, forceUpdate, out bool newFileToDisplay) == false)
            {
                if (this.dataHandler != null)
                { 
                    this.dataHandler.IsProgrammaticControlUpdate = false;
                }
                throw new Exception(String.Format("in ShowFile: possible problem with fileIndex, where its not a valid row index in the image table.", fileIndex));
            }

            // Update each control with the data for the now current image
            // This is always done as it's assumed either the image changed or that a control refresh is required due to database changes
            // the call to TryMoveToImage() above refreshes the data stored under this.dataHandler.ImageCache.Current.
            this.dataHandler.IsProgrammaticControlUpdate = true;
            foreach (KeyValuePair<string, DataEntryControl> control in this.DataEntryControls.ControlsByDataLabel)
            {
                // update value
                string controlType = this.dataHandler.FileDatabase.FileTableColumnsByDataLabel[control.Key].ControlType;
                control.Value.SetContentAndTooltip(this.dataHandler.ImageCache.Current.GetValueDisplayString(control.Value.DataLabel));

                // for note controls, update the autocomplete list if an edit occurred
                if (controlType == Constant.Control.Note)
                {
                    DataEntryNote noteControl = (DataEntryNote)control.Value;
                    if (noteControl.ContentChanged)
                    {
                        noteControl.ContentControl.Autocompletions = this.dataHandler.FileDatabase.GetDistinctValuesInFileDataColumn(control.Value.DataLabel);
                        noteControl.ContentChanged = false;
                    }
                }
            }
            this.dataHandler.IsProgrammaticControlUpdate = false;

            // update the status bar to show which image we are on out of the total displayed under the current selection
            // the total is always refreshed as it's not known if ShowFile() is being called due to a change in the selection
            this.StatusBar.SetCurrentFile(fileIndex + 1); // Add one because indexes are 0-based
            this.StatusBar.SetCount(this.dataHandler.FileDatabase.CurrentlySelectedFileCount);
            this.StatusBar.ClearMessage();

            this.FileNavigatorSlider.Value = fileIndex + 1;

            // display new file if the file changed
            // this avoids unnecessary image reloads and refreshes in cases where ShowFile() is just being called to refresh controls
            this.markersOnCurrentFile = this.dataHandler.FileDatabase.GetMarkersOnFile(this.dataHandler.ImageCache.Current.ID);
            List<Marker> displayMarkers = this.GetDisplayMarkers();

            if (newFileToDisplay)
            {
                if (this.dataHandler.ImageCache.Current.IsVideo)
                {
                    this.MarkableCanvas.SetNewVideo(this.dataHandler.ImageCache.Current.GetFileInfo(this.dataHandler.FileDatabase.FolderPath), displayMarkers);
                    this.EnableImageManipulationMenus(false);

                }
                else
                {
                    this.MarkableCanvas.SetNewImage(this.dataHandler.ImageCache.GetCurrentImage(), displayMarkers);
                    // Draw markers for this file
                    this.MarkableCanvas_UpdateMarkers();
                    this.EnableImageManipulationMenus(true);
                }
            }
            else if (this.IsDisplayingSingleImage())
            {
                if (this.dataHandler.ImageCache.Current.IsVideo)
                {
                    this.MarkableCanvas.SwitchToVideoView();
                }
                else
                {
                    this.MarkableCanvas.SwitchToImageView();
                    this.MarkableCanvas_UpdateMarkers();
                }
            }
            this.DataGridSelectionsTimer_Reset();

            // Set the file player status
            if (this.dataHandler.ImageCache.CurrentRow == 0)
            {
                this.FilePlayer.BackwardsControlsEnabled(false);
            }
            else
            {
                this.FilePlayer.BackwardsControlsEnabled(true);
            }

            if (this.dataHandler.ImageCache.CurrentRow == this.dataHandler.FileDatabase.CurrentlySelectedFileCount - 1)
            {
                this.FilePlayer.ForwardsControlsEnabled(false);
            }
            else
            {
                this.FilePlayer.ForwardsControlsEnabled(true);
            }

            // Refresh the CopyPreviousButton and its Previews as needed
            this.CopyPreviousValuesSetEnableStatePreviewsAndGlowsAsNeeded();

            // Refresh the QuickPasteEntry previews if needed
            if (this.IsDisplayingSingleImage() && this.quickPasteWindow != null)
            { 
                this.quickPasteWindow.RefreshQuickPasteWindowPreviewAsNeeded();
            }

            // Refresh the markable canvas if needed
            this.MarkableCanvas.RefreshIfMultipleImagesAreDisplayed(isInSliderNavigation, forceUpdate);
        }

        private bool TryShowImageWithoutSliderCallback(bool forward, ModifierKeys modifiers)
        {
            // Check to see if there are any images to show, 
            if (this.dataHandler.FileDatabase.CurrentlySelectedFileCount <= 0)
            {
                return false;
            }
            // determine how far to move and in which direction
            int increment = 1;
            if ((modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
            {
                increment *= 5;
            }
            if ((modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                increment *= 10;
            }
            return TryShowImageWithoutSliderCallback(forward, increment);
        }

        private bool TryShowImageWithoutSliderCallback(bool forward, int increment)
        {
            // Check to see if there are any images to show, 
            if (this.dataHandler.FileDatabase.CurrentlySelectedFileCount <= 0)
            {
                return false;
            }

            int desiredRow = forward
                ? this.dataHandler.ImageCache.CurrentRow + increment
                : this.dataHandler.ImageCache.CurrentRow - increment;

            // Set the desiredRow to either the maximum or minimum row if it exceeds the bounds,
            if (desiredRow >= this.dataHandler.FileDatabase.CurrentlySelectedFileCount)
            {
                desiredRow = this.dataHandler.FileDatabase.CurrentlySelectedFileCount - 1;
            }
            else if (desiredRow < 0)
            {
                desiredRow = 0;
            }

            // If the desired row is the same as the current row, the image is already being displayed
            if (desiredRow != this.dataHandler.ImageCache.CurrentRow)
            {
                // Move to the desired row
                this.FileNavigatorSlider_EnableOrDisableValueChangedCallback(false);
                this.ShowFile(desiredRow);
                this.FileNavigatorSlider_EnableOrDisableValueChangedCallback(true);
            }
            return true;
        }
        #endregion

        #region Keyboard shortcuts
        // If its an arrow key and the textbox doesn't have the focus,
        // navigate left/right image or up/down to look at differenced image
        private void Window_PreviewKeyDown(object sender, KeyEventArgs currentKey)
        {
            if (this.dataHandler == null ||
                this.dataHandler.FileDatabase == null ||
                this.dataHandler.FileDatabase.CurrentlySelectedFileCount == 0)
            {
                // SAULXXX: BUG - this only works when the datagrid pane is in a tab, and when files are loaded.
                // Perhaps we need to change the enable state?
                switch (currentKey.Key)
                {
                    case Key.Home:
                        this.ImageSetPane.IsEnabled = true;
                        this.ImageSetPane.IsSelected = true;
                        break;
                    case Key.End:
                        this.DataGridPane.IsEnabled = true;
                        this.DataGridPane.IsSelected = true;
                        // SAULXXX: If its floating, we should really be making it topmost
                        // To do that, we would have to iterate through the floating windows and set it.
                        //if (this.DataGridPane.IsFloating)
                        //{

                        //}
                        break;
                    default:
                        break;
                }
                return; // No images are loaded, so don't try to interpret any keys
            }

            // Don't interpret keyboard shortcuts if the focus is on a control in the control grid, as the text entered may be directed
            // to the controls within it. That is, if a textbox or combo box has the focus, then take no as this is normal text input
            // and NOT a shortcut key.  Similarly, if a menu is displayed keys should be directed to the menu rather than interpreted as
            // shortcuts.
            if (this.SendKeyToDataEntryControlOrMenu(currentKey))
            {
                return;
            }

            // Interpret key as a possible shortcut key. 
            // Depending on the key, take the appropriate action
            int keyRepeatCount = this.state.GetKeyRepeatCount(currentKey);
            switch (currentKey.Key)
            {
                case Key.B:                 // Bookmark (Save) the current pan / zoom level of the image
                    this.MarkableCanvas.SetBookmark();
                    break;
                case Key.Escape:
                    this.TrySetKeyboardFocusToMarkableCanvas(false, currentKey);
                    break;
                case Key.OemPlus:           // Restore the zoom level / pan coordinates of the bookmark
                    this.MarkableCanvas.ApplyBookmark();
                    break;
                case Key.OemMinus:          // Restore the zoom level / pan coordinates of the bookmark
                    this.MarkableCanvas.ZoomOutAllTheWay();
                    break;
                case Key.M:                 // Toggle the magnifying glass on and off
                    this.MenuItemDisplayMagnifyingGlass_Click(this, null);
                    break;
                case Key.U:                 // Increase the magnifing glass zoom level
                    FilePlayer_Stop();      // In case the FilePlayer is going
                    this.MarkableCanvas.MagnifierZoomIn();
                    break;
                case Key.D:                 // Decrease the magnifing glass zoom level
                    FilePlayer_Stop();      // In case the FilePlayer is going
                    this.MarkableCanvas.MagnifierZoomOut();
                    break;
                case Key.Right:             // next image
                    FilePlayer_Stop();      // In case the FilePlayer is going
                    if (keyRepeatCount % this.state.Throttles.RepeatedKeyAcceptanceInterval == 0)
                    {
                        this.TryShowImageWithoutSliderCallback(true, Keyboard.Modifiers);
                    }
                    break;
                case Key.Left:              // previous image
                    FilePlayer_Stop();      // In case the FilePlayer is going
                    if (keyRepeatCount % this.state.Throttles.RepeatedKeyAcceptanceInterval == 0)
                    {
                        this.TryShowImageWithoutSliderCallback(false, Keyboard.Modifiers);
                    }
                    break;
                case Key.Up:                // show visual difference to next image
                    if (IsDisplayingMultipleImagesInOverview())
                    {
                        this.FilePlayer.Direction = FilePlayerDirectionEnum.Backward;
                        this.FilePlayer_ScrollRow();
                    }
                    else
                    {
                        FilePlayer_Stop(); // In case the FilePlayer is going
                        this.TryViewPreviousOrNextDifference();
                    }
                    break;
                case Key.Down:              // show visual difference to previous image
                    if (IsDisplayingMultipleImagesInOverview())
                    {
                        this.FilePlayer.Direction = FilePlayerDirectionEnum.Forward;
                        this.FilePlayer_ScrollRow();
                    }
                    else
                    {
                        FilePlayer_Stop(); // In case the FilePlayer is going
                        this.TryViewCombinedDifference();
                    }
                    break;
                case Key.C:
                    this.CopyPreviousValues_Click();
                    break;
                case Key.Q:
                    // Toggle the QuickPaste window
                    if (this.quickPasteWindow == null || (this.quickPasteWindow.Visibility != Visibility.Visible))
                    {
                        this.QuickPasteWindowShow();
                    }
                    else
                    {
                        this.QuickPasteWindowHide();
                    }
                    break;
                case Key.Tab:
                    FilePlayer_Stop(); // In case the FilePlayer is going
                    this.MoveFocusToNextOrPreviousControlOrCopyPreviousButton(Keyboard.Modifiers == ModifierKeys.Shift);
                    break;
                case Key.PageDown:
                    if (IsDisplayingMultipleImagesInOverview())
                    {
                        this.FilePlayer.Direction = FilePlayerDirectionEnum.Forward;
                        this.FilePlayer_ScrollPage();
                    }
                    break;
                case Key.PageUp:
                    if (IsDisplayingMultipleImagesInOverview())
                    {
                        this.FilePlayer.Direction = FilePlayerDirectionEnum.Backward;
                        this.FilePlayer_ScrollPage();
                    }
                    break;
                case Key.Home:
                    {
                        this.ImageSetPane.IsActive = true;
                        break;
                    }
                case Key.End:
                    {
                        this.DataGridPane.IsActive = true;
                        break;
                    }
                default:
                    return;
            }
            currentKey.Handled = true;
        }
        #endregion

        #region Setting Focus
        // Because of shortcut keys, we want to reset the focus when appropriate to the 
        // image control. This is done from various places.

        // Whenever the user clicks on the image, reset the image focus to the image control 
        private void MarkableCanvas_PreviewMouseDown(object sender, MouseButtonEventArgs eventArgs)
        {
            FilePlayer_Stop(); // In case the FilePlayer is going
            this.TrySetKeyboardFocusToMarkableCanvas(true, eventArgs);
        }


        // When we move over the canvas and the user isn't in the midst of typing into a text field, reset the top level focus
        private void MarkableCanvas_MouseEnter(object sender, MouseEventArgs eventArgs)
        {
            IInputElement focusedElement = FocusManager.GetFocusedElement(this);
            if ((focusedElement == null) || (focusedElement is TextBox == false))
            {
                this.TrySetKeyboardFocusToMarkableCanvas(true, eventArgs);
            }
        }

        // Save/restore the focus whenever we leave / enter the controls or the file navigator
        private void FocusSaveOn_MouseLeave(object sender, MouseEventArgs e)
        {
            IInputElement focusedElement = FocusManager.GetFocusedElement(this);
            if (focusedElement == null || 
                focusedElement is Timelapse.Images.MarkableCanvas ||
                focusedElement is System.Windows.Controls.TabItem )
            {
                // We only want to save the focus on controls
                //string message = (lastControlWithFocus == null) ? "Leave: No control has focus" : "Leave: " + lastControlWithFocus.GetType().ToString();
                //Debug.Print(message);
                //return;
            }
            lastControlWithFocus = focusedElement;
           // Debug.Print("Leave: " + lastControlWithFocus.GetType().ToString());
        }

        private void FocusRestoreOn_MouseEnter(object sender, MouseEventArgs e)
        {
            if (lastControlWithFocus != null && lastControlWithFocus.IsEnabled == true)
            {
                Keyboard.Focus(lastControlWithFocus);
                this.CopyPreviousValuesSetEnableStatePreviewsAndGlowsAsNeeded();
            }
        }

        // Actually set the top level keyboard focus to the image control
        private void TrySetKeyboardFocusToMarkableCanvas(bool checkForControlFocus, InputEventArgs eventArgs)
        {
            // Ensures that a floating window does not go behind the main window 
            this.DockingManager_FloatingDataEntryWindowWindowTopmost(true);

            // If the text box or combobox has the focus, we usually don't want to reset the focus. 
            // However, there are a few instances (e.g., after enter has been pressed) where we no longer want it 
            // to have the focus, so we allow for that via this flag.
            if (checkForControlFocus && eventArgs is KeyEventArgs)
            {
                // If we are in a data control, don't reset the focus.
                if (this.SendKeyToDataEntryControlOrMenu((KeyEventArgs)eventArgs))
                {
                    return;
                }
            }

            // Don't raise the window just because we set the keyboard focus to it
            Keyboard.DefaultRestoreFocusMode = RestoreFocusMode.None;
            Keyboard.Focus(this.MarkableCanvas);
        }

        // Return true if the current focus is in a textbox or combobox data control
        private bool SendKeyToDataEntryControlOrMenu(KeyEventArgs eventData)
        {
            // check if a menu is open
            // it is sufficient to check one always visible item from each top level menu (file, edit, etc.)
            // NOTE: this must be kept in sync with the menu definitions in XAML
            if (this.MenuItemExit.IsVisible || // file menu
                this.MenuItemCopyPreviousValues.IsVisible || // edit menu
                this.MenuItemAudioFeedback.IsVisible ||   // options menu
                this.MenuItemViewNextImage.IsVisible ||   // view menu
                this.MenuItemSelectAllFiles.IsVisible ||  // selection menu
                this.MenuItemAbout.IsVisible)             // help menu
            {
                return true;
            }

            // by default focus will be on the MarkableCanvas
            // opening a menu doesn't change the focus
            IInputElement focusedElement = FocusManager.GetFocusedElement(this);
            if (focusedElement == null)
            {
                return false;
            }

            // check if focus is on a control
            // NOTE: this list must be kept in sync with the System.Windows classes used by the classes in Timelapse\Util\DataEntry*.cs
            Type type = focusedElement.GetType();
            if (Constant.Control.KeyboardInputTypes.Contains(type))
            {
                // send all keys to controls by default except
                // - escape as that's a natural way to back out of a control (the user can also hit enter)
                // - tab as that's the Windows keyboard navigation standard for moving between controls
                FilePlayer_Stop(); // In case the FilePlayer is going
                return eventData.Key != Key.Escape && eventData.Key != Key.Tab;
            }

            return false;
        }
        #endregion


        #region UTC control
        private bool IsUTCOffsetControlHidden()
        {
            // Find the Utcoffset control visibility
            foreach (ControlRow control in this.templateDatabase.Controls)
            {
                string controlType = control.Type;
                if (controlType == Constant.DatabaseColumn.UtcOffset)
                {
                    return !control.Visible;
                }
            }
            return false;
        }

        private bool IsUTCOffsetInDatabase()
        {
            // Find the Utcoffset control
            foreach (ControlRow control in this.templateDatabase.Controls)
            {
                string controlType = control.Type;
                if (controlType == Constant.DatabaseColumn.UtcOffset)
                {
                    return true;
                }
            }
            return false;
        }

        private bool IsUTCOffsetVisible()
        {
            // Find the Utcoffset control
            foreach (ControlRow control in this.templateDatabase.Controls)
            {
                string controlType = control.Type;
                if (controlType == Constant.DatabaseColumn.UtcOffset)
                {
                    return control.Visible;
                }
            }
            return false;
        }
        #endregion

       
        #region FilePlayer and FilePlayerTimer
        // The user has clicked on the file player. Take action onwhat was requested
        private void FilePlayer_FilePlayerChange(object sender, FilePlayerEventArgs args)
        {
            switch (args.Selection)
            {
                case FilePlayerSelectionEnum.First:
                    FilePlayer_Stop();
                    FileNavigatorSlider.Value = 1;
                    break;
                case FilePlayerSelectionEnum.Page:
                    this.FilePlayer_ScrollPage();
                    break;
                case FilePlayerSelectionEnum.Row:
                    this.FilePlayer_ScrollRow();
                    break;
                case FilePlayerSelectionEnum.Last:
                    FilePlayer_Stop();
                    FileNavigatorSlider.Value = this.dataHandler.FileDatabase.CurrentlySelectedFileCount;
                    break;
                case FilePlayerSelectionEnum.Step:
                    FilePlayer_Stop();
                    FilePlayerTimer_Tick(null, null);
                    break;
                case FilePlayerSelectionEnum.PlayFast:
                    //FilePlayer_Play(Constant.FilePlayerValues.PlayFastDefault);
                    FilePlayer_Play(TimeSpan.FromSeconds(this.state.FilePlayerFastValue));
                    break;
                case FilePlayerSelectionEnum.PlaySlow:
                    //FilePlayer_Play(Constant.FilePlayerValues.PlaySlowDefault);
                    FilePlayer_Play(TimeSpan.FromSeconds(this.state.FilePlayerSlowValue));
                    break;
                case FilePlayerSelectionEnum.Stop:
                default:
                    FilePlayer_Stop();
                    break;
            }
        }

        //Stop the timer, reset the timer interval, and then restart the timer 
        private void FilePlayer_Play(TimeSpan timespan)
        {
            this.FilePlayerTimer.Stop();
            this.FilePlayerTimer.Interval = timespan;
            this.FilePlayerTimer.Start();
        }

        // Stop both the file player and the timer
        private void FilePlayer_Stop()
        {
            this.FilePlayerTimer.Stop();
            this.FilePlayer.Stop();
        }

        // Scroll a row of images the ClickableImaesGrid
        private void FilePlayer_ScrollRow()
        {
            bool direction = (this.FilePlayer.Direction == FilePlayerDirectionEnum.Forward) ? true : false;
            this.TryShowImageWithoutSliderCallback(direction, this.MarkableCanvas.ClickableImagesGrid.ImagesInRow);
        }

        // Scroll a page of images the ClickableImaegsGrid
        private void FilePlayer_ScrollPage()
        {
            bool direction = (this.FilePlayer.Direction == FilePlayerDirectionEnum.Forward) ? true : false;
            this.TryShowImageWithoutSliderCallback(direction, this.MarkableCanvas.ClickableImagesGrid.ImagesInRow * this.MarkableCanvas.ClickableImagesGrid.RowsInGrid);
        }

        // On every tick, try to show the next/previous file as indicated by the direction
        private void FilePlayerTimer_Tick(object sender, EventArgs e)
        {
            bool direction = (this.FilePlayer.Direction == FilePlayerDirectionEnum.Forward) ? true : false;
            this.TryShowImageWithoutSliderCallback(direction, ModifierKeys.None);

            // Stop the timer if the image reaches the beginning or end of the image set
            if ((this.dataHandler.ImageCache.CurrentRow >= this.dataHandler.FileDatabase.CurrentlySelectedFileCount - 1) || (this.dataHandler.ImageCache.CurrentRow <= 0))
            {
                FilePlayer_Stop();
            }
        }
        #endregion

        #region Find Callbacks and Methods

        // Find forward on enter 
        private void FindBoxTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                FindBox_FindImage(true);
            }
        }

        // Depending upon which buttion was pressed, invoked a forwards or backwards find operation 
        private void FindBoxButton_Click(object sender, RoutedEventArgs e)
        {
            Button findButton = sender as Button;
            bool isForward = (findButton == this.FindForwardButton) ? true : false;
            this.FindBox_FindImage(isForward);
        }

        // Close the find box
        private void FindBoxClose_Click(object sender, RoutedEventArgs e)
        {
            FindBoxVisibility(false);
        }

        // Search either forwards or backwards for the image file name specified in the text box
        private void FindBox_FindImage(bool isForward)
        {
            string searchTerm = this.FindBoxTextBox.Text;
            ImageRow row = this.dataHandler.ImageCache.Current;

            int currentIndex = this.dataHandler.FileDatabase.Files.IndexOf(row);
            int foundIndex = this.dataHandler.FileDatabase.FindByFileName(currentIndex, isForward, searchTerm);
            if (foundIndex != -1)
            {
                this.ShowFile(foundIndex);
            }
            else
            {
                // Flash the text field to indicate no result
                if (this.FindResource("ColorAnimationBriefFlash") is Storyboard sb)
                {
                    Storyboard.SetTarget(sb, this.FindBoxTextBox);
                    sb.Begin();
                }
            }
        }

        // Make the find box visible on the display
        private void FindBoxVisibility(bool isVisible)
        {
            // Only make the find box visible if there are files to view
            if (this.FindBox != null && this.IsFileDatabaseAvailable() && this.dataHandler.FileDatabase.CurrentlySelectedFileCount > 0)
            {
                this.FindBox.IsOpen = isVisible;
                this.FindBoxTextBox.Focus();
            }
        }
        #endregion

        #region Single vs Multiple Image View
        // Check to see if we are displaying at least one image in an active image set pane (not in the overview)
        private bool IsDisplayingActiveSingleImage()
        {
            return IsDisplayingSingleImage() && this.ImageSetPane.IsActive;
        }

        // Check to see if we are displaying at least one image (not in the overview), regardless of whether the ImageSetPane is active
        private bool IsDisplayingSingleImage()
        {
            // Always false If we are in the overiew
            if (this.MarkableCanvas.IsClickableImagesGridVisible == true) return false;

            // True only if we are displaying at least one file in an image set
            return this.IsFileDatabaseAvailable() &&
                   this.dataHandler.FileDatabase.CurrentlySelectedFileCount > 0;
        }

        private bool IsDisplayingMultipleImagesInOverview()
        {
            return this.MarkableCanvas.IsClickableImagesGridVisible && this.ImageSetPane.IsActive ? true : false;
        }

        private void SwitchedToClickableImagesGrid()
        {
            this.FilePlayer_Stop();
            this.FilePlayer.SwitchFileMode(false);

            // Refresh the CopyPreviousButton and its Previews as needed
            this.CopyPreviousValuesSetEnableStatePreviewsAndGlowsAsNeeded();
            if (this.quickPasteWindow != null)
            {
                this.quickPasteWindow.IsEnabled = false;
            }
        }

        private void SwitchedToSingleImagesView()
        {
            this.FilePlayer.SwitchFileMode(true);
            this.DataGridSelectionsTimer_Reset();

            // Refresh the CopyPreviousButton and its Previews as needed
            this.CopyPreviousValuesSetEnableStatePreviewsAndGlowsAsNeeded();

            // Enable the quickPasteWindow if it exists
            if (this.quickPasteWindow != null)
            {
                this.quickPasteWindow.IsEnabled = true;
            }
        }

        // If the DoubleClick on the ClickableImagesGrid selected an image or video, display it.
        private void ClickableImagesGrid_DoubleClick(object sender, ClickableImagesGridEventArgs e)
        {
            if (e.ImageRow != null)
            {
                // Switch to either the video or image view as needed
                if (this.dataHandler.ImageCache.Current.IsVideo && this.dataHandler.ImageCache.Current.IsDisplayable())
                {
                    this.MarkableCanvas.SwitchToVideoView();
                }
                else
                {
                    this.MarkableCanvas.SwitchToImageView();
                }
                this.FileNavigatorSlider_EnableOrDisableValueChangedCallback(false);
                this.ShowFile(this.dataHandler.FileDatabase.GetFileOrNextFileIndex(e.ImageRow.ID));
                this.FileNavigatorSlider_EnableOrDisableValueChangedCallback(true);
            }
        }

        #endregion

        #region DataGrid events
        // When a user selects a row in the datagrid, show its corresponding image.
        // Note that because multiple selections are enabled, we show the image of the row that received the mouse-up
        private void DataGrid_RowSelected(object sender, MouseButtonEventArgs e)
        {
            if (sender != null)
            {
                if (sender is DataGridRow row)
                {
                    this.FileNavigatorSlider_EnableOrDisableValueChangedCallback(false);
                    DataRowView rowView = row.Item as DataRowView;
                    long fileID = (long)rowView.Row.ItemArray[0];
                    this.ShowFile(this.dataHandler.FileDatabase.GetFileOrNextFileIndex(fileID));
                    this.FileNavigatorSlider_EnableOrDisableValueChangedCallback(true);

                    // The datagrid isn't floating: Switch from the dataGridPane view to the ImagesetPane view
                    if (!this.DataGridPane.IsFloating)
                    {
                        this.ImageSetPane.IsActive = true;
                    }

                    // Switch the Markable Canvas to either the video or image view as needed
                    if (this.dataHandler.ImageCache.Current.IsVideo && this.dataHandler.ImageCache.Current.IsDisplayable())
                    {
                        this.MarkableCanvas.SwitchToVideoView();
                    }
                    else
                    {
                        this.MarkableCanvas.SwitchToImageView();
                    }
                }
            }
        }

        // This event handler is invoked whenever the user does a selection in the overview.
        // It is used to refresh (and match) what rows are selected in the DataGrid. 
        // However, because user selections can change rapidly (e.g., by dragging within the overview), we throttle the refresh using a timer 
        private void ClickableImagesGrid_SelectionChanged(object sender, ClickableImagesGridEventArgs e)
        {
            this.DataGridSelectionsTimer_Reset();
        }

        // If the DataGrid is visible, refresh it so its selected rows match the selections in the Overview. 
        private void DataGridSelectionsTimer_Tick(object sender, EventArgs e)
        {
            //this.DataGrid.UpdateLayout(); // Doesn't seem to be needed, but just in case...
            List<Tuple<long, int>> IdRowIndex = new List<Tuple<long, int>>();
            if (this.IsDisplayingSingleImage())
            {
                // Only the current row is  selected in the single images view, so just use that.
                int currentRowIndex = this.dataHandler.ImageCache.CurrentRow;
                IdRowIndex.Add(new Tuple<long, int>(this.dataHandler.FileDatabase.Files[currentRowIndex].ID, currentRowIndex));
            }
            else
            {
                // multiple selections are possible in the 
                foreach (int rowIndex in this.MarkableCanvas.ClickableImagesGrid.GetSelected())
                {
                    IdRowIndex.Add(new Tuple<long, int>(this.dataHandler.FileDatabase.Files[rowIndex].ID, rowIndex));
                }
            }
            if (this.DataGrid.Items.Count > 0)
            {
                this.DataGrid.SelectAndScrollIntoView(IdRowIndex);
            }
            //this.DataGrid.UpdateLayout(); // Doesn't seem to be needed, but just in case...
            this.DataGridSelectionsTimer_Reset();
        }

        // Reset the timer, where we start it up again if the datagrid pane is active, floating or visible
        private void DataGridSelectionsTimer_Reset()
        {
            this.DataGridSelectionsTimer.Stop();
            if (this.DataGridPane.IsActive == true || this.DataGridPane.IsFloating == true)
            {
                this.DataGridSelectionsTimer.Start();
            }
        }

        // When we scroll the datagrid, we want to stop the timer updating the selection, 
        // as otherwise it would jump to the selection position
        private void DataGridScrollBar_Scroll(object sender, ScrollChangedEventArgs e)
        {
            // Stop the timer only if we are actually scrolling, i.e., if the scrolbar thumb has changed positions
            if (e.VerticalChange != 0)
            {
                this.DataGridSelectionsTimer.Stop();
            }
        }
        #endregion

        #region Help Document - Drag Drop
        private void HelpDocument_PreviewDrag(object sender, DragEventArgs dragEvent)
        {
            Utilities.OnHelpDocumentPreviewDrag(dragEvent);
        }


        private void HelpDocument_Drop(object sender, DragEventArgs dropEvent)
        {
            if (Utilities.IsSingleTemplateFileDrag(dropEvent, out string templateDatabaseFilePath))
            {
                if (this.TryOpenTemplateAndBeginLoadFoldersAsync(templateDatabaseFilePath, out BackgroundWorker backgroundWorker) == false)
                {
                    this.state.MostRecentImageSets.TryRemove(templateDatabaseFilePath);
                    this.RecentFileSets_Refresh();
                }
                dropEvent.Handled = true;
            }
        }
        #endregion

        #region Utilities
        // Returns whether there is an open file database
        private bool IsFileDatabaseAvailable()
        {
            if (this.dataHandler == null ||
                this.dataHandler.FileDatabase == null)
            {
                return false;
            }
            return true;
        }

        public void MaybeShowFileCountsDialog(bool onFileLoading, Window owner)
        {
            if (onFileLoading && this.state.SuppressFileCountOnImportDialog)
            {
                return;
            }

            Dictionary<FileSelectionEnum, int> counts = this.dataHandler.FileDatabase.GetFileCountsBySelection();
            FileCountsByQuality imageStats = new FileCountsByQuality(counts, owner);
            if (onFileLoading)
            {
                imageStats.Message.Hint = "\u2022 " + imageStats.Message.Hint + Environment.NewLine + "\u2022 If you check don't show this message again this dialog can be turned back on via the Options menu.";
                imageStats.DontShowAgain.Visibility = Visibility.Visible;
            }
            Nullable<bool> result = imageStats.ShowDialog();
            if (onFileLoading && result.HasValue && result.Value && imageStats.DontShowAgain.IsChecked.HasValue)
            {
                this.state.SuppressFileCountOnImportDialog = imageStats.DontShowAgain.IsChecked.Value;
            }
        }

        // Returns the currently active counter control, otherwise null
        private DataEntryCounter FindSelectedCounter()
        {
            foreach (DataEntryControl control in this.DataEntryControls.Controls)
            {
                if (control is DataEntryCounter counter)
                {
                    if (counter.IsSelected)
                    {
                        return counter;
                    }
                }
            }
            return null;
        }

        // Say the given text
        public void Speak(string text)
        {
            if (this.state.AudioFeedback)
            {
                this.speechSynthesizer.SpeakAsyncCancelAll();
                this.speechSynthesizer.SpeakAsync(text);
            }
        }

        // If we are not showing all images, then warn the user and make sure they want to continue.
        private bool MaybePromptToApplyOperationIfPartialSelection(bool userOptedOutOfMessage, string operationDescription, Action<bool> persistOptOut)
        {
            // if showing all images then no need for showing the warning message
            if (userOptedOutOfMessage || this.dataHandler.FileDatabase.ImageSet.FileSelection == FileSelectionEnum.All)
            {
                return true;
            }

            string title = "Apply " + operationDescription + " to this selection?";
            MessageBox messageBox = new MessageBox(title, this, MessageBoxButton.OKCancel);

            messageBox.Message.What = operationDescription + " will be applied only to the subset of images shown by the " + this.dataHandler.FileDatabase.ImageSet.FileSelection + " selection." + Environment.NewLine;
            messageBox.Message.What += "Is this what you want?";

            messageBox.Message.Reason = "You have the following selection on: " + this.dataHandler.FileDatabase.ImageSet.FileSelection + "." + Environment.NewLine;
            messageBox.Message.Reason += "Only data for those images available in this " + this.dataHandler.FileDatabase.ImageSet.FileSelection + " selection will be affected" + Environment.NewLine;
            messageBox.Message.Reason += "Data for images not shown in this " + this.dataHandler.FileDatabase.ImageSet.FileSelection + " selection will be unaffected." + Environment.NewLine;

            messageBox.Message.Solution = "Select " + Environment.NewLine;
            messageBox.Message.Solution += "\u2022 'Ok' for Timelapse to continue to " + operationDescription + Environment.NewLine;
            messageBox.Message.Solution += "\u2022 'Cancel' to abort";

            messageBox.Message.Hint = "This is not an error." + Environment.NewLine;
            messageBox.Message.Hint += "\u2022 We are asking just in case you forgot you had the " + this.dataHandler.FileDatabase.ImageSet.FileSelection + " on. " + Environment.NewLine;
            messageBox.Message.Hint += "\u2022 You can use the 'Select' menu to change to other views." + Environment.NewLine;
            messageBox.Message.Hint += "If you check don't show this message this dialog can be turned back on via the Options menu.";

            messageBox.Message.Icon = MessageBoxImage.Question;
            messageBox.DontShowAgain.Visibility = Visibility.Visible;

            bool proceedWithOperation = (bool)messageBox.ShowDialog();
            if (proceedWithOperation && messageBox.DontShowAgain.IsChecked.HasValue)
            {
                persistOptOut(messageBox.DontShowAgain.IsChecked.Value);
            }
            return proceedWithOperation;
        }
        #endregion
    }
}
