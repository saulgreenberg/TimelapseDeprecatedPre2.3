using System;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Timelapse.Database;
using Timelapse.Enums;
using Timelapse.Images;
using Timelapse.Util;

namespace Timelapse.Controls
{
    // Create a popup that displays images surrounding the current image as long as they belong in the current episode.
    // It alternates between the left / right of the current image (shown as a '^' marker) but stops on the side where the
    // episode limit is reached, then filling the other side. If there are no images in the episode, only the '^' marker will be displayed.
    // Importantly, the images on either side are chosen from the order that images were loaded, thus ignoring select and sort criteria.
    // It is sensitive to:
    // - whether images were intially loaded in time-order - if not, then the left/right images may not be the ones in the episode
    // - whether images to the left/right were deleted, as the subsequent images may have a time difference greater than the threshold.
    public class EpisodePopup : Popup
    {
        // We need to access the FileDatabase to get file tables, do selects, etc.
        public FileDatabase FileDatabase { get; set; }
        private double ImageHeight { get; set; }

        private readonly TimelapseWindow timelapseWindow = GlobalReferences.MainWindow;
        private readonly MarkableCanvas markableCanvas;

        public EpisodePopup(MarkableCanvas markableCanvas, FileDatabase fileDatabase, double imageHeight)
        {
            this.markableCanvas = markableCanvas;
            this.FileDatabase = fileDatabase;
            this.ImageHeight = imageHeight;

            this.Placement = PlacementMode.Bottom;
            this.PlacementTarget = markableCanvas;
            this.IsOpen = false;
        }

        // Show or hide the popup, where we display up to the maxNumberImagesToDisplay
        public void Show(bool isVisible, int maxNumberImagesToDisplay)
        {
            ImageRow currentImageRow = timelapseWindow?.DataHandler?.ImageCache?.Current;
            TimeSpan timeThreshold = timelapseWindow.State.EpisodeTimeThreshold;

            if (isVisible == false || currentImageRow == null || FileDatabase == null)
            {
                // Hide the popup if asked or if basic data isn't available, including deleting the children
                this.IsOpen = false;
                this.Child = null;
                return;
            }

            // Images or placeholders will be contained in a horizontal stack panel, which in turn is the popup's child
            StackPanel sp = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };
            this.Child = sp;

            double width = 0;  // Used to calculate the placement offset of the popup relative to the placement target
            double height = 0;

            // Add a visual marker to show the position of the label in the image list
            Label label = EpisodePopup.CreateLabel("^", this.ImageHeight);
            width += label.Width;
            height = Math.Max(height, label.Height);
            sp.Children.Add(label);

            int margin = 5;
            FileTable fileTable; // To hold the results of the database selection as a table of ImageRows

            // We will only consider images whose relative path is the same as the current file
            string relativePath = currentImageRow.RelativePath;

            // Calculate the lower and upper extent of the range of dates we should examine
            // The maximum date range we need to consider would be the current date plus/minus the (time threshold * the number of images we could display),
            // While this could produce more hits than we need, it should give us a relatively short table of possible candidates
            DateTime lowerDateTime = currentImageRow.DateTime - TimeSpan.FromTicks(timeThreshold.Ticks * maxNumberImagesToDisplay);
            DateTime upperDateTime = currentImageRow.DateTime + TimeSpan.FromTicks(timeThreshold.Ticks * maxNumberImagesToDisplay);
            string slowerDateTime = DateTimeHandler.ToDatabaseDateTimeString(lowerDateTime);
            string supperDateTime = DateTimeHandler.ToDatabaseDateTimeString(upperDateTime);

            // Get a table of files (sorted by datetime) with that relative path which falls between the lower and upper date range
            DataTable dt = this.FileDatabase.GetIDandDateWithRelativePathAndBetweenDates(relativePath, slowerDateTime, supperDateTime);

            // Find the current image in that table by its ID
            int rowWithCurrentImageRowID = -1;
            int availableRows = dt.Rows.Count;
            for (int i = 0; i < availableRows; i++)
            {
                if (Convert.ToInt64(dt.Rows[i][0]) == currentImageRow.ID)
                {
                    rowWithCurrentImageRowID = i;
                    break;
                }
            }

            // From that current image, alternate between going to the previous/next row.
            // If the date difference between alternating successive images is less than the time threshold, 
            // display it. 
            int goBackwardsRow = rowWithCurrentImageRowID - 1;
            int goForwardsRow = rowWithCurrentImageRowID + 1;
            int imagesLeftToDisplay = maxNumberImagesToDisplay;
            DateTime lastBackwardsDateTime = currentImageRow.DateTime;
            DateTime lastForwardsDateTime = currentImageRow.DateTime;
            while (true && (goBackwardsRow >= 0 || goForwardsRow < availableRows))
            {
                // Abort when there is no more work to do
                if (imagesLeftToDisplay <= 0)
                {
                    break;
                }

                // Start on the left
                if (goBackwardsRow >= 0)
                {
                    // Add a popup image to the left of the caret
                    using (fileTable = this.FileDatabase.SelectFileInDataTableById(dt.Rows[goBackwardsRow][0].ToString()))
                    {
                        if (fileTable.Any())
                        {
                            if ((lastBackwardsDateTime - fileTable[0].DateTime).Duration() <= timeThreshold)
                            {
                                Image image = EpisodePopup.CreateImage(fileTable[0], margin, this.ImageHeight);
                                width += image.Source.Width;
                                height = Math.Max(height, image.Source.Height);
                                sp.Children.Insert(0, image);
                                imagesLeftToDisplay--;
                                lastBackwardsDateTime = fileTable[0].DateTime;
                            }
                            else
                            {
                                // Stop searching backwards
                                goBackwardsRow = -1;
                            }
                        }
                    }
                    goBackwardsRow--;
                }

                // Now try to add a popup image to the right if we still have some more  images left to display
                if (goForwardsRow < availableRows && imagesLeftToDisplay > 0)
                {
                    using (fileTable = this.FileDatabase.SelectFileInDataTableById(dt.Rows[goForwardsRow][0].ToString()))
                    {
                        if (fileTable.Any())
                        {
                            if ((lastForwardsDateTime - fileTable[0].DateTime).Duration() <= timeThreshold)
                            {
                                Image image = EpisodePopup.CreateImage(fileTable[0], margin, this.ImageHeight);
                                width += image.Source.Width;
                                height = Math.Max(height, image.Source.Height);
                                sp.Children.Add(image);
                                imagesLeftToDisplay--;
                                lastForwardsDateTime = fileTable[0].DateTime;
                            }
                            else
                            {
                                // Stop searching forwards
                                goForwardsRow = availableRows;
                            }
                        }
                    }
                    goForwardsRow++;
                }
            }
            // Position and open the popup so it appears horizontallhy centered just above the cursor
            this.HorizontalOffset = this.markableCanvas.ActualWidth / 2.0 - width / 2.0;
            this.VerticalOffset = -height - 2 * margin;
            this.IsOpen = true;

            // Cleanup
            dt.Dispose();
        }

        private static Image CreateImage(ImageRow imageRow, int margin, double imageHeight)
        {
            Image image = new Image
            {
                Source = imageRow.GetBitmapFromFile(GlobalReferences.MainWindow.FolderPath, Convert.ToInt32(imageHeight), ImageDisplayIntentEnum.Persistent, out bool isCorruptOrMissing)
            };
            if (isCorruptOrMissing)
            {
                image.Source = Constant.ImageValues.FileNoLongerAvailable.Value;
            }
            image.Margin = new Thickness(margin);
            return image;
        }

        private static Label CreateLabel(string content, double height)
        {
            return new Label
            {
                Content = content,
                FontSize = 48.0,
                FontWeight = FontWeights.Bold,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Bottom,
                Height = height,
                Width = 40,
                Foreground = Brushes.Black,
                Background = Brushes.LightGray
            };
        }
    }
}
