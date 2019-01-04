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
        // Show the QuickPaste window
        private void QuickPasteWindowShow()
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
                    QuickPasteEntries = this.quickPasteEntries,
                    Topmost = false
                };

                quickPasteWindow.QuickPasteEvent += this.QuickPasteWindow_QuickPasteEvent;
            }

            // Show the window if it makes sense to do soo
            if (this.IsFileDatabaseAvailable() && this.dataHandler.FileDatabase.CurrentlySelectedFileCount > 0)
            { 
                this.quickPasteWindow.Show();
            }
        }

        // Terminate the QickPaste window
        private void QuickPasteWindowHide()
        {
            // If the quickpast window doesn't exist create it, and
            // add an event handler to it thatis used to generate events that identify the user action taken in that window
            if (this.quickPasteWindow != null && this.quickPasteWindow.IsLoaded)
            {
                this.quickPasteWindow.Hide();
                this.quickPasteWindow.Visibility = Visibility.Collapsed;
            }
        }

        private void QuickPasteWindowTerminate()
        {
            if (this.quickPasteWindow != null)
            {
                this.quickPasteWindow.Close();
                this.quickPasteWindow = null;
            }
        }

        // The QuickPaste controls generate various events, depending on what the user selected.
        // Depending on the event received, perform the action indicated by the event by calling the appropriate method below
        private void QuickPasteWindow_QuickPasteEvent(object sender, QuickPasteEventArgs e)
        {
            switch (e.EventType)
            {
                case QuickPasteEventIdentifierEnum.New:
                    this.QuickPasteEntryNew();
                    break;
                case QuickPasteEventIdentifierEnum.Edit:
                    this.QuickPasteEntryEdit(e.QuickPasteEntry);
                    break;
                case QuickPasteEventIdentifierEnum.Delete:
                    this.QuickPasteEntryDelete(e.QuickPasteEntry);
                    break;
                case QuickPasteEventIdentifierEnum.MouseEnter:
                    QuickPasteDataControlsHighlight(e.QuickPasteEntry);
                    break;
                case QuickPasteEventIdentifierEnum.MouseLeave:
                    QuickPasteDataControlsUnHighlight(e.QuickPasteEntry);
                    break;
                case QuickPasteEventIdentifierEnum.Paste:
                    QuickPasteEntryPasteIntoDataControls(e.QuickPasteEntry);
                    break;
                default:
                    break;
            }
        }

        // Create a quickpaste entry from the current data controls,
        // add it to the quickpaste entries, and update the display and the ImageSetTable database as needed
        private void QuickPasteEntryNew()
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
                this.QuickPasteRefreshWindowAndXML();
            }

            // Restore the quickPaste window back to its topmost state
            if (this.quickPasteWindow.IsLoaded)
            {
                this.quickPasteWindow.Topmost = true;
            }
        }

        // Delete the quickPaste Entry from the quickPasteEntries
        private void QuickPasteEntryDelete(QuickPasteEntry quickPasteEntry)
        {
            this.quickPasteEntries = QuickPasteOperations.DeleteQuickPasteEntry(quickPasteEntries, quickPasteEntry);
            this.QuickPasteRefreshWindowAndXML();
        }

        // Open the quickPaste Editor window
        private void QuickPasteEntryEdit(QuickPasteEntry quickPasteEntry)
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
                QuickPasteRefreshWindowAndXML();
            }

            // Restore the quickPaste window back to its topmost state
            if (this.quickPasteWindow.IsLoaded)
            {
                this.quickPasteWindow.Topmost = true;
            }
        }

        // Highlight the data controls affected by the Quickpaste entry
        private void QuickPasteDataControlsHighlight(QuickPasteEntry quickPasteEntry)
        {
            if (!this.IsDisplayingSingleImage())
            {
                return; // only allow highightng in single image mode
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
                        control.Container.Background = Constant.Control.QuickPasteFieldHighlightBrush;
                        control.ShowPreviewControlValue(item.Value);
                    }
                }
            }
        }

        // Unhighlight the data controls affected by the Quickpaste entry
        private void QuickPasteDataControlsUnHighlight(QuickPasteEntry quickPasteEntry)
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
                        control.HidePreviewControlValue();
                        break;
                    }
                }
            }
        }

        // Quickpast the given entry into the data control
        private void QuickPasteEntryPasteIntoDataControls(QuickPasteEntry quickPasteEntry)
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
                        control.FlashPreviewControlValue();
                        break;
                    }
                }
            }
            this.MarkableCanvas.Focus();
        }

        // Update the Quickpaste XML in the ImageSetTable and refresh the Quickpaste window to reflect the current contents
        private void QuickPasteRefreshWindowAndXML()
        {
            this.quickPasteWindow.Refresh(this.quickPasteEntries);
            this.dataHandler.FileDatabase.ImageSet.QuickPasteXML = QuickPasteOperations.QuickPasteEntriesToXML(this.quickPasteEntries);
        }
    }
}
