using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Timelapse.Util;

namespace Timelapse.Images
{
    /// <summary>
    /// Maintain a list of bounding boxes as well as their collective maximum confidence level.
    /// Given a canvas of a given size, it will also draw the bounding boxes into that canvas
    /// </summary>
    public class BoundingBoxes
    {
        #region Public Properties
        // List of Bounding Boxes associated with the image
        public List<BoundingBox> Boxes { get; private set; }
        public float MaxConfidence { get; set; }
        #endregion
        
        #region Constructor
        public BoundingBoxes()
        {
            this.Boxes = new List<BoundingBox>();
            this.MaxConfidence = 0;
        }
        #endregion

        #region Public Methods - Draw BoundingBoxes In Canvas
        /// <summary>
        /// If detections are turned on, draw all bounding boxes relative to 0,0 and contrained by width and height within the provided
        /// The width/height should be the actual width/height of the image (also located at 0,0) as it appears in the canvas , which is required if the bounding boxes are to be drawn in the correct places
        /// if the image has a margin, that should be included as well otherwise set it to 0
        /// The canvas should also be cleared of prior bounding boxes before this is invoked.
        /// </summary>
        /// <param name="canvas"></param>
        public bool DrawBoundingBoxesInCanvas(Canvas canvas, double width, double height, int margin = 0, TransformGroup transformGroup = null)
        {
            if (canvas == null)
            {
                return false;
            }

            // Remove existing bounding boxes, if any.
            // Note that we do this even if detections may not exist, as we need to clear things if the user had just toggled detections off
            if (GlobalReferences.DetectionsExists == false || Keyboard.IsKeyDown(Key.H))
            {
                // As detection don't exist, there won't be any bounding boxes to draw.
                return false;
            }

            // Max Confidence is over all bounding boxes, regardless of the categories.
            // So we just use it as a short cut, i.e., if none of the bounding boxes are above the threshold, we can abort.
            // Also, we add a slight correction value to the MaxConfidence, so confidences near the threshold will still appear.
            double correction = 0.005;
            if (this.MaxConfidence + correction < Util.GlobalReferences.TimelapseState.BoundingBoxDisplayThreshold && this.MaxConfidence + correction < Util.GlobalReferences.TimelapseState.BoundingBoxThresholdOveride)
            {
                // Ignore any bounding box that is below the desired confidence threshold for displaying it.
                // Note that the BoundingBoxDisplayThreshold is the user-defined default set in preferences, while the BoundingBoxThresholdOveride is the threshold
                // determined in the select dialog. For example, if (say) the preference setting is .6 but the selection is at .4 confidence, then we should 
                // show bounding boxes when the confidence is .4 or more.
                return false;
            }

            foreach (BoundingBox bbox in this.Boxes)
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


                SolidColorBrush brush;
                bool colorblind = Util.GlobalReferences.TimelapseState.BoundingBoxColorBlindFriendlyColors;
                byte opacity = colorblind ? (byte)255 : (byte)Math.Round(255 * bbox.Confidence);
                switch (bbox.DetectionCategory)
                {
                    // The color and opacity of the bounding box depends upon its category and whether we are using color-blind friendly colors
                    case "0":
                        // In the current implementation, the first category is usually assigned to 'Empty', so this will likely never appear.
                        brush = Util.ColorsAndBrushes.SolidColorBrushFromColor(Colors.LavenderBlush, opacity);
                        break;
                    case "1":
                        brush = Util.ColorsAndBrushes.SolidColorBrushFromColor(Colors.DeepSkyBlue, opacity);
                        break;
                    case "2":
                        brush = (colorblind)
                            ? Util.ColorsAndBrushes.SolidColorBrushFromColor(Colors.Yellow)
                            : Util.ColorsAndBrushes.SolidColorBrushFromColor(Colors.Red, opacity);
                        break;
                    case "3":
                        brush = Util.ColorsAndBrushes.SolidColorBrushFromColor(Colors.White, opacity);
                        break;
                    default:
                        brush = Util.ColorsAndBrushes.SolidColorBrushFromColor(Colors.PaleGreen, opacity);
                        break;
                }
                rect.Stroke = brush;

                // Set the stroke thickness, which depends upon the size of the available height
                int stroke_thickness = Math.Min(width, height) > 400 ? 4 : 2;
                rect.StrokeThickness = stroke_thickness;
                rect.ToolTip = bbox.DetectionLabel + " detected, confidence=" + bbox.Confidence.ToString();
                foreach (KeyValuePair<string, string> classification in bbox.Classifications)
                {
                    rect.ToolTip += Environment.NewLine + classification.Key + " " + classification.Value;
                }

                Point screenPositionTopLeft;
                Point screenPositionBottomRight;
                if (transformGroup == null)
                {
                    // The image is not being transformed.
                    // Calculate the actual position of the bounding box from the ratios
                    screenPositionTopLeft = BoundingBox.ConvertRatioToPoint(bbox.Rectangle.Left, bbox.Rectangle.Top, width, height);
                    screenPositionBottomRight = BoundingBox.ConvertRatioToPoint(bbox.Rectangle.Left + bbox.Rectangle.Width, bbox.Rectangle.Top + bbox.Rectangle.Height, width, height);
                }
                else
                {
                    // The image is transformed, so we  have to apply that transformation to the bounding boxes
                    screenPositionTopLeft = transformGroup.Transform(BoundingBox.ConvertRatioToPoint(bbox.Rectangle.Left, bbox.Rectangle.Top, width, height));
                    screenPositionBottomRight = transformGroup.Transform(BoundingBox.ConvertRatioToPoint(bbox.Rectangle.Left + bbox.Rectangle.Width, bbox.Rectangle.Top + bbox.Rectangle.Height, width, height));
                }
                // Adjust the offset by any margin value (which could be 0)
                screenPositionTopLeft.X += margin;
                screenPositionTopLeft.Y += margin;
                Point screenPostionWidthHeight = new Point(screenPositionBottomRight.X - screenPositionTopLeft.X, screenPositionBottomRight.Y - screenPositionTopLeft.Y);

                // We also adjust the rect width and height to take into account the stroke thickness, to avoid the stroke overlapping the contained item
                // as otherwise the border thickness would overlap with the entity in the bounding box)
                rect.Width = screenPostionWidthHeight.X + (2 * stroke_thickness);
                rect.Height = screenPostionWidthHeight.Y + (2 * stroke_thickness);

                // Now add the rectangle to the canvas, also adjusting for the stroke thickness.
                Canvas.SetLeft(rect, screenPositionTopLeft.X - stroke_thickness);
                Canvas.SetTop(rect, screenPositionTopLeft.Y - stroke_thickness);
                canvas.Children.Add(rect);
                canvas.Tag = Constant.MarkableCanvas.BoundingBoxCanvasTag;
            }
            Canvas.SetZIndex(canvas, 1);
            return true;
        }
        #endregion
    }
}
