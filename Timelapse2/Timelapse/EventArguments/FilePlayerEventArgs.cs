using System;
using System.Collections.Generic;
using System.Linq;
using Timelapse.Enums;

namespace Timelapse.EventArguments
{
    public class FilePlayerEventArgs : EventArgs
    {
        public FilePlayerDirectionEnum Direction { get; internal set; }

        public FilePlayerSelectionEnum Selection { get; internal set; }

        public FilePlayerEventArgs(FilePlayerDirectionEnum direction, FilePlayerSelectionEnum selection)
        {
            this.Direction = direction;
            this.Selection = selection;
        }
    }
}
