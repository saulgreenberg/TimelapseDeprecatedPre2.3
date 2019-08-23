using System;
using System.Windows;
using System.Windows.Input;
using Timelapse.Dialog;
using Timelapse.Images;
using Timelapse.Util;

namespace Timelapse
{
    // Options Menu Callbacks
    public partial class TimelapseWindow : Window, IDisposable
    {
        // Options sub-menu opening
        private void Options_SubmenuOpening(object sender, RoutedEventArgs e)
        {
            FilePlayer_Stop(); // In case the FilePlayer is going
        }

        // Audio feedback: toggle on / off
        private void MenuItemAudioFeedback_Click(object sender, RoutedEventArgs e)
        {
            // We don't have to do anything here...
            this.state.AudioFeedback = !this.state.AudioFeedback;
            this.MenuItemAudioFeedback.IsChecked = this.state.AudioFeedback;
        }

        // Display Magnifier: toggle on / off
        private void MenuItemDisplayMagnifyingGlass_Click(object sender, RoutedEventArgs e)
        {
            this.dataHandler.FileDatabase.ImageSet.MagnifyingGlassEnabled = !this.dataHandler.FileDatabase.ImageSet.MagnifyingGlassEnabled;
            this.MarkableCanvas.MagnifyingGlassEnabled = this.dataHandler.FileDatabase.ImageSet.MagnifyingGlassEnabled;
            this.MenuItemDisplayMagnifyingGlass.IsChecked = this.dataHandler.FileDatabase.ImageSet.MagnifyingGlassEnabled;
        }

        // Increase magnification of the magnifying glass. 
        private void MenuItemMagnifyingGlassIncrease_Click(object sender, RoutedEventArgs e)
        {
            // Increase the magnification by several steps to make
            // the effect more visible through a menu option versus the keyboard equivalent
            for (int i = 0; i < 6; i++)
            {
                this.MarkableCanvas.MagnifierZoomIn();
            }
        }

        // Decrease the magnification of the magnifying glass. 
        private void MenuItemMagnifyingGlassDecrease_Click(object sender, RoutedEventArgs e)
        {
            // Decrease the magnification by several steps to make
            // the effect more visible through a menu option versus the keyboard equivalent
            for (int i = 0; i < 6; i++)
            {
                this.MarkableCanvas.MagnifierZoomOut();
            }
        }

        // Adjust FilePlayer playback speeds
        private void MenuItemFilePlayerOptions_Click(object sender, RoutedEventArgs e)
        {
            FilePlayerOptions filePlayerOptions = new FilePlayerOptions(this.state, this);
            filePlayerOptions.ShowDialog();
        }

        private void MenuItemEpisodeShowHide_Click(object sender, RoutedEventArgs e)
        {
            Episodes.ShowEpisodes = !Episodes.ShowEpisodes;
            MenuItemEpisodeShowHide.IsChecked = Episodes.ShowEpisodes;

            if (this.IsDisplayingMultipleImagesInOverview())
            {
                this.MarkableCanvas.RefreshIfMultipleImagesAreDisplayed(false, false);
            }
            else
            {
                this.DisplayEpisodeTextIfWarranted(this.dataHandler.ImageCache.CurrentRow);
            }
        }

        private void MenuItemEpisodeOptions_Click(object sender, RoutedEventArgs e)
        {
            EpisodeOptions episodeOptions = new EpisodeOptions(this.state.EpisodeTimeThreshold, this);
            bool? result = episodeOptions.ShowDialog();
            if (result == true)
            {
                // the time threshold has changed, so save its new state
                this.state.EpisodeTimeThreshold = episodeOptions.EpisodeTimeThreshold;
                Episodes.TimeThreshold = this.state.EpisodeTimeThreshold; // so we don't have to pass it as a parameter
                Episodes.Reset();
            }

            if (this.IsDisplayingMultipleImagesInOverview())
            {
                this.MarkableCanvas.RefreshIfMultipleImagesAreDisplayed(false, false);
            }
            else
            {
                this.DisplayEpisodeTextIfWarranted(this.dataHandler.ImageCache.CurrentRow);
            }
        }

        // Hide or show various informational dialogs"
        private void MenuItemDialogsOnOrOff_Click(object sender, RoutedEventArgs e)
        {
            DialogsHideOrShow dialog = new DialogsHideOrShow(this.state, this);
            dialog.ShowDialog();
        }

        private void MenuItemGenerateVideoThumbnails_Click(object sender, RoutedEventArgs e)
        {
            string[] videoFileExtensions = { Constant.File.AviFileExtension, Constant.File.Mp4FileExtension };
            VideoThumbnailer.GenerateVideoThumbnailsInAllFolders(this.FolderPath, Constant.File.VideoThumbnailFolderName, videoFileExtensions);
        }

        private void MenuItemDeleteVideoThumbnails_Click(object sender, RoutedEventArgs e)
        {
            VideoThumbnailer.DeleteVideoThumbnailsInAllFolders(this.FolderPath, Constant.File.VideoThumbnailFolderName);
        }

        /// <summary>Show advanced Timelapse options</summary>
        private void MenuItemAdvancedTimelapseOptions_Click(object sender, RoutedEventArgs e)
        {
            AdvancedTimelapseOptions advancedTimelapseOptions = new AdvancedTimelapseOptions(this.state, this.MarkableCanvas, this);
            advancedTimelapseOptions.ShowDialog();
            // Reset how some controls appear depending upon the current options
            this.EnableOrDisableMenusAndControls();

            if (this.dataHandler != null && this.dataHandler.FileDatabase != null)
            {
                // If we aren't using detections, then hide their existence even if detection data may be present
                GlobalReferences.DetectionsExists = this.state.UseDetections ? this.dataHandler.FileDatabase.DetectionsExists() : false;
            }
            else
            {
                GlobalReferences.DetectionsExists = false;
            }

            // redisplay the file as the options may change how bounding boxes should be displayed
            if (this.dataHandler != null)
            {
                this.FileShow(this.dataHandler.ImageCache.CurrentRow, true);
            }
        }

        #region Depracated menu items
        // Depracated
        // private void MenuItemAdvancedImageSetOptions_Click(object sender, RoutedEventArgs e)
        // {
        //    AdvancedImageSetOptions advancedImageSetOptions = new AdvancedImageSetOptions(this.dataHandler.FileDatabase, this);
        //    advancedImageSetOptions.ShowDialog();
        // }

        // Depracated
        // SaulXXX This is a temporary function to allow a user to check for and to delete any duplicate records.
        // private void MenuItemDeleteDuplicates_Click(object sender, RoutedEventArgs e)
        // {
        //    // Warn user that they are in a selected view, and verify that they want to continue
        //    if (this.dataHandler.FileDatabase.ImageSet.FileSelection != FileSelection.All)
        //    {
        //        // Need to be viewing all files
        //        MessageBox messageBox = new MessageBox("You need to select All Files before deleting duplicates", this);
        //        messageBox.Message.Problem = "Delete Duplicates should be applied to All Files, but you only have a subset selected";
        //        messageBox.Message.Solution = "On the Select menu, choose 'All Files' and try again";
        //        messageBox.Message.Icon = MessageBoxImage.Exclamation;
        //        messageBox.ShowDialog();
        //        return;
        //    }
        //    else
        //    {
        //        // Generate a list of duplicate rows showing their filenames (including relative path) 
        //        List<string> filenames = new List<string>();
        //        FileTable table = this.dataHandler.FileDatabase.GetDuplicateFiles();
        //        if (table != null && table.Count() != 0)
        //        {
        //            // populate the list
        //            foreach (ImageRow image in table)
        //            {
        //                string separator = String.IsNullOrEmpty(image.RelativePath) ? "" : "/";
        //                filenames.Add(image.RelativePath + separator + image.FileName);
        //            }
        //        }

        // // Raise a dialog box that shows the duplicate files (if any), where the user needs to confirm their deletion
        //        DeleteDuplicates deleteDuplicates = new DeleteDuplicates(this, filenames);
        //        bool? result = deleteDuplicates.ShowDialog();
        //        if (result == true)
        //        {
        //            // Delete the duplicate files
        //            this.dataHandler.FileDatabase.DeleteDuplicateFiles();
        //            // Reselect on the current select settings, which updates the view to remove the deleted files
        //            this.SelectFilesAndShowFile();
        //        }
        //    }
        // }
        #endregion
    }
}
