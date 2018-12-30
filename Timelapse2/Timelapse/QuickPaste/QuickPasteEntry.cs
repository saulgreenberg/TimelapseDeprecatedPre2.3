using System;
using System.Collections.Generic;
namespace Timelapse.QuickPaste
{
    // QuickPasteEntry Data Structure: collects all the data controls and their values as a single potential quickpaste entry
    public class QuickPasteEntry
    {
        public string Title { get; set; }                 // a user- or system-supplied Title that will be displayed to identify the QuickPaste Entry
        public List<QuickPasteItem> Items { get; set; }   // a list of QuickPasteItems, each identifying a potential pastable control

        public QuickPasteEntry()
        {
            Title = String.Empty;
            Items = new List<QuickPasteItem>();
        }
    }
}
