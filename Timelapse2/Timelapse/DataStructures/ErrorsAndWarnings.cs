using System.Collections.Generic;

namespace Timelapse.Util
{
    /// <summary>
    /// Holds two lists of strings that will eventually be displayed somewhere:
    /// - error messages
    /// - warning messages
    /// </summary>
    public class ErrorsAndWarnings
    {
        #region Public Properties
        // A list of error messages
        public List<string> Errors { get; set; }

        // A list of warning messages
        public List<string> Warnings { get; set; }
        #endregion

        #region Constructor
        /// <summary>
        /// ErrorsAndWarnings: A data structure holding messages indicating Errors and Warnings
        /// </summary>
        public ErrorsAndWarnings()
        {
            this.Errors = new List<string>();
            this.Warnings = new List<string>();
        }
        #endregion
    }
}
