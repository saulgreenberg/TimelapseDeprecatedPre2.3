using System;
using Timelapse.Controls;
using Timelapse.Database;

namespace Timelapse.EventArguments
{
    // Event indicates which image was double clicked on in the ClickableImagesGrid 
    public class ClickableImagesGridEventArgs : EventArgs
    {
        public ClickableImagesGrid Grid { get; set; }
        public ImageRow ImageRow { get; set; }
        public ClickableImagesGridEventArgs(ClickableImagesGrid grid, ImageRow imageRow)
        {
            this.Grid = grid;
            this.ImageRow = imageRow;
        }
    }
}
