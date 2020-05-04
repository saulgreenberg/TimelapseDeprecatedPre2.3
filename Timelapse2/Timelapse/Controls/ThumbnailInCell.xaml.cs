using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Timelapse.Database;
using Timelapse.Enums;
using Timelapse.Images;
using Timelapse.Util;

namespace Timelapse.Controls
{
    /// <summary>
    /// ThumbnailInCell User Control, which is used to fill each cell in the ThumbnailGrid
    /// </summary>
    public partial class ThumbnailInCell : UserControl
    {
        #region Public Properties
        public double DesiredRenderWidth
        {
            get
            {
                return this.Image.Width;
            }
            set
            {
                this.Image.Width = value;
                this.Image.MinWidth = value;
                this.Image.MaxWidth = value;
            }
        }

        public int Row { get; set; }
        public int Column { get; set; }

        public int GridIndex { get; set; } = 0;
        public Size DesiredRenderSize
        {
            get
            {
                if (this.Image == null || this.Image.Source == null)
                {
                    this.size.Width = 0;
                    this.size.Height = 0;
                }
                else
                {
                    this.size.Width = this.Image.Source.Width;
                    if (this.Image.Source.Width != 0)
                    {
                        this.size.Height = this.Image.Width * this.Image.Source.Height / this.Image.Source.Width;
                    }
                }
                return this.size;
            }
        }

        public int FileTableIndex { get; set; }

        public ImageRow ImageRow { get; set; }

        // A canvas used to display the bounding boxes
        private readonly Canvas bboxCanvas = new Canvas();

        // bounding boxes for detection
        private BoundingBoxes boundingBoxes;
        // Bounding boxes for detection. Whenever one is set, it is redrawn
        public BoundingBoxes BoundingBoxes
        {
            get
            {
                return this.boundingBoxes;
            }
            set
            {
                // update and render bounding boxes
                this.boundingBoxes = value;
                this.ShowOrHideBoundingBoxes(true);
            }
        }

        // Whether the Checkbox is checked i.e., the ThumbnailInCell is selected
        private bool isSelected = false;
        public bool IsSelected
        {
            get
            {
                return this.isSelected;
            }
            set
            {
                this.isSelected = value;
                // Show or hide the checkmark 
                if (this.isSelected)
                {
                    this.Cell.Background = this.selectedBrush;
                    this.SelectionTextBlock.Text = "\u2713"; // Checkmark in unicode
                    this.SelectionTextBlock.Background.Opacity = 0.7;
                }
                else
                {
                    this.Cell.Background = this.unselectedBrush;
                    this.SelectionTextBlock.Text = "   ";
                    this.SelectionTextBlock.Background.Opacity = 0.35;
                }
            }
        }

        // Path is the RelativePath/FileName of the image file
        public string Path
        {
            get
            {
                return (this.ImageRow == null) ? String.Empty : System.IO.Path.Combine(this.ImageRow.RelativePath, this.ImageRow.File);
            }
        }

        public string RootFolder { get; set; }

        #endregion

        #region Private Variables
        private Size size = new Size(0, 0);
        private readonly Brush unselectedBrush = Brushes.Black;
        private readonly Brush selectedBrush = Brushes.LightBlue;
        #endregion

        // Constructors: Width is the desired width of the image
        public ThumbnailInCell(double width)
        {
            this.InitializeComponent();
            this.DesiredRenderWidth = width;
            this.RootFolder = String.Empty;
        }

        public void SetTextFontSize(double value)
        {
            this.SelectionTextBlock.FontSize = value;
            this.FileNameTextBlock.FontSize = value;
            this.EpisodeTextBlock.FontSize = value;
        }

        // Get the bitmap, scaled to fit the cellWidth/Height, from the image row's image or video 
        public BitmapSource GetThumbnail(double cellWidth, double cellHeight)
        {
            BitmapSource bf;
            double finalDesiredWidth;
            if (this.ImageRow.IsVideo == false)
            {
                // Calculate scale factor to ensure that images of different aspect ratios completely fit in the cell
                double desiredHeight = cellWidth / this.ImageRow.GetBitmapAspectRatioFromFile(this.RootFolder);
                double scale = Math.Min(cellWidth / cellWidth, cellHeight / desiredHeight); // 1st term is ScaleWidth, 2nd term is ScaleHeight
                finalDesiredWidth = (cellWidth * scale - 8);  // Subtract another 2 pixels for the grid border (I think)

                bf = this.ImageRow.GetBitmapFromFile(this.RootFolder, Convert.ToInt32(finalDesiredWidth), ImageDisplayIntentEnum.TransientLoading, out _);
            }
            else
            {
                // Get it from the video - for some reason the scale adjustment doesn't seem to be needed, not sure why.
                bf = this.ImageRow.GetBitmapFromFile(this.RootFolder, Convert.ToInt32(cellWidth), ImageDisplayIntentEnum.TransientLoading, out _);
            }
            return bf;
        }

        // Rerender the image to the given width
        public void DisplayEpisodeAndBoundingBoxesIfWarranted(FileTable fileTable, int fileIndex)
        {
            this.DisplayEpisodeTextIfWarranted(fileTable, fileIndex);
            this.ShowOrHideBoundingBoxes(true);
        }

        public void SetSource (BitmapSource bitmapSource)
        {
            try
            { 
                this.Image.Source = bitmapSource;
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.Print("SetSource: Could not set the bitmapSource: " + e.Message);
            }
        }

        // Rerender the image to the given width
        public Double Rerender(FileTable fileTable, int fileIndex, double desiredWidth, double cellWidth, double cellHeight)
        {
            BitmapSource bf;
            if (this.ImageRow.IsVideo == false)
            {
                // Calculate scale factor to ensure that images of different aspect ratios completely fit in the cell
                double desiredHeight = desiredWidth / this.ImageRow.GetBitmapAspectRatioFromFile(this.RootFolder);
                double scale = Math.Min(cellWidth / desiredWidth, cellHeight / desiredHeight); // 1st term is ScaleWidth, 2nd term is ScaleHeight
                this.DesiredRenderWidth = (desiredWidth * scale - this.Image.Margin.Left - this.Image.Margin.Right - 2);  // Subtract another 2 pixels for the grid border (I think)

                bf = this.ImageRow.GetBitmapFromFile(this.RootFolder, Convert.ToInt32(this.DesiredRenderWidth), ImageDisplayIntentEnum.TransientLoading, out _);
            }
            else
            {
                bf = this.ImageRow.GetBitmapFromFile(this.RootFolder, Convert.ToInt32(this.DesiredRenderWidth), ImageDisplayIntentEnum.TransientLoading, out _);

                // Calculate scale factor to ensure that images of different aspect ratios completely fit in the cell
                double aspectRatio = bf.Width / bf.Height;
                double desiredHeight = desiredWidth / aspectRatio;
                double scale = Math.Min(cellWidth / desiredWidth, cellHeight / desiredHeight); // 1st term is ScaleWidth, 2nd term is ScaleHeight
                this.DesiredRenderWidth = (desiredWidth * scale - this.Image.Margin.Left - this.Image.Margin.Right - 2);  // Subtract another 2 pixels for the grid border (I think)
                bf = new TransformedBitmap(bf, new ScaleTransform(this.DesiredRenderWidth / bf.Width, this.DesiredRenderWidth / bf.Width));
            }
            this.Image.Source = bf;


            this.DisplayEpisodeTextIfWarranted(fileTable, fileIndex);



            // A bit of a hack to calculate the height on stock error images. When the loaded image is one of the ones held in the resource,
            // the size is in pixels rather than in device-independent pixels. To get the correct size,
            // we know that these images are 640x480, so we just multiple the desired width by .75 (i.e., 480/640)to get the desired height.
            if (bf == Constant.ImageValues.FileNoLongerAvailable.Value || bf == Constant.ImageValues.Corrupt.Value)
            {
                this.Image.Height = 0.75 * this.DesiredRenderWidth;
            }
            else
            {
                this.Image.Height = bf.PixelHeight;
            }

            this.ShowOrHideBoundingBoxes(true);
            return this.Image.Height;
        }

        // Get and display the episode text if various conditions are met
        public void DisplayEpisodeTextIfWarranted(FileTable fileTable, int fileIndex)
        {
            if (Episodes.ShowEpisodes)
            {
                // A descriptive string: the filename without the extention, plus the time in HH:MM
                // This was on request from a user, who needed to scan for the first/last image in a timelapse capture sequence
                string timeInHHMM = (this.ImageRow.Time.Length > 3) ? this.ImageRow.Time.Remove(this.ImageRow.Time.Length - 3) : String.Empty;

                string filename = System.IO.Path.GetFileNameWithoutExtension(this.ImageRow.File);
                filename = ThumbnailInCell.ShortenFileNameIfNeeded(filename, 1);
                this.FileNameTextBlock.Text = filename + " (" + timeInHHMM + ")";

                if (Episodes.EpisodesDictionary.ContainsKey(fileIndex) == false)
                {
                    Episodes.EpisodeGetEpisodesInRange(fileTable, fileIndex);
                }
                Tuple<int, int> episode = Episodes.EpisodesDictionary[fileIndex];
                if (episode.Item1 == int.MaxValue)
                {
                    this.EpisodeTextBlock.Text = "\u221E";
                }
                else
                {
                    this.EpisodeTextBlock.Text = (episode.Item2 == 1) ? "Single" : String.Format("{0}/{1}", episode.Item1, episode.Item2);
                }
                this.EpisodeTextBlock.Foreground = (episode.Item1 == 1) ? Brushes.Red : Brushes.Black;
                this.EpisodeTextBlock.FontWeight = (episode.Item1 == 1 && episode.Item2 != 1) ? FontWeights.Bold : FontWeights.Normal;
            }
            this.EpisodeTextBlock.Visibility = Episodes.ShowEpisodes ? Visibility.Visible : Visibility.Hidden;
            this.FileNameTextBlock.Visibility = this.EpisodeTextBlock.Visibility;
        }

        // Most images have a black bar at its bottom and top. We want to aligh 
        // the checkbox / text label to be just outside that black bar. This is guesswork, 
        // but the margin should line up within reason most of the time
        // Also, values are hard-coded vs. dynamic. Ok until we change the standard width or layout of the display space.
        public void AdjustMargin(int state)
        {
            int margin;
            switch (state)
            {
                case 2:
                    margin = 8;
                    break;
                case 3:
                    margin = 6;
                    break;
                case 1:
                default:
                    margin = 12;
                    break;
            }
            this.FileNameTextBlock.Margin = new Thickness(0, margin, margin, 0);
            this.EpisodeTextBlock.Margin = this.FileNameTextBlock.Margin;
            //this.CheckboxViewbox.Margin = new Thickness(margin, margin, 0, 0);
        }

        // Return a shortened version of the file name so that it fits in the available space 
        // Note that we left trim it, and we show an ellipsis on the left side if it doesn't fit.
        // Also, values are hard-coded vs. dynamic. Ok until we change the standard width or layout of the display space.
        private static string ShortenFileNameIfNeeded(string filename, int state)
        {
            // Check the arguments for null 
            if (filename == null)
            {
                // this should not happen
                TracePrint.PrintStackTrace(1);
                return "Unknown file name";
            }

            string ellipsis = "\u2026";
            switch (state)
            {
                case 2:
                case 3:
                    return filename.Length <= 10 ? filename : ellipsis + filename.Remove(0, filename.Length - 10);
                case 1:
                default:
                    return filename.Length <= 20 ? filename : ellipsis + filename.Remove(0, filename.Length - 20);
            }
        }

        #region Draw Bounding Box
        /// <summary>
        /// Redraw  or clear the bounding boxes depending on the visibility state
        /// </summary>
        /// 
        public void ShowOrHideBoundingBoxes(bool visibility)
        {
            if (visibility && this.Image?.Source != null)
            {
                Size size = new Size(this.Image.Width, this.DesiredRenderSize.Height);
                this.DrawBoundingBox(size);
            }
            else
            {
                // There is no image visible, so remove the bounding boxes
                this.bboxCanvas.Children.Clear();
                this.Cell.Children.Remove(this.bboxCanvas);
            }
        }

        // Draw bounding boxes into a boundingbox canvas that overlays the MarkableCanvas 
        private void DrawBoundingBox(Size canvasRenderSize)
        {
            // Remove existing bounding boxes, if any.
            // Note that we do this even if detections may not exist, as we need to clear things if the user had just toggled
            // detections off
            this.bboxCanvas.Children.Clear();
            this.Cell.Children.Remove(this.bboxCanvas);

            if (GlobalReferences.DetectionsExists == false || Keyboard.IsKeyDown(Key.H))
            {
                // As detection don't exist, there won't be any bounding boxes to draw.
                return;
            }

            int stroke_thickness = 2;
            // Max Confidence is over all bounding boxes, regardless of the categories.
            // So we just use it as a short cut, i.e., if none of the bounding boxes are above the threshold, we can abort.
            // Also, we add a slight correction value to the MaxConfidence, so confidences near the threshold will still appear.
            double correction = 0.005;
            if (this.BoundingBoxes.MaxConfidence + correction < Util.GlobalReferences.TimelapseState.BoundingBoxDisplayThreshold && this.BoundingBoxes.MaxConfidence + correction < Util.GlobalReferences.TimelapseState.BoundingBoxThresholdOveride)
            {
                // Ignore any bounding box that is below the desired confidence threshold for displaying it.
                // Note that the BoundingBoxDisplayThreshold is the user-defined default set in preferences, while the BoundingBoxThresholdOveride is the threshold
                // determined in the select dialog. For example, if (say) the preference setting is .6 but the selection is at .4 confidence, then we should 
                // show bounding boxes when the confidence is .4 or more.
                return;
            }

            this.bboxCanvas.Width = canvasRenderSize.Width;
            this.bboxCanvas.Height = canvasRenderSize.Height;
            foreach (BoundingBox bbox in this.BoundingBoxes.Boxes)
            {
                if (bbox.Confidence + correction < Util.GlobalReferences.TimelapseState.BoundingBoxDisplayThreshold && bbox.Confidence + correction < Util.GlobalReferences.TimelapseState.BoundingBoxThresholdOveride)
                {
                    // Ignore any bounding box that is below the desired confidence threshold for displaying it.
                    // Note that the BoundingBoxDisplayThreshold is the user-defined default set in preferences, while the BoundingBoxThresholdOveride is the threshold
                    // determined in the select dialog. For example, if (say) the preference setting is .6 but the selection is at .4 confidence, then we should 
                    // show bounding boxes when the confidence is .4 or more.
                    continue;
                }

                // Create a bounding box 
                Rectangle rect = new Rectangle();
                byte transparency = (byte)Math.Round(255 * bbox.Confidence);

                // The color of the bounding box depends upon its category
                SolidColorBrush brush;
                switch (bbox.DetectionCategory)
                {
                    case "0":
                        brush = new SolidColorBrush(Color.FromArgb(transparency, 0, 255, 0)); // Green
                        break;
                    case "1":
                        brush = new SolidColorBrush(Color.FromArgb(transparency, 255, 0, 0)); // Red
                        break;
                    case "2":
                        brush = new SolidColorBrush(Color.FromArgb(transparency, 0, 0, 255)); // Blue
                        break;
                    case "3":
                        brush = new SolidColorBrush(Color.FromArgb(transparency, 0, 255, 255)); // Peacock green/blue
                        break;
                    default:
                        brush = new SolidColorBrush(Color.FromArgb(transparency, 255, 255, 255)); // White
                        break;
                }
                rect.Stroke = brush;
                rect.StrokeThickness = stroke_thickness;
                rect.ToolTip = bbox.DetectionLabel + " detected, confidence=" + bbox.Confidence.ToString();
                foreach (KeyValuePair<string, string> classification in bbox.Classifications)
                {
                    rect.ToolTip += Environment.NewLine + classification.Key + " " + classification.Value;
                }

                // Calculate the actual position of the bounding box from the ratios
                Point screenPositionTopLeft = BoundingBox.ConvertRatioToPoint(bbox.Rectangle.Left, bbox.Rectangle.Top, canvasRenderSize.Width, canvasRenderSize.Height);
                Point screenPositionBottomRight = BoundingBox.ConvertRatioToPoint(bbox.Rectangle.Left + bbox.Rectangle.Width, bbox.Rectangle.Top + bbox.Rectangle.Height, canvasRenderSize.Width, canvasRenderSize.Height);
                Point screenPostionWidthHeight = new Point(screenPositionBottomRight.X - screenPositionTopLeft.X, screenPositionBottomRight.Y - screenPositionTopLeft.Y);

                // We also adjust the rect width and height to take into account the stroke thickness, 
                // as otherwise the border thickness would overlap with the entity in the bounding box)
                rect.Width = screenPostionWidthHeight.X + (2 * stroke_thickness);
                rect.Height = screenPostionWidthHeight.Y + (2 * stroke_thickness);

                // Now add the rectangle to the canvas, also adjusting for the stroke thickness.
                Canvas.SetLeft(rect, screenPositionTopLeft.X - stroke_thickness);
                Canvas.SetTop(rect, screenPositionTopLeft.Y - stroke_thickness);
                this.bboxCanvas.Children.Add(rect);
                this.bboxCanvas.Tag = Constant.MarkableCanvas.BoundingBoxCanvasTag;
            }
            Canvas.SetLeft(this.bboxCanvas, 0);
            Canvas.SetTop(this.bboxCanvas, 0);
            Canvas.SetZIndex(this.bboxCanvas, 1);
            this.Cell.Children.Add(this.bboxCanvas);
        }
        #endregion
    }
}
