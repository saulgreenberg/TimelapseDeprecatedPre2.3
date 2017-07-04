using System;
using Timelapse.Database;

namespace Timelapse.Controls
{
    public class ClickableImagesGridEventArgs : EventArgs
    {
        public ClickableImagesGrid Grid { get; set; }
        public ImageRow ImageRow { get; set; }
        public ClickableImagesGridEventArgs(ClickableImagesGrid grid, ImageRow imageRow)
        {
            this.Grid = grid;
            this.ImageRow= imageRow;
        }
    }
}
