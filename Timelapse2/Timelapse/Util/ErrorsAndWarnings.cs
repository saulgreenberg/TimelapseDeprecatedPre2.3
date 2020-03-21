using System.Collections.Generic;

namespace Timelapse.Util
{
    public class ErrorsAndWarnings
    {
        public List<string> Errors { get; set; }
        public List<string> Warnings { get; set; }

        public ErrorsAndWarnings()
        {
            this.Errors = new List<string>();
            this.Warnings = new List<string>();
        }
    }
}
