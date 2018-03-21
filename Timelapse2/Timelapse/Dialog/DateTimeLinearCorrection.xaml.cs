using System;
using System.Diagnostics;
using System.Windows;
using Timelapse.Controls;
using Timelapse.Database;
using Timelapse.Util;
using Xceed.Wpf.Toolkit;
using MessageBox = Timelapse.Dialog.MessageBox;

namespace Timelapse
{
    /// <summary>
    /// Interaction logic for DialogDateTimeLinearCorrection.xaml
    /// This dialog lets the user specify a corrected date and time of a file. All other dates and times are then corrected by the same amount.
    /// This is useful if (say) the camera was not initialized to the correct date and time.
    /// </summary>
    public partial class DateTimeLinearCorrection : Window
    {
        private DateTimeOffset latestImageDateTime;
        private DateTimeOffset earliestImageDateTime;
        private DateTimeOffset lastDateEnteredWithDateTimePicker; // Keeps track of the last valid date in the date picker so we can revert to it if needed.
        private bool displayingPreview = false;
        private FileDatabase fileDatabase;

        // Create the interface
        public DateTimeLinearCorrection(FileDatabase fileDatabase, Window owner)
        {
            this.InitializeComponent();
            this.Owner = owner;
            this.fileDatabase = fileDatabase;
            this.latestImageDateTime = DateTimeOffset.MinValue;
            this.earliestImageDateTime = DateTimeOffset.MaxValue;

            // Skip images with bad dates
            ImageRow latestImageRow = null;
            ImageRow earliestImageRow = null;
            foreach (ImageRow image in this.fileDatabase.Files)
            {
                DateTimeOffset currentImageDateTime = image.GetDateTime();

                // If the current image's date is later, then its a candidate latest image  
                if (currentImageDateTime >= this.latestImageDateTime)
                {
                    latestImageRow = image;
                    this.latestImageDateTime = currentImageDateTime;
                }

                if (currentImageDateTime <= this.earliestImageDateTime)
                {
                    earliestImageRow = image;
                    this.earliestImageDateTime = currentImageDateTime;
                }
            }

            // At this point, we should have succeeded getting the oldest and newest data/time
            // Configure feedback for earliest date and its image
            this.earliestImageName.Content = earliestImageRow.FileName;
            this.earliestImageDate.Content = DateTimeHandler.ToDisplayDateTimeString(this.earliestImageDateTime);
            this.imageEarliest.Source = earliestImageRow.LoadBitmap(this.fileDatabase.FolderPath);

            // Configure feedback for latest date (in datetime picker) and its image
            this.latestImageName.Content = latestImageRow.FileName;
            DataEntryHandler.Configure(this.dateTimePickerLatestDateTime, this.latestImageDateTime.DateTime);
            this.lastDateEnteredWithDateTimePicker = this.latestImageDateTime;
            this.dateTimePickerLatestDateTime.ValueChanged += this.DateTimePicker_ValueChanged;
            this.imageLatest.Source = latestImageRow.LoadBitmap(this.fileDatabase.FolderPath);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Utilities.SetDefaultDialogPosition(this);
            Utilities.TryFitDialogWindowInWorkingArea(this);
        }

        private void PreviewDateTimeChanges()
        {
            this.PrimaryPanel.Visibility = Visibility.Collapsed;
            this.FeedbackPanel.Visibility = Visibility.Visible;

            // Preview the changes
            TimeSpan newestImageAdjustment = this.dateTimePickerLatestDateTime.Value.Value - this.latestImageDateTime;
            TimeSpan intervalFromOldestToNewestImage = this.latestImageDateTime - this.earliestImageDateTime;
            TimeSpan mostRecentAdjustment = TimeSpan.Zero;
            foreach (ImageRow image in this.fileDatabase.Files)
            {
                string oldDT = image.Date + " " + image.Time;
                string newDT = String.Empty;
                string status = "Skipped: invalid date/time";
                string difference = string.Empty;
                double imagePositionInInterval;

                DateTimeOffset imageDateTime;
                TimeSpan oneSecond = TimeSpan.FromSeconds(1);

                imageDateTime = image.GetDateTime();
                // adjust the date / time
                if (intervalFromOldestToNewestImage == TimeSpan.Zero)
                {
                    imagePositionInInterval = 1;
                }
                else
                {
                    imagePositionInInterval = (double)(imageDateTime - this.earliestImageDateTime).Ticks / (double)intervalFromOldestToNewestImage.Ticks;
                }

                TimeSpan adjustment = TimeSpan.FromTicks((long)(imagePositionInInterval * newestImageAdjustment.Ticks));

                // Pretty print the adjustment time
                if (adjustment.Duration() >= oneSecond)
                {
                    string sign = (adjustment < TimeSpan.Zero) ? "-" : "+";
                    status = "Changed";

                    // Pretty print the adjustment time, depending upon how many day(s) were included 
                    string format;
                    if (adjustment.Days == 0)
                    {
                        format = "{0:s}{1:D2}:{2:D2}:{3:D2}"; // Don't show the days field
                    }
                    else if (adjustment.Duration().Days == 1)
                    {
                        format = "{0:s}{1:D2}:{2:D2}:{3:D2} {0:s} {4:D} day";
                    }
                    else
                    {
                        format = "{0:s}{1:D2}:{2:D2}:{3:D2} {0:s} {4:D} days";
                    }
                    difference = string.Format(format, sign, adjustment.Duration().Hours, adjustment.Duration().Minutes, adjustment.Duration().Seconds, adjustment.Duration().Days);

                    // Get the new date/time
                    newDT = DateTimeHandler.ToDisplayDateTimeString(imageDateTime + adjustment);
                }
                else
                {
                    status = "Unchanged";
                }
                this.DateUpdateFeedbackCtl.AddFeedbackRow(image.FileName, status, oldDT, newDT, difference);
            }
        }

        // 1st click of Ok: Show a preview of the changes.
        // 2nd click of OK: Update the database if the OK button is clicked
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // A few checks just to make sure we actually have something to do...
            if (this.dateTimePickerLatestDateTime.Value.HasValue == false)
            {
                this.DialogResult = false;
                return;
            }

            TimeSpan newestImageAdjustment = this.dateTimePickerLatestDateTime.Value.Value - this.latestImageDateTime;
            if (newestImageAdjustment == TimeSpan.Zero)
            {
                // nothing to do
                this.DialogResult = false;
                return;
            }

            // 1st real click of Ok: Show the preview before actually making any changes.
            if (this.displayingPreview == false)
            {
                this.PreviewDateTimeChanges();
                this.displayingPreview = true;
                this.OkButton.Content = "Apply Changes";
                return;
            }

            // 2nd click of Ok
            // We've shown the preview, which means the user actually wants to do the changes. 
            // Calculate the date/time difference and Update the database

            // In the single image case the the oldest and newest times will be the same
            // since Timelapse has only whole seconds resolution it's also possible with small selections from fast cameras that multiple images have the same time
            TimeSpan intervalFromOldestToNewestImage = this.latestImageDateTime - this.earliestImageDateTime;
            if (intervalFromOldestToNewestImage == TimeSpan.Zero)
            {
                this.fileDatabase.AdjustFileTimes(newestImageAdjustment);
            }
            else
            {
                this.fileDatabase.AdjustFileTimes(
                   (DateTimeOffset imageDateTime) =>
                   {
                       double imagePositionInInterval = (double)(imageDateTime - this.earliestImageDateTime).Ticks / (double)intervalFromOldestToNewestImage.Ticks;
                       Debug.Assert((-0.0000001 < imagePositionInInterval) && (imagePositionInInterval < 1.0000001), String.Format("Interval position {0} is not between 0.0 and 1.0.", imagePositionInInterval));
                       TimeSpan adjustment = TimeSpan.FromTicks((long)(imagePositionInInterval * newestImageAdjustment.Ticks)); // Used to have a  .5 increment, I think to force rounding upwards
                                                                                                                                // TimeSpan.Duration means we do these checks on the absolute value (positive) of the Timespan, as slow clocks will have negative adjustments.
                       Debug.Assert((TimeSpan.Zero <= adjustment.Duration()) && (adjustment.Duration() <= newestImageAdjustment.Duration()), String.Format("Expected adjustment {0} to be within [{1} {2}].", adjustment, TimeSpan.Zero, newestImageAdjustment));
                       return imageDateTime + adjustment;
                   },
                   0,
                   this.fileDatabase.CurrentlySelectedFileCount - 1);
            }
            this.DialogResult = true;
        }

        // Cancel - do nothing
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        private void DateTimePicker_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            // Because of the bug in the DateTimePicker, we have to get the changed value from the string
            // as DateTimePicker.Value.Value can have the old date rather than the new one.
            if (DateTimeHandler.TryParseDisplayDateTimeString(this.dateTimePickerLatestDateTime.Text, out DateTime newDateTime) == false)
            {
                // If we can't parse the date,  do nothing.
                System.Diagnostics.Debug.Print("DateTimeLinearCorrection|ValueChanged: Could not parse the date:" + this.dateTimePickerLatestDateTime.Text);
                return;
            }

            // Don't let the date picker go below the oldest time. If it does, don't change the date and play a beep.
            if (this.dateTimePickerLatestDateTime.Value.Value <= this.earliestImageDateTime)
            {
               // SAULXX this.dateTimePickerLatestDateTime.Value = this.lastDateEnteredWithDateTimePicker;
                MessageBox messageBox = new MessageBox("Your new time has to be later than the earliest time", this);
                messageBox.Message.Icon = MessageBoxImage.Exclamation;
                messageBox.Message.Problem = "Your new time has to be later than the earliest time   ";
                messageBox.Message.Reason = "Even the slowest clock gains some time.";
                messageBox.Message.Solution = "The date/time was unchanged from where you last left it.";
                messageBox.Message.Hint = "The image on the left shows the earliest time recorded for images in this filtered view  shown over the left image";
                messageBox.ShowDialog();
            }
            else
            {
                // Keep track of the last valid date in the date picker so we can revert to it if needed.
                this.lastDateEnteredWithDateTimePicker = newDateTime;
            }
            // Enable the Ok button only if the latest time has actually changed from its original version
            TimeSpan newestImageAdjustment = newDateTime - this.latestImageDateTime;
            this.OkButton.IsEnabled = (newestImageAdjustment == TimeSpan.Zero) ? false : true;
        }

        // Mitigates a bug where ValueChanged is not triggered when the date/time is changed
        private void DateTimePickerLatestDateTime_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            DateTimePicker_ValueChanged(null, null);
        }
    }
}
