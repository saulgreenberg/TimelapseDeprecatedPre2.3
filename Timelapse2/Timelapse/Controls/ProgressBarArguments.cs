using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Timelapse.Controls
{
    public class ProgressBarArguments
    {
        // Between 0 - 100
        public int PercentDone { get; set; }

        // Any text message, preferably not too long
        public string Message { get; set; }

        // Whether the Cancel button should be enabled or disabled
        public bool CancelEnabled { get; set; }

        public ProgressBarArguments(int percentDone, string message)
        {
            this.PercentDone = percentDone;
            this.Message = message;
            this.CancelEnabled = true;
        }

        public ProgressBarArguments(int percentDone, string message, bool cancelEnabled)
        {
            this.PercentDone = percentDone;
            this.Message = message;
            this.CancelEnabled = cancelEnabled;
        }
    }
}
