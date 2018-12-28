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

    #region Data Structures
    // Data Structure: QuickPasteItem
    // Captures the essence of a data control and its value, where:
    // DataLabel identifies the control
    // Label is used to identify the control to the user 
    // Use indicates whether the item should be pasted or not (this can be set be the user) 
    // Value is the data can be pasted into a single data control 

    public class QuickPasteItem
    {
        public string DataLabel { get; set; }
        public string Label { get; set; }
        public bool Use { get; set; }
        public string Value { get; set; }

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

    // Data Structure: QuickPasteEntry 
    // 
    // - a user- or system-supplied Title that will be displayed to identify the QuickPaste
    // - a list of QuickPasteItems, each identifying a potential pastable control
    public class QuickPasteEntry
    {
        public string Title { get; set; }
        public List<QuickPasteItem> Items { get; set; }

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

        public static string QuickPasteEntriesToXML(List<QuickPasteEntry> quickPasteEntries)
        {

            XDocument xDocument = new XDocument(new XElement("Entries",
                quickPasteEntries.Select(i => new XElement("Entry",
                     new XElement("Title", i.Title),
                        i.Items.Select(v => new XElement("Item",
                        new XElement("Label", v.Label),
                        new XElement("DataLabel", v.DataLabel),
                        new XElement("Value", v.Value.ToString()),
                     new XElement("Use", v.Use.ToString()
                )))))));
            //System.Diagnostics.Debug.Print(xDocument.ToString());
            return xDocument.ToString();
        }

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
                             }
                   ).ToList()
                };

            List<QuickPasteEntry> quickPasteEntries = new List<QuickPasteEntry>();
            foreach (QuickPasteEntry quickPasteEntry in entries)
            {
                quickPasteEntries.Add(quickPasteEntry);
            }

            foreach (QuickPasteEntry quickPasteEntry in quickPasteEntries)
            { 
                System.Diagnostics.Debug.Print(">" + quickPasteEntry.Title);
                foreach(QuickPasteItem item in quickPasteEntry.Items)
                {
                    System.Diagnostics.Debug.Print("-->" + item.DataLabel + " " + item.Label + "|" + item.Value + " " + item.Use.ToString());
                }
            }
            
    

            //List<QuickPasteEntry> quickPasteEntries =
            //     IEnumerable com = from i in xDocument.Elements("Entries")
            //         .Elements("Entry")
            //         select new QuickPasteEntry
            //          {
            //             Title = (string) i.Element("Title"),
            //             from k in .Elements("Item")
            //             select new QuickPasteItem
            //          {
            //                 DataLabel = string(k.Element("DataLabel"))
            //             Label = string(k.Element("Label"))
            //             Value = string(k.Element("Value"))
            //             Use = Boolean(k.Element("Use"))
            //             }
            //         };

            //// List<QuickPasteEntry> quickPasteEntries = new List<QuickPasteEntry>();

            return quickPasteEntries;
        }
        #endregion
    }
}
