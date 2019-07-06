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
                // PERHAPS BUG - this only works when the datagrid pane is in a tab, and when files are loaded.
                // Maybe we need to change the enable state?
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
            this.Handle_PreviewKeyDown(currentKey, false);
        }

        // There is a bug in avalondock, where a floating window will always have the IsRepeat set to true. 
        // Thus we have to implement our own version of it
        private void Window_PreviewKeyUp(object sender, KeyEventArgs currentKey)
        {
            // Force the end of a key repeat cycle
            this.state.ResetKeyRepeat();
        }

        public void Handle_PreviewKeyDown(KeyEventArgs currentKey, bool forceSendToMainWindow)
        { 
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

            // Next, - but only if forceSendToMainWindow is true,
            // don't interpret keyboard shortcuts if the focus is on a control in the control grid, as the text entered may be directed
            // to the controls within it. That is, if a textbox or combo box has the focus, then take no as this is normal text input
            // and NOT a shortcut key.  Similarly, if a menu is displayed keys should be directed to the menu rather than interpreted as
            // shortcuts.
            if (forceSendToMainWindow == false && this.SendKeyToDataEntryControlOrMenu(currentKey))
            {
                return;
            }

            // Finally, test for other shortcut keys and take the appropriate action as needed
            DirectionEnum direction;
            int keyRepeatCount = this.state.GetKeyRepeatCount(currentKey);
            switch (currentKey.Key)
            {
                case Key.B:                 // Save a Bookmark of the current pan / zoom level of the image
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
                    this.FilePlayer_Stop();      // In case the FilePlayer is going
                    this.MarkableCanvas.MagnifierZoomIn();
                    break;
                case Key.D:                 // Decrease the magnifing glass zoom level
                    this.FilePlayer_Stop();      // In case the FilePlayer is going
                    this.MarkableCanvas.MagnifierZoomOut();
                    break;
                case Key.Right:             // next /previous image
                case Key.Left:              // previous image
                    this.FilePlayer_Stop();      // In case the FilePlayer is going
                    direction = currentKey.Key == Key.Right ? DirectionEnum.Next : DirectionEnum.Previous;
                    if (currentKey.IsRepeat == false || (currentKey.IsRepeat == true && keyRepeatCount  % this.state.Throttles.RepeatedKeyAcceptanceInterval == 0))
                    {
                        this.TryFileShowWithoutSliderCallback(direction, Keyboard.Modifiers);
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
                        this.FilePlayer_Stop(); // In case the FilePlayer is going
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
                        this.FilePlayer_Stop(); // In case the FilePlayer is going
                        this.TryViewCombinedDifference();
                    }
                    break;
                case Key.C:
                    this.CopyPreviousValues_Click();
                    break;
                case Key.E:
                    this.MenuItemEpisodeShowHide_Click(null, null);
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
                    this.FilePlayer_Stop(); // In case the FilePlayer is going
                    this.MoveFocusToNextOrPreviousControlOrCopyPreviousButton(Keyboard.Modifiers == ModifierKeys.Shift);
                    break;
                case Key.PageDown:
                case Key.PageUp:
                    direction = currentKey.Key == Key.PageDown ? DirectionEnum.Next : DirectionEnum.Previous;
                    if (IsDisplayingMultipleImagesInOverview())
                    {
                        this.FilePlayer.Direction = direction;
                        this.FilePlayer_ScrollPage();
                    }
                    else
                    {
                        this.FilePlayer_Stop();      // In case the FilePlayer is going
                        if (currentKey.IsRepeat == false || (currentKey.IsRepeat == true && keyRepeatCount % this.state.Throttles.RepeatedKeyAcceptanceInterval == 0))
                        {
                            this.TryFileShowWithoutSliderCallback(direction, Keyboard.Modifiers);
                        }
                    }
                    break;
                case Key.Home:
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
