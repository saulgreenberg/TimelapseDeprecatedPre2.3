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
        // ImageHeight is calculated from the width * the image's aspect ratio, but checks for nulls, etc. 
        // Note: while the image width should always be the cell width, the height depends on the aspect ratio
        public double ImageHeight
        {
            get
            {
                return (this.Image == null || this.Image.Source == null || this.Image.Source.Width == 0)
                        ? 0
                        : this.Image.Width * this.Image.Source.Height / this.Image.Source.Width;
            }
        }

        public int Row { get; set; }
        public int Column { get; set; }
        public int GridIndex { get; set; } = 0;
        public int FileTableIndex { get; set; }
        public ImageRow ImageRow { get; set; }

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
                this.RefreshBoundingBoxes(true);
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
        // A canvas used to display the bounding boxes
        private readonly Canvas bboxCanvas = new Canvas();
        private double CellHeight { get; set; }
        private double CellWidth { get; set; }

        private readonly Brush unselectedBrush = Brushes.Black;
        private readonly Brush selectedBrush = Brushes.LightBlue;
        private readonly Color selectedColor = Colors.LightBlue;
        #endregion

        #region Constructor: Width / height is the desired size of the image
        public ThumbnailInCell(double cellWidth, double cellHeight)
        {
            this.InitializeComponent();

            this.CellHeight = cellHeight;
            this.CellWidth = cellWidth;

            this.Image.Width = cellWidth;
            this.Image.MinWidth = cellWidth;
            this.Image.MaxWidth = cellWidth;

            this.RootFolder = String.Empty;
        }

        private void ThumbnailInCell_Loaded(object sender, RoutedEventArgs e)
        {
            // Heuristic for setting font sizes
            this.SetTextFontSize();
            this.AdjustMargin();
            if (this.ImageRow.IsVideo)
            {
                this.InitializePlayButton();
                this.PlayButton.Visibility = Visibility.Visible;
            }
        }
        #endregion

        #region Public: Get/Set Thumbnail bitmaps
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

                bf = this.ImageRow.GetBitmapFromFile(this.RootFolder, Convert.ToInt32(finalDesiredWidth), ImageDisplayIntentEnum.TransientNavigating, out _);
            }
            else
            {
                // Get it from the video - for some reason the scale adjustment doesn't seem to be needed, not sure why.
                bf = this.ImageRow.GetBitmapFromFile(this.RootFolder, Convert.ToInt32(cellWidth), ImageDisplayIntentEnum.TransientLoading, out _);
            }
            return bf;
        }

        public void SetThumbnail(BitmapSource bitmapSource)
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
        #endregion

        #region Episodes and Bounding Boxes
        public void RefreshBoundingBoxesAndEpisodeInfo(FileTable fileTable, int fileIndex)
        {
            this.RefreshEpisodeInfo(fileTable, fileIndex);
            this.RefreshBoundingBoxes(true);
        }

        /// <summary>
        /// Redraw  or clear the bounding boxes depending on the visibility state
        /// </summary>
        /// 
        public void RefreshBoundingBoxes(bool visibility)
        {
            if (visibility && this.Image?.Source != null)
            {
                this.DrawBoundingBox();
            }
            else
            {
                // There is no image visible, so remove the bounding boxes
                this.bboxCanvas.Children.Clear();
                this.Cell.Children.Remove(this.bboxCanvas);
            }
        }

        public void InitializePlayButton()
        {
            // Already initialized
            if (PlayButton.Children.Count > 0)
            {
                return;
            }
            double canvasHeight = this.CellHeight / 5;
            double canvasWidth = this.CellWidth;
            double ellipseDiameter = canvasHeight * .5;
            double ellipseRadius = ellipseDiameter / 2;
            this.PlayButton.Height = canvasHeight;

            Ellipse ellipse = new Ellipse
            {
                Width = ellipseDiameter,
                Height = ellipseDiameter,
                Fill = new SolidColorBrush
                {
                    Color = selectedColor,
                    Opacity = 0.5
                }
            };
            Canvas.SetLeft(ellipse, canvasWidth / 2 - ellipseDiameter / 2);

            Point center = new Point(ellipseRadius, ellipseRadius);
            PointCollection trianglePoints = BitmapUtilities.GetTriangleVerticesInscribedInCircle(center, (float)ellipseDiameter / 2);

            // Construct the triangle
            Polygon triangle = new Polygon
            {
                Points = trianglePoints,
                Fill = new SolidColorBrush
                {
                    Color = Colors.Blue,
                    Opacity = 0.5
                },
            };
            Canvas.SetLeft(triangle, canvasWidth / 2 - ellipseDiameter / 2);

            this.PlayButton.Children.Add(ellipse);
            this.PlayButton.Children.Add(triangle);
        }
        // Get and display the episode text if various conditions are met
        public void RefreshEpisodeInfo(FileTable fileTable, int fileIndex)
        {
            if (Keyboard.IsKeyDown(Key.H))
            {
                this.EpisodeTextBlock.Visibility = Visibility.Hidden;
                this.FileNameTextBlock.Visibility = Visibility.Hidden;
                this.TimeTextBlock.Visibility = Visibility.Hidden;
                return;
            }
            if (Episodes.ShowEpisodes)
            {
                // Episode number
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
                this.EpisodeTextBlock.FontWeight = (episode.Item1 == 1 && episode.Item2 != 1) ? FontWeights.Bold : FontWeights.Normal; ;

                // Filename without the extention and Time in HH: MM
                // This was on request from a user, who needed to scan for the first/last image in a timelapse capture sequence
                this.FileNameTextBlock.Text = System.IO.Path.GetFileNameWithoutExtension(this.ImageRow.File);
                string timeInHHMM = (this.ImageRow.Time.Length > 3) ? this.ImageRow.Time.Remove(this.ImageRow.Time.Length - 3) : String.Empty;
                this.TimeTextBlock.Text = " (" + timeInHHMM + ")";
            }
            this.EpisodeTextBlock.Visibility = Episodes.ShowEpisodes ? Visibility.Visible : Visibility.Hidden;
            this.FileNameTextBlock.Visibility = this.EpisodeTextBlock.Visibility;
            this.TimeTextBlock.Visibility = this.EpisodeTextBlock.Visibility;
        }


        // Draw bounding boxes into a boundingbox canvas that overlays the MarkableCanvas 
        private void DrawBoundingBox()
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

            this.bboxCanvas.Width = this.Image.Width;
            this.bboxCanvas.Height = this.ImageHeight;
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

                // Use slightly thicker bounding box outline for larger vs. smaller images
                int stroke_thickness = Math.Min(this.Image.Width, this.ImageHeight) > 400 ? 2 : 1;
                rect.StrokeThickness = stroke_thickness;
                rect.ToolTip = bbox.DetectionLabel + " detected, confidence=" + bbox.Confidence.ToString();
                foreach (KeyValuePair<string, string> classification in bbox.Classifications)
                {
                    rect.ToolTip += Environment.NewLine + classification.Key + " " + classification.Value;
                }

                // Calculate the actual position of the bounding box from the ratios
                Point screenPositionTopLeft = BoundingBox.ConvertRatioToPoint(bbox.Rectangle.Left, bbox.Rectangle.Top, this.Image.Width, this.ImageHeight);
                Point screenPositionBottomRight = BoundingBox.ConvertRatioToPoint(bbox.Rectangle.Left + bbox.Rectangle.Width, bbox.Rectangle.Top + bbox.Rectangle.Height, this.Image.Width, this.ImageHeight);
                Point screenPostionWidthHeight = new Point(screenPositionBottomRight.X - screenPositionTopLeft.X, screenPositionBottomRight.Y - screenPositionTopLeft.Y);

                // We also adjust the rect width and height to take into account the stroke thickness, to avoid the stroke overlapping the contained item
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

        #region Private: Adjust Fonts and Margins of the Info Panel
        // Set the font size of the text for the info panel's children
        private void SetTextFontSize()
        {
            int fontSize = this.CellHeight / 10 > 30 ? 30 : (int)this.CellHeight / 10;
            this.SelectionTextBlock.FontSize = fontSize;
            this.FileNameTextBlock.FontSize = fontSize;
            this.TimeTextBlock.FontSize = fontSize;
            this.EpisodeTextBlock.FontSize = fontSize;
        }

        // Most images have a black bar at its bottom and top. We want to align 
        // the checkbox / text label to be just outside that black bar. This is guesswork, 
        // but the margin should line up within reason most of the time
        // Also, values are hard-coded vs. dynamic. Ok until we change the standard width or layout of the display space.
        private void AdjustMargin()
        {
            int margin = (int)Math.Ceiling(this.CellHeight / 25) + 1;
            this.InfoPanel.Margin = new Thickness(0, margin, margin, 0);
        }
        #endregion

    }
}
