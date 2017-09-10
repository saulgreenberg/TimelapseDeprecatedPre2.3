using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Timelapse.Controls;
using Timelapse.Database;
using Timelapse.Dialog;
using Timelapse.Images;
using Timelapse.Util;
using DialogResult = System.Windows.Forms.DialogResult;
using MessageBox = Timelapse.Dialog.MessageBox;
using SaveFileDialog = System.Windows.Forms.SaveFileDialog;
using Xceed.Wpf.AvalonDock.Layout.Serialization;
using System.Windows.Media.Animation;

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
        private TimelapseState state;                                   // Status information concerning the state of the UI
        private TemplateDatabase templateDatabase;                      // The database that holds the template

        // Timer for periodically updating images as the ImageNavigator slider is being used
        private DispatcherTimer timerFileNavigator;

        // Timer used to AutoPlay images via MediaControl buttons
       DispatcherTimer FilePlayerTimer = new DispatcherTimer { };

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

            // Set the window's title
            this.Title = Constant.MainWindowBaseTitle;

            // Create the speech synthesiser
            this.speechSynthesizer = new SpeechSynthesizer();

            // Recall user's state from prior sessions
            this.state = new TimelapseState();
            this.state.ReadFromRegistry();
            this.MenuItemAudioFeedback.IsChecked = this.state.AudioFeedback;
            this.MenuItemOrderFilesByDateTime.IsChecked = this.state.OrderFilesByDateTime;
            this.MenuItemClassifyDarkImagesWhenLoading.IsChecked = this.state.ClassifyDarkImagesWhenLoading;

            // Populate the most recent image set list
            this.MenuItemRecentFileSets_Refresh();

            // Timer to force the image to update to the current slider position when the user pauses while dragging the  slider 
            this.timerFileNavigator = new DispatcherTimer();
            this.timerFileNavigator.Interval = this.state.Throttles.DesiredIntervalBetweenRenders;
            this.timerFileNavigator.Tick += this.TimerFileNavigator_Tick;

            // Callback to ensure AutoPlay stops when the user clicks on it
            this.FileNavigatorSlider.PreviewMouseDown += this.ContentControl_MouseDown;
            this.FileNavigatorSliderReset();

            // Timer activated / deactivated by Autoplay media control buttons
            FilePlayerTimer.Tick += FilePlayerTimer_Tick;

            // restore the window and its size to its previous location
            this.Top = this.state.TimelapseWindowPosition.Y;
            this.Left = this.state.TimelapseWindowPosition.X;
            this.Height = this.state.TimelapseWindowPosition.Height;
            this.Width = this.state.TimelapseWindowPosition.Width;
            Utilities.TryFitWindowInWorkingArea(this);
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
                VersionClient updater = new VersionClient(this, Constant.ApplicationName, Constant.LatestVersionFilenameXML);
                updater.TryGetAndParseVersion(false);
                this.state.MostRecentCheckForUpdates = DateTime.UtcNow;
            }

            //SAULXX This is where we should restore the layout, but there is a bug in how it is restored.
            //this.DockingManager_RestoreLayout(this.state.AvalonDockSavedLayout);

            // Avalon Dock: Initially hide the Date Entry Control Panel
            // For some reason, it doesn't hide it if visibility is set to false in XAML
            this.DataEntryControlPanel.IsVisible = false;
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
                if (this.dataHandler.FileDatabase.ImageSet.FileSelection == FileSelection.Custom)
                {
                    // don't save custom selections, revert to All 
                    this.dataHandler.FileDatabase.ImageSet.FileSelection = FileSelection.All;
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
            }

            // persist user specific state to the registry
            if (this.Top > -10 && this.Left > -10)
            {
                this.state.TimelapseWindowPosition = new Rect(new Point(this.Left, this.Top), new Size(this.Width, this.Height));
            }
            this.state.TimelapseWindowSize = new Size(this.Width, this.Height);

            //SAULXX This is where we should save the layout
            //this.state.AvalonDockSavedLayout = this.DockingManager_SaveLayout();

            // persist user specific state to the registry
            this.state.WriteToRegistry();
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
            Utilities.ShowExceptionReportingDialog("The template editor needs to close.", e, this);
        }
        #endregion

        #region Image Loading
        private bool TryGetTemplatePath(out string templateDatabasePath)
        {
            // prompt user to select a template
            // default the template selection dialog to the most recently opened database
            string defaultTemplateDatabasePath;
            this.state.MostRecentImageSets.TryGetMostRecent(out defaultTemplateDatabasePath);
            if (Utilities.TryGetFileFromUser("Select a TimelapseTemplate.tdb file, which should be located in the root folder containing your images and videos",
                                             defaultTemplateDatabasePath,
                                             String.Format("Template files (*{0})|*{0}", Constant.File.TemplateDatabaseFileExtension),
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

        /// <summary>
        /// Load the specified database template and then the associated images.
        /// </summary>
        /// <param name="templateDatabasePath">Fully qualified path to the template database file.</param>
        /// <returns>true only if both the template and image database file are loaded (regardless of whether any images were loaded) , false otherwise</returns>
        /// <remarks>This method doesn't particularly need to be public. But making it private imposes substantial complexity in invoking it via PrivateObject
        /// in unit tests.</remarks>
        public bool TryOpenTemplateAndBeginLoadFoldersAsync(string templateDatabasePath, out BackgroundWorker backgroundWorker)
        {
            backgroundWorker = null;
            // Try to create or open the template database
            if (!TemplateDatabase.TryCreateOrOpen(templateDatabasePath, out this.templateDatabase))
            {
                // notify the user the template couldn't be loaded rather than silently doing nothing
                MessageBox messageBox = new MessageBox("Timelapse could not load the template.", this);
                messageBox.Message.Problem = "Timelapse could not load the Template File:" + Environment.NewLine;
                messageBox.Message.Problem += "\u2022 " + templateDatabasePath;
                messageBox.Message.Reason = "The template may be corrupted or somehow otherwise invalid. ";
                messageBox.Message.Solution = "You may have to recreate the template, or use another copy of it (if you have one).";
                messageBox.Message.Result = "Timelapse won't do anything. You can try to select another template file.";
                messageBox.Message.Hint = "See if you can examine the template file in the Timelapse Template Editor.";
                messageBox.Message.Hint += "If you can't, there is likley something wrong with it and you will have to recreate it.";
                messageBox.Message.Icon = MessageBoxImage.Error;
                messageBox.ShowDialog();
                return false;
            }
            // The .tdb templateDatabase should now be loaded

            // Try to get the image database file path 
            // importImages will be true if its a new image database file, (meaning we should later ask the user to try to import some images)
            string fileDatabaseFilePath;
            bool importImages;
            if (this.TrySelectDatabaseFile(templateDatabasePath, out fileDatabaseFilePath, out importImages) == false)
            {
                // No image database file was selected
                return false;
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
                        // if there are any new or missing columns, report them now
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
            // So lets load the database for real. The useTemplateDBTemplate signals whether to use the DDB template or the TDB template.
            FileDatabase fileDatabase = FileDatabase.CreateOrOpen(fileDatabaseFilePath, this.templateDatabase, this.state.OrderFilesByDateTime, this.state.CustomSelectionTermCombiningOperator, templateSyncResults);

            // Generate and render the data entry controls, regardless of whether there are actually any files in the files database.
            this.dataHandler = new DataEntryHandler(fileDatabase);
            this.DataEntryControls.CreateControls(fileDatabase, this.dataHandler);
            this.SetUserInterfaceCallbacks();
            this.MarkableCanvas.DataEntryControls = this.DataEntryControls; // so the markable canvas can access the controls
            this.dataHandler.ClickableImagesGrid = this.MarkableCanvas.ClickableImagesGrid;

            this.Title =  Constant.MainWindowBaseTitle + " (" + Path.GetFileName(fileDatabase.FilePath) +  ")";
            this.state.MostRecentImageSets.SetMostRecent(templateDatabasePath);
            this.MenuItemRecentFileSets_Refresh();

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

                IEnumerable<string> folderPaths;
                if (this.ShowFolderSelectionDialog(out folderPaths))
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
                // Profiling of a 1000 image load on quad core, single 80+MB/s capable SSD shows the following:
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
                TimeZoneInfo imageSetTimeZone = this.dataHandler.FileDatabase.ImageSet.GetTimeZone();
                DateTime previousImageRender = DateTime.UtcNow - this.state.Throttles.DesiredIntervalBetweenRenders;

                // SaulXXX There is a bug in the Parallel.ForEach somewhere when initially loading files, where it may occassionally duplicate an entry and skip a nearby image.
                // It also occassion produces an SQLite Database locked error. 
                // While I have kept the call here in case we eventually track down this bug, I have reverted to foreach
                // Parallel.ForEach(new SequentialPartitioner<FileInfo>(filesToAdd), Utilities.GetParallelOptions(this.state.ClassifyDarkImagesWhenLoading ? 2 : 4), (FileInfo fileInfo) =>
                int filesProcessed = 0;
                foreach (FileInfo fileInfo in filesToAdd)
                {
                    ImageRow file;
                    if (this.dataHandler.FileDatabase.GetOrCreateFile(fileInfo, imageSetTimeZone, out file))
                    {
                        // the database already has an entry for this file so skip it
                        // feedback is displayed, albeit fleetingly unless a large number of images are skipped.
                        folderLoadProgress.BitmapSource = Constant.Images.FileAlreadyLoaded.Value;
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
                            file.ImageQuality = FileSelection.Ok;
                        }
                        else
                        {
                            // Create the bitmap and determine its quality
                            // avoid ImageProperties.LoadImage() here as the create exception needs to surface to set the image quality to corrupt
                            // framework bug: WriteableBitmap.Metadata returns null rather than metatada offered by the underlying BitmapFrame, so 
                            // retain the frame and pass its metadata to TryUseImageTaken().
                            bitmapSource = file.LoadBitmap(this.FolderPath, ImageDisplayIntent.TransientLoading);

                            // Set the ImageQuality to corrupt if the returned bitmap is the corrupt image, otherwise set it to its Ok/Dark setting
                            if (bitmapSource == Constant.Images.Corrupt.Value)
                            {
                                file.ImageQuality = FileSelection.Corrupted;
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
                                    file.ImageQuality = FileSelection.Ok;
                                }
                                else
                                {
                                    while (file.ImageQuality == FileSelection.Corrupted && retries_attempted < MAX_RETRIES)
                                    {
                                        // See what images were retried
                                        Utilities.PrintFailure("Retrying dark image classification : " + retries_attempted.ToString() + " " + fileInfo);
                                        retries_attempted++;
                                        file.ImageQuality = bitmapSource.AsWriteable().GetImageQuality(this.state.DarkPixelThreshold, this.state.DarkPixelRatioThreshold);
                                    }
                                    if (retries_attempted == MAX_RETRIES && file.ImageQuality == FileSelection.Corrupted)
                                    {
                                        // We've reached the maximum number of retires. Give up, and just set the image quality (perhaps incorrectly) to ok
                                        file.ImageQuality = FileSelection.Ok;
                                    }
                                }
                            }
                            // see if the datetime can be updated from the metadata
                            // SAULXXX Note: This fails on video reading when we try to read the dark threshold. Check into it....
                            file.TryReadDateTimeOriginalFromMetadata(this.FolderPath, imageSetTimeZone);
                        }
                    }
                    catch (Exception exception)
                    {
                        // We couldn't manage the image for whatever reason, so mark it as corrupted.
                        Utilities.PrintFailure(String.Format("Load of {0} failed as it's likely corrupted, in TryBeginImageFolderLoadAsync. {1}", file.FileName, exception.ToString()));
                        bitmapSource = Constant.Images.Corrupt.Value;
                        file.ImageQuality = FileSelection.Corrupted;
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
                                folderLoadProgress.BitmapSource = file.LoadBitmap(this.FolderPath, ImageDisplayIntent.TransientLoading);
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
                // Debug.Print("Show: " + folderLoadProgress.CurrentFile.ToString());
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
                    ImportImageSetXmlFile importLegacyXmlDialog = new ImportImageSetXmlFile();
                    importLegacyXmlDialog.Owner = this;
                    bool? dialogResult = importLegacyXmlDialog.ShowDialog();
                    if (dialogResult == true)
                    {
                        ImageDataXml.Read(Path.Combine(this.FolderPath, Constant.File.XmlDataFileName), this.dataHandler.FileDatabase);
                        this.SelectFilesAndShowFile(this.dataHandler.FileDatabase.ImageSet.MostRecentFileID, this.dataHandler.FileDatabase.ImageSet.FileSelection); // to regenerate the controls and markers for this image
                    }
                }
            };

            // update UI for import
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

        private void Feedback(BitmapSource bitmap, int percent, string message)
        {
            if (bitmap != null)
            {
                this.MarkableCanvas.SetNewImage(bitmap, null);
            }
            this.FeedbackControl.Message.Content = message;
            this.FeedbackControl.ProgressBar.Value = percent;
        }

        /// <summary>
        /// When folder loading has completed add callbacks, prepare the UI, set up the image set, and show the image.
        /// </summary>
        private void OnFolderLoadingComplete(bool filesJustAdded)
        {        
            // Show the image, hide the load button, and make the feedback panels visible
            this.ImageSetPane.IsActive = true;
            this.FileNavigatorSlider_EnableOrDisableValueChangedCallback(false);
            this.MarkableCanvas.Focus(); // We start with this having the focus so it can interpret keyboard shortcuts if needed. 

            // Adjust the visibility and enable CopyPreviousValuesButton callbacks, where copyable controls will highlight as one enters the CopyPreviousValuesButton
            this.CopyPreviousValuesButton.Visibility = Visibility.Visible;
            this.DataEntryControlPanel.IsVisible = true;
            this.DataEntryControls.CopyPreviousValuesButton = this.CopyPreviousValuesButton; // so we can disable / enable it as needed

            // Show the File Player
            FilePlayer.Visibility = Visibility.Visible;

            // if this is completion of an existing .ddb open, set the current selection and the image index to the ones from the previous session with the image set
            // also if this is completion of import to a new .ddb
            long mostRecentFileID = this.dataHandler.FileDatabase.ImageSet.MostRecentFileID;
            FileSelection fileSelection = this.dataHandler.FileDatabase.ImageSet.FileSelection;
            if (filesJustAdded && (this.dataHandler.ImageCache.CurrentRow != Constant.Database.InvalidRow && this.dataHandler.ImageCache.CurrentRow != Constant.Database.InvalidRow))
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

            if (this.DataGridPane.IsVisible)
            {
                DataGridPane_IsActiveChanged(null, null);
            }
        }

        private void EnableOrDisableMenusAndControls()
        {
            bool imageSetAvailable = this.IsFileDatabaseAvailable();
            bool filesSelected = (imageSetAvailable && this.dataHandler.FileDatabase.CurrentlySelectedFileCount > 0) ? true : false;

            // Depending upon whether images exist in the data set,
            // enable / disable menus and menu items as needed
            // file menu
            this.MenuItemAddFilesToImageSet.IsEnabled = imageSetAvailable;
            this.MenuItemLoadFiles.IsEnabled = !imageSetAvailable;
            this.MenuItemRecentImageSets.IsEnabled = !imageSetAvailable;
            this.MenuItemExportThisImage.IsEnabled = filesSelected;
            this.MenuItemExportAsCsvAndPreview.IsEnabled = filesSelected;
            this.MenuItemExportAsCsv.IsEnabled = filesSelected;
            this.MenuItemImportFromCsv.IsEnabled = filesSelected;
            this.MenuItemRenameFileDatabaseFile.IsEnabled = filesSelected;
            this.MenuFileCloseImageSet.IsEnabled = imageSetAvailable;
            // edit menu
            this.MenuItemEdit.IsEnabled = filesSelected;
            this.MenuItemDeleteCurrentFile.IsEnabled = filesSelected;
            // view menu
            this.MenuItemView.IsEnabled = filesSelected;
            // select menu
            this.MenuItemSelect.IsEnabled = filesSelected;
            // options menu
            // always enable at top level when an image set exists so that image set advanced options are accessible
            this.MenuItemOptions.IsEnabled = imageSetAvailable;
            this.MenuItemOrderFilesByDateTime.IsEnabled = filesSelected;
            this.MenuItemAdvancedDeleteDuplicates.IsEnabled = filesSelected;
            this.MenuItemAudioFeedback.IsEnabled = filesSelected;
            this.MenuItemMagnifyingGlass.IsEnabled = imageSetAvailable;
            this.MenuItemDisplayMagnifyingGlass.IsChecked = imageSetAvailable && this.dataHandler.FileDatabase.ImageSet.MagnifyingGlassEnabled;
            this.MenuItemImageCounts.IsEnabled = filesSelected;

            this.MenuItemDialogsOnOrOff.IsEnabled = filesSelected;
            this.MenuItemAdvancedTimelapseOptions.IsEnabled = filesSelected;

            // this.MenuItemAdvancedImageSetOptions.IsEnabled = imagesExist; SAULXXX: I don't think we need this anymore, as there is now a date correction option that does this. Remove it from the XAML as well, and delete that dialog?

            // Also adjust the enablement of the various other UI components.
            this.ControlsPanel.IsEnabled = filesSelected;  // If images don't exist, the user shouldn't be allowed to interact with the control tray
            this.CopyPreviousValuesButton.IsEnabled = filesSelected;
            this.FileNavigatorSlider.IsEnabled = filesSelected;
            this.MarkableCanvas.IsEnabled = filesSelected;
            this.MarkableCanvas.MagnifyingGlassEnabled = filesSelected && this.dataHandler.FileDatabase.ImageSet.MagnifyingGlassEnabled;

            if (filesSelected == false)
            {
                this.ShowFile(Constant.Database.InvalidRow);
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
        private void SelectFilesAndShowFile()
        {
            if (this.dataHandler == null || this.dataHandler.FileDatabase == null)
            {
                Utilities.PrintFailure("SelectFilesAndShowFile: Expected a file database to be available.");
            }
            this.SelectFilesAndShowFile(this.dataHandler.FileDatabase.ImageSet.FileSelection);
        }

        private void SelectFilesAndShowFile(FileSelection selection)
        {
            long fileID = Constant.Database.DefaultFileID;
            if (this.dataHandler != null && this.dataHandler.ImageCache != null && this.dataHandler.ImageCache.Current != null)
            {
                fileID = this.dataHandler.ImageCache.Current.ID;
            }
            this.SelectFilesAndShowFile(fileID, selection);
        }
        private void SelectFilesAndShowFile(long imageID, FileSelection selection)
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

            // Select the files according to the given selection
            this.dataHandler.FileDatabase.SelectFiles(selection);

            // explain to user if their selection has gone empty and change to all files
            if ((this.dataHandler.FileDatabase.CurrentlySelectedFileCount < 1) && (selection != FileSelection.All))
            {
                // These cases are reached when 
                // 1) datetime modifications result in no files matching a custom selection
                // 2) all files which match the selection get deleted
                MessageBox messageBox = new MessageBox("Resetting selection to All files (no files currently match the current selection)", this);
                messageBox.Message.Icon = MessageBoxImage.Information;
                messageBox.Message.Result = "The 'All files' selection will be applied, where all files in your image set are displayed.";

                switch (selection)
                {
                    case FileSelection.Corrupted:
                        messageBox.Message.Problem = "Corrupted files were previously selected but no files are currently corrupted, so nothing can be shown.";
                        messageBox.Message.Reason = "No files have their 'ImageQuality' field set to Corrupted.";
                        messageBox.Message.Hint = "If you have files you think should be marked as 'Corrupted', set their 'ImageQuality' field to 'Corrupted' and then reselect corrupted files.";
                        break;

                    case FileSelection.Custom:
                        messageBox.Message.Problem = "No files currently match the custom selection so nothing can be shown.";
                        messageBox.Message.Reason = "No files match the criteria set in the current Custom selection.";
                        messageBox.Message.Hint = "Create a different custom selection and apply it view the matching files.";
                        break;
                    case FileSelection.Dark:
                        messageBox.Message.Problem = "Dark files were previously selected but no files are currently dark so nothing can be shown.";
                        messageBox.Message.Reason = "No files have their 'ImageQuality' field set to Dark.";
                        messageBox.Message.Hint = "If you have files you think should be marked as 'Dark', set their 'ImageQuality' field to 'Dark' and then reselect dark files.";
                        break;
                    case FileSelection.Missing:
                        messageBox.Message.Problem = "Missing files were previously selected. However, none of the files are marked as missing, so nothing can be shown.";
                        messageBox.Message.Reason = "No files have their 'ImageQuality' field set to Missing.";
                        messageBox.Message.Hint = "If you have files that you think should be marked as 'Missing' (i.e., whose images are no longer available as shown by the displayed graphic), set their 'ImageQuality' field to 'Missing' and then reselect 'Missing' files.";
                        break;
                    case FileSelection.MarkedForDeletion:
                        messageBox.Message.Problem = "Files marked for deletion were previously selected but no files are currently marked so nothing can be shown.";
                        messageBox.Message.Reason = "No files have their 'Delete?' field checked.";
                        messageBox.Message.Hint = "If you have files you think should be marked for deletion, check their 'Delete?' field and then reselect files marked for deletion.";
                        break;
                    case FileSelection.Ok:
                        messageBox.Message.Problem = "Ok files were previously selected but no files are currently OK so nothing can be shown.";
                        messageBox.Message.Reason = "No files have their 'ImageQuality' field set to Ok.";
                        messageBox.Message.Hint = "If you have files you think should be marked as 'Ok', set their 'ImageQuality' field to 'Ok' and then reselect Ok files.";
                        break;
                    default:
                    throw new NotSupportedException(String.Format("Unhandled selection {0}.", selection));
                }
                this.StatusBar.SetMessage("Resetting selection to All files.");
                messageBox.ShowDialog();

                selection = FileSelection.All;
                this.dataHandler.FileDatabase.SelectFiles(selection);
            }

            // Change the selection to reflect what the user selected. Update the menu state accordingly
            // Set the checked status of the radio button menu items to the selection.
            string status;
            switch (selection)
            {
                case FileSelection.All:
                    status = "all files";
                    break;
                case FileSelection.Corrupted:
                    status = "corrupted files";
                    break;
                case FileSelection.Custom:
                    status = "files matching your custom selection";
                    break;
                case FileSelection.Dark:
                    status = "dark files";
                    break;
                case FileSelection.MarkedForDeletion:
                    status = "files marked for deletion";
                    break;
                case FileSelection.Missing:
                    status = "missing files no longer available for display";
                    break;
                case FileSelection.Ok:
                    status = "light files";
                    break;
                default:
                    throw new NotSupportedException(String.Format("Unhandled file selection {0}.", selection));
            }
            // Show feedback of the status description in both the status bar and the data entry control panel title
            this.StatusBar.SetView(status);
            this.DataEntryControlPanel.Title = "Data entry for " + status;
            this.MenuItemSelectSetSelection(selection);

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
            this.ShowFile(this.dataHandler.FileDatabase.GetFileOrNextFileIndex(imageID));

            // Update the status bar accordingly
            this.StatusBar.SetCurrentFile(this.dataHandler.ImageCache.CurrentRow + 1);  // We add 1 because its a 0-based list
            this.StatusBar.SetCount(this.dataHandler.FileDatabase.CurrentlySelectedFileCount);
            this.FileNavigatorSlider_EnableOrDisableValueChangedCallback(true);
            this.dataHandler.FileDatabase.ImageSet.FileSelection = selection;    // Remember the current selection
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
                        counter.ContentControl.PreviewTextInput += this.CounterCtl_PreviewTextInput;
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

        private void MoveFocusToNextOrPreviousControlOrImageSlider(bool moveToPreviousControl)
        {
            // identify the currently selected control
            // if focus is currently set to the canvas this defaults to the first or last control, as appropriate
            int currentControl = moveToPreviousControl ? this.DataEntryControls.Controls.Count : -1;

            IInputElement focusedElement = FocusManager.GetFocusedElement(this);
            if (focusedElement != null)
            {
                Type type = focusedElement.GetType();
                if (Constant.Control.KeyboardInputTypes.Contains(type))
                {
                    DataEntryControl focusedControl;
                    if (DataEntryHandler.TryFindFocusedControl(focusedElement, out focusedControl))
                    {
                        int index = 0;
                        foreach (DataEntryControl control in this.DataEntryControls.Controls)
                        {
                            if (Object.ReferenceEquals(focusedControl, control))
                            {
                                currentControl = index;
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
                    control.Focus(this);
                    return;
                }
            }

            // no control was found so set focus to the slider
            // this has also the desirable side effect of binding the controls into both next and previous loops so that keys can be used to cycle
            // continuously through them
            this.FileNavigatorSlider.Focus();
        }

        /// <summary>
        /// When the mouse enters / leaves the copy button, the controls that are copyable will be highlighted. 
        /// </summary>
        private void CopyPreviousValues_MouseEnter(object sender, MouseEventArgs e)
        {
            this.CopyPreviousValuesButton.Background = Constant.Control.CopyableFieldHighlightBrush;
            foreach (KeyValuePair<string, DataEntryControl> pair in this.DataEntryControls.ControlsByDataLabel)
            {
                DataEntryControl control = (DataEntryControl)pair.Value;
                if (control.Copyable)
                {
                    control.Container.Background = Constant.Control.CopyableFieldHighlightBrush;
                }
            }
        }

        /// <summary>
        ///  When the mouse enters / leaves the copy button, the controls that are copyable will be highlighted. 
        /// </summary>
        private void CopyPreviousValues_MouseLeave(object sender, MouseEventArgs e)
        {
            this.CopyPreviousValuesButton.ClearValue(Control.BackgroundProperty);
            foreach (KeyValuePair<string, DataEntryControl> pair in this.DataEntryControls.ControlsByDataLabel)
            {
                DataEntryControl control = (DataEntryControl)pair.Value;
                control.Container.ClearValue(Control.BackgroundProperty);
            }
        }
        private void CopyPreviousValues_Click(object sender, RoutedEventArgs e)
        {
            this.FilePlayer_Stop(); // In case the FilePlayer is going
            int previousRow = this.dataHandler.ImageCache.CurrentRow - 1;
            if (previousRow < 0)
            {
                return; // We are already on the first image, so there is nothing to copy
            }

            foreach (KeyValuePair<string, DataEntryControl> pair in this.DataEntryControls.ControlsByDataLabel)
            {
                DataEntryControl control = pair.Value;
                if (this.dataHandler.FileDatabase.IsControlCopyable(control.DataLabel))
                {
                    control.SetContentAndTooltip(this.dataHandler.FileDatabase.Files[previousRow].GetValueDisplayString(control.DataLabel));
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

        #region Differencing
        // Cycle through the image differences in the order: current, then previous and next differenced images.
        // Create and cache the differenced images.
        private void TryViewPreviousOrNextDifference()
        {
            // Note:  No matter what image we are viewing, the source image should have  been cached before entering this function\
            // If it isn't (or if its a video), abort
            if (this.dataHandler == null ||
                this.dataHandler.ImageCache == null ||
                this.dataHandler.ImageCache.Current == null ||
                this.dataHandler.ImageCache.Current.IsVideo)
            {
                this.StatusBar.SetMessage(String.Format("Differences can't be shown for videos, missing, or corrupt files"));
                return;
            }

            // Go to the next image in the cycle we want to show.
            this.dataHandler.ImageCache.MoveToNextStateInPreviousNextDifferenceCycle();

            // If we are supposed to display the unaltered image, do it and get out of here.
            // The unaltered image will always be cached at this point, so there is no need to check.
            if (this.dataHandler.ImageCache.CurrentDifferenceState == ImageDifference.Unaltered)
            {
                this.MarkableCanvas.SetDisplayImage(this.dataHandler.ImageCache.GetCurrentImage());

                // Check if its a corrupted image
                if (!this.dataHandler.ImageCache.Current.IsDisplayable())
                {
                    // TO DO AS WE MAY HAVE TO GET THE INDEX OF THE NEXT IN CYCLE IMAGE???
                    this.StatusBar.SetMessage(String.Format("Difference can't be shown: the current file is likely missing or corrupted"));
                }
                else
                {
                    this.StatusBar.ClearMessage();
                }
                return;
            }

            // Generate and cache difference image if needed
            if (this.dataHandler.ImageCache.GetCurrentImage() == null)
            {
                ImageDifferenceResult result = this.dataHandler.ImageCache.TryCalculateDifference();
                switch (result)
                {
                    case ImageDifferenceResult.CurrentImageNotAvailable:
                    case ImageDifferenceResult.NextImageNotAvailable:
                    case ImageDifferenceResult.PreviousImageNotAvailable:
                    case ImageDifferenceResult.NotCalculable:
                        this.StatusBar.SetMessage(String.Format("Difference can't be shown: the {0} file is a video, missing, corrupt, or a different size", this.dataHandler.ImageCache.CurrentDifferenceState == ImageDifference.Previous ? "previous" : "next"));
                        return;
                    case ImageDifferenceResult.Success:
                        this.StatusBar.SetMessage(String.Format("Viewing difference from {0} file.", this.dataHandler.ImageCache.CurrentDifferenceState == ImageDifference.Previous ? "previous" : "next"));
                        break;
                    default:
                        throw new NotSupportedException(String.Format("Unhandled difference result {0}.", result));
                }
            }

            // display the differenced image
            // the magnifying glass always displays the original non-diferenced image so ImageToDisplay is updated and ImageToMagnify left unchnaged
            // this allows the user to examine any particular differenced area and see what it really looks like in the non-differenced image. 
            this.MarkableCanvas.SetDisplayImage(this.dataHandler.ImageCache.GetCurrentImage());
            this.StatusBar.SetMessage(String.Format("Viewing difference from {0} file.", this.dataHandler.ImageCache.CurrentDifferenceState == ImageDifference.Previous ? "previous" : "next"));
        }

        private void TryViewCombinedDifference()
        {
            if (this.dataHandler == null ||
                this.dataHandler.ImageCache == null ||
                this.dataHandler.ImageCache.Current == null ||
                this.dataHandler.ImageCache.Current.IsVideo)
            {
                this.StatusBar.SetMessage(String.Format("Combined differences can't be shown for videos, missing, or corrupt files"));
                return;
            }

            // If we are in any state other than the unaltered state, go to the unaltered state, otherwise the combined diff state
            this.dataHandler.ImageCache.MoveToNextStateInCombinedDifferenceCycle();
            if (this.dataHandler.ImageCache.CurrentDifferenceState == ImageDifference.Unaltered)
            {
                this.MarkableCanvas.SetDisplayImage(this.dataHandler.ImageCache.GetCurrentImage());
                this.StatusBar.ClearMessage();
                return;
            }

            // Generate and cache difference image if needed
            if (this.dataHandler.ImageCache.GetCurrentImage() == null)
            {
                ImageDifferenceResult result = this.dataHandler.ImageCache.TryCalculateCombinedDifference(this.state.DifferenceThreshold);
                switch (result)
                {
                    case ImageDifferenceResult.CurrentImageNotAvailable:
                        this.StatusBar.SetMessage("Combined difference can't be shown: the current file is a video, missing, corrupt, or a different size");
                        return;
                    case ImageDifferenceResult.NextImageNotAvailable:
                    case ImageDifferenceResult.NotCalculable:
                    case ImageDifferenceResult.PreviousImageNotAvailable:
                        this.StatusBar.SetMessage(String.Format("Combined differences can't be shown: surrounding files include a video, missing, corrupt, or a different size file"));
                        return;
                    case ImageDifferenceResult.Success:
                        this.StatusBar.SetMessage("Viewing differences from both the next and previous files");
                        break;
                    default:
                        throw new NotSupportedException(String.Format("Unhandled combined difference result {0}.", result));
                }
            }

            // display differenced image
            this.MarkableCanvas.SetDisplayImage(this.dataHandler.ImageCache.GetCurrentImage());
            this.StatusBar.SetMessage("Viewing differences from both the next and previous files");
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

            this.timerFileNavigator.Stop(); // Restart the timer 
            this.timerFileNavigator.Interval = this.state.Throttles.DesiredIntervalBetweenRenders; // Throttle values may have changed, so we reset it just in case.
            this.timerFileNavigator.Start();
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
        #endregion

        #region DataGridPane activation
        // Update the datagrid whenever it is made visible. 
        // SAULXXX: Note that it currently shows the selected item on the single view, but not the selected item(s) on the multiple grid view. This should be fixed.
        // SAULXXX: However, to do this properly we need to somehow know that we were in the clickable grid view before the datagrid was activated. Alternately, we need to save the selected row index(es) somewhere
        private void DataGridPane_IsActiveChanged(object sender, EventArgs e)
        {
            if (this.dataHandler == null || this.dataHandler.FileDatabase == null)
            {
                this.DataGrid.ItemsSource = null;
                return;
            }

            if (this.DataGridPane.IsActive || this.DataGridPane.IsFloating)
            {
                this.dataHandler.FileDatabase.BindToDataGrid(this.DataGrid, null);
                if ((this.dataHandler.ImageCache != null) && (this.dataHandler.ImageCache.CurrentRow != Constant.Database.InvalidRow))
                {
                    // both UpdateLayout() calls are needed to get the data grid to highlight the selected row
                    // This seems related to initial population as the selection highlight updates without calling UpdateLayout() on subsequent calls
                    // to SelectAndScrollIntoView().
                    this.DataGrid.UpdateLayout();
                    this.DataGrid.SelectAndScrollIntoView(this.dataHandler.ImageCache.CurrentRow);
                    this.DataGrid.UpdateLayout();
                }
            }
        }
        #endregion

        #region Showing images
        private void ShowFirstDisplayableImage(int firstRowInSearch)
        {
            int firstImageDisplayable = this.dataHandler.FileDatabase.FindFirstDisplayableImage(firstRowInSearch);
            if (firstImageDisplayable != -1)
            {
                this.ShowFile(firstImageDisplayable);
            }
        }

        // ShowFile is invoked here from a 1-based slider, so we need to correct it to the 0-base index
        private void ShowFile(Slider fileNavigatorSlider)
        {
            this.ShowFile((int)fileNavigatorSlider.Value - 1, true);
        }

        // ShowFile is invoked from elsewhere than from the slider
        private void ShowFile(int fileIndex)
        {
            this.ShowFile(fileIndex, false);
        }

        // Show the image in the specified row
        private void ShowFile(int fileIndex, bool isInSliderNavigation)
        {
            // If there is no image set open, or if there is no image to show, then show an image indicating the empty image set.
            if (this.IsFileDatabaseAvailable() == false || this.dataHandler.FileDatabase.CurrentlySelectedFileCount < 1)
            {
                this.MarkableCanvas.SetNewImage(Constant.Images.NoFilesAvailable.Value, null);
                this.markersOnCurrentFile = null;
                this.MarkableCanvas_UpdateMarkers();

                // We could invalidate the cache here, but it will be reset anyways when images are loaded. 
                return;
            }

            // Reset the Clickable Images Grid to the current image
            // SAULXX: COULD SET FOLDER PATH AND FILEDATABASE ON LOAD, BUT MAY BE BETTER TO JUST KEEP ON DOING IT HERE
            this.MarkableCanvas.ClickableImagesGrid.FolderPath = this.FolderPath;
            this.MarkableCanvas.ClickableImagesGrid.FileTableStartIndex = fileIndex;
            this.MarkableCanvas.ClickableImagesGrid.FileTable = this.dataHandler.FileDatabase.Files;


            // for the bitmap caching logic below to work this should be the only place where code in TimelapseWindow moves the image enumerator
            bool newFileToDisplay;
            if (this.dataHandler.ImageCache.TryMoveToFile(fileIndex, out newFileToDisplay) == false)
            {
                throw new ArgumentOutOfRangeException("newImageRow", String.Format("{0} is not a valid row index in the image table.", fileIndex));
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
            List<Marker> displayMarkers = this.GetDisplayMarkers(false);

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
            else if (!this.MarkableCanvas.IsClickableImagesGridVisible)
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

            // if the data grid has been bound, set the selected row to the current file and scroll so it's visible
            if (this.DataGrid.Items != null &&
                this.DataGrid.Items.Count > fileIndex &&
                this.DataGrid.SelectedIndex != fileIndex)
            {
                this.DataGrid.SelectAndScrollIntoView(fileIndex);
            }

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
            this.MarkableCanvas.RefreshIfMultipleImagesAreDisplayed(isInSliderNavigation);
        }

        private bool TryShowImageWithoutSliderCallback(bool forward, ModifierKeys modifiers)
        {
            // Check to see if there are any images to show, 
            if (this.dataHandler.FileDatabase.CurrentlySelectedFileCount <= 0)
            {
                return false;
            }
            // determine how far to move and in which direction
            int increment = forward ? 1 : -1;
            if ((modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
            {
                increment *= 5;
            }
            if ((modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                increment *= 10;
            }

            int desiredRow = this.dataHandler.ImageCache.CurrentRow + increment;

            // Set the desiredRow to either the maximum or minimum row if it exceeds the bounds,
            if (desiredRow >= this.dataHandler.FileDatabase.CurrentlySelectedFileCount)
            {
                desiredRow = this.dataHandler.FileDatabase.CurrentlySelectedFileCount - 1;
            }
            else if (desiredRow < 0)
            {
                desiredRow = 0;
            }

            // If the desired row is the same as the current row, the image us already being displayed
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
                    FilePlayer_Stop(); // In case the FilePlayer is going
                    this.TryViewPreviousOrNextDifference();
                    break;
                case Key.Down:              // show visual difference to previous image
                    FilePlayer_Stop(); // In case the FilePlayer is going
                    this.TryViewCombinedDifference();
                    break;
                case Key.C:
                    this.CopyPreviousValues_Click(null, null);
                    break;
                case Key.Tab:
                    FilePlayer_Stop(); // In case the FilePlayer is going
                    this.MoveFocusToNextOrPreviousControlOrImageSlider(Keyboard.Modifiers == ModifierKeys.Shift);
                    break;
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

        // Actually set the top level keyboard focus to the image control
        private void TrySetKeyboardFocusToMarkableCanvas(bool checkForControlFocus, InputEventArgs eventArgs)
        {
            //Ensures that a floating window does not go behind the main window 
            this.DockingManager_FloatingWindowTopmost(true);

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

        #region Marking and Counting
        // Event handler: A marker, as defined in e.Marker, has been either added (if e.IsNew is true) or deleted (if it is false)
        // Depending on which it is, add or delete the tag from the current counter control's list of tags 
        // If its deleted, remove the tag from the current counter control's list of tags
        // Every addition / deletion requires us to:
        // - update the contents of the counter control 
        // - update the data held by the image
        // - update the list of markers held by that counter
        // - regenerate the list of markers used by the markableCanvas
        private void MarkableCanvas_RaiseMarkerEvent(object sender, MarkerEventArgs e)
        {
            if (e.IsNew)
            {
                // A marker has been added
                DataEntryCounter currentCounter = this.FindSelectedCounter(); // No counters are selected, so don't mark anything
                if (currentCounter == null)
                {
                    return;
                }
                this.MarkableCanvas_AddMarker(currentCounter, e.Marker);
                return;
            }
            // An existing marker has been deleted.
            DataEntryCounter counter = (DataEntryCounter)this.DataEntryControls.ControlsByDataLabel[e.Marker.DataLabel];

            // Part 1. Decrement the counter only if there is a number in it
            string oldCounterData = counter.Content;
            string newCounterData = String.Empty;
            if (oldCounterData != String.Empty)
            {
                int count = Convert.ToInt32(oldCounterData);
                count = (count == 0) ? 0 : count - 1;           // Make sure its never negative, which could happen if a person manually enters the count 
                newCounterData = count.ToString();
            }
            if (!newCounterData.Equals(oldCounterData))
            {
                // Don't bother updating if the value hasn't changed (i.e., already at a 0 count)
                // Update the datatable and database with the new counter values
                this.dataHandler.IsProgrammaticControlUpdate = true;
                counter.SetContentAndTooltip(newCounterData);
                this.dataHandler.IsProgrammaticControlUpdate = false;
                this.dataHandler.FileDatabase.UpdateFile(this.dataHandler.ImageCache.Current.ID, counter.DataLabel, newCounterData);
            }

            // Part 2. Remove the marker in memory and from the database
            // Each marker in the countercoords list reperesents a different control. 
            // So just check the first markers's DataLabel in each markersForCounters list to see if it matches the counter's datalabel.
            MarkersForCounter markersForCounter = null;
            foreach (MarkersForCounter markers in this.markersOnCurrentFile)
            {
                // If there are no markers, we don't have to do anything.
                if (markers.Markers.Count == 0)
                {
                    continue;
                }

                // There are no markers associated with this counter
                //if (markers.Markers[0].DataLabel == markers.DataLabel)
                if (markers.Markers[0].DataLabel == e.Marker.DataLabel)
                {
                    // We found the marker counter associated with that control
                    markersForCounter = markers;
                    break;
                }
            }

            // Part 3. Remove the found metatag from the metatagcounter and from the database
            if (markersForCounter != null)
            { 
                    markersForCounter.RemoveMarker(e.Marker);
                    this.Speak(counter.Content); // Speak the current count
                    this.dataHandler.FileDatabase.SetMarkerPositions(this.dataHandler.ImageCache.Current.ID, markersForCounter);
             }
             this.MarkableCanvas_UpdateMarkers(); // Refresh the Markable Canvas, where it will also delete the markers at the same time
        }

        /// <summary>
        /// A new marker associated with a counter control has been created;
        /// Increment the counter controls value, and add the marker to all data structures (including the database)
        /// </summary>
        private void MarkableCanvas_AddMarker(DataEntryCounter counter, Marker marker)
        {
            // Get the Counter Control's contents,  increment its value (as we have added a new marker) 
            // Then update the control's content as well as the database
            // If we can't convert it to an int, assume that someone set the default value to either a non-integer in the template, or that it's a space. In either case, revert it to zero.
            // If we can't convert it to an int, assume that someone set the default value to either a non-integer in the template, or that it's a space. In either case, revert it to zero.
            int count;
            if (Int32.TryParse(counter.Content, out count) == false)
            {
                count = 0; 
            }
            ++count;

            string counterContent = count.ToString();
            this.dataHandler.IsProgrammaticControlUpdate = true;
            this.dataHandler.FileDatabase.UpdateFile(this.dataHandler.ImageCache.Current.ID, counter.DataLabel, counterContent);
            counter.SetContentAndTooltip(counterContent);
            this.dataHandler.IsProgrammaticControlUpdate = false;

            // Find the MarkersForCounters associated with this particular control so we can add a marker to it
            MarkersForCounter markersForCounter = null;
            foreach (MarkersForCounter markers in this.markersOnCurrentFile)
            {
                if (markers.DataLabel == counter.DataLabel)
                {
                    markersForCounter = markers;
                    break;
                }
            }

            // fill in marker information
            marker.ShowLabel = true; // Show the annotation as its created. We will clear it on the next refresh
            marker.LabelShownPreviously = false;
            marker.Brush = Brushes.Red;               // Make it Red (for now)
            marker.DataLabel = counter.DataLabel;
            marker.Tooltip = counter.Label;   // The tooltip will be the counter label plus its data label
            marker.Tooltip += "\n" + counter.DataLabel;
            markersForCounter.AddMarker(marker);

            // update this counter's list of points in the database
            this.dataHandler.FileDatabase.SetMarkerPositions(this.dataHandler.ImageCache.Current.ID, markersForCounter);
            this.MarkableCanvas.Markers = this.GetDisplayMarkers(true);
            this.Speak(counter.Content + " " + counter.Label); // Speak the current count
        }

        // Create a list of markers from those stored in each image's counters, 
        // and then set the markableCanvas's list of markers to that list. We also reset the emphasis for those tags as needed.
        private void MarkableCanvas_UpdateMarkers()
        {
            this.MarkableCanvas.Markers = this.GetDisplayMarkers(false); // By default, we don't show the annotation
        }

        private List<Marker> GetDisplayMarkers(bool showAnnotation)
        {
            // No markers?
            if (this.markersOnCurrentFile == null)
            {
                return null;
            }

            // The markable canvas uses a simple list of markers to decide what to do.
            // So we just create that list here, where we also reset the emphasis of some of the markers
            List<Marker> markers = new List<Marker>();
            DataEntryCounter selectedCounter = this.FindSelectedCounter();
            for (int counter = 0; counter < this.markersOnCurrentFile.Count; counter++)
            {
                MarkersForCounter markersForCounter = this.markersOnCurrentFile[counter];
                DataEntryControl control;
                if (this.DataEntryControls.ControlsByDataLabel.TryGetValue(markersForCounter.DataLabel, out control) == false)
                {
                    // If we can't find the counter, its likely because the control was made invisible in the template,
                    // which means that there is no control associated with the marker. So just don't create the 
                    // markers associated with this control. Note that if the control is later made visible in the template,
                    // the markers will then be shown. 
                    continue;
                }

                // Update the emphasise for each tag to reflect how the user is interacting with tags
                DataEntryCounter currentCounter = (DataEntryCounter)this.DataEntryControls.ControlsByDataLabel[markersForCounter.DataLabel];
                bool emphasize = markersForCounter.DataLabel == this.state.MouseOverCounter;
                foreach (Marker marker in markersForCounter.Markers)
                {
                    // the first time through, show an annotation. Otherwise we clear the flags to hide the annotation.
                    if (marker.ShowLabel && !marker.LabelShownPreviously)
                    {
                        marker.ShowLabel = true;
                        marker.LabelShownPreviously = true;
                    }
                    else
                    {
                        marker.ShowLabel = false;
                    }

                    if (selectedCounter != null && currentCounter.DataLabel == selectedCounter.DataLabel)
                    {
                        marker.Brush = (SolidColorBrush)new BrushConverter().ConvertFromString(Constant.SelectionColour);
                    }
                    else
                    {
                        marker.Brush = (SolidColorBrush)new BrushConverter().ConvertFromString(Constant.StandardColour);
                    }

                    marker.Emphasise = emphasize;
                    marker.Tooltip = currentCounter.Label;
                    markers.Add(marker); // Add the MetaTag in the list 
                }
            }
            return markers;
        }
        #endregion

        #region All Menu Callbacks
        private void MenuItem_SubmenuOpened(object sender, RoutedEventArgs e)
        {
            if (this.IsUTCOffsetControlHidden())
            {
                this.MenuItemSetTimeZone.IsEnabled = false;
            }
        }

        #endregion

        #region File Menu Callbacks and Support Functions
        private void File_SubmenuOpening(object sender, RoutedEventArgs e)
        {
            FilePlayer_Stop(); // In case the FilePlayer is going
            this.MenuItemRecentFileSets_Refresh();
        }

        private void MenuItemAddImagesToImageSet_Click(object sender, RoutedEventArgs e)
        {
            IEnumerable<string> folderPaths;
            if (this.ShowFolderSelectionDialog(out folderPaths))
            {
                BackgroundWorker backgroundWorker;
                this.TryBeginImageFolderLoadAsync(folderPaths, out backgroundWorker);
            }
        }

        /// <summary>Load the images from a folder.</summary>
        private void MenuItemLoadImages_Click(object sender, RoutedEventArgs e)
        {
            string templateDatabasePath;
            if (this.TryGetTemplatePath(out templateDatabasePath))
            {
                BackgroundWorker backgroundWorker;
                this.TryOpenTemplateAndBeginLoadFoldersAsync(templateDatabasePath, out backgroundWorker);
            }     
        }

        /// <summary>Write the .csv file and preview it in excel.</summary>
        private void MenuItemExportCsv_Click(object sender, RoutedEventArgs e)
        {
            if (this.state.SuppressSelectedCsvExportPrompt == false &&
                this.dataHandler.FileDatabase.ImageSet.FileSelection != FileSelection.All)
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
                csvWriter.ExportToCsv(this.dataHandler.FileDatabase, csvFilePath, this.excludeDateTimeAndUTCOffsetWhenExporting);
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

        /// <summary>
        /// Export the current image to the folder selected by the user via a folder browser dialog.
        /// and provide feedback in the status bar if done.
        /// </summary>
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
            SaveFileDialog dialog = new SaveFileDialog();
            dialog.Title = "Export a copy of the currently displayed file";
            dialog.Filter = String.Format("*{0}|*{0}", Path.GetExtension(this.dataHandler.ImageCache.Current.FileName));
            dialog.FileName = sourceFile;
            dialog.OverwritePrompt = true;

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
            string csvFilePath;
            if (Utilities.TryGetFileFromUser("Select a .csv file to merge into the current image set",
                                             Path.Combine(this.dataHandler.FileDatabase.FolderPath, csvFileName),
                                             String.Format("Comma separated value files (*{0})|*{0}", Constant.File.CsvFileExtension),
                                             out csvFilePath) == false)
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
                List<string> importErrors;
                if (csvReader.TryImportFromCsv(csvFilePath, this.dataHandler.FileDatabase, out importErrors) == false)
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
            this.SelectFilesAndShowFile();
            this.StatusBar.SetMessage(".csv file imported.");
        }

        private void MenuItemRecentImageSet_Click(object sender, RoutedEventArgs e)
        {
            string recentDatabasePath = (string)((MenuItem)sender).ToolTip;
            BackgroundWorker backgroundWorker;
            if (this.TryOpenTemplateAndBeginLoadFoldersAsync(recentDatabasePath, out backgroundWorker) == false)
            {
                this.state.MostRecentImageSets.TryRemove(recentDatabasePath);
                this.MenuItemRecentFileSets_Refresh();
            }
        }

        /// <summary>
        /// Update the list of recent databases displayed under File -> Recent Databases.
        /// </summary>
        private void MenuItemRecentFileSets_Refresh()
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

        private void MenuItemRenameFileDatabaseFile_Click(object sender, RoutedEventArgs e)
        {
            RenameFileDatabaseFile renameFileDatabase = new RenameFileDatabaseFile(this.dataHandler.FileDatabase.FileName, this);
            renameFileDatabase.Owner = this;
            bool? result = renameFileDatabase.ShowDialog();
            if (result == true)
            {
                this.dataHandler.FileDatabase.RenameFile(renameFileDatabase.NewFilename);
            }
        }

        // Close the current image set and return to state allowing other image sets to be opened.
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
                    if (this.dataHandler.FileDatabase.ImageSet.FileSelection == FileSelection.Custom)
                    {
                        this.dataHandler.FileDatabase.ImageSet.FileSelection = FileSelection.All;
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
        }

        /// <summary>
        /// Exit Timelapse
        /// </summary>
        private void MenuItemExit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
            Application.Current.Shutdown();
        }

        private bool ShowFolderSelectionDialog(out IEnumerable<string> folderPaths)
        {
            CommonOpenFileDialog folderSelectionDialog = new CommonOpenFileDialog();
            folderSelectionDialog.Title = "Select one or more folders ...";
            folderSelectionDialog.DefaultDirectory = this.mostRecentFileAddFolderPath == null ? this.FolderPath : this.mostRecentFileAddFolderPath;
            folderSelectionDialog.InitialDirectory = folderSelectionDialog.DefaultDirectory;
            folderSelectionDialog.IsFolderPicker = true;
            folderSelectionDialog.Multiselect = true;
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

        private void FolderSelectionDialog_FolderChanging(object sender, CommonFileDialogFolderChangeEventArgs e)
        {
            // require folders to be loaded be either the same folder as the .tdb and .ddb or subfolders of it
            if (e.Folder.StartsWith(this.FolderPath, StringComparison.OrdinalIgnoreCase) == false)
            {
                e.Cancel = true;
            }
        }
        #endregion

        #region Edit Menu Callbacks
        private void Edit_SubmenuOpening(object sender, RoutedEventArgs e)
        {
            FilePlayer_Stop(); // In case the FilePlayer is going
        }

        private void MenuItemFindByFileName_Click(object sender, RoutedEventArgs e)
        {
            this.FindBoxVisibility(true);
        }

        // Populate a data field from metadata (example metadata displayed from the currently selected image)
        private void MenuItemPopulateFieldFromMetadata_Click(object sender, RoutedEventArgs e)
        {
            // If we are not in the selection All view, or if its a corrupt image or deleted image, tell the person. Selecting ok will shift the selection.
            // We want to be on a valid image as otherwise the metadata of interest won't appear
            if (this.dataHandler.ImageCache.Current.IsDisplayable() == false)
            {
                int firstFileDisplayable = this.dataHandler.FileDatabase.GetCurrentOrNextDisplayableFile(this.dataHandler.ImageCache.CurrentRow);
                if (firstFileDisplayable == -1)
                {
                    // There are no displayable images, and thus no metadata to choose from, so abort
                    MessageBox messageBox = new MessageBox("Populate a data field with image metadata of your choosing.", this);
                    messageBox.Message.Problem = "Timelapse can't extract any metadata, as there are no valid displayable file." + Environment.NewLine;
                    messageBox.Message.Reason += "Timelapse must have at least one valid file in order to get its metadata. However, the image files are either missing (not available) or corrupted.";
                    messageBox.Message.Icon = MessageBoxImage.Error;
                    messageBox.ShowDialog();
                    return;
                }
            }

            if (this.MaybePromptToApplyOperationIfPartialSelection(this.state.SuppressSelectedPopulateFieldFromMetadataPrompt, 
                                                               "'Populate a data field with image metadata of your choosing...'", 
                                                               (bool optOut) =>
                                                               {
                                                                   this.state.SuppressSelectedPopulateFieldFromMetadataPrompt = optOut;
                                                               }))
            {
                PopulateFieldWithMetadata populateField = new PopulateFieldWithMetadata(this.dataHandler.FileDatabase, this.dataHandler.ImageCache.Current.GetFilePath(this.FolderPath), this);
                    this.ShowBulkImageEditDialog(populateField);
            }
        }

        /// <summary>Delete the current image by replacing it with a placeholder image, while still making a backup of it</summary>
        private void Delete_SubmenuOpening(object sender, RoutedEventArgs e)
        {
            try
            {
                int deletedImages = this.dataHandler.FileDatabase.GetFileCount(FileSelection.MarkedForDeletion);
                this.MenuItemDeleteFiles.IsEnabled = deletedImages > 0;
                this.MenuItemDeleteFilesAndData.IsEnabled = deletedImages > 0;
                this.MenuItemDeleteCurrentFileAndData.IsEnabled = true;
                this.MenuItemDeleteCurrentFile.IsEnabled = this.dataHandler.ImageCache.Current.IsDisplayable() || this.dataHandler.ImageCache.Current.ImageQuality == FileSelection.Corrupted;
            }
            catch (Exception exception)
            {
                Utilities.PrintFailure(String.Format("Delete submenu failed to open in Delete_SubmenuOpening. {0}", exception.ToString()));

                // This function was blowing up on one user's machine, but not others.
                // I couldn't figure out why, so I just put this fallback in here to catch that unusual case.
                this.MenuItemDeleteFiles.IsEnabled = true;
                this.MenuItemDeleteFilesAndData.IsEnabled = true;
                this.MenuItemDeleteCurrentFile.IsEnabled = true;
                this.MenuItemDeleteCurrentFileAndData.IsEnabled = true;
            }
        }

        /// <summary>Delete all images marked for deletion, and optionally the data associated with those images.
        /// Deleted images are actually moved to a backup folder.</summary>
        private void MenuItemDeleteFiles_Click(object sender, RoutedEventArgs e)
        {
            MenuItem menuItem = sender as MenuItem;

            // This callback is invoked by DeleteImage (which deletes the current image) and DeleteImages (which deletes the images marked by the deletion flag)
            // Thus we need to use two different methods to construct a table containing all the images marked for deletion
            List<ImageRow> imagesToDelete;
            bool deleteCurrentImageOnly;
            bool deleteFilesAndData;
            if (menuItem.Name.Equals(this.MenuItemDeleteFiles.Name) || menuItem.Name.Equals(this.MenuItemDeleteFilesAndData.Name))
            {
                deleteCurrentImageOnly = false;
                deleteFilesAndData = menuItem.Name.Equals(this.MenuItemDeleteFilesAndData.Name);
                // get list of all images marked for deletion in the current seletion
                imagesToDelete = this.dataHandler.FileDatabase.GetFilesMarkedForDeletion().ToList();
                for (int index = imagesToDelete.Count - 1; index >= 0; index--)
                {
                    if (this.dataHandler.FileDatabase.Files.Find(imagesToDelete[index].ID) == null)
                    {
                        imagesToDelete.Remove(imagesToDelete[index]);
                    }
                }
            }
            else
            {
                // Delete current image case. Get the ID of the current image and construct a datatable that contains that image's datarow
                deleteCurrentImageOnly = true;
                deleteFilesAndData = menuItem.Name.Equals(this.MenuItemDeleteCurrentFileAndData.Name);
                imagesToDelete = new List<ImageRow>();
                if (this.dataHandler.ImageCache.Current != null)
                {
                    imagesToDelete.Add(this.dataHandler.ImageCache.Current);
                }
            }

            // If no images are selected for deletion. Warn the user.
            // Note that this should never happen, as the invoking menu item should be disabled (and thus not selectable)
            // if there aren't any images to delete. Still,...
            if (imagesToDelete == null || imagesToDelete.Count < 1)
            {
                MessageBox messageBox = new MessageBox("No files are marked for deletion", this);
                messageBox.Message.Problem = "You are trying to delete files marked for deletion, but no files have their 'Delete?' field checked.";
                messageBox.Message.Hint = "If you have files that you think should be deleted, check their Delete? field.";
                messageBox.Message.Icon = MessageBoxImage.Information;
                messageBox.ShowDialog();
                return;
            }

            DeleteImages deleteImagesDialog = new DeleteImages(this.dataHandler.FileDatabase, imagesToDelete, deleteFilesAndData, deleteCurrentImageOnly, this);
            bool? result = deleteImagesDialog.ShowDialog();
            if (result == true)
            {
                // cache the current ID as the current image may be invalidated
                long currentFileID = this.dataHandler.ImageCache.Current.ID;

                Mouse.OverrideCursor = Cursors.Wait;
                List<ColumnTuplesWithWhere> imagesToUpdate = new List<ColumnTuplesWithWhere>();
                List<long> imageIDsToDropFromDatabase = new List<long>();
                foreach (ImageRow image in imagesToDelete)
                {
                    // invalidate cache so Missing placeholder will be displayed
                    // release any handle open on the file so it can be moved
                    this.dataHandler.ImageCache.TryInvalidate(image.ID);
                    // SAULXXX Note that we should likely pop up a dialog box that displays non-missing files that we can't (for whatever reason) delete
                    // SAULXXX If we can't delete it, we may want to abort changing the various DeleteFlage and ImageQuality values. 
                    // SAULXXX A good way is to put an 'image.ImageFileExists' field in, and then do various tests on that.
                    image.TryMoveFileToDeletedFilesFolder(this.dataHandler.FileDatabase.FolderPath);

                    if (deleteFilesAndData)
                    {
                        // mark the image row for dropping
                        imageIDsToDropFromDatabase.Add(image.ID);
                    }
                    else
                    {
                        // as only the file was deleted, change image quality to FileNoLongerAvailable and clear the delete flag
                        image.DeleteFlag = false;
                        image.ImageQuality = FileSelection.Missing;
                        List<ColumnTuple> columnTuples = new List<ColumnTuple>()
                        {
                            new ColumnTuple(Constant.DatabaseColumn.DeleteFlag, Constant.Boolean.False),
                            new ColumnTuple(Constant.DatabaseColumn.ImageQuality, FileSelection.Missing.ToString())
                        };
                        imagesToUpdate.Add(new ColumnTuplesWithWhere(columnTuples, image.ID));
                    }
                }

                if (deleteFilesAndData)
                {
                    // drop images
                    this.dataHandler.FileDatabase.DeleteFilesAndMarkers(imageIDsToDropFromDatabase);

                    // Reload the file datatable. Then find and show the image closest to the last one shown
                    this.SelectFilesAndShowFile(currentFileID, this.dataHandler.FileDatabase.ImageSet.FileSelection);
                    if (this.dataHandler.FileDatabase.CurrentlySelectedFileCount > 0)
                    {
                        int nextImageRow = this.dataHandler.FileDatabase.GetFileOrNextFileIndex(currentFileID);
                        this.ShowFile(nextImageRow);
                    }
                    else
                    {
                        this.EnableOrDisableMenusAndControls();
                    }
                }
                else
                {
                    // update image properties
                    this.dataHandler.FileDatabase.UpdateFiles(imagesToUpdate);
                    // SAULXXX: Todd's verson didn'thave this next line, which updates the data table. It meant the display wasn't updating to show the missing image quality / unchecked delete flag.
                    // SAULXXX: There is likely a more efficient way to do this - need to check.
                    // SAULXXX: I notice in later versions he has a new way of doing this, so this fix will likely happen then
                    this.SelectFilesAndShowFile(currentFileID, this.dataHandler.FileDatabase.ImageSet.FileSelection);

                    // display the updated properties on the current image
                    int nextImageRow = this.dataHandler.FileDatabase.FindClosestImageRow(currentFileID);
                    this.ShowFile(nextImageRow);
                }
                Mouse.OverrideCursor = null;
            }
        }

        /// <summary>Add some text to the image set log</summary>
        private void MenuItemLog_Click(object sender, RoutedEventArgs e)
        {
            EditLog editImageSetLog = new EditLog(this.dataHandler.FileDatabase.ImageSet.Log, this);
            editImageSetLog.Owner = this;
            bool? result = editImageSetLog.ShowDialog();
            if (result == true)
            {
                this.dataHandler.FileDatabase.ImageSet.Log = editImageSetLog.Log.Text;
                this.dataHandler.FileDatabase.SyncImageSetToDatabase();
            }
        }

        // Various dialogs perform a bulk edit, after which various states have to be refreshed
        // This method shows the dialog and (if a bulk edit is done) refreshes those states.
        private void ShowBulkImageEditDialog(Window dialog)
        {
            dialog.Owner = this;
            long currentFileID = this.dataHandler.ImageCache.Current.ID;
            bool? result = dialog.ShowDialog();
            if (result == true)
            {
                this.SelectFilesAndShowFile();
                // SAULXX: Note that the above operation may remove some of the files from view, as it may no longer fit into the selection. Should we give feedback to the user about this?
            }
        }
        #endregion

        #region Options Menu Callbacks
        private void Options_SubmenuOpening(object sender, RoutedEventArgs e)
        {
            FilePlayer_Stop(); // In case the FilePlayer is going
        }
        /// <summary>Show advanced image set options</summary>
        private void MenuItemOrderFilesByDateTime_Click(object sender, RoutedEventArgs e)
        {
            this.state.OrderFilesByDateTime = !this.state.OrderFilesByDateTime;
            if (this.dataHandler != null && this.dataHandler.FileDatabase != null)
            {
                this.dataHandler.FileDatabase.OrderFilesByDateTime = this.state.OrderFilesByDateTime;
            }
            this.MenuItemOrderFilesByDateTime.IsChecked = this.state.OrderFilesByDateTime;

            // Reselect on the current select settings, which reorders the list to date or name order, depending on the above
            this.SelectFilesAndShowFile();
            string message = (this.state.OrderFilesByDateTime) ? "Files now sorted by date and time" : "Files now sorted by the order they were added to the image set";
            this.StatusBar.SetMessage(message);
        }

        private void MenuItemClassifyDarkImagesWhenLoading_Click(object sender, RoutedEventArgs e)
        {
            DarkImagesClassifyAutomatically darkImagesOptions = new DarkImagesClassifyAutomatically(this.state, this);
            darkImagesOptions.ShowDialog();
            this.MenuItemClassifyDarkImagesWhenLoading.IsChecked = this.state.ClassifyDarkImagesWhenLoading;
        }

        private void MenuItemAdvancedImageSetOptions_Click(object sender, RoutedEventArgs e)
        {
            AdvancedImageSetOptions advancedImageSetOptions = new AdvancedImageSetOptions(this.dataHandler.FileDatabase, this);
            advancedImageSetOptions.ShowDialog();
        }

        /// <summary>Show advanced Timelapse options</summary>
        private void MenuItemAdvancedTimelapseOptions_Click(object sender, RoutedEventArgs e)
        {
            AdvancedTimelapseOptions advancedTimelapseOptions = new AdvancedTimelapseOptions(this.state, this.MarkableCanvas, this);
            advancedTimelapseOptions.ShowDialog();
        }

        // SaulXXX This is a temporary function to allow a user to check for and to delete any duplicate records.
        private void MenuItemDeleteDuplicates_Click(object sender, RoutedEventArgs e)
        {
            // Warn user that they are in a selected view, and verify that they want to continue
            if (this.dataHandler.FileDatabase.ImageSet.FileSelection != FileSelection.All)
            {
                // Need to be viewing all files
                MessageBox messageBox = new MessageBox("You need to select All Files before deleting duplicates", this);
                messageBox.Message.Problem = "Delete Duplicates should be applied to All Files, but you only have a subset selected";
                messageBox.Message.Solution = "On the Select menu, choose 'All Files' and try again" ;
                messageBox.Message.Icon = MessageBoxImage.Exclamation;
                messageBox.ShowDialog();
                return;
            }
            else
            {
                // Generate a list of duplicate rows showing their filenames (including relative path) 
                List<string> filenames = new List<string>();
                FileTable table = this.dataHandler.FileDatabase.GetDuplicateFiles();
                if (table != null && table.Count() != 0)
                { 
                    // populate the list
                    foreach (ImageRow image in table)
                    {
                        string separator = (image.RelativePath == String.Empty) ? "" : "/";
                        filenames.Add(image.RelativePath + separator + image.FileName );
                    }
                }

                // Raise a dialog box that shows the duplicate files (if any), where the user needs to confirm their deletion
                DeleteDuplicates deleteDuplicates = new DeleteDuplicates(this, filenames);
                bool? result = deleteDuplicates.ShowDialog();
                if (result == true)
                {
                    // Delete the duplicate files
                   this.dataHandler.FileDatabase.DeleteDuplicateFiles();
                   // Reselect on the current select settings, which updates the view to remove the deleted files
                   this.SelectFilesAndShowFile();
                }  
            }
        }

        /// <summary>Toggle the magnifier on and off</summary>
        private void MenuItemDisplayMagnifyingGlass_Click(object sender, RoutedEventArgs e)
        {
            this.dataHandler.FileDatabase.ImageSet.MagnifyingGlassEnabled = !this.dataHandler.FileDatabase.ImageSet.MagnifyingGlassEnabled;
            this.MarkableCanvas.MagnifyingGlassEnabled = this.dataHandler.FileDatabase.ImageSet.MagnifyingGlassEnabled;
            this.MenuItemDisplayMagnifyingGlass.IsChecked = this.dataHandler.FileDatabase.ImageSet.MagnifyingGlassEnabled;
        }

        /// <summary>Increase the magnification of the magnifying glass. We do this several times to make
        /// the increase effect more visible through a menu option versus the keyboard equivalent</summary>
        private void MenuItemMagnifyingGlassIncrease_Click(object sender, RoutedEventArgs e)
        {
            for (int i=0; i<6; i++)
            { 
                this.MarkableCanvas.MagnifierZoomIn();
            }
        }

        /// <summary> Decrease the magnification of the magnifying glass. We do this several times to make
        /// the increase effect more visible through a menu option versus the keyboard equivalent</summary>
        private void MenuItemMagnifyingGlassDecrease_Click(object sender, RoutedEventArgs e)
        {
            for (int i = 0; i < 6; i++)
            {
                this.MarkableCanvas.MagnifierZoomOut();
            }
        }

        private void MenuItemOptionsDarkImages_Click(object sender, RoutedEventArgs e)
        {
            if (this.MaybePromptToApplyOperationIfPartialSelection(this.state.SuppressSelectedDarkThresholdPrompt,
                                                               "'(Re-) classify dark files...'",
                                                               (bool optOut) =>
                                                               {
                                                                   this.state.SuppressSelectedDarkThresholdPrompt = optOut; // SG TODO
                                                               }))
            {
                using (DarkImagesThreshold darkThreshold = new DarkImagesThreshold(this.dataHandler.FileDatabase, this.dataHandler.ImageCache.CurrentRow, this.state, this))
                {
                    darkThreshold.Owner = this;
                    darkThreshold.ShowDialog();
                }
            }
        }

        /// <summary>Correct the date by specifying an offset</summary>
        private void MenuItemDateTimeFixedCorrection_Click(object sender, RoutedEventArgs e)
        {
             // Warn user that they are in a selected view, and verify that they want to continue
            if (this.MaybePromptToApplyOperationIfPartialSelection(this.state.SuppressSelectedDateTimeFixedCorrectionPrompt, 
                                                               "'Add a fixed correction value to every date/time...'", 
                                                               (bool optOut) =>
                                                               {
                                                                   this.state.SuppressSelectedDateTimeFixedCorrectionPrompt = optOut;
                                                               }))
            {
                DateTimeFixedCorrection fixedDateCorrection = new DateTimeFixedCorrection(this.dataHandler.FileDatabase, this.dataHandler.ImageCache.Current, this);
                this.ShowBulkImageEditDialog(fixedDateCorrection);
            }
        }

        /// <summary>Correct for drifting clock times. Correction applied only to images in the selected view.</summary>
        private void MenuItemDateTimeLinearCorrection_Click(object sender, RoutedEventArgs e)
        {
            // Warn user that they are in a selected view, and verify that they want to continue
            if (this.MaybePromptToApplyOperationIfPartialSelection(this.state.SuppressSelectedDateTimeLinearCorrectionPrompt,
                                                               "'Correct for camera clock drift'",
                                                               (bool optOut) =>
                                                               {
                                                                   this.state.SuppressSelectedDateTimeLinearCorrectionPrompt = optOut;
                                                               }))
            { 
                DateTimeLinearCorrection linearDateCorrection = new DateTimeLinearCorrection(this.dataHandler.FileDatabase, this);
                this.ShowBulkImageEditDialog(linearDateCorrection);
            }
        }

        /// <summary>Correct for daylight savings time</summary>
        private void MenuItemDaylightSavingsTimeCorrection_Click(object sender, RoutedEventArgs e)
        {
            // If we are not in the selection All view, or if its a corrupt image, tell the person. Selecting ok will shift the views..
            if (this.dataHandler.ImageCache.Current.IsDisplayable() == false)
            {
                // Just a corrupted image
                MessageBox messageBox = new MessageBox("Can't correct for daylight savings time.", this);
                messageBox.Message.Problem = "This is a corrupted file.";
                messageBox.Message.Solution = "To correct for daylight savings time, you need to:" + Environment.NewLine;
                messageBox.Message.Solution += "\u2022 be displaying a file with a valid date ";
                messageBox.Message.Solution += "\u2022 where that file should be the one at the daylight savings time threshold.";
                messageBox.Message.Icon = MessageBoxImage.Exclamation;
                messageBox.ShowDialog();
                return;
            }

            if (this.MaybePromptToApplyOperationIfPartialSelection(this.state.SuppressSelectedDaylightSavingsCorrectionPrompt, 
                                                               "'Correct for daylight savings time...'", 
                                                               (bool optOut) =>
                                                               {
                                                                   this.state.SuppressSelectedDaylightSavingsCorrectionPrompt = optOut;
                                                               }))
            {
                DateDaylightSavingsTimeCorrection dateTimeChange = new DateDaylightSavingsTimeCorrection(this.dataHandler.FileDatabase, this.dataHandler.ImageCache, this);
                this.ShowBulkImageEditDialog(dateTimeChange);
            }
        }

        // Correct ambiguous dates dialog (i.e. dates that could be read as either month/day or day/month
        private void MenuItemCorrectAmbiguousDates_Click(object sender, RoutedEventArgs e)
        {
            // Warn user that they are in a selection view, and verify that they want to continue
            if (this.MaybePromptToApplyOperationIfPartialSelection(this.state.SuppressSelectedAmbiguousDatesPrompt, 
                                                               "'Correct ambiguous dates...'",
                                                               (bool optOut) =>
                                                               {
                                                                   this.state.SuppressSelectedAmbiguousDatesPrompt = optOut;
                                                               }))
            {
                DateCorrectAmbiguous dateCorrection = new DateCorrectAmbiguous(this.dataHandler.FileDatabase, this);
                if (dateCorrection.Abort)
                {
                    MessageBox messageBox = new MessageBox("No ambiguous dates found", this);
                    messageBox.Message.What = "No ambiguous dates found.";
                    messageBox.Message.Reason = "All of the images in this selected view have unambguous date fields." + Environment.NewLine;
                    messageBox.Message.Result = "No corrections needed, and no changes have been made." + Environment.NewLine;
                    messageBox.Message.Icon = MessageBoxImage.Information;
                    messageBox.ShowDialog();
                    messageBox.Close();
                    return;
                 }
                 this.ShowBulkImageEditDialog(dateCorrection);
            }
        }

        private void MenuItemSetTimeZone_Click(object sender, RoutedEventArgs e)
        {
            // Warn user that they are in a selecction view, and verify that they want to continue
            if (this.MaybePromptToApplyOperationIfPartialSelection(this.state.SuppressSelectedSetTimeZonePrompt,
                                                               "'Set the time zone of every date/time...'",
                                                               (bool optOut) =>
                                                               {
                                                                   this.state.SuppressSelectedSetTimeZonePrompt = optOut;
                                                               }))
            {
                DateTimeSetTimeZone fixedDateCorrection = new DateTimeSetTimeZone(this.dataHandler.FileDatabase, this.dataHandler.ImageCache.Current, this);
                this.ShowBulkImageEditDialog(fixedDateCorrection);
            }
        }

        private void MenuItemDialogsOnOrOff_Click(object sender, RoutedEventArgs e)
        {
            DialogsHideOrShow dialog = new DialogsHideOrShow(this.state, this);
            bool? result = dialog.ShowDialog();
        }

        private void MenuItemRereadDateTimesfromFiles_Click(object sender, RoutedEventArgs e)
        {
            // If we are not in the selection All view, or if its a corrupt file, tell the person. Selecting ok will shift the views..
            if (this.MaybePromptToApplyOperationIfPartialSelection(this.state.SuppressSelectedRereadDatesFromFilesPrompt,
                                                               "'Reread dates from files...'",
                                                               (bool optOut) =>
                                                               {
                                                                   this.state.SuppressSelectedRereadDatesFromFilesPrompt = optOut;
                                                               }))
            {
                DateTimeRereadFromFiles rereadDates = new DateTimeRereadFromFiles(this.dataHandler.FileDatabase, this);
                this.ShowBulkImageEditDialog(rereadDates);
            }
        }

        /// <summary> Toggle the audio feedback on and off</summary>
        private void MenuItemAudioFeedback_Click(object sender, RoutedEventArgs e)
        {
            // We don't have to do anything here...
            this.state.AudioFeedback = !this.state.AudioFeedback;
            this.MenuItemAudioFeedback.IsChecked = this.state.AudioFeedback;
        }
        #endregion

        #region View Menu Callbacks
        private void View_SubmenuOpening(object sender, RoutedEventArgs e)
        {
            FilePlayer_Stop(); // In case the FilePlayer is going
        }

        private void MenuItemZoomIn_Click(object sender, RoutedEventArgs e)
        {
            Point mousePosition = Mouse.GetPosition(this.MarkableCanvas.ImageToDisplay);
            this.MarkableCanvas.ScaleImage(mousePosition, true);
        }

        private void MenuItemZoomOut_Click(object sender, RoutedEventArgs e)
        {
            Point mousePosition = Mouse.GetPosition(this.MarkableCanvas.ImageToDisplay);
            this.MarkableCanvas.ScaleImage(mousePosition, false);
        }

        /// <summary>Navigate to the next file in this image set</summary>
        private void MenuItemShowNextFile_Click(object sender, RoutedEventArgs e)
        {
            this.TryShowImageWithoutSliderCallback(true, ModifierKeys.None);
        }

        /// <summary>Navigate to the previous file in this image set</summary>
        private void MenuItemShowPreviousFile_Click(object sender, RoutedEventArgs e)
        {
            this.TryShowImageWithoutSliderCallback(false, ModifierKeys.None);
        }

        /// <summary>Cycle through the image differences</summary>
        private void MenuItemViewDifferencesCycleThrough_Click(object sender, RoutedEventArgs e)
        {
            this.TryViewPreviousOrNextDifference();
        }

        /// <summary>View the combined image differences</summary>
        private void MenuItemViewDifferencesCombined_Click(object sender, RoutedEventArgs e)
        {
            this.TryViewCombinedDifference();
        }



        #endregion

        #region Selection Menu
        private void MenuItemSelect_SubmenuOpening(object sender, RoutedEventArgs e)
        {
            FilePlayer_Stop(); // In case the FilePlayer is going
            Dictionary<FileSelection, int> counts = this.dataHandler.FileDatabase.GetFileCountsBySelection();

            this.MenuItemSelectLightFiles.IsEnabled = counts[FileSelection.Ok] > 0;
            this.MenuItemSelectDarkFiles.IsEnabled = counts[FileSelection.Dark] > 0;
            this.MenuItemSelectCorruptedFiles.IsEnabled = counts[FileSelection.Corrupted] > 0;
            this.MenuItemSelectFilesNoLongerAvailable.IsEnabled = counts[FileSelection.Missing] > 0;
            this.MenuItemSelectFilesMarkedForDeletion.IsEnabled = this.dataHandler.FileDatabase.GetFileCount(FileSelection.MarkedForDeletion) > 0;
        }
        /// <summary>Select the appropriate selection and update the view</summary>
        private void MenuItemSelectFiles_Click(object sender, RoutedEventArgs e)
        {
            MenuItem item = (MenuItem)sender;
            FileSelection selection;
            // find out which selection was selected
            if (item == this.MenuItemSelectAllFiles)
            {
                selection = FileSelection.All;
            }
            else if (item == this.MenuItemSelectLightFiles)
            {
                selection = FileSelection.Ok;
            }
            else if (item == this.MenuItemSelectCorruptedFiles)
            {
                selection = FileSelection.Corrupted;
            }
            else if (item == this.MenuItemSelectDarkFiles)
            {
                selection = FileSelection.Dark;
            }
            else if (item == this.MenuItemSelectFilesNoLongerAvailable)
            {
                selection = FileSelection.Missing;
            }
            else if (item == this.MenuItemSelectFilesMarkedForDeletion)
            {
                selection = FileSelection.MarkedForDeletion;
            }
            else
            {
                selection = FileSelection.All;   // Just in case
            }

            // Treat the checked status as a radio button i.e., toggle their states so only the clicked menu item is checked.
            this.SelectFilesAndShowFile(this.dataHandler.ImageCache.Current.ID, selection);  // Go to the first result (i.e., index 0) in the given selection set
        }

        // helper function to put a checkbox on the currently selected menu item i.e., to make it behave like a radiobutton menu
        private void MenuItemSelectSetSelection(MenuItem checked_item)
        {
            this.MenuItemSelectAllFiles.IsChecked = (this.MenuItemSelectAllFiles == checked_item);
            this.MenuItemSelectCorruptedFiles.IsChecked = (this.MenuItemSelectCorruptedFiles == checked_item);
            this.MenuItemSelectDarkFiles.IsChecked = (this.MenuItemSelectDarkFiles == checked_item);
            this.MenuItemSelectLightFiles.IsChecked = (this.MenuItemSelectLightFiles == checked_item);
            this.MenuItemSelectFilesNoLongerAvailable.IsChecked = (MenuItemSelectFilesNoLongerAvailable == checked_item);
            this.MenuItemSelectFilesMarkedForDeletion.IsChecked = (this.MenuItemSelectFilesMarkedForDeletion == checked_item);
            this.MenuItemView.IsChecked = false;
        }

        // helper function to put a checkbox on the currently selected menu item i.e., to make it behave like a radiobutton menu
        private void MenuItemSelectSetSelection(FileSelection selection)
        {
            this.MenuItemSelectAllFiles.IsChecked = (selection == FileSelection.All);
            this.MenuItemSelectCorruptedFiles.IsChecked = (selection == FileSelection.Corrupted);
            this.MenuItemSelectDarkFiles.IsChecked = (selection == FileSelection.Dark);
            this.MenuItemSelectLightFiles.IsChecked = (selection == FileSelection.Ok);
            this.MenuItemSelectFilesNoLongerAvailable.IsChecked = (selection == FileSelection.Missing);
            this.MenuItemSelectFilesMarkedForDeletion.IsChecked = (selection == FileSelection.MarkedForDeletion);
            this.MenuItemSelectCustomSelection.IsChecked = (selection == FileSelection.Custom);
        }

        private void MenuItemSelectCustomSelection_Click(object sender, RoutedEventArgs e)
        {
            // the first time the custom selection dialog is launched update the DateTime and UtcOffset search terms to the time of the current image
            SearchTerm firstDateTimeSearchTerm = this.dataHandler.FileDatabase.CustomSelection.SearchTerms.First(searchTerm => searchTerm.DataLabel == Constant.DatabaseColumn.DateTime);
            if (firstDateTimeSearchTerm.GetDateTime() == Constant.ControlDefault.DateTimeValue.DateTime)
            {
                DateTimeOffset defaultDate = this.dataHandler.ImageCache.Current.GetDateTime();
                this.dataHandler.FileDatabase.CustomSelection.SetDateTimesAndOffset(defaultDate);
            }

            // show the dialog and process the resuls
            Dialog.CustomSelection customSelection = new Dialog.CustomSelection(this.dataHandler.FileDatabase, this, this.IsUTCOffsetControlHidden());
            customSelection.Owner = this;
            bool? changeToCustomSelection = customSelection.ShowDialog();
            // Set the selection to show all images and a valid image
            if (changeToCustomSelection == true)
            {
                this.SelectFilesAndShowFile(this.dataHandler.ImageCache.Current.ID, FileSelection.Custom);
            }
        }

        /// <summary>Show a dialog box telling the user how many images were loaded, etc.</summary>
        public void MenuItemImageCounts_Click(object sender, RoutedEventArgs e)
        {
            this.MaybeShowFileCountsDialog(false, this);
        }
        #endregion

        #region Help Menu Callbacks
        private void Help_SubmenuOpening(object sender, RoutedEventArgs e)
        {
            FilePlayer_Stop(); // In case the FilePlayer is going
        }

        /// <summary> Display a message describing the version, etc.</summary> 
        private void MenuItemAbout_Click(object sender, RoutedEventArgs e)
        {
            AboutTimelapse about = new AboutTimelapse(this);
            if ((about.ShowDialog() == true) && about.MostRecentCheckForUpdate.HasValue)
            {
                this.state.MostRecentCheckForUpdates = about.MostRecentCheckForUpdate.Value;
            }
        }

        /// <summary> Display the Timelapse home page</summary> 
        private void MenuTimelapseWebPage_Click(object sender, RoutedEventArgs e)
        {
            Uri uri = new Uri("http://saul.cpsc.ucalgary.ca/timelapse/pmwiki.php?n=Main.Version2HomePage");
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri));
        }

        /// <summary>  Display the manual in a web browser</summary> 
        private void MenuTutorialManual_Click(object sender, RoutedEventArgs e)
        {
            Uri uri = new Uri("http://saul.cpsc.ucalgary.ca/timelapse/uploads/Installs/Timelapse2/Timelapse2Manual.pdf");
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri));
        }

        /// <summary> Display the page in the web browser that lets you join the timelapse mailing list </summary> 
        private void MenuJoinTimelapseMailingList_Click(object sender, RoutedEventArgs e)
        {
            Uri uri = new Uri("http://mailman.ucalgary.ca/mailman/listinfo/timelapse-l");
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri));
        }

        /// <summary> Download the sample images from a web browser </summary> 
        private void MenuDownloadSampleImages_Click(object sender, RoutedEventArgs e)
        {
            Uri uri = new Uri("http://saul.cpsc.ucalgary.ca/timelapse/uploads/Main/TutorialImageSet2.zip");
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri));
        }

        /// <summary>Send mail to the timelapse mailing list</summary> 
        private void MenuMailToTimelapseMailingList_Click(object sender, RoutedEventArgs e)
        {
            Uri uri = new Uri("mailto:timelapse-l@mailman.ucalgary.ca");
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri));
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

            Dictionary<FileSelection, int> counts = this.dataHandler.FileDatabase.GetFileCountsBySelection();
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
                if (control is DataEntryCounter)
                {
                    DataEntryCounter counter = (DataEntryCounter)control;
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
            if (userOptedOutOfMessage || this.dataHandler.FileDatabase.ImageSet.FileSelection == FileSelection.All)
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

        #region Bookmarking pan/zoom levels
        // Bookmark (Save) the current pan / zoom level of the image
        private void MenuItem_BookmarkSavePanZoom(object sender, RoutedEventArgs e)
        {
            this.MarkableCanvas.SetBookmark();
        }

        // Restore the zoom level / pan coordinates of the bookmark
        private void MenuItem_BookmarkSetPanZoom(object sender, RoutedEventArgs e)
        {
            this.MarkableCanvas.ApplyBookmark();
        }

        // Restore the zoomed out / pan coordinates 
        private void MenuItem_BookmarkDefaultPanZoom(object sender, RoutedEventArgs e)
        {
            this.MarkableCanvas.ZoomOutAllTheWay();
        }
        #endregion

        #region HelpDocumentDragDrop
        private void HelpDocument_PreviewDrag(object sender, DragEventArgs dragEvent)
        {
            Utilities.OnHelpDocumentPreviewDrag(dragEvent);
        }


        private void HelpDocument_Drop(object sender, DragEventArgs dropEvent)
        {
            string templateDatabaseFilePath;
            if (Utilities.IsSingleTemplateFileDrag(dropEvent, out templateDatabaseFilePath))
            {
                BackgroundWorker backgroundWorker;
                if (this.TryOpenTemplateAndBeginLoadFoldersAsync(templateDatabaseFilePath, out backgroundWorker) == false)
                {
                    this.state.MostRecentImageSets.TryRemove(templateDatabaseFilePath);
                    this.MenuItemRecentFileSets_Refresh();
                }
                dropEvent.Handled = true;
            }
        }
        #endregion

        #region AvalonDock callbacks
        private void LayoutAnchorable_PropertyChanging(object sender, System.ComponentModel.PropertyChangingEventArgs e)
        {

            if (e.PropertyName == Constant.AvalonDock.FloatingWindowFloatingHeightProperty || e.PropertyName == Constant.AvalonDock.FloatingWindowFloatingWidthProperty)
            {
                DockingManager_FloatingWindowLimitSize();
            }
            this.FindBoxVisibility(false);
        }

        private void DockingManager_LayoutUpdated(object sender, EventArgs e)
        {
            this.DockingManager_FloatingWindowTopmost(false);
        }

        // Enable or disable floating windows normally always being on top. 
        // Also shows floating windows in the task bar if it can be hidden
        private void DockingManager_FloatingWindowTopmost(bool topMost)
        {
            foreach (var floatingWindow in this.DockingManager.FloatingWindows)
            {
                floatingWindow.MinHeight = Constant.AvalonDock.FloatingWindowMinimumHeight;
                floatingWindow.MinWidth = Constant.AvalonDock.FloatingWindowMinimumWidth;

                // SAULXXX: Need a way to discern DataGridPane from other panes, so we can make only that floating window topmost.
                if (topMost)
                {
                    if (floatingWindow.Owner == null)
                    {
                        floatingWindow.Owner = this;
                    }
                }
                else if (floatingWindow.Owner != null)
                {
                    // Set this to null if we want windows NOT to appear always on top, otherwise to true
                    // floatingWindow.Owner = null;
                   floatingWindow.ShowInTaskbar = true;
                }
            }
        }

        // When a floating window is resized, limit it to the size of the scrollviewer.
        // SAULXX: Limitatinons, I think it also applies to the instructions and data pane floating windows!
        private void DockingManager_FloatingWindowLimitSize()
        {
            // System.Diagnostics.Debug.Print("DockingManager_FloatingWindowLimitSize");
            foreach (var floatingWindow in this.DockingManager.FloatingWindows)
            {
                if (floatingWindow.HasContent)
                {
                    if (floatingWindow.Height > this.DataEntryScrollViewer.ActualHeight)
                    {
                        // System.Diagnostics.Debug.Print(String.Format("Height {0} {1}", floatingWindow.ActualHeight, this.DataEntryScrollViewer.ActualHeight));
                        floatingWindow.Height = this.DataEntryScrollViewer.ActualHeight + Constant.AvalonDock.FloatingWindowLimitSizeHeightCorrection;
                    }
                    if (floatingWindow.Width > this.DataEntryScrollViewer.ActualWidth)
                    {
                        // System.Diagnostics.Debug.Print(String.Format("Width {0} {1}", floatingWindow.ActualWidth, this.DataEntryScrollViewer.ActualWidth));
                        floatingWindow.Width = this.DataEntryScrollViewer.ActualWidth + Constant.AvalonDock.FloatingWindowLimitSizeWidthCorrection;
                    }
                }
                //System.Diagnostics.Debug.Print(String.Format("{0} {1}", floatingWindow.ActualHeight, floatingWindow.ActualWidth));
                //System.Diagnostics.Debug.Print("-----");
            }
        }

        // SAULXX Not yet used, as some bugs remain in it.
        //private string DockingManager_SaveLayout()
        //{
        //    //string configFile = Utilities.CreateConfigurationFolderIfNeededAndGetFileName(filepath);

        //    XmlLayoutSerializer layoutSerializer = new XmlLayoutSerializer(this.DockingManager);
        //    //layoutSerializer.Serialize(configFile);
        //    System.Text.StringBuilder str = new System.Text.StringBuilder();
        //    StringWriter writer = new StringWriter(str);
        //    layoutSerializer.Serialize(writer);
        //    //  Debug.Print(str.ToString());
        //    return str.ToString();

        //}
        //// SAULXX PlaceholderNot yet used, as some bugs remain in it.
        //private void DockingManager_RestoreLayout(string layout)
        //{
        //    return;
        //    if (layout == "") return;
        //    StreamReader streamReader = new StreamReader(new MemoryStream(System.Text.Encoding.ASCII.GetBytes(layout)));
        //    XmlLayoutSerializer layoutSerializer = new XmlLayoutSerializer(this.DockingManager);
        //    Debug.Print(streamReader.ReadToEnd());
        //    layoutSerializer.Deserialize(streamReader);
        //}
        #endregion

        #region FilePlayer and FilePlayerTimer
        // The user has clicked on the file player. Take action onwhat was requested
        private void FilePlayer_FilePlayerChange(object sender, FilePlayerEventArgs args)
        {
            switch (args.Selection)
            {
                case FilePlayerSelection.First:
                    FilePlayer_Stop();
                    FileNavigatorSlider.Value = 1;
                    break;
                case FilePlayerSelection.Last:
                    FilePlayer_Stop();
                    FileNavigatorSlider.Value = this.dataHandler.FileDatabase.CurrentlySelectedFileCount;
                    break;
                case FilePlayerSelection.Step:
                    FilePlayer_Stop();
                    FilePlayerTimer_Tick(null, null);
                    break;
                case FilePlayerSelection.PlayFast:
                    FilePlayer_Play(Constant.ThrottleValues.PlayQuickly);
                    break;
                case FilePlayerSelection.PlaySlow:
                    FilePlayer_Play(Constant.ThrottleValues.PlaySlowly);
                    break;
                case FilePlayerSelection.Stop:
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

        // On every tick, try to show the next/previous file as indicated by the direction
        private void FilePlayerTimer_Tick(object sender, EventArgs e)
        {
            bool direction = (this.FilePlayer.Direction == FilePlayerDirection.Forward) ? true : false;
            this.TryShowImageWithoutSliderCallback(direction, 0);

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
                Storyboard sb = this.FindResource("flashAnimation") as Storyboard;
                if (sb != null)
                { 
                    Storyboard.SetTarget(sb, this.FindBoxTextBox);
                    sb.Begin();
                }
            }
        }

        // Make the find box visible on the display
        private void FindBoxVisibility (bool isVisible)
        {
            // Only make the find box visible if there are files to view
            if (this.FindBox != null && this.IsFileDatabaseAvailable() && this.dataHandler.FileDatabase.CurrentlySelectedFileCount > 0)
            {
                this.FindBox.IsOpen = isVisible;
                this.FindBoxTextBox.Focus();
            }
        }
        #endregion

        // If the DoubleClick on the ClickableImagesGrid selected an image or video, display it.
        private void ClickableImagesGrid_DoubleClick(object sender, ClickableImagesGridEventArgs e)
        {
           if (e.ImageRow != null )
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
    }
}
