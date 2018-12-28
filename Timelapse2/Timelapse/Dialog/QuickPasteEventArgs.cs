using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Timelapse.Dialog
{
    /// <summary>
    /// The QuickPaste event argument contains 
    /// - a reference to the QuickPasteEntry
    /// </summary>
    public class QuickPasteEventArgs : EventArgs
    {
        /// <summary>
        /// Gets or sets the MetaTag
        /// </summary>
        public QuickPasteEntry QuickPasteEntry { get; set; }
        public int EventType { get; set; }

        /// <summary>
        /// The QuickPast event argument contains 
        /// - a reference to the QuickPasteEntry
        /// </summary>
        public QuickPasteEventArgs(QuickPasteEntry quickPasteEntry, int eventType)
        {
            this.QuickPasteEntry = quickPasteEntry;
            this.EventType = eventType;
        }
    }
}
