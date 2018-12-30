using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Controls;
using Timelapse.QuickPaste;

// A Partial class collecting the QuickPaste methods. 
namespace Timelapse
{
    public partial class TimelapseWindow : Window, IDisposable
    {
        // Show the QickPaste window
        private void ShowQuickPasteWindow()
        {
            if (this.quickPasteEntries == null)
            {
                return;
            }

            // If the quickpast window doesn't exist create it, and
            // add an event handler to it thatis used to generate events that identify the user action taken in that window
            if (this.quickPasteWindow == null || (!this.quickPasteWindow.IsLoaded))
            {
                // The quickPasteWindow hasn't been created yet, so do so.
                this.quickPasteWindow = new QuickPasteWindow()
                {
                    Owner = this,
                    QuickPasteEntries = this.quickPasteEntries
                };

                quickPasteWindow.QuickPasteEvent += this.QuickPasteWindow_QuickPasteEvent;
            }

            // Show the window
            this.quickPasteWindow.Show();
        }

        // Hide the QickPaste window
        private void HideQuickPasteWindow()
        {
            // If the quickpast window doesn't exist create it, and
            // add an event handler to it thatis used to generate events that identify the user action taken in that window
            if (this.quickPasteWindow != null && this.quickPasteWindow.IsLoaded)
            {
                this.quickPasteWindow.Hide();
            }
        }

        // The QuickPaste controls generate various events, depending on what the user selected.
        // Depending on the event received, perform the action indicated by the event by calling the appropriate method below
        private void QuickPasteWindow_QuickPasteEvent(object sender, QuickPasteEventArgs e)
        {
            switch (e.EventType)
            {
                case QuickPasteEventIdentifierEnum.New:
                    this.NewQuickPasteEntry();
                    break;
                case QuickPasteEventIdentifierEnum.Edit:
                    this.EditQuickPasteEntry(e.QuickPasteEntry);
                    break;
                case QuickPasteEventIdentifierEnum.Delete:
                    this.DeleteQuickPasteEntry(e.QuickPasteEntry);
                    break;
                case QuickPasteEventIdentifierEnum.MouseEnter:
                    HighlightQuickPasteDataControls(e.QuickPasteEntry);
                    break;
                case QuickPasteEventIdentifierEnum.MouseLeave:
                    UnHighlightQuickPasteDataControls(e.QuickPasteEntry);
                    break;
                case QuickPasteEventIdentifierEnum.Paste:
                    PasteQuickPasteEntryIntoDataControls(e.QuickPasteEntry);
                    break;
                default:
                    break;
            }
        }

        // Create a quickpaste entry from the current data controls,
        // add it to the quickpaste entries, and update the display and the ImageSetTable database as needed
        private void NewQuickPasteEntry()
        {
            string title = "QuickPaste #" + (this.quickPasteEntries.Count + 1).ToString();
            QuickPasteEntry quickPasteEntry = QuickPasteOperations.TryGetQuickPasteItemFromDataFields(this.dataHandler.FileDatabase, this.dataHandler.ImageCache.CurrentRow, title);
            if (quickPasteEntry == null)
            {
                return;
            }

            // Make sure the quickPasteWindow is not topmost, as it may otherwise occlude part of the QuickPaste Editor
            if (this.quickPasteWindow.IsLoaded)
            {
                this.quickPasteWindow.Topmost = false;
            }
            QuickPasteEditor quickPasteEditor = new QuickPasteEditor(quickPasteEntry, this.dataHandler.FileDatabase)
            {
                Owner = this
            };
            if (quickPasteEditor.ShowDialog() == true)
            {
                quickPasteEntry = quickPasteEditor.QuickPasteEntry;
                if (this.quickPasteEntries == null)
                {
                    // This shouldn't be necessary, but just in case...
                    this.quickPasteEntries = new List<QuickPasteEntry>();
                }
                this.quickPasteEntries.Add(quickPasteEntry);
                this.QuickPasteUpdateAll();
            }

            // Restore the quickPaste window back to its topmost state
            if (this.quickPasteWindow.IsLoaded)
            {
                this.quickPasteWindow.Topmost = true;
            }
        }

        // Open the quickPaste Editor window
        private void EditQuickPasteEntry(QuickPasteEntry quickPasteEntry)
        {
            if (quickPasteEntry == null)
            {
                return;
            }

            // Make sure the quickPasteWindow is not topmost, as it may otherwise occlude part of the QuickPaste Editor
            if (this.quickPasteWindow.IsLoaded)
            {
                this.quickPasteWindow.Topmost = false;
            }

            QuickPasteEditor quickPasteEditor = new QuickPasteEditor(quickPasteEntry, this.dataHandler.FileDatabase)
            {
                Owner = this
            };

            if (quickPasteEditor.ShowDialog() == true)
            {
                quickPasteEntry = quickPasteEditor.QuickPasteEntry;
                QuickPasteUpdateAll();
            }

            // Restore the quickPaste window back to its topmost state
            if (this.quickPasteWindow.IsLoaded)
            {
                this.quickPasteWindow.Topmost = true;
            }
        }

        // Highlight the data controls affected by the Quickpaste entry
        private void HighlightQuickPasteDataControls(QuickPasteEntry quickPasteEntry)
        {
            if (!this.IsDisplayingSingleImage())
            {
                return; // only allow copying in single image mode
            }

            this.FilePlayer_Stop(); // In case the FilePlayer is going
            int row = this.dataHandler.ImageCache.CurrentRow;
            if (!this.dataHandler.FileDatabase.IsFileRowInRange(row))
            {
                return; // This shouldn't happen, but just in case...
            }

            foreach (QuickPasteItem item in quickPasteEntry.Items)
            {
                if (item.Use == false)
                {
                    continue;
                }

                // Find the data entry control that matches the quickPasteItem's DataLael
                foreach (KeyValuePair<string, DataEntryControl> pair in this.DataEntryControls.ControlsByDataLabel)
                {
                    DataEntryControl control = pair.Value;
                    if (control.DataLabel == item.DataLabel)
                    {
                        control.Container.Background = Constant.Control.CopyableFieldHighlightBrush;
                        break;
                    }
                }
            }
        }

        // Unhighlight the data controls affected by the Quickpaste entry
        private void UnHighlightQuickPasteDataControls(QuickPasteEntry quickPasteEntry)
        {
            if (!this.IsDisplayingSingleImage())
            {
                return; // only allow copying in single image mode
            }

            this.FilePlayer_Stop(); // In case the FilePlayer is going
            int row = this.dataHandler.ImageCache.CurrentRow;
            if (!this.dataHandler.FileDatabase.IsFileRowInRange(row))
            {
                return; // This shouldn't happen, but just in case...
            }

            foreach (QuickPasteItem item in quickPasteEntry.Items)
            {
                // If the item wasn't used, then it wasn't highlit.
                if (item.Use == false)
                {
                    continue;
                }

                // Find the data entry control that matches the quickPasteItem's DataLael
                foreach (KeyValuePair<string, DataEntryControl> pair in this.DataEntryControls.ControlsByDataLabel)
                {
                    DataEntryControl control = pair.Value;
                    if (control.DataLabel == item.DataLabel)
                    {
                        control.Container.ClearValue(Control.BackgroundProperty);
                        break;
                    }
                }
            }
        }

        // Quickpast the given entry into the data control
        private void PasteQuickPasteEntryIntoDataControls(QuickPasteEntry quickPasteEntry)
        {
            if (!this.IsDisplayingSingleImage())
            {
                return; // only allow copying in single image mode
            }

            this.FilePlayer_Stop(); // In case the FilePlayer is going
            int row = this.dataHandler.ImageCache.CurrentRow;
            if (!this.dataHandler.FileDatabase.IsFileRowInRange(row))
            {
                return; // This shouldn't happen, but just in case...
            }

            foreach (QuickPasteItem item in quickPasteEntry.Items)
            {
                if (item.Use == false)
                {
                    continue;
                }

                // Find the data entry control that matches the quickPasteItem's DataLael
                foreach (KeyValuePair<string, DataEntryControl> pair in this.DataEntryControls.ControlsByDataLabel)
                {
                    DataEntryControl control = pair.Value;
                    if (control.DataLabel == item.DataLabel)
                    {
                        control.SetContentAndTooltip(item.Value);
                        break;
                    }
                }
            }
        }

        // Delete the quickPaste Entry from the quickPasteEntries
        private void DeleteQuickPasteEntry(QuickPasteEntry quickPasteEntry)
        {
            this.quickPasteEntries = QuickPasteOperations.DeleteQuickPasteEntry(quickPasteEntries, quickPasteEntry);
            this.QuickPasteUpdateAll();
        }

        // Update the Quickpaste XML in the ImageSetTable and refresh the Quickpaste window to reflect the current contents
        private void QuickPasteUpdateAll()
        {
            this.quickPasteWindow.Refresh(this.quickPasteEntries);
            this.dataHandler.FileDatabase.ImageSet.QuickPasteXML = QuickPasteOperations.QuickPasteEntriesToXML(this.quickPasteEntries);
        }
    }
}
