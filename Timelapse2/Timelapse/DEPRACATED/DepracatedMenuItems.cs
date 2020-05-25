//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace Timelapse.DEPRACATED
//{
//class DepracatedMenuItems
//{

#region  MenuItemGenerateVideoThumbnails
// These two used to be in the Options menu - it was a test to generate thumbnails for all video files but it is not longer needed since FFMPEG is now used which is fast enough
//private void MenuItemGenerateVideoThumbnails_Click(object sender, RoutedEventArgs e)
//{
//    string[] videoFileExtensions = { Constant.File.AviFileExtension, Constant.File.Mp4FileExtension, Constant.File.ASFFileExtension, };
//    VideoThumbnailer.GenerateVideoThumbnailsInAllFolders(this.FolderPath, Constant.File.VideoThumbnailFolderName, videoFileExtensions);
//}

//private void MenuItemDeleteVideoThumbnails_Click(object sender, RoutedEventArgs e)
//{
//    VideoThumbnailer.DeleteVideoThumbnailsInAllFolders(this.FolderPath, Constant.File.VideoThumbnailFolderName);
//}
#endregion

#region MenuItemAdvancedImageSetOptions
// Depracated - used to be in MenuOptions
// private void MenuItemAdvancedImageSetOptions_Click(object sender, RoutedEventArgs e)
// {
//    AdvancedImageSetOptions advancedImageSetOptions = new AdvancedImageSetOptions(this.dataHandler.FileDatabase, this);
//    advancedImageSetOptions.ShowDialog();
// }
#endregion

#region  MenuItemDeleteDuplicates

// Depracated - used to be in MenuOptions
// SaulXXX This was a temporary function to allow a user to check for and to delete any duplicate records.
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

//   }
//}
