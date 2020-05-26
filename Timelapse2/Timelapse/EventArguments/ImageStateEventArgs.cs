using System;

namespace Timelapse.EventArguments
{
    /// <summary>
    /// Used by ImageAdjuster - essentially to indicate whether there is a new image available to adjust, and wether we are displaying the single image
    /// </summary>
    public class ImageStateEventArgs : EventArgs
    {
        public bool IsNewImage { get; set; }
        public bool IsPrimaryImage { get; set; }

        public ImageStateEventArgs(bool isNewImage, bool isPrimaryImage)
        {
            this.IsNewImage = isNewImage;
            this.IsPrimaryImage = isPrimaryImage;
        }
    }
}
