using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
namespace Timelapse.Images
{
    /// <summary>
    /// A BoundingBox instance contains data describing a bounding box's appearance and the data associated with that bounding box.
    /// </summary>
    public class BoundingBox
    {
        // Gets or sets the bounding box's outline color
        public Brush Brush { get; set; }

        // Gets or sets the bounding box's normalized location in the canvas,
        // with coordinates specifying its fractional position relative to the image topleft corner
        public Rect Rectangle { get; set; }

        public string Category { get; set; }

        /// Gets or sets the bounding box's normalized location in the canvas, as a relative rectangle .
        public float Confidence { get; set; }

        public BoundingBox(float x1, float y1, float width, float height, float confidence, string category)
        {
            SetValues(x1, y1, width, height, confidence, category);
        }
        public BoundingBox(string coordinates, float confidence, string category)
        {
            float[] coords = Array.ConvertAll(coordinates.Split(','), float.Parse);
            SetValues(coords[0], coords[1], coords[2], coords[3], confidence, category);
        }

        public static Point ConvertRatioToPoint(double x, double y, double width, double height)
        {
            Point point = new Point(x * width, y * height);
            return point;
        }

        private void SetValues(float x1, float y1, float width, float height, float confidence, string category)
        {
            this.Brush = (SolidColorBrush)new BrushConverter().ConvertFromString(Constant.StandardColour);
            this.Rectangle = new Rect(new Point(x1, y1), new Point(x1 + width, y1 + height));
            this.Confidence = confidence;
            this.Category = category;
        }
    }
}
