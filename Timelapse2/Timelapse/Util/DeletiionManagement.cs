using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Timelapse.Util
{
   public enum DeleteFolderManagement : int
    {
        // Directs how the Delete Folder is managed by Timelapse
        ManualDelete = 0,
        AskToDeleteOnExit = 1,
        AutoDeleteOnExit = 2,
    }
}
