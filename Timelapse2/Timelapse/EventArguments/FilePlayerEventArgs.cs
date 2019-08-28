using System;
using Timelapse.Enums;

namespace Timelapse.EventArguments
{
    public class FilePlayerEventArgs : EventArgs
    {
        public DirectionEnum Direction { get; internal set; }

        public FilePlayerSelectionEnum Selection { get; internal set; }

        public FilePlayerEventArgs(DirectionEnum direction, FilePlayerSelectionEnum selection)
        {
            this.Direction = direction;
            this.Selection = selection;
        }
    }
}
