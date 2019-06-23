using System;
using System.Windows;
using Timelapse.Controls;

namespace Timelapse
{
    // Enabling or Disabling Menus and Controls
    public partial class TimelapseWindow : Window, IDisposable
    {
        private void EnableOrDisableMenusAndControls()
        {
            bool imageSetAvailable = this.IsFileDatabaseAvailable();
            bool filesSelected = (imageSetAvailable && this.dataHandler.FileDatabase.CurrentlySelectedFileCount > 0) ? true : false;

            // Depending upon whether images exist in the data set,
            // enable / disable menus and menu items as needed

            // File menu
            this.MenuItemAddFilesToImageSet.IsEnabled = imageSetAvailable;
            this.MenuItemLoadFiles.IsEnabled = !imageSetAvailable;
            this.MenuItemRecentImageSets.IsEnabled = !imageSetAvailable;
            this.MenuItemImportDetectionData.IsEnabled = filesSelected;
            this.MenuItemExportThisImage.IsEnabled = filesSelected;
            this.MenuItemExportAsCsvAndPreview.IsEnabled = filesSelected;
            this.MenuItemExportAsCsv.IsEnabled = filesSelected;
            this.MenuItemImportFromCsv.IsEnabled = filesSelected;
            this.MenuItemRenameFileDatabaseFile.IsEnabled = filesSelected;
            this.MenuFileCloseImageSet.IsEnabled = imageSetAvailable;
            this.MenuItemImportDetectionData.IsEnabled = imageSetAvailable;

            // Edit menu
            this.MenuItemEdit.IsEnabled = filesSelected;
            this.MenuItemDeleteCurrentFile.IsEnabled = filesSelected;
            // this.MenuItemAdvancedImageSetOptions.IsEnabled = imagesExist; SAULXXX: I don't think we need this anymore, as there is now a date correction option that does this. Remove it from the XAML as well, and delete that dialog?

            // Options menu
            // always enable at top level when an image set exists so that image set advanced options are accessible
            this.MenuItemOptions.IsEnabled = true; // imageSetAvailable;
            // this.MenuItemAdvancedDeleteDuplicates.IsEnabled = filesSelected; // DEPRACATED
            this.MenuItemAudioFeedback.IsEnabled = filesSelected;
            this.MenuItemEpisodeOptions.IsEnabled = filesSelected;
            this.MenuItemEpisodeShowHide.IsEnabled = filesSelected;
            this.MenuItemMagnifyingGlass.IsEnabled = imageSetAvailable;
            this.MenuItemDisplayMagnifyingGlass.IsChecked = imageSetAvailable && this.dataHandler.FileDatabase.ImageSet.MagnifyingGlassEnabled;
            this.MenuItemDialogsOnOrOff.IsEnabled = filesSelected;
            this.MenuItemAdvancedTimelapseOptions.IsEnabled = true;

            // View menu
            this.MenuItemView.IsEnabled = filesSelected;

            // Select menu
            this.MenuItemSelect.IsEnabled = filesSelected;

            // Sort menu
            this.MenuItemSort.IsEnabled = filesSelected;

            // Recognition menu
            this.MenuItemImportDetectionData.IsEnabled = filesSelected;
            this.MenuItemDetectorOptions.IsEnabled = filesSelected;

            // Windows menu is always enabled

            // Enablement state of the various other UI components.
            this.ControlsPanel.IsEnabled = filesSelected;  // If images don't exist, the user shouldn't be allowed to interact with the control tray
            this.FileNavigatorSlider.IsEnabled = filesSelected;
            this.MarkableCanvas.IsEnabled = filesSelected;
            this.MarkableCanvas.MagnifyingGlassEnabled = filesSelected && this.dataHandler.FileDatabase.ImageSet.MagnifyingGlassEnabled;

            if (filesSelected == false)
            {
                this.FileShow(Constant.DatabaseValues.InvalidRow);
                this.StatusBar.SetMessage("Image set is empty.");
                this.StatusBar.SetCurrentFile(0);
                this.StatusBar.SetCount(0);
            }
        }

        // Enable or disable the various menu items that allow images to be manipulated
        private void EnableImageManipulationMenus(bool enable)
        {
            this.MenuItemZoomIn.IsEnabled = enable;
            this.MenuItemZoomOut.IsEnabled = enable;
            this.MenuItemViewDifferencesCycleThrough.IsEnabled = enable;
            this.MenuItemViewDifferencesCombined.IsEnabled = enable;
            this.MenuItemDisplayMagnifyingGlass.IsEnabled = enable;
            this.MenuItemMagnifyingGlassIncrease.IsEnabled = enable;
            this.MenuItemMagnifyingGlassDecrease.IsEnabled = enable;
            this.MenuItemBookmarkSavePanZoom.IsEnabled = enable;
            this.MenuItemBookmarkSetPanZoom.IsEnabled = enable;
            this.MenuItemBookmarkDefaultPanZoom.IsEnabled = enable;
        }
    }
}
