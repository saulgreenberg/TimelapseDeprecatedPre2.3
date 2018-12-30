using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Controls;
using Timelapse.QuickPaste;

// A Partial class collecting the QuickPaste methods. 
// Essentially, a quickpaste event is received, 
// where the appropriate method listed afterwards is invoked depending on the event type
namespace Timelapse
{
    public partial class TimelapseWindow : Window, IDisposable
    {
        // The QuickPaste controls generate various events, depending on what the user selected.
        // Depending on the event received, perform the action indicated by the event
        private void QuickPasteWindow_QuickPasteEvent(object sender, QuickPasteEventArgs e)
        {
            switch (e.EventType)
            {
                case QuickPasteEventIdentifierEnum.New:
                    this.MenuItemNewQuickPaste_Click(null, null);
                    break;
                case QuickPasteEventIdentifierEnum.Edit:
                    this.OpenQuickPasteEditor(e.QuickPasteEntry);
                    break;
                case QuickPasteEventIdentifierEnum.Delete:
                    this.DeleteQuickPasteEntry(e.QuickPasteEntry);
                    break;
                case QuickPasteEventIdentifierEnum.MouseEnter:
                    HighlightQuickPaste(e.QuickPasteEntry);
                    break;
                case QuickPasteEventIdentifierEnum.MouseLeave:
                    UnHighlightQuickPaste(e.QuickPasteEntry);
                    break;
                case QuickPasteEventIdentifierEnum.Paste:
                    TryQuickPaste(e.QuickPasteEntry);
                    break;
                default:
                    break;
            }
        }
        
        // Highlight the controls affected by the Quickpaste entry
        private void HighlightQuickPaste(QuickPasteEntry quickPasteEntry)
        {
            if (!this.IsDisplayingSingleImage()) return; // only allow copying in single image mode

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

        private void UnHighlightQuickPaste(QuickPasteEntry quickPasteEntry)
        {
            if (!this.IsDisplayingSingleImage()) return; // only allow copying in single image mode

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
                        control.Container.ClearValue(Control.BackgroundProperty);
                        break;
                    }
                }
            }
        }

        // Quickpast the given entry
        private void TryQuickPaste(QuickPasteEntry quickPasteEntry)
        {
            if (!this.IsDisplayingSingleImage()) return; // only allow copying in single image mode

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

        // Opent the quickPaste Editor window
        private void OpenQuickPasteEditor(QuickPasteEntry quickPasteEntry)
        {
            if (quickPasteEntry == null)
            {
                return;
            }
            QuickPasteEditor quickPasteConfiguration = new QuickPasteEditor(quickPasteEntry)
            {
                Owner = this
            };

            if (quickPasteConfiguration.ShowDialog() == true)
            {
                quickPasteEntry = quickPasteConfiguration.QuickPasteEntry;
                QuickPasteUpdateAll();
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
