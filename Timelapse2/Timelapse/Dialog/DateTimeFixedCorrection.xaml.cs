using System;
using System.Windows;
using Timelapse.Controls;
using Timelapse.Database;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    /// <summary>
    /// This dialog lets the user specify a corrected date and time of an file. All other dates and times are then corrected by the same amount.
    /// This is useful if (say) the camera was not initialized to the correct date and time.
    /// It assumes that Timelapse is configured to display all files, and the its currently displaying a valid file and thus a valid date.
    /// </summary>
    public partial class DateTimeFixedCorrection : Window
    {
        private bool displayingPreview;
        private FileDatabase fileDatabase;
        private DateTimeOffset initialDate;

        public DateTimeFixedCorrection(FileDatabase fileDatabase, ImageRow imageToCorrect, Window owner)
        {
            this.InitializeComponent();
            this.displayingPreview = false;
            this.fileDatabase = fileDatabase;
            this.Owner = owner;

            // get the image filename and display it
            this.FileName.Content = imageToCorrect.File;
            this.FileName.ToolTip = this.FileName.Content;

            // display the image
            this.image.Source = imageToCorrect.LoadBitmap(this.fileDatabase.FolderPath, out bool isCorruptOrMissing);

            // configure datetime picker
            this.initialDate = imageToCorrect.DateTimeIncorporatingOffset;
            this.OriginalDate.Content = DateTimeHandler.ToDisplayDateTimeString(this.initialDate);
            DataEntryHandler.Configure(this.DateTimePicker, this.initialDate.DateTime);
            this.DateTimePicker.ValueChanged += this.DateTimePicker_ValueChanged;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dialogs.SetDefaultDialogPosition(this);
            Dialogs.TryFitDialogWindowInWorkingArea(this);
        }

        private void PreviewDateTimeChanges()
        {
            this.PrimaryPanel.Visibility = Visibility.Collapsed;
            this.FeedbackPanel.Visibility = Visibility.Visible;

            TimeSpan adjustment = this.DateTimePicker.Value.Value - this.initialDate.DateTime;

            // Preview the changes

            foreach (ImageRow image in this.fileDatabase.FileTable)
            {
                string newDateTime = String.Empty;
                string status = "Skipped: invalid date/time";
                string difference = String.Empty;
                DateTimeOffset imageDateTime = image.DateTimeIncorporatingOffset;

                // Pretty print the adjustment time
                if (adjustment.Duration() >= Constant.Time.DateTimeDatabaseResolution)
                {
                    difference = DateTimeHandler.ToDisplayTimeSpanString(adjustment);
                    status = "Pending";
                    newDateTime = DateTimeHandler.ToDisplayDateTimeString(imageDateTime + adjustment);
                }
                else
                {
                    status = "Unchanged";
                }
                this.DateTimeChangeFeedback.ShowDifferenceColumn = true;
                this.DateTimeChangeFeedback.AddFeedbackRow(image.File, status, image.DateTimeAsDisplayable, newDateTime, difference);
            }
        }

        private void ChangesButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.DateTimePicker.Value.HasValue == false)
            {
                this.DialogResult = false;
                return;
            }

            // 1st click: Show the preview before actually making any changes.
            if (this.displayingPreview == false)
            {
                this.PreviewDateTimeChanges();
                this.displayingPreview = true;
                this.ChangesButton.Content = "_Apply Changes";
                return;
            }

            // 2nd click
            // Calculate and apply the date/time difference
            // SAULXXX: Try to parse the new datetime. If we cannot, then don't do anything.
            // This is not the best solution, as it means some changes are ignored. But we don't really have much choice here.
            if (DateTimeHandler.TryParseDisplayDateTimeString((string)this.OriginalDate.Content, out DateTime originalDateTime) == false)
            {
                // we couldn't parse it, thus we can't update anything.
                System.Windows.MessageBox.Show("Could not change the date/time, as it date is not in a format recongized by Timelapse: " + (string)this.OriginalDate.Content);
                this.DialogResult = false;
                return;
            }

            TimeSpan adjustment = this.DateTimePicker.Value.Value - originalDateTime;
            if (adjustment == TimeSpan.Zero)
            {
                this.DialogResult = false; // No difference, so nothing to correct
                return;
            }

            // Update the database
            this.fileDatabase.AdjustFileTimes(adjustment);
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
            TimeSpan difference = TimeSpan.Zero;
            if (DateTimeHandler.TryParseDisplayDateTimeString(this.DateTimePicker.Text, out DateTime newDateTime))
            {
                difference = newDateTime - this.initialDate;
                this.ChangesButton.IsEnabled = (difference == TimeSpan.Zero) ? false : true;
            }
            this.ChangesButton.IsEnabled = (difference == TimeSpan.Zero) ? false : true;
        }

        // Mitigates a bug where ValueChanged is not triggered when the date/time is changed
        private void DateTimePicker_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            this.DateTimePicker_ValueChanged(null, null);
        }
    }
}
