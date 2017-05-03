using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Timelapse.Controls;

namespace Timelapse.Controls
{
    public class FilePlayerEventArgs : EventArgs
    {
        public FilePlayerDirection Direction { get; internal set; }

        public FilePlayerSelection Selection { get; internal set; }

        public FilePlayerEventArgs(FilePlayerDirection direction, FilePlayerSelection selection)
        {
            this.Direction = direction;
            this.Selection = selection;
        }
    }
}
