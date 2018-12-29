using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Xml.Linq;
using Timelapse.Database;

namespace Timelapse.Dialog
{
    [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:FileMayOnlyContainASingleType", Justification = "Reviewed.")]

    #region QuickPaste Data Structures
    // QuickPasteItem: Captures the essence of a single data control and its value
    public class QuickPasteItem
    {
        public string DataLabel { get; set; }       // identifies the control
        public string Label { get; set; }           // used to identify the control to the user
        public bool Use { get; set; }               // indicates whether the item should be used in a quickpaste (this can be set be the user) 
        public string Value { get; set; }           // the data can be pasted into a single data control 

        public QuickPasteItem() : this(String.Empty, String.Empty, String.Empty, false)
        {
        }
        public QuickPasteItem(string dataLabel, string label, string value, bool enabled)
        {
            this.DataLabel = dataLabel;
            this.Label = label;
            this.Value = value;
            this.Use = enabled;
        }
    }

    // QuickPasteEntry Data Structure: collects all the data controls and their values as a single potential quickpaste entry
    public class QuickPasteEntry
    {
        public string Title { get; set; }               // a user- or system-supplied Title that will be displayed to identify the QuickPaste Entry
        public List<QuickPasteItem> Items { get; set; } // a list of QuickPasteItems, each identifying a potential pastable control

        public QuickPasteEntry()
        {
        }
    }
    #endregion

    #region QuickPaste Static Methods
    // The CustomPaste class provides static utility functions for creating and altering quick paste entries.
    public static class QuickPaste
    {
        // Return QuickPaste Entry, where its title and each of its items represents a potential pastable control 
        public static QuickPasteEntry TryGetQuickPasteItemFromDataFields(FileDatabase fileDatabase, int rowIndex, string title)
        {
            QuickPasteEntry quickPasteEntry = new QuickPasteEntry()
            {
                Title = title,
                Items = new List<QuickPasteItem>()
            };

            if (fileDatabase.IsFileRowInRange(rowIndex) == false)
            {
                return null;
            }

            // Create quick paste entry for each non-standard control.
            foreach (ControlRow row in fileDatabase.Controls)
            {
                switch (row.Type)
                {
                    // User defined control types are also potential items to paste
                    case Constant.Control.FixedChoice:
                    case Constant.Control.Note:
                    case Constant.Control.Flag:
                    case Constant.Control.Counter:
                        quickPasteEntry.Items.Add(new QuickPasteItem(
                            row.DataLabel, 
                            row.Label, 
                            fileDatabase.Files[rowIndex].GetValueDisplayString(row.DataLabel), 
                            row.Copyable));
                        break;
                    default:
                        // Standard controls are not used in quick pastes, as it is unlikely the user will want to alter their contents
                        break;
                }
            }
            return quickPasteEntry;
        }

        // Transform the QuickPasteEntries data structure into an XML document that can will eventually be saved as a string in the ImageSetTable database
        public static string QuickPasteEntriesToXML(List<QuickPasteEntry> quickPasteEntries)
        {
            XDocument xDocument = new XDocument(new XElement("Entries",
                quickPasteEntries.Select(i => new XElement("Entry",
                     new XElement("Title", i.Title),
                        i.Items.Select(v => new XElement("Item",
                        new XElement("Label", v.Label),
                        new XElement("DataLabel", v.DataLabel),
                        new XElement("Value", v.Value.ToString()),
                     new XElement("Use", v.Use.ToString())))))));
            return xDocument.ToString();
        }

        // Transform the XML string (stroed in the ImageSetTable) into a QuickPasteEntries data structure 
        public static List<QuickPasteEntry> QuickPasteEntriesFromXML(string xml)
        {
            XDocument xDocument = XDocument.Parse(xml);

            IEnumerable entries =
                from r in xDocument.Descendants("Entry")
                select new QuickPasteEntry
                {
                    Title = (string)r.Element("Title"),
                    Items = (from v in r.Elements("Item")
                             select new QuickPasteItem
                             {
                                 DataLabel = (string)v.Element("DataLabel"),
                                 Label = (string)v.Element("Label"),
                                 Value = (string)v.Element("Value"),
                                 Use = (bool)v.Element("Use"),
                             }).ToList()
                };

            List<QuickPasteEntry> quickPasteEntries = new List<QuickPasteEntry>();
            foreach (QuickPasteEntry quickPasteEntry in entries)
            {
                quickPasteEntries.Add(quickPasteEntry);
            }
            return quickPasteEntries;
        }
        #endregion
    }
}
