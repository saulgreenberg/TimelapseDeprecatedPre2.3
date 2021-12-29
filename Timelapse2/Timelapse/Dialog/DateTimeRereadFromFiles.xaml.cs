﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Controls;
using Timelapse.Database;
using Timelapse.Enums;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    public partial class DateTimeRereadFromFiles : BusyableDialogWindow
    {
        #region Private Variables
        private readonly FileDatabase fileDatabase;

        // Tracks whether any changes to the database was made
        private bool IsAnyDataUpdated;

        // To help determine periodic updates to the progress bar 
        private DateTime lastRefreshDateTime = DateTime.Now;
        #endregion

        #region Constructor, Closing, AutoGenerated
        public DateTimeRereadFromFiles(Window owner, FileDatabase fileDatabase) : base(owner)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(fileDatabase, nameof(fileDatabase));

            this.InitializeComponent();

            this.fileDatabase = fileDatabase;

            // Tracks whether any changes to the data or database are made
            this.IsAnyDataUpdated = false;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Set up a progress handler that will update the progress bar
            this.InitalizeProgressHandler(this.BusyCancelIndicator);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            this.DialogResult = this.Token.IsCancellationRequested || this.IsAnyDataUpdated;
        }

        // Label and size the datagrid column headers
        private void DatagridFeedback_AutoGeneratedColumns(object sender, EventArgs e)
        {
            this.FeedbackGrid.Columns[0].Header = "File name (only for files whose date differs)";
            this.FeedbackGrid.Columns[0].Width = new DataGridLength(1, DataGridLengthUnitType.Auto);
            this.FeedbackGrid.Columns[1].Header = "Old date  \x2192  New Date if it differs";
            this.FeedbackGrid.Columns[1].Width = new DataGridLength(2, DataGridLengthUnitType.Star);
        }
        #endregion

        #region Calculate times and Update files
        private async Task<ObservableCollection<DateTimeFeedbackTuple>> TaskRereadDatesAsync()
        {
            // A side effect of running this task is that the FileTable will be updated, which means that,
            // at the very least, the calling function will need to run FilesSelectAndShow to either
            // reload the FileTable with the updated data, or to reset the FileTable back to its original form
            // if the operation was cancelled.
            this.IsAnyDataUpdated = true;

            // Reread the Date/Times from each file 
            return await Task.Run(() =>
            {
                ObservableCollection<DateTimeFeedbackTuple> feedbackRows = new ObservableCollection<DateTimeFeedbackTuple>();

                // Pass 1. For each file, check to see what dates/times need updating.
                this.Progress.Report(new ProgressBarArguments(0, "Pass 1: Examining image and video dates...", true, false));
                int count = this.fileDatabase.CountAllCurrentlySelectedFiles;
                TimeZoneInfo imageSetTimeZone = DateTimeHandler.GetNeutralTimeZone();

                // Get the list of image rows (files) whose dates have changed
                List<ImageRow> filesToAdjust = GetImageRowsWithChangedDates(this.Progress, count, imageSetTimeZone, feedbackRows, out int missingFiles);

                // We are done if the operation has been cancelled, or there are no files with changed dates.
                if (CheckIfAllDone(filesToAdjust, feedbackRows, missingFiles))
                {
                    return feedbackRows;
                }

                // Pass 2. Update files in the database
                // Provide feedback that we are in the second pass, disabling the Cancel button in the progress bar as we shouldn't cancel half-way through a database update.
                string message = String.Format("Pass 2: Updating {0} files. Please wait...", filesToAdjust.Count);
                this.Progress.Report(new ProgressBarArguments(100, message, false, true));
                Thread.Sleep(Constant.ThrottleValues.RenderingBackoffTime);  // Allow the UI to update.

                //// Update the database
                this.DatabaseUpdateFileDates(filesToAdjust);

                // Provide summary feedback 
                message = string.Format("Updated {0}/{1} files whose dates have changed.", filesToAdjust.Count, count);
                feedbackRows.Insert(0, (new DateTimeFeedbackTuple("---", message)));
                if (missingFiles > 0)
                {
                    message = (missingFiles == 1)
                    ? String.Format("{0} file is missing, and was not examined.", missingFiles)
                    : String.Format("{0} files are missing, and were not examined.", missingFiles);
                    feedbackRows.Insert(1, (new DateTimeFeedbackTuple("---", message)));
                }
                return feedbackRows;
            }, this.Token).ConfigureAwait(continueOnCapturedContext: true); // Set to true as we need to continue in the UI context
        }

        // Returns:
        // - the list of files whose dates have changed
        // - a collection of feedback information for each file whose dates were changed, each row detailing the file name and how the dates were changed
        // - the number of missing Files, if any
        private List<ImageRow> GetImageRowsWithChangedDates(IProgress<ProgressBarArguments> progress, int count, TimeZoneInfo imageSetTimeZone, ObservableCollection<DateTimeFeedbackTuple> feedbackRows, out int missingFiles)
        {
            List<ImageRow> filesToAdjust = new List<ImageRow>();
            missingFiles = 0;
            for (int fileIndex = 0; fileIndex < count; ++fileIndex)
            {
                if (Token.IsCancellationRequested)
                {
                    // A cancel was requested. Clear all pending changes and abort
                    feedbackRows.Clear();
                    break;
                }

                // We will store the various times here
                ImageRow file = this.fileDatabase.FileTable[fileIndex];
                DateTimeOffset originalDateTime = file.DateTimeIncorporatingOffset;
                string feedbackMessage = string.Empty;

                try
                {
                    // Get the image (if its there), get the new dates/times, and add it to the list of images to be updated 
                    // Note that if the image can't be created, we will just to the catch.
                    bool usingMetadataTimestamp = true;
                    if (file.FileExists(this.fileDatabase.FolderPath) == false)
                    {
                        // The file does not exist. Generate a feedback message
                        missingFiles++;
                    }
                    else
                    {
                        // Read the date from the file, and check to see if its different from the recorded date
                        DateTimeAdjustmentEnum dateTimeAdjustment = file.TryReadDateTimeOriginalFromMetadata(this.fileDatabase.FolderPath, imageSetTimeZone);
                        if (dateTimeAdjustment == DateTimeAdjustmentEnum.MetadataNotUsed)
                        {
                            // We couldn't read the metadata, so get a candidate date/time from the file info instead
                            file.SetDateTimeOffsetFromFileInfo(this.fileDatabase.FolderPath);
                            usingMetadataTimestamp = false;
                        }
                        DateTimeOffset rescannedDateTime = file.DateTimeIncorporatingOffset;
                        bool sameDate = rescannedDateTime.Date == originalDateTime.Date;
                        bool sameTime = rescannedDateTime.TimeOfDay == originalDateTime.TimeOfDay;
                        bool sameUTCOffset = rescannedDateTime.Offset == originalDateTime.Offset;

                        if (!(sameDate && sameTime && sameUTCOffset))
                        {
                            // Date has been updated - add it to the queue of files to be processed, and generate a feedback message.
                            filesToAdjust.Add(file);
                            feedbackMessage = "\x2713"; // Checkmark 
                            feedbackMessage += DateTimeHandler.ToStringDisplayDateTime(originalDateTime) + " \x2192 " + DateTimeHandler.ToStringDisplayDateTime(rescannedDateTime);
                            feedbackMessage += usingMetadataTimestamp ? " (read from metadata)" : " (read from file)";
                            feedbackRows.Add(new DateTimeFeedbackTuple(file.File, feedbackMessage));
                        }
                    }
                }
                catch (Exception exception)
                {
                    // This shouldn't happen, but just in case. 
                    TracePrint.PrintMessage(string.Format("Unexpected exception processing '{0}' in DateTimeReread. {1}", file.File, exception.ToString()));
                    feedbackMessage += string.Format("\x2716 skipping: {0}", exception.Message);
                    feedbackRows.Add(new DateTimeFeedbackTuple(file.File, feedbackMessage));
                    break;
                }

                // Update the progress bar every time interval to indicate what file we are working on
                TimeSpan intervalFromLastRefresh = DateTime.Now - this.lastRefreshDateTime;
                if (intervalFromLastRefresh > Constant.ThrottleValues.ProgressBarRefreshInterval)
                {
                    int percentDone = Convert.ToInt32(fileIndex / Convert.ToDouble(count) * 100.0);
                    progress.Report(new ProgressBarArguments(percentDone, String.Format("Pass 1: Checking dates for {0} / {1} files", fileIndex, count), true, false));
                    Thread.Sleep(Constant.ThrottleValues.RenderingBackoffTime);
                    this.lastRefreshDateTime = DateTime.Now;
                }
            }
            return filesToAdjust;
        }

        // We are done if the operation has been cancelled, or there are no files with changed dates.
        private bool CheckIfAllDone(List<ImageRow> filesToAdjust, ObservableCollection<DateTimeFeedbackTuple> feedbackRows, int missingFiles)
        {
            string message;
            // Abort (with feedback) if no dates needed changing and no cancellation request is pending
            if (filesToAdjust.Count <= 0 && Token.IsCancellationRequested == false)
            {
                // None of the file dates need updating, so no need to do anything more.
                message = "No files updated as their dates have not changed.";
                feedbackRows.Add(new DateTimeFeedbackTuple("---", message));

                if (missingFiles > 0)
                {
                    message = (missingFiles == 1)
                    ? String.Format("{0} file is missing, and was not examined.", missingFiles)
                    : String.Format("{0} files are missing, and were not examined.", missingFiles);
                    feedbackRows.Add(new DateTimeFeedbackTuple("---", message));
                }
                return true;
            }

            // Abort (with feedback) the operation was cancelled
            if (Token.IsCancellationRequested == true)
            {
                feedbackRows.Clear();
                message = "No changes were made";
                feedbackRows.Add(new DateTimeFeedbackTuple("Cancelled", message));
                return true;
            }
            return false;
        }

        // Update dates in the database for the given image rows 
        private void DatabaseUpdateFileDates(List<ImageRow> filesToAdjust)
        {
            // Update the database
            List<ColumnTuplesWithWhere> imagesToUpdate = new List<ColumnTuplesWithWhere>();
            foreach (ImageRow image in filesToAdjust)
            {
                imagesToUpdate.Add(image.GetDateTimeColumnTuples());
            }
            this.fileDatabase.UpdateFiles(imagesToUpdate);  // Write the updates to the database
        }
        #endregion

        #region Button callbacks
        // Set up the UI and invoke the Reread
        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            // Configure the UI's initial state
            this.CancelButton.IsEnabled = false;
            this.CancelButton.Visibility = Visibility.Hidden;
            this.StartDoneButton.Content = "_Done";
            this.StartDoneButton.Click -= this.StartButton_Click;
            this.StartDoneButton.Click += this.DoneButton_Click;
            this.StartDoneButton.IsEnabled = false;
            this.BusyCancelIndicator.IsBusy = true;
            this.WindowCloseButtonIsEnabled(false);

            // Reread the Date/Times from each file
            // feedbackRows will hold key / value pairs that will be bound to the datagrid feedback, 
            // which is the way to make those pairs appear in the data grid during background worker progress updates
            // The progress bar will be displayed during this process.
            ObservableCollection<DateTimeFeedbackTuple> feedbackRows = await TaskRereadDatesAsync().ConfigureAwait(true);

            // Hide the busy indicator and update the UI, e.g., to show which files have changed dates
            this.BusyCancelIndicator.IsBusy = false;
            this.FeedbackGrid.Visibility = Visibility.Visible;
            this.FeedbackGrid.ItemsSource = feedbackRows;
            this.StartDoneButton.IsEnabled = true;
            this.WindowCloseButtonIsEnabled(true);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        private void DoneButton_Click(object sender, RoutedEventArgs e)
        {
            // We return false if the database was not altered, i.e., if this was all a no-op
            this.DialogResult = this.IsAnyDataUpdated;
        }
        #endregion
    }
}
