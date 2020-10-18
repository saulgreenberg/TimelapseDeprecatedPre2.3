using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Database;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    public partial class DateTimeSetTimeZone : Window
    {
        private bool displayingPreview;
        private readonly FileDatabase fileDatabase;

        public DateTimeSetTimeZone(FileDatabase fileDatabase, ImageRow imageToCorrect, Window owner)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(fileDatabase, nameof(fileDatabase));
            ThrowIf.IsNullArgument(imageToCorrect, nameof(imageToCorrect));

            this.InitializeComponent();
            this.displayingPreview = false;
            this.fileDatabase = fileDatabase;
            this.Owner = owner;

            // get the image's current time
            DateTimeOffset currentDateTime = imageToCorrect.DateTimeIncorporatingOffset;
            this.originalDate.Content = DateTimeHandler.ToStringDisplayDateTimeUtcOffset(currentDateTime);

            // get the image filename and display it
            this.FileName.Content = imageToCorrect.File;
            this.FileName.ToolTip = this.FileName.Content;

            // display the image
            this.image.Source = imageToCorrect.LoadBitmap(this.fileDatabase.FolderPath, out bool isCorruptOrMissing);

            // configure timezone picker
            TimeZoneInfo imageSetTimeZone = this.fileDatabase.ImageSet.GetSystemTimeZone();
            this.TimeZones.SelectedItem = imageSetTimeZone.DisplayName;
            this.TimeZones.SelectionChanged += this.TimeZones_SelectionChanged;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dialogs.TryPositionAndFitDialogIntoWindow(this);
        }

        private static void PreviewDateTimeChanges()
        {
            // SAULXXX MODIFIED this.TimeZoneUpdateFeedback.AddFeedbackRow, SO NEED TO REWRITE IF WE EVER USE THIS METHOD AGAIN
            // CURRENTLY COMMENTED OUT JUST TO AVOID MESSAGE WARNINGS ABOUT UNUSED PARAMETERS
            //this.PrimaryPanel.Visibility = Visibility.Collapsed;
            //this.FeedbackPanel.Visibility = Visibility.Visible;

            //// Preview the changes
            //TimeZoneInfo newTimeZone = this.TimeZones.TimeZonesByDisplayName[(string)this.TimeZones.SelectedItem];
            //foreach (ImageRow image in this.fileDatabase.FileTable)
            //{
            // SAULXXX MODIFIED this.TimeZoneUpdateFeedback.AddFeedbackRow, SO NEED TO REWRITE IF WE EVER USE THIS METHOD AGAIN
            // CURRENTLY COMMENTED OUT JUST TO AVOID MESSAGE WARNINGS ABOUT UNUSED PARAMETERS
            //string newDateTime = String.Empty;
            //string status;
            //DateTimeOffset currentImageDateTime = image.DateTimeIncorporatingOffset;
            //TimeSpan utcOffset = newTimeZone.GetUtcOffset(currentImageDateTime);
            //DateTimeOffset previewImageDateTime = currentImageDateTime.SetOffset(utcOffset);

            //// Pretty print the adjustment time
            //if (currentImageDateTime != previewImageDateTime)
            //{
            //    status = "Changed";
            //    newDateTime = DateTimeHandler.ToDisplayDateTimeUtcOffsetString(previewImageDateTime);
            //}
            //else
            //{
            //    status = "Unchanged";
            //}

            //this.TimeZoneUpdateFeedback.AddFeedbackRow(image.File, status, DateTimeHandler.ToDisplayDateTimeUtcOffsetString(currentImageDateTime), newDateTime, String.Empty);
            //}
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        private void ChangesButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.TimeZones.SelectedItem == null)
            {
                this.DialogResult = false;
                return;
            }

            // 1st click: Show the preview before actually making any changes.
            if (this.displayingPreview == false)
            {
                PreviewDateTimeChanges();
                this.displayingPreview = true;
                this.ChangesButton.Content = "_Apply Changes";
                return;
            }

            // 2nd click
            // Update the database
            TimeZoneInfo newTimeZone = this.TimeZones.TimeZonesByDisplayName[(string)this.TimeZones.SelectedItem];
            this.fileDatabase.UpdateAdjustedFileTimes(
                (string fileName, int fileIndex, int count, DateTimeOffset imageDateTime) =>
                {
                    TimeSpan utcOffset = newTimeZone.GetUtcOffset(imageDateTime);
                    return imageDateTime.SetOffset(utcOffset);
                },
                0,
                this.fileDatabase.CountAllCurrentlySelectedFiles - 1,
                CancellationToken.None);
            this.DialogResult = true;
        }

        private void TimeZones_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            this.ChangesButton.IsEnabled = true;
        }
    }
}
