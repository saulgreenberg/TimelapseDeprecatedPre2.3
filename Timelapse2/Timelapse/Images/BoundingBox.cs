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
        /// <summary>
        /// Gets or sets the bounding box's outline color
        /// </summary>
        public Brush Brush { get; set; }

        /// <summary>
        /// Gets or sets the bounding box's normalized location in the canvas, as a relative rectangle .
        /// </summary>
        public Rect Rectangle { get; set; }

        /// <summary>
        /// Gets or sets the bounding box's normalized location in the canvas, as a relative rectangle .
        /// </summary>
        public float Confidence { get; set; }

        public BoundingBox(float y1, float x1, float y2, float x2, float confidence)
        {
            this.Brush = (SolidColorBrush)new BrushConverter().ConvertFromString(Constant.StandardColour);
            this.Rectangle = new Rect(new Point(x1, y1), new Point (x2, y2));
            this.Confidence = confidence;
        }
    }
}
