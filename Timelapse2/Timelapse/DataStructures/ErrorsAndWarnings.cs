using System.Collections.Generic;

namespace Timelapse.Util
{
    public class ErrorsAndWarnings
    {
        // A list of error messages
        public List<string> Errors { get; set; }

        // A list of warning messages
        public List<string> Warnings { get; set; }

        /// <summary>
        /// ErrorsAndWarnings: A data structure holding messages indicating Errors and Warnings
        /// </summary>
        public ErrorsAndWarnings()
        {
            this.Errors = new List<string>();
            this.Warnings = new List<string>();
        }
    }
}
