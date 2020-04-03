using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Timelapse.EventArguments
{
    public class ImageChangedEventArgs : EventArgs
    {
        public string ImagePath { get; set; }
        public bool IsAnImageDisplayed { get; set; }

        public ImageChangedEventArgs(string imagePath , bool isAnImageDisplayed)
        {
            this.ImagePath = imagePath;
            this.IsAnImageDisplayed = isAnImageDisplayed;
        }
    }
}
