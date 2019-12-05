using System;
using System.Windows.Media.Imaging;
using Timelapse.Database;
using Timelapse.Enums;
using Timelapse.Util;

namespace Timelapse.Images
{
    public class ImageQuality
    {
        public WriteableBitmap Bitmap { get; set; }
        public double DarkPixelRatioFound { get; set; }
        public string FileName { get; set; }
        public bool IsColor { get; set; }
        public Nullable<FileSelectionEnum> NewImageQuality { get; set; }
        public FileSelectionEnum OldImageQuality { get; set; }

        public ImageQuality(ImageRow image)
        {
            // Check the arguments for null 
            if (image == null)
            {
                // this should not happen
                TraceDebug.PrintStackTrace(1);
                throw new ArgumentNullException(nameof(image));
            }

            this.Bitmap = null;
            this.DarkPixelRatioFound = 0;
            this.FileName = image.File;
            this.IsColor = false;
            this.OldImageQuality = image.ImageQuality;
            this.NewImageQuality = null;
        }
    }
}
