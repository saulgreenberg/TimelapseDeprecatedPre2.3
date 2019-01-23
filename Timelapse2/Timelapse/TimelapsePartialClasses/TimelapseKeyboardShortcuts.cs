using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using Timelapse.Database;
using Timelapse.Enums;
using Timelapse.Util;

namespace Timelapse
{
    // Keyboard shortcuts
    public partial class TimelapseWindow : Window, IDisposable
    {
        // If its an arrow key and the textbox doesn't have the focus,
        // navigate left/right image or up/down to look at differenced image
        private void Window_PreviewKeyDown(object sender, KeyEventArgs currentKey)
        {
            if (this.dataHandler == null ||
                this.dataHandler.FileDatabase == null ||
                this.dataHandler.FileDatabase.CurrentlySelectedFileCount == 0)
            {
                // SAULXXX: BUG - this only works when the datagrid pane is in a tab, and when files are loaded.
                // Perhaps we need to change the enable state?
                switch (currentKey.Key)
                {
                    case Key.Home:
                        this.ImageSetPane.IsEnabled = true;
                        this.ImageSetPane.IsSelected = true;
                        break;
                    case Key.End:
                        this.DataGridPane.IsEnabled = true;
                        this.DataGridPane.IsSelected = true;
                        // SAULXXX: If its floating, we should really be making it topmost
                        // To do that, we would have to iterate through the floating windows and set it.
                        // if (this.DataGridPane.IsFloating)
                        // {

                        // }
                        break;
                    default:
                        break;
                }
                return; // No images are loaded, so don't try to interpret any keys
            }

            // First, try to interpret key as a possible valid quickpaste shortcut key. 
            // If so, send it to the Quickpaste window and mark the event as handled.
            if ((Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) &&
                 ((currentKey.Key >= Key.D0 && currentKey.Key <= Key.D9) || (currentKey.Key >= Key.NumPad0 && currentKey.Key <= Key.NumPad9)))
            {
                if (this.quickPasteWindow != null && this.quickPasteWindow.Visibility == Visibility.Visible)
                {
                    // The quickpaste window is visible, and thus able to take shortcuts.
                    string key = new KeyConverter().ConvertToString(currentKey.Key);
                    if (key.StartsWith("NumPad"))
                    {
                        key = key.Remove(0, 6);
                    }
                    if (Int32.TryParse(key, out int shortcutIndex) && shortcutIndex != 0)
                    {
                        this.quickPasteWindow.TryQuickPasteShortcut(shortcutIndex);
                        currentKey.Handled = true;
                    }
                } 
                return;
            }

            // Next, don't interpret keyboard shortcuts if the focus is on a control in the control grid, as the text entered may be directed
            // to the controls within it. That is, if a textbox or combo box has the focus, then take no as this is normal text input
            // and NOT a shortcut key.  Similarly, if a menu is displayed keys should be directed to the menu rather than interpreted as
            // shortcuts.
            if (this.SendKeyToDataEntryControlOrMenu(currentKey))
            {
                return;
            }

            // Finally, test for other shortcut keys and take the appropriate action as needed
            int keyRepeatCount = this.state.GetKeyRepeatCount(currentKey);
            switch (currentKey.Key)
            {
                case Key.B:                 // Bookmark (Save) the current pan / zoom level of the image
                    this.MarkableCanvas.SetBookmark();
                    break;
                case Key.Escape:
                    this.TrySetKeyboardFocusToMarkableCanvas(false, currentKey);
                    break;
                case Key.OemPlus:           // Restore the zoom level / pan coordinates of the bookmark
                    this.MarkableCanvas.ApplyBookmark();
                    break;
                case Key.OemMinus:          // Restore the zoom level / pan coordinates of the bookmark
                    this.MarkableCanvas.ZoomOutAllTheWay();
                    break;
                case Key.M:                 // Toggle the magnifying glass on and off
                    this.MenuItemDisplayMagnifyingGlass_Click(this, null);
                    break;
                case Key.U:                 // Increase the magnifing glass zoom level
                    FilePlayer_Stop();      // In case the FilePlayer is going
                    this.MarkableCanvas.MagnifierZoomIn();
                    break;
                case Key.D:                 // Decrease the magnifing glass zoom level
                    FilePlayer_Stop();      // In case the FilePlayer is going
                    this.MarkableCanvas.MagnifierZoomOut();
                    break;
                case Key.Right:             // next image
                    FilePlayer_Stop();      // In case the FilePlayer is going
                    // There appears to be a bug associated with avalondock, where a floating window will always have the IsRepeat set to true. No idea why...
                    // I suspect as well that the repeat count may be wrong - to test.
                    // So I disabled throttling as otherwise it throttles when it shouldn't
                    if (currentKey.IsRepeat == false || (currentKey.IsRepeat == true && keyRepeatCount % this.state.Throttles.RepeatedKeyAcceptanceInterval == 0))
                    {
                        this.TryFileShowWithoutSliderCallback(DirectionEnum.Next, Keyboard.Modifiers);
                    }
                    break;
                case Key.Left:              // previous image
                    FilePlayer_Stop();      // In case the FilePlayer is going
                    if (currentKey.IsRepeat == false || (currentKey.IsRepeat == true && keyRepeatCount % this.state.Throttles.RepeatedKeyAcceptanceInterval == 0))
                    {
                        this.TryFileShowWithoutSliderCallback(DirectionEnum.Previous, Keyboard.Modifiers);
                    }
                    break;
                case Key.Up:                // show visual difference to next image
                    if (IsDisplayingMultipleImagesInOverview())
                    {
                        this.FilePlayer.Direction = DirectionEnum.Previous;
                        this.FilePlayer_ScrollRow();
                    }
                    else
                    {
                        FilePlayer_Stop(); // In case the FilePlayer is going
                        this.TryViewPreviousOrNextDifference();
                    }
                    break;
                case Key.Down:              // show visual difference to previous image
                    if (IsDisplayingMultipleImagesInOverview())
                    {
                        this.FilePlayer.Direction = DirectionEnum.Next;
                        this.FilePlayer_ScrollRow();
                    }
                    else
                    {
                        FilePlayer_Stop(); // In case the FilePlayer is going
                        this.TryViewCombinedDifference();
                    }
                    break;
                case Key.C:
                    this.CopyPreviousValues_Click();
                    break;
                case Key.Z:
                    // NOTE: WE SHOULD DO THE GETEPISODES IN A SELECT CALLBACK, NOT HERE. THIS IS JUST FOR TESTING.
                    Episodes.ShowEpisodes = !Episodes.ShowEpisodes;
                    if (Episodes.ShowEpisodes)
                    {
                        Episodes.SetEpisodesFromFileTable(this.dataHandler.FileDatabase.FileTable);
                    }
                    else
                    {
                        // may not be needed
                        // Episodes.EpisodesList = new Dictionary<int, Tuple<int, int>>(); 
                    }
                    this.TryFileShowWithoutSliderCallback(); // force the display of the episode number
                    break;
                case Key.Q:
                    // Toggle the QuickPaste window
                    if (this.quickPasteWindow == null || (this.quickPasteWindow.Visibility != Visibility.Visible))
                    {
                        this.QuickPasteWindowShow();
                    }
                    else
                    {
                        this.QuickPasteWindowHide();
                    }
                    break;
                case Key.Tab:
                    FilePlayer_Stop(); // In case the FilePlayer is going
                    this.MoveFocusToNextOrPreviousControlOrCopyPreviousButton(Keyboard.Modifiers == ModifierKeys.Shift);
                    break;
                case Key.PageDown:
                    if (IsDisplayingMultipleImagesInOverview())
                    {
                        this.FilePlayer.Direction = DirectionEnum.Next;
                        this.FilePlayer_ScrollPage();
                    }
                    break;
                case Key.PageUp:
                    if (IsDisplayingMultipleImagesInOverview())
                    {
                        this.FilePlayer.Direction = DirectionEnum.Previous;
                        this.FilePlayer_ScrollPage();
                    }
                    break;
                case Key.Home:
                    {
                        this.ImageSetPane.IsActive = true;
                        break;
                    }
                case Key.End:
                    {
                        this.DataGridPane.IsActive = true;
                        break;
                    }
                default:
                    return;
            }
            currentKey.Handled = true;
        }
    }
}
