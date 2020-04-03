using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Timelapse.EventArguments
{
    // Whenever the ImageAdjuster raises an event, it sets the image processing values 
    public class ImageAdjusterEventArgs : EventArgs
    {
        public int Brightness { get; set; }
        public int Contrast { get; set; }
        public bool DetectEdges { get; set; }
        public bool Sharpen { get; set; }

        public ImageAdjusterEventArgs(int brightness, int contrast, bool sharpen, bool detectEdges)
        {
            this.Brightness = brightness;
            this.Contrast = contrast;
            this.Sharpen = sharpen;
            this.DetectEdges = detectEdges;
        }
    }
}
