using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Common;
using Timelapse.Database;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Contract: the abort state should be checked by the caller. If it is true, the
    /// .Show should not be invoked.
    /// </summary>
    public partial class DateCorrectAmbiguous : Window
    {
        private List<AmbiguousDate> ambiguousDatesList; // Will contain a list of all initial images containing ambiguous dates and their state
        private int ambiguousDatesListIndex;
        private FileDatabase database;
        private bool displayingPreview;

        // Whether the operation is aborted, ie., because there are no ambiguous dates
        public bool Abort { get; set; }

        #region Constructor and Loading
        public DateCorrectAmbiguous(FileDatabase database, Window owner)
        {
            this.InitializeComponent();
            this.ambiguousDatesList = new List<AmbiguousDate>();
            this.database = database;
            this.displayingPreview = false;
            this.Owner = owner;

            // We add this in code behind as we don't want to invoke the radiobutton callbacks when the interface is created.
            this.OriginalDate.Checked += this.DateBox_Checked;
            this.SwappedDate.Checked += this.DateBox_Checked;

            // Find the ambiguous dates in the current selected set
            if (this.FindAllAmbiguousDatesInSelectedImageSet() == true)
            {
                this.Abort = false;
                this.MoveToAmbiguousDate(null); // Go to first ambiguous date
            }
            else
            {
                this.Abort = true;
            }

            // Start displaying from the first ambiguous date.
            this.ambiguousDatesListIndex = 0;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dialogs.SetDefaultDialogPosition(this);
            Dialogs.TryFitDialogWindowInWorkingArea(this);
            // If the caller invokes Show with Abort = true (i.e., count = 0), this will at least show an empty dialog.
            this.UpdateDisplay(this.ambiguousDatesList.Count > 0);
        }
        #endregion

        #region Create the ambiguous date list
        // Create a list of all initial images containing ambiguous dates.
        // This includes calculating the start and end rows of all images matching an ambiguous date
        private bool FindAllAmbiguousDatesInSelectedImageSet()
        {
            int start = this.SearchForNextAmbiguousDateInSelectedImageSet(0);
            while (start != -1)
            {
                int end = this.GetLastImageOnSameDay(start, out int count);
                this.ambiguousDatesList.Add(new AmbiguousDate(start, end, count, false));
                start = this.SearchForNextAmbiguousDateInSelectedImageSet(end + 1);
            }
            return (this.ambiguousDatesList.Count > 0) ? true : false;
        }

        // Starting from the index, navigate successive image rows until an ambiguous date is found
        // If it can't find an ambiguous date, it will return -1.
        private int SearchForNextAmbiguousDateInSelectedImageSet(int startIndex)
        {
            for (int index = startIndex; index < this.database.CurrentlySelectedFileCount; index++)
            {
                ImageRow image = this.database.FileTable[index];
                DateTimeOffset imageDateTime = image.DateTimeIncorporatingOffset;
                if (imageDateTime.Day <= TimeConstants.MonthsInYear)
                {
                    return index; // If the date is ambiguous, return the row index. 
                }
            }
            return -1; // -1 means all dates are unambiguous
        }

        // Given a starting index, find its date and then go through the successive images until the date differs.
        // Return the final image that is dated the same date as this image
        // Assumption is that the index is valid and is pointing to an image with a valid date.
        // However, it still tests for problems and returns -1 if there was a problem.
        private int GetLastImageOnSameDay(int startIndex, out int count)
        {
            count = 1; // We start at 1 as we have at least one image (the starting image) with this date
            int lastMatchingDate = -1;

            // Check if index is in range
            if (startIndex >= this.database.CurrentlySelectedFileCount || startIndex < 0)
            {
                return -1;   // The index is out of range.
            }

            // Parse the provided starting date. Return -1 if it cannot.
            ImageRow image = this.database.FileTable[startIndex];
            DateTimeOffset desiredDateTime = image.DateTimeIncorporatingOffset;

            lastMatchingDate = startIndex;
            for (int index = startIndex + 1; index < this.database.CurrentlySelectedFileCount; index++)
            {
                // Parse the date for the given row.
                image = this.database.FileTable[index];
                DateTimeOffset imageDateTime = image.DateTimeIncorporatingOffset;
               
                if (desiredDateTime.Date == imageDateTime.Date)
                {
                    lastMatchingDate = index;
                    count++;
                    continue;
                }
                return lastMatchingDate; // This statement is reached only when the date differs, which means the last valid image is the one before it.
            }
            return lastMatchingDate; // if we got here, it means that we arrived at the end of the records
        }
        #endregion

        #region Navigate through ambiguous dates
        // return true if there is an amiguous date in the forward / backwards direction in the ambiguous date list
        private bool IsThereAnAmbiguousDate(bool directionForward)
        {
            if (directionForward == true)
            {
                return (this.ambiguousDatesListIndex + 1) < this.ambiguousDatesList.Count;
            }
            else
            {
                return directionForward == false && (this.ambiguousDatesListIndex - 1) >= 0;
            }
        }

        // From the current starting range, show the next or previous ambiguous date in the list. 
        // While it tests to ensure there is one, this really should be done before this is called
        private bool MoveToAmbiguousDate(bool? directionForward)
        {
            int index;
            if (directionForward == null)
            {
                index = this.ambiguousDatesListIndex;
            }
            else
            {
                index = (bool)directionForward ? this.ambiguousDatesListIndex + 1 : this.ambiguousDatesListIndex - 1;
            }

            // It shouldn't be out of range, but if it is, return false
            if (index > this.ambiguousDatesList.Count || index < 0)
            {
                return false;
            }

            ImageRow image;
            this.ambiguousDatesListIndex = index;

            // We found an ambiguous date; provide appropriate feedback
            image = this.database.FileTable[this.ambiguousDatesList[index].StartRange];
            this.OriginalDateLabel.Content = image.DateTimeIncorporatingOffset.Date;

            // If we can't swap the date, we just return the original unaltered date. However, we expect that swapping would always work at this point.
            this.SwappedDateLabel.Content = DateTimeHandler.TrySwapDayMonth(image.DateTime, out DateTimeOffset swappedDate) ? DateTimeHandler.ToDisplayDateTimeString(swappedDate) : DateTimeHandler.ToDisplayDateTimeString(image.DateTimeIncorporatingOffset);

            this.NumberOfImagesWithSameDate.Content = this.ambiguousDatesList[this.ambiguousDatesListIndex].Count.ToString();

            // Display the image. While we expect it to be on a valid image (our assumption), we can still show a missing or corrupted file if needed
            this.Image.Source = image.LoadBitmap(this.database.FolderPath, out bool isCorruptOrMissing);
            this.FileName.Content = image.File;
            this.FileName.ToolTip = image.File;

            return true;
        }
        #endregion

        #region Feedback of state
        // Update the display
        private void UpdateDisplay(bool isAmbiguousDate)
        {
            // Enable / Disable the Next / Previous buttons as needed
            this.NextDate.IsEnabled = this.IsThereAnAmbiguousDate(true);
            this.PreviousDate.IsEnabled = this.IsThereAnAmbiguousDate(false);

            if (isAmbiguousDate)
            {
                ImageRow image;
                image = this.database.FileTable[this.ambiguousDatesList[this.ambiguousDatesListIndex].StartRange];
                this.OriginalDateLabel.Content = DateTimeHandler.ToDisplayDateString(image.DateTimeIncorporatingOffset.Date);

                // If we can't swap the date, we just return the original unaltered date. However, we expect that swapping would always work at this point.
                this.SwappedDateLabel.Content = DateTimeHandler.TrySwapDayMonth(image.DateTime, out DateTimeOffset swappedDate) ? DateTimeHandler.ToDisplayDateString(swappedDate.Date) : DateTimeHandler.ToDisplayDateString(image.DateTimeIncorporatingOffset.Date);

                this.NumberOfImagesWithSameDate.Content = this.ambiguousDatesList[this.ambiguousDatesListIndex].Count.ToString();

                // Display the image. While we expect it to be on a valid image (our assumption), we can still show a missing or corrupted file if needed
                this.Image.Source = image.LoadBitmap(this.database.FolderPath, out bool isCorruptOrMissing);
                this.FileName.Content = image.File;
                this.FileName.ToolTip = image.File;

                // Set the next button and the radio button back to their defaults
                // As we do this, unlink and then relink the callback as we don't want to invoke the data update
                this.OriginalDate.Checked -= this.DateBox_Checked;
                this.OriginalDate.IsChecked = !this.ambiguousDatesList[this.ambiguousDatesListIndex].Swapped;
                this.SwappedDate.IsChecked = this.ambiguousDatesList[this.ambiguousDatesListIndex].Swapped;
                this.OriginalDate.Checked += this.DateBox_Checked;
            }
            else
            {
                // Hide date-specific items so they are no longer visible on the screen
                this.OriginalDateLabel.Visibility = Visibility.Hidden;
                this.SwappedDateLabel.Visibility = Visibility.Hidden;

                this.OriginalDate.Visibility = Visibility.Hidden;
                this.SwappedDate.Visibility = Visibility.Hidden;

                this.FileName.Content = String.Empty;
                this.FileName.ToolTip = this.FileName.Content;
                this.NumberOfImagesWithSameDate.Content = "No ambiguous dates left";
                this.Image.Source = null;
            }
        }
        #endregion

        private void PreviewDateTimeChanges()
        {
            this.DateChangeFeedback.ShowDifferenceColumn = false;
            this.PrimaryPanel.Visibility = Visibility.Collapsed;
            this.FeedbackPanel.Visibility = Visibility.Visible;

            foreach (AmbiguousDate ambiguousDate in this.ambiguousDatesList)
            {
                ImageRow image;
                image = this.database.FileTable[ambiguousDate.StartRange];
                string newDate = String.Empty;
                if (ambiguousDate.Swapped)
                {
                    DateTimeHandler.TrySwapDayMonth(image.DateTime, out DateTimeOffset swappedDate);
                    newDate = DateTimeHandler.ToDisplayDateString(swappedDate.Date);
                    string countStatus = ambiguousDate.Count.ToString() + " file";
                    countStatus += (ambiguousDate.Count > 1) ? "s" : String.Empty;
                    this.DateChangeFeedback.AddFeedbackRow(image.File, countStatus, DateTimeHandler.ToDisplayDateString(image.DateTimeIncorporatingOffset.Date), newDate, "--");
                }
            }
            this.DateChangeFeedback.Column0Name = "Sample file";
            this.DateChangeFeedback.Column1Name = "# files with same date";
            this.DateChangeFeedback.Column2Name = "Current date";
            this.DateChangeFeedback.Column3Name = "New date";
        }

        // Actually update the dates as needed
        private void ApplyDateTimeChanges()
        {
            foreach (AmbiguousDate ambDate in this.ambiguousDatesList)
            {
                if (ambDate.Swapped)
                {
                    this.database.ExchangeDayAndMonthInFileDates(ambDate.StartRange, ambDate.EndRange);
                }
            }
        }
        #region UI Callbacks
        // This handler is triggered only when the radio button state is changed. This means
        // we should swap the dates regardless of which radio button was actually pressed.
        private void DateBox_Checked(object sender, RoutedEventArgs e)
        {
            // determine if we should swap the dates or not
            RadioButton selected = sender as RadioButton;
            if (selected == this.SwappedDate)
            {
                this.ambiguousDatesList[this.ambiguousDatesListIndex].Swapped = true;
            }
            else
            {
                this.ambiguousDatesList[this.ambiguousDatesListIndex].Swapped = false;
            }

            // Enable previews only when there is something to see
            AmbiguousDate swappedDatesAvailable = this.ambiguousDatesList.FirstOrDefault(a => a.Swapped == true);
            this.PreviewChangesButton.IsEnabled = swappedDatesAvailable != null;
        }

        private void PreviewChangesButton_Click(object sender, RoutedEventArgs e)
        {
            // 1st click: Show the preview before actually making any changes.
            if (this.displayingPreview == false)
            {
                this.displayingPreview = true;
                this.PreviewDateTimeChanges();
                this.PreviewChangesButton.Content = "_Apply Changes";
                return;
            }

            // 2nd click: Make the changes
            this.ApplyDateTimeChanges();
            this.DialogResult = true;
        }
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        // If the user clicks the next button, try to show the next ambiguous date.
        private void NextPreviousButton_Click(object sender, RoutedEventArgs e)
        {
            Button direction = sender as Button;
            bool result = this.MoveToAmbiguousDate(direction == NextDate);
            this.UpdateDisplay(result);
        }

        private void SwapAllButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (AmbiguousDate ambDate in this.ambiguousDatesList)
            {
                ambDate.Swapped = true;
            }
            this.UpdateDisplay(true);
        }
        #endregion

        #region Convenience classes
        // A class that stores various properties for each ambiguous date found
        internal class AmbiguousDate
        {
            public int StartRange { get; set; }
            public int EndRange { get; set; }
            public int Count { get; set; }

            public bool Swapped { get; set; }

            public AmbiguousDate(int startRange, int endRange, int count, bool swapped)
            {
                this.StartRange = startRange;
                this.EndRange = endRange;
                this.Swapped = swapped;
                this.Count = count;
            }
        }
        #endregion
    }
}
