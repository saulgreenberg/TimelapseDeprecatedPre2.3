using Timelapse.Database;
using Timelapse.Images;
using Timelapse.Util;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using Timelapse.Enums;

namespace Timelapse.Dialog
{
    public partial class DarkImagesThreshold : Window, IDisposable
    {
        private const int MinimumRectangleWidth = 12;

        private WriteableBitmap bitmap;
        private int darkPixelThreshold;
        private double darkPixelRatio; 
        private double darkPixelRatioFound;
        private FileDatabase database;
        private bool disposed;
        private FileTableEnumerator imageEnumerator;
        private bool isColor;
        private bool updateImageQualityForAllSelectedImagesStarted;
        private bool stop;
        private TimelapseUserRegistrySettings userSettings;

        public DarkImagesThreshold(FileDatabase database, int currentImageIndex, TimelapseUserRegistrySettings state, TimelapseWindow owner)
        {
            this.InitializeComponent();
            this.Owner = owner;

            this.database = database;
            this.imageEnumerator = new FileTableEnumerator(database, currentImageIndex);
            this.darkPixelThreshold = state.DarkPixelThreshold;
            this.darkPixelRatio = state.DarkPixelRatioThreshold;
            this.darkPixelRatioFound = 0;
            this.disposed = false;
            this.isColor = false;
            this.updateImageQualityForAllSelectedImagesStarted = false;
            this.stop = false;
            this.userSettings = state;
        }

        // Display the image and associated details in the UI
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Make sure the title bar of the dialog box is on the screen. For small screens it may default to being off the screen
            Dialogs.SetDefaultDialogPosition(this);
            Dialogs.TryFitDialogWindowInWorkingArea(this);

            this.DarkThreshold.Value = this.userSettings.DarkPixelThreshold;
            this.DarkThreshold.ValueChanged += this.DarkThresholdSlider_ValueChanged;

            this.ScrollImages.Minimum = 0;
            this.ScrollImages.Maximum = this.database.CurrentlySelectedFileCount - 1;
            this.ScrollImages.Value = this.imageEnumerator.CurrentRow;

            this.SetPreviousNextButtonStates();
            this.ScrollImages_ValueChanged(null, null);
            this.ScrollImages.ValueChanged += this.ScrollImages_ValueChanged;
            this.Focus();               // necessary for the left/right arrow keys to work.
        }

        // If its an arrow key and the textbox doesn't have the focus,
        // navigate left/right image or up/down to look at differenced image
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
            if (this.OriginalClassification.Content.ToString() == Constant.ImageQuality.Light || this.OriginalClassification.Content.ToString() == Constant.ImageQuality.Dark)
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
                    this.NewClassification.Content = Constant.ImageQuality.Light;       // Color image
                }
                else if (this.darkPixelRatio <= this.darkPixelRatioFound)
                {
                    this.NewClassification.Content = Constant.ImageQuality.Dark;  // Dark grey scale image
                }
                else
                {
                    this.NewClassification.Content = Constant.ImageQuality.Light;   // Light grey scale image
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
            this.bitmap = this.imageEnumerator.Current.LoadBitmap(this.database.FolderPath, out bool isCorruptOrMissing).AsWriteable();
            this.Image.Source = this.bitmap;
            this.FileName.Content = this.imageEnumerator.Current.File;
            this.FileName.ToolTip = this.imageEnumerator.Current.File;
            this.OriginalClassification.Content = this.imageEnumerator.Current.ImageQuality.ToString(); // The original image classification

            this.RecalculateImageQualityForCurrentImage();
            this.Repaint();
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

        private void SetPreviousNextButtonStates()
        {
            this.PreviousFile.IsEnabled = (this.imageEnumerator.CurrentRow == 0) ? false : true;
            this.NextFile.IsEnabled = (this.imageEnumerator.CurrentRow < this.database.CurrentlySelectedFileCount - 1) ? true : false;
        }

        // Update the database if the OK button is clicked
        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            // second click - exit
            if (this.updateImageQualityForAllSelectedImagesStarted)
            {
                this.DialogResult = true;
                return;
            }

            // first click - do update
            // Update the Carnassial variables to the current settings
            this.userSettings.DarkPixelThreshold = this.darkPixelThreshold;
            this.userSettings.DarkPixelRatioThreshold = this.darkPixelRatio;

            this.CancelButton.Content = "_Stop";

            this.updateImageQualityForAllSelectedImagesStarted = true;
            this.BeginUpdateImageQualityForAllSelectedImagesAsync();
            this.DisplayImageAndDetails(); // Goes back to the original image
        }

        // Cancel or Stop - exit the dialog
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

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
            this.darkPixelRatio = this.userSettings.DarkPixelRatioThreshold;
            Canvas.SetLeft(this.DarkPixelRatioThumb, this.darkPixelRatio * (this.FeedbackCanvas.ActualWidth - this.DarkPixelRatioThumb.ActualWidth));

            // Move the slider to its original position
            this.DarkThreshold.Value = this.userSettings.DarkPixelRatioThreshold;
            this.RecalculateImageQualityForCurrentImage();
            this.Repaint();
        }

        // Reset the thresholds to the Carnassial Default settings
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

        private void BeginUpdateImageQualityForAllSelectedImagesAsync()
        {
            List<ImageRow> selectedFiles = this.database.FileTable.ToList();
            this.ApplyButton.Content = "_Done";
            this.ApplyButton.IsEnabled = false;
            this.DarkPixelRatioThumb.IsEnabled = false;
            this.DarkThreshold.IsEnabled = false;
            this.PreviousFile.IsEnabled = false;
            this.NextFile.IsEnabled = false;
            this.ScrollImages.IsEnabled = false;
            this.ResetButton.IsEnabled = false;

            BackgroundWorker backgroundWorker = new BackgroundWorker()
            {
                WorkerReportsProgress = true
            };
            backgroundWorker.DoWork += (ow, ea) =>
            {
                TimeSpan desiredRenderInterval = TimeSpan.FromSeconds(1.0 / Constant.ThrottleValues.DesiredMaximumImageRendersPerSecondDefault);
                DateTime previousImageRender = DateTime.UtcNow - desiredRenderInterval;
                object renderLock = new object();

                int fileIndex = 0;
                List<ColumnTuplesWithWhere> filesToUpdate = new List<ColumnTuplesWithWhere>();
                // SaulXXX There was a bug in the Parallel.ForEach that occurred when initially loading files as some files were duplicated or missing (see Issue 16 and 18) 
                // While it may also affect this loop, the consequences are small if the occassional file is skipped (duplicate testing won't hurt)
                Parallel.ForEach(new SequentialPartitioner<ImageRow>(selectedFiles), Utilities.GetParallelOptions(3), (ImageRow file, ParallelLoopState loopState) =>
                {
                    if (this.stop)
                    {
                        loopState.Break();
                    }

                    //// If its not a valid image, say so and go onto the next one.
                    ImageQuality imageQuality = new ImageQuality(file);
                    //if ((imageQuality.OldImageQuality != FileSelectionEnum.Light) && (imageQuality.OldImageQuality != FileSelectionEnum.Dark))
                    //{
                    //    imageQuality.NewImageQuality = null;
                    //    backgroundWorker.ReportProgress(0, imageQuality);
                    //    return;
                    //}

                    try
                    {
                        // Get the image, and add it to the list of images to be updated if the imageQuality has changed
                        // Note that if the image can't be created, we will just go to the catch.
                        // We also use a TransientLoading, as the estimate of darkness will work just fine on that
                        imageQuality.Bitmap = file.LoadBitmap(this.database.FolderPath, ImageDisplayIntentEnum.TransientLoading, out bool isCorruptOrMissing).AsWriteable();
                        // IMMEDIATE NEED TO CHECK IF ITS A DISPLAYABLE IMAGE i.e., CORRUPT, MISSING, VIDEO
                        //if (imageQuality.Bitmap == (BitmapSource) Constant.ImageValues.FileNoLongerAvailable.Value || imageQuality.Bitmap == (BitmapSource)Constant.ImageValues.Corrupt.Value)
                        if (isCorruptOrMissing)
                        {
                            imageQuality.NewImageQuality = null;
                            backgroundWorker.ReportProgress(0, imageQuality);
                            return;
                        }
                        imageQuality.NewImageQuality = imageQuality.Bitmap.IsDark(this.darkPixelThreshold, this.darkPixelRatio, out this.darkPixelRatioFound, out this.isColor);
                        imageQuality.IsColor = this.isColor;
                        imageQuality.DarkPixelRatioFound = this.darkPixelRatioFound;
                        if (imageQuality.OldImageQuality != imageQuality.NewImageQuality.Value)
                        {
                            filesToUpdate.Add(new ColumnTuplesWithWhere(new List<ColumnTuple> { new ColumnTuple(Constant.DatabaseColumn.ImageQuality, imageQuality.NewImageQuality.Value.ToString()) }, file.ID));
                        }
                    }
                    catch (Exception exception)
                    {
                        // file isn't there?
                        Debug.Fail("Exception while assessing image quality.", exception.ToString());
                    }

                    int currentFileIndex = Interlocked.Increment(ref fileIndex);
                    DateTime utcNow = DateTime.UtcNow;
                    if (utcNow - previousImageRender > desiredRenderInterval)
                    {
                        lock (renderLock)
                        {
                            if (utcNow - previousImageRender > desiredRenderInterval)
                            {
                                backgroundWorker.ReportProgress((int)(100.0 * (double)currentFileIndex / (double)selectedFiles.Count), imageQuality);
                            }
                        }
                    }
                });

                this.database.UpdateFiles(filesToUpdate);
            };
            backgroundWorker.ProgressChanged += (o, ea) =>
            {
                // this gets called on the UI thread
                ImageQuality imageQuality = (ImageQuality)ea.UserState;
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
                this.ScrollImages.Value = Math.Min((double)ea.ProgressPercentage / 100.0 * selectedFiles.Count, selectedFiles.Count - 1);
            };
            backgroundWorker.RunWorkerCompleted += (o, ea) =>
            {
                this.DisplayImageAndDetails();
                this.ApplyButton.IsEnabled = true;
                this.CancelButton.IsEnabled = false;
                TimelapseWindow tw = (TimelapseWindow)this.Owner;
                tw.MaybeFileShowCountsDialog(false, this);
            };
            backgroundWorker.RunWorkerAsync();
        }
    }
}
