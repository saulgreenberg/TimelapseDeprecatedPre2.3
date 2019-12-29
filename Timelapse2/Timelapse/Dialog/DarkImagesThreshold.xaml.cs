using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Timelapse.Controls;
using Timelapse.Database;
using Timelapse.Enums;
using Timelapse.Images;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    public partial class DarkImagesThreshold : DialogWindow, IDisposable
    {
        private readonly FileDatabase fileDatabase;
        private readonly TimelapseUserRegistrySettings state;

        private const int MinimumRectangleWidth = 12;

        private WriteableBitmap bitmap;
        private int darkPixelThreshold;
        private double darkPixelRatio;
        private double darkPixelRatioFound;

        private bool disposed;
        private readonly FileTableEnumerator imageEnumerator;
        private bool isColor;
        private bool updateImageQualityForAllSelectedImagesStarted;
        private readonly bool stop;

        #region Initialization
        public DarkImagesThreshold(TimelapseWindow owner, FileDatabase fileDatabase, TimelapseUserRegistrySettings state, int currentImageIndex) : base(owner)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(fileDatabase, nameof(fileDatabase));
            ThrowIf.IsNullArgument(state, nameof(state));

            this.InitializeComponent();
            this.Owner = owner;

            this.fileDatabase = fileDatabase;
            this.imageEnumerator = new FileTableEnumerator(fileDatabase);
            this.imageEnumerator.TryMoveToFile(currentImageIndex);
            this.darkPixelThreshold = state.DarkPixelThreshold;
            this.darkPixelRatio = state.DarkPixelRatioThreshold;
            this.darkPixelRatioFound = 0;
            this.disposed = false;
            this.isColor = false;
            this.updateImageQualityForAllSelectedImagesStarted = false;
            this.stop = false;
            this.state = state;
        }

        // Display the image and associated details in the UI
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.DarkThreshold.Value = this.state.DarkPixelThreshold;
            this.DarkThreshold.ValueChanged += this.DarkThresholdSlider_ValueChanged;

            this.ScrollImages.Minimum = 0;
            this.ScrollImages.Maximum = this.fileDatabase.CurrentlySelectedFileCount - 1;
            this.ScrollImages.Value = this.imageEnumerator.CurrentRow;

            this.SetPreviousNextButtonStates();
            this.ScrollImages_ValueChanged(null, null);
            this.ScrollImages.ValueChanged += this.ScrollImages_ValueChanged;
            this.Focus();               // necessary for the left/right arrow keys to work.
        }
        #endregion

        #region Closing and Disposing
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
                if (this.imageEnumerator != null)
                {
                    this.imageEnumerator.Dispose();
                }
            }

            this.disposed = true;
        }
        #endregion

        #region UI Updating
        public void Repaint()
        {
            // Color the bar to show the current color given the dark color threshold
            byte greyColor = (byte)Math.Round(255 - (double)255 * this.darkPixelThreshold);
            Brush brush = new SolidColorBrush(Color.FromArgb(255, greyColor, greyColor, greyColor));
            this.RectDarkPixelRatioFound.Fill = brush;
            this.lblGreyColorThreshold.Content = (greyColor + 1).ToString();

            // Size the bar to show how many pixels in the current image are at least as dark as that color
            if (this.isColor)
            {
                // color image
                this.RectDarkPixelRatioFound.Width = MinimumRectangleWidth;
            }
            else
            {
                this.RectDarkPixelRatioFound.Width = this.FeedbackCanvas.ActualWidth * this.darkPixelRatioFound;
                if (this.RectDarkPixelRatioFound.Width < MinimumRectangleWidth)
                {
                    this.RectDarkPixelRatioFound.Width = MinimumRectangleWidth; // Just so something is always visible
                }
            }
            this.RectDarkPixelRatioFound.Height = this.FeedbackCanvas.ActualHeight;

            // Show the location of the %age threshold bar
            this.DarkPixelRatioThumb.Height = this.FeedbackCanvas.ActualHeight;
            this.DarkPixelRatioThumb.Width = MinimumRectangleWidth;
            Canvas.SetLeft(this.DarkPixelRatioThumb, (this.FeedbackCanvas.ActualWidth - this.DarkPixelRatioThumb.ActualWidth) * this.darkPixelRatio);

            this.UpdateLabels();
        }

        // Update all the labels to show the current values
        private void UpdateLabels()
        {
            this.DarkPixelRatio.Content = String.Format("{0,3:##0}%", 100 * this.darkPixelRatio);
            this.RatioFound.Content = String.Format("{0,3:##0}", 100 * this.darkPixelRatioFound);

            //// We don't want to update labels if the image is not valid 
            if (this.OriginalClassification.Content.ToString() == Constant.ImageQuality.Ok || this.OriginalClassification.Content.ToString() == Constant.ImageQuality.Dark)
            {
                if (this.isColor)
                {
                    // color image 
                    this.ThresholdMessage.Text = "Color - therefore not dark";
                    this.Percent.Visibility = Visibility.Hidden;
                    this.RatioFound.Content = String.Empty;
                }
                else
                {
                    this.ThresholdMessage.Text = "of the pixels are darker than the threshold";
                    this.Percent.Visibility = Visibility.Visible;
                }

                if (this.isColor)
                {
                    this.NewClassification.Content = Constant.ImageQuality.Ok;       // Color image
                }
                else if (this.darkPixelRatio <= this.darkPixelRatioFound)
                {
                    this.NewClassification.Content = Constant.ImageQuality.Dark;  // Dark grey scale image
                }
                else
                {
                    this.NewClassification.Content = Constant.ImageQuality.Ok;   // Light grey scale image
                }
            }
            else
            {
                this.NewClassification.Content = "----";
            }
        }

        // Utility routine for calling a typical sequence of UI update actions
        private void DisplayImageAndDetails()
        {
            this.bitmap = this.imageEnumerator.Current.LoadBitmap(this.fileDatabase.FolderPath, out _).AsWriteable();
            this.Image.Source = this.bitmap;
            this.FileName.Content = this.imageEnumerator.Current.File;
            this.FileName.ToolTip = this.imageEnumerator.Current.File;
            this.OriginalClassification.Content = this.imageEnumerator.Current.ImageQuality.ToString(); // The original image classification

            this.RecalculateImageQualityForCurrentImage();
            this.Repaint();
        }
        #endregion 

        /// <summary>
        /// Redo image quality calculations with current thresholds and return the ratio of pixels at least as dark as the threshold for the current image.
        /// Does not update the database.
        /// </summary>
        private void RecalculateImageQualityForCurrentImage()
        {
            this.bitmap.IsDark(this.darkPixelThreshold, this.darkPixelRatio, out this.darkPixelRatioFound, out this.isColor);
        }

        /// <summary>
        /// Redo image quality calculations with current thresholds for all images selected.  Updates the database.
        /// </summary>
        [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1117:ParametersMustBeOnSameLineOrSeparateLines", Justification = "Reviewed.")]

        private async Task BeginUpdateImageQualityForAllSelectedImagesAsync()
        {
            // Set up a progress handler that will update the progress bar
            Progress<UpdateArguments> progressHandler = new Progress<UpdateArguments>(value =>
            {
                // Update the progress bar
                this.UpdateDisplay(value.PercentDone, value.TotalCount, value.ImageQuality);
            });
            IProgress<UpdateArguments> progress = progressHandler as IProgress<UpdateArguments>;
            // Update the UI 
            List<ImageRow> selectedFiles = this.fileDatabase.FileTable.ToList();

            await Task.Run(() =>
            {
                //TimeSpan desiredRenderInterval = TimeSpan.FromSeconds(1.0 / Constant.ThrottleValues.DesiredMaximumImageRendersPerSecondDefault);
                //DateTime previousImageRender = DateTime.UtcNow - desiredRenderInterval;
                //object renderLock = new object();

                int fileIndex = 0;
                List<ColumnTuplesWithWhere> filesToUpdate = new List<ColumnTuplesWithWhere>();

                foreach (ImageRow file in selectedFiles)
                {
                    if (this.stop)
                    {
                        break;
                    }
                    ImageQuality imageQuality = new ImageQuality(file);
                    try
                    {
                        // Get the image, and add it to the list of images to be updated if the imageQuality has changed
                        // Note that if the image can't be created, we will just go to the catch.
                        // We also use a TransientLoading, as the estimate of darkness will work just fine on that
                        imageQuality.Bitmap = file.LoadBitmap(this.fileDatabase.FolderPath, ImageDisplayIntentEnum.TransientLoading, out bool isCorruptOrMissing).AsWriteable();
                        if (isCorruptOrMissing)
                        {
                            // If we can't read the image, just set its quality to OK
                            imageQuality.NewImageQuality = FileSelectionEnum.Ok;
                        }
                        else
                        {
                            // Set the image quality. Note that videos are always classified as Ok.
                            imageQuality.NewImageQuality = file.IsVideo
                                ? FileSelectionEnum.Ok
                                : imageQuality.Bitmap.IsDark(this.darkPixelThreshold, this.darkPixelRatio, out this.darkPixelRatioFound, out this.isColor);
                        }
                        imageQuality.IsColor = this.isColor;
                        imageQuality.DarkPixelRatioFound = this.darkPixelRatioFound;
                        if (imageQuality.OldImageQuality != imageQuality.NewImageQuality.Value)
                        {
                            filesToUpdate.Add(new ColumnTuplesWithWhere(new List<ColumnTuple> { new ColumnTuple(Constant.DatabaseColumn.ImageQuality, imageQuality.NewImageQuality.Value.ToString()) }, file.ID));
                            file.ImageQuality = imageQuality.NewImageQuality.Value;
                        }
                    }
                    catch (Exception exception)
                    {
                        // file isn't there?
                        imageQuality.NewImageQuality = FileSelectionEnum.Ok;
                        Debug.Fail("Exception while assessing image quality.", exception.ToString());
                    }

                    fileIndex++;
                    if (this.ReadyToRefresh())
                    {
                        progress.Report(new UpdateArguments((int)(100.0 * fileIndex / selectedFiles.Count), selectedFiles.Count, imageQuality));
                        Thread.Sleep(Constant.ThrottleValues.RenderingBackoffTime);  // Allows the UI thread to update every now and then
                    }
                }

                // Update the database to reflect the changed values
                this.fileDatabase.UpdateFiles(filesToUpdate);
            }).ConfigureAwait(true);

            this.DisplayImageAndDetails();
            this.ApplyButton.IsEnabled = true;
            this.CancelButton.IsEnabled = false;
            TimelapseWindow tw = (TimelapseWindow)this.Owner;
            tw.MaybeFileShowCountsDialog(false, this.Owner);
        }

        private void UpdateDisplay(int percentDone, int totalCount, ImageQuality imageQuality)
        {
            // this gets called on the UI thread
            // ImageQuality imageQuality = (ImageQuality)ea.UserState;
            this.Image.Source = imageQuality.Bitmap;

            this.FileName.Content = imageQuality.FileName;
            this.OriginalClassification.Content = imageQuality.OldImageQuality;
            this.NewClassification.Content = imageQuality.NewImageQuality;
            this.DarkPixelRatio.Content = String.Format("{0,3:##0}%", 100 * this.darkPixelRatio);
            this.RatioFound.Content = String.Format("{0,3:##0}", 100 * imageQuality.DarkPixelRatioFound);

            if (imageQuality.IsColor) // color image 
            {
                this.ThresholdMessage.Text = "Color - therefore not dark";
                this.Percent.Visibility = Visibility.Hidden;
                this.RatioFound.Content = String.Empty;
            }
            else
            {
                this.ThresholdMessage.Text = "of the pixels are darker than the threshold";
                this.Percent.Visibility = Visibility.Visible;
            }

            // Size the bar to show how many pixels in the current image are at least as dark as that color
            this.RectDarkPixelRatioFound.Width = this.FeedbackCanvas.ActualWidth * imageQuality.DarkPixelRatioFound;
            if (this.RectDarkPixelRatioFound.Width < 6)
            {
                this.RectDarkPixelRatioFound.Width = 6; // Just so something is always visible
            }
            this.RectDarkPixelRatioFound.Height = this.FeedbackCanvas.ActualHeight;

            // update image scroll bar position
            this.ScrollImages.Value = Math.Min(percentDone / 100.0 * totalCount, totalCount - 1);
        }

        #region UI Menu Callbacks for resetting thresholds
        // A drop-down menu providing the user with two ways to reset thresholds
        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            Button resetButton = (Button)sender;
            resetButton.ContextMenu.IsEnabled = true;
            resetButton.ContextMenu.PlacementTarget = sender as Button;
            resetButton.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            resetButton.ContextMenu.IsOpen = true;
        }

        // Reset the thresholds to their initial settings
        private void MenuItemResetCurrent_Click(object sender, RoutedEventArgs e)
        {
            // Move the thumb to correspond to the original value
            this.darkPixelRatio = this.state.DarkPixelRatioThreshold;
            Canvas.SetLeft(this.DarkPixelRatioThumb, this.darkPixelRatio * (this.FeedbackCanvas.ActualWidth - this.DarkPixelRatioThumb.ActualWidth));

            // Move the slider to its original position
            this.DarkThreshold.Value = this.state.DarkPixelRatioThreshold;
            this.RecalculateImageQualityForCurrentImage();
            this.Repaint();
        }

        // Reset the thresholds to the Default settings
        private void MenuItemResetDefault_Click(object sender, RoutedEventArgs e)
        {
            // Move the thumb to correspond to the original value
            this.darkPixelRatio = Constant.ImageValues.DarkPixelRatioThresholdDefault;
            Canvas.SetLeft(this.DarkPixelRatioThumb, this.darkPixelRatio * (this.FeedbackCanvas.ActualWidth - this.DarkPixelRatioThumb.ActualWidth));

            // Move the slider to its original position
            this.DarkThreshold.Value = Constant.ImageValues.DarkPixelThresholdDefault;
            this.RecalculateImageQualityForCurrentImage();
            this.Repaint();
        }


        #endregion

        #region UI Callbacks - setting thresholds for what is dark

        // Set a new value for the dark pixel threshold and update the UI
        private void DarkThresholdSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (this.DarkPixelRatio == null)
            {
                return;
            }
            this.darkPixelThreshold = Convert.ToInt32(e.NewValue);

            this.RecalculateImageQualityForCurrentImage();
            this.Repaint();
        }

        // Set a new value for the Dark Pixel Ratio and update the UI
        private void Thumb_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            UIElement thumb = e.Source as UIElement;

            if ((Canvas.GetLeft(thumb) + e.HorizontalChange) >= (this.FeedbackCanvas.ActualWidth - this.DarkPixelRatioThumb.ActualWidth))
            {
                Canvas.SetLeft(thumb, this.FeedbackCanvas.ActualWidth - this.DarkPixelRatioThumb.ActualWidth);
                this.darkPixelRatio = 1;
            }
            else if ((Canvas.GetLeft(thumb) + e.HorizontalChange) <= 0)
            {
                Canvas.SetLeft(thumb, 0);
                this.darkPixelRatio = 0;
            }
            else
            {
                Canvas.SetLeft(thumb, Canvas.GetLeft(thumb) + e.HorizontalChange);
                this.darkPixelRatio = (Canvas.GetLeft(thumb) + e.HorizontalChange) / this.FeedbackCanvas.ActualWidth;
            }
            if (this.DarkPixelRatio == null)
            {
                return;
            }

            this.RecalculateImageQualityForCurrentImage();
            // We don't repaint, as this will screw up the thumb dragging. So just update the labels instead.
            this.UpdateLabels();
        }
        #endregion

        #region UI callbacks - Navigating through images
        // Scroll to another image
        private void ScrollImages_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (this.updateImageQualityForAllSelectedImagesStarted)
            {
                return;
            }

            this.imageEnumerator.TryMoveToFile(Convert.ToInt32(this.ScrollImages.Value));
            this.DisplayImageAndDetails();
            this.SetPreviousNextButtonStates();
        }

        // If its an arrow key navigate left/right image 
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Interpret key as a possible shortcut key. 
            // Depending on the key, take the appropriate action
            switch (e.Key)
            {
                case Key.Right:             // next file
                    this.NextButton_Click(null, null);
                    break;
                case Key.Left:              // previous file
                    this.PreviousButton_Click(null, null);
                    break;
                default:
                    return;
            }
            e.Handled = true;
        }

        // Navigate to the previous image
        private void PreviousButton_Click(object sender, RoutedEventArgs e)
        {
            this.imageEnumerator.MovePrevious();
            this.ScrollImages.Value = this.imageEnumerator.CurrentRow;
        }

        // Navigate to the next image
        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            this.imageEnumerator.MoveNext();
            this.ScrollImages.Value = this.imageEnumerator.CurrentRow;
        }

        // Helper for the above, where previous/next buttons are enabled/disabled as needed
        private void SetPreviousNextButtonStates()
        {
            this.PreviousFile.IsEnabled = (this.imageEnumerator.CurrentRow == 0) ? false : true;
            this.NextFile.IsEnabled = (this.imageEnumerator.CurrentRow < this.fileDatabase.CurrentlySelectedFileCount - 1) ? true : false;
        }
        #endregion

        #region Button callbacks
        // Update the database if the OK button is clicked
        private async void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            // second click - exit
            if (this.updateImageQualityForAllSelectedImagesStarted)
            {
                this.DialogResult = true;
                return;
            }

            // first click - do update
            // Update the variables to the current settings
            this.state.DarkPixelThreshold = this.darkPixelThreshold;
            this.state.DarkPixelRatioThreshold = this.darkPixelRatio;

            // update the UI
            this.CancelButton.Content = "_Stop";
            this.updateImageQualityForAllSelectedImagesStarted = true;
            this.ApplyButton.Content = "_Done";
            this.ApplyButton.IsEnabled = false;
            this.DarkPixelRatioThumb.IsEnabled = false;
            this.DarkThreshold.IsEnabled = false;
            this.PreviousFile.IsEnabled = false;
            this.NextFile.IsEnabled = false;
            this.ScrollImages.IsEnabled = false;
            this.ResetButton.IsEnabled = false;

            await this.BeginUpdateImageQualityForAllSelectedImagesAsync().ConfigureAwait(true);
            this.DisplayImageAndDetails(); // Goes back to the original image
        }

        // Cancel or Stop - exit the dialog
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
        #endregion
        private class UpdateArguments
        {
            public int PercentDone { get; set; }
            public int TotalCount { get; set; }
            public ImageQuality ImageQuality { get; set; }

            public UpdateArguments(int percentDone, int totalCount, ImageQuality imageQuality)
            {
                this.PercentDone = percentDone;
                this.TotalCount = totalCount;
                this.ImageQuality = imageQuality;
            }
        }
    }
}
