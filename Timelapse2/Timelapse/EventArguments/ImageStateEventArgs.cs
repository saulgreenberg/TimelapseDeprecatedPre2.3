using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Timelapse.EventArguments
{
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
