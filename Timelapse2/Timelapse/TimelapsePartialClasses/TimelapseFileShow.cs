﻿using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Timelapse.Controls;
using Timelapse.Images;

namespace Timelapse
{
    // Showing Files
    public partial class TimelapseWindow : Window, IDisposable
    {
        // FileShow is invoked here from a 1-based slider, so we need to correct it to the 0-base index
        // By default, don't force the update
        private void FileShow(Slider fileNavigatorSlider)
        {
            this.FileShow((int)fileNavigatorSlider.Value - 1, true, false);
        }

        // FileShow is invoked from elsewhere than from the slider. 
        // By default, don't force the update
        private void FileShow(int fileIndex)
        {
            this.FileShow(fileIndex, false, false);
        }

        // FileShow is invoked from elsewhere than from the slider. 
        // The argument specifies whether we should force the update
        private void FileShow(int fileIndex, bool forceUpdate)
        {
            this.FileShow(fileIndex, false, forceUpdate);
        }

        // Show the image in the specified row, but only if its a different image.
        private void FileShow(int fileIndex, bool isInSliderNavigation, bool forceUpdate)
        {
            // If there is no image set open, or if there is no image to show, then show an image indicating the empty image set.
            if (this.IsFileDatabaseAvailable() == false || this.dataHandler.FileDatabase.CurrentlySelectedFileCount < 1)
            {
                this.MarkableCanvas.SetNewImage(Constant.ImageValues.NoFilesAvailable.Value, null);
                this.markersOnCurrentFile = null;
                this.MarkableCanvas_UpdateMarkers();
                this.MarkableCanvas.SwitchToImageView();

                // We could invalidate the cache here, but it will be reset anyways when images are loaded. 
                if (this.dataHandler != null)
                {
                    this.dataHandler.IsProgrammaticControlUpdate = false;
                }

                // We also need to do a bit of cleanup of UI elements that make no sense when there are no images to show.
                this.QuickPasteWindowHide();
                return;
            }

            // If we are already showing the desired file, and if we are not forcing an update, 
            // then abort as there is no need to redisplay the image.
            if (this.dataHandler.ImageCache.CurrentRow == fileIndex && forceUpdate == false)
            {
                return;
            }

            // Reset the Clickable Images Grid to the current image
            // SAULXX: COULD SET FOLDER PATH AND FILEDATABASE ON LOAD, BUT MAY BE BETTER TO JUST KEEP ON DOING IT HERE
            this.MarkableCanvas.ClickableImagesGrid.FolderPath = this.FolderPath;
            this.MarkableCanvas.ClickableImagesGrid.FileTableStartIndex = fileIndex;
            this.MarkableCanvas.ClickableImagesGrid.FileTable = this.dataHandler.FileDatabase.Files;

            // for the bitmap caching logic below to work this should be the only place where code in TimelapseWindow moves the image enumerator
            if (this.dataHandler.ImageCache.TryMoveToFile(fileIndex, forceUpdate, out bool newFileToDisplay) == false)
            {
                if (this.dataHandler != null)
                {
                    this.dataHandler.IsProgrammaticControlUpdate = false;
                }
                throw new Exception(String.Format("in FileShow: possible problem with fileIndex, where its not a valid row index in the image table.", fileIndex));
            }

            // Update each control with the data for the now current image
            // This is always done as it's assumed either the image changed or that a control refresh is required due to database changes
            // the call to TryMoveToImage() above refreshes the data stored under this.dataHandler.ImageCache.Current.
            this.dataHandler.IsProgrammaticControlUpdate = true;
            foreach (KeyValuePair<string, DataEntryControl> control in this.DataEntryControls.ControlsByDataLabel)
            {
                // update value
                string controlType = this.dataHandler.FileDatabase.FileTableColumnsByDataLabel[control.Key].ControlType;
                control.Value.SetContentAndTooltip(this.dataHandler.ImageCache.Current.GetValueDisplayString(control.Value.DataLabel));

                // for note controls, update the autocomplete list if an edit occurred
                if (controlType == Constant.Control.Note)
                {
                    DataEntryNote noteControl = (DataEntryNote)control.Value;
                    if (noteControl.ContentChanged)
                    {
                        noteControl.ContentControl.Autocompletions = this.dataHandler.FileDatabase.GetDistinctValuesInFileDataColumn(control.Value.DataLabel);
                        noteControl.ContentChanged = false;
                    }
                }
            }
            this.dataHandler.IsProgrammaticControlUpdate = false;

            // update the status bar to show which image we are on out of the total displayed under the current selection
            // the total is always refreshed as it's not known if FileShow() is being called due to a change in the selection
            this.StatusBar.SetCurrentFile(fileIndex + 1); // Add one because indexes are 0-based
            this.StatusBar.SetCount(this.dataHandler.FileDatabase.CurrentlySelectedFileCount);
            this.StatusBar.ClearMessage();

            this.FileNavigatorSlider.Value = fileIndex + 1;

            // display new file if the file changed
            // this avoids unnecessary image reloads and refreshes in cases where FileShow() is just being called to refresh controls
            this.markersOnCurrentFile = this.dataHandler.FileDatabase.GetMarkersOnFile(this.dataHandler.ImageCache.Current.ID);
            List<Marker> displayMarkers = this.GetDisplayMarkers();

            if (newFileToDisplay)
            {
                if (this.dataHandler.ImageCache.Current.IsVideo)
                {
                    this.MarkableCanvas.SetNewVideo(this.dataHandler.ImageCache.Current.GetFileInfo(this.dataHandler.FileDatabase.FolderPath), displayMarkers);
                    this.EnableImageManipulationMenus(false);
                }
                else
                {
                    this.MarkableCanvas.SetNewImage(this.dataHandler.ImageCache.GetCurrentImage(), displayMarkers);
                    // Draw markers for this file
                    this.MarkableCanvas_UpdateMarkers();
                    this.EnableImageManipulationMenus(true);
                }
            }
            else if (this.IsDisplayingSingleImage())
            {
                if (this.dataHandler.ImageCache.Current.IsVideo)
                {
                    this.MarkableCanvas.SwitchToVideoView();
                }
                else
                {
                    this.MarkableCanvas.SwitchToImageView();
                    this.MarkableCanvas_UpdateMarkers();
                }
            }
            this.DataGridSelectionsTimer_Reset();

            // Set the file player status
            if (this.dataHandler.ImageCache.CurrentRow == 0)
            {
                this.FilePlayer.BackwardsControlsEnabled(false);
            }
            else
            {
                this.FilePlayer.BackwardsControlsEnabled(true);
            }

            if (this.dataHandler.ImageCache.CurrentRow == this.dataHandler.FileDatabase.CurrentlySelectedFileCount - 1)
            {
                this.FilePlayer.ForwardsControlsEnabled(false);
            }
            else
            {
                this.FilePlayer.ForwardsControlsEnabled(true);
            }

            // Refresh the CopyPreviousButton and its Previews as needed
            this.CopyPreviousValuesSetEnableStatePreviewsAndGlowsAsNeeded();

            // Refresh the QuickPasteEntry previews if needed
            if (this.IsDisplayingSingleImage() && this.quickPasteWindow != null)
            {
                this.quickPasteWindow.RefreshQuickPasteWindowPreviewAsNeeded();
            }

            // Refresh the markable canvas if needed
            this.MarkableCanvas.RefreshIfMultipleImagesAreDisplayed(isInSliderNavigation, forceUpdate);
        }

        private bool TryFileShowWithoutSliderCallback(bool forward, ModifierKeys modifiers)
        {
            // Check to see if there are any images to show, 
            if (this.dataHandler.FileDatabase.CurrentlySelectedFileCount <= 0)
            {
                return false;
            }
            // determine how far to move and in which direction
            int increment = 1;
            if ((modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
            {
                increment *= 5;
            }
            if ((modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                increment *= 10;
            }
            return TryFileShowWithoutSliderCallback(forward, increment);
        }

        private bool TryFileShowWithoutSliderCallback(bool forward, int increment)
        {
            // Check to see if there are any images to show, 
            if (this.dataHandler.FileDatabase.CurrentlySelectedFileCount <= 0)
            {
                return false;
            }

            int desiredRow = forward
                ? this.dataHandler.ImageCache.CurrentRow + increment
                : this.dataHandler.ImageCache.CurrentRow - increment;

            // Set the desiredRow to either the maximum or minimum row if it exceeds the bounds,
            if (desiredRow >= this.dataHandler.FileDatabase.CurrentlySelectedFileCount)
            {
                desiredRow = this.dataHandler.FileDatabase.CurrentlySelectedFileCount - 1;
            }
            else if (desiredRow < 0)
            {
                desiredRow = 0;
            }

            // If the desired row is the same as the current row, the image is already being displayed
            if (desiredRow != this.dataHandler.ImageCache.CurrentRow)
            {
                // Move to the desired row
                this.FileNavigatorSlider_EnableOrDisableValueChangedCallback(false);
                this.FileShow(desiredRow);
                this.FileNavigatorSlider_EnableOrDisableValueChangedCallback(true);
            }
            return true;
        }
    }
}