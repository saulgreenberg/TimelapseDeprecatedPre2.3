using System;
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
        private readonly TimelapseWindow timelapseWindow = GlobalReferences.MainWindow;
        private readonly MarkableCanvas markableCanvas;
        private readonly FileDatabase fileDatabase;
        private const double ObjectHeight = 256;
        public EpisodePopup(MarkableCanvas markableCanvas)
        {
            this.markableCanvas = markableCanvas;
            this.Placement = PlacementMode.Bottom;
            this.PlacementTarget = markableCanvas;
            this.IsOpen = false;
            this.Child = new Label { Content = "FOOBAR" };

            fileDatabase = timelapseWindow?.DataHandler?.FileDatabase;
        }

        public void Show(bool isVisible, int maxNumberImagesToDisplay)
        {
            ImageRow currentImageRow = timelapseWindow?.DataHandler?.ImageCache?.Current;
            TimeSpan timeThreshold = timelapseWindow.State.EpisodeTimeThreshold;

            if (isVisible == false || currentImageRow == null || fileDatabase == null)
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
            Label label = EpisodePopup.CreateLabel("^", ObjectHeight);
            width += label.Width;
            height = Math.Max(height, label.Height);
            sp.Children.Add(label);

            bool goForwards = true;    // Whether we should continue going forwards
            bool goBackwards = true;    // Whether we should continue going backwards
            long forwardsID = currentImageRow.ID + 1;   // The ID to check and retrieve in the forwards direction from the current image
            long backwardsID = currentImageRow.ID - 1;  // The ID to check and retrieve in the backwards direction from the current image
            int margin = 5;
            FileTable fileTable; // To hold the results of the database selection as a table of ImageRows
            DateTime lastBackwardsDateTime = currentImageRow.DateTime;
            DateTime lastForwardsDateTime = currentImageRow.DateTime;

            while (true)
            {
                // Abort when there is no more work to do
                if (goBackwards == false && goForwards == false)
                {
                    break;
                }

                if (goBackwards)
                {
                    fileTable = this.fileDatabase.SelectFileInDataTableById(backwardsID.ToString());
                    if (fileTable.Any())
                    {
                        if ((lastBackwardsDateTime - fileTable[0].DateTime).Duration() < timeThreshold)
                        {
                            Image image = EpisodePopup.CreateImage(fileTable[0], margin);
                            width += image.Source.Width;
                            height = Math.Max(height, image.Source.Height);
                            sp.Children.Insert(0, image);
                            lastBackwardsDateTime = fileTable[0].DateTime;
                            backwardsID--;
                        }
                        else
                        {
                            goBackwards = false;
                        }
                    }
                    else
                    {
                        goBackwards = false;
                    }
                    fileTable.Dispose();
                }
                if (sp.Children.Count > maxNumberImagesToDisplay)
                {
                    // We've collected all the images we need
                    break;
                }

                if (goForwards)
                {
                    fileTable = this.fileDatabase.SelectFileInDataTableById(forwardsID.ToString());
                    if (fileTable.Any())
                    {
                        if ((lastForwardsDateTime - fileTable[0].DateTime).Duration() < timeThreshold)
                        {
                            Image image = EpisodePopup.CreateImage(fileTable[0], margin);
                            width += image.Source.Width;
                            height = Math.Max(height, image.Source.Height);
                            sp.Children.Add(image);
                            lastForwardsDateTime = fileTable[0].DateTime;
                            forwardsID++;
                        }
                        else
                        {
                            goForwards = false;
                        }
                    }
                    else
                    {
                        goForwards = false;
                    }
                    fileTable.Dispose();
                }

                if (sp.Children.Count > maxNumberImagesToDisplay)
                {
                    // We've collected all the images we need
                    break;
                }
            }

            // Position and open the popup so it appears horizontallhy centered just above the cursor
            this.HorizontalOffset = this.markableCanvas.ActualWidth / 2.0 - width / 2.0;
            this.VerticalOffset = -height - 2 * margin;
            this.IsOpen = true;
        }

        private static Image CreateImage(ImageRow imageRow, int margin)
        {
            Image image = new Image
            {
                Source = imageRow.GetBitmapFromFile(GlobalReferences.MainWindow.FolderPath, Convert.ToInt32(ObjectHeight), ImageDisplayIntentEnum.Persistent, out bool isCorruptOrMissing)
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
