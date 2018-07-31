using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Timelapse.Database;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    /// <summary>
    /// This dialog box asks the user if he/she wants to delete the images (and possibly the data) of images rows as specified in the deletedImageTable
    /// What actually happens is that the image is replaced by a 'dummy' placeholder image,
    /// and the original image is copied into a subfolder called Deleted.
    /// </summary>
    public partial class DeleteImages : Window
    {
        // these variables will hold the values of the passed in parameters
        private bool deleteImageAndData;
        private List<ImageRow> filesToDelete;

        public List<long> ImageFilesRemovedByID { get; private set; }

        /// <summary>
        /// Ask the user if he/she wants to delete one or more images and (depending on whether deleteData is set) the data associated with those images.
        /// Other parameters indicate various specifics of how the deletion was specified, which also determines what is displayed in the interface:
        /// -deleteData is true when the data associated with that image should be deleted.
        /// -useDeleteFlags is true when the user is trying to delete images with the deletion flag set, otherwise its the current image being deleted
        /// </summary>
        public DeleteImages(FileDatabase database, List<ImageRow> imagesToDelete, bool deleteImageAndData, bool deleteCurrentImageOnly, Window owner)
        {
            this.InitializeComponent();
            this.deleteImageAndData = deleteImageAndData;
            this.filesToDelete = imagesToDelete;
            this.Owner = owner;

            this.ImageFilesRemovedByID = new List<long>();

            if (this.deleteImageAndData)
            {
                this.OkButton.IsEnabled = false;
                this.chkboxConfirm.Visibility = Visibility.Visible;
            }
            else
            {
                this.OkButton.IsEnabled = true;
                this.chkboxConfirm.Visibility = Visibility.Collapsed;
            }
            this.GridGallery.RowDefinitions.Clear();

            // Construct the dialog's text based on the state of the flags
            if (deleteCurrentImageOnly)
            {
                string imageOrVideo = imagesToDelete[0].IsVideo ? "video" : "image";
                this.Message.Title = String.Format("Delete the current {0} ", imageOrVideo);
                this.Message.What = String.Format("Deletes the current {0} (shown below) ", imageOrVideo);
                this.Message.Result = String.Format("\u2022 The deleted {0} will be backed up in a sub-folder named {1}.{2}", imageOrVideo, Constant.File.DeletedFilesFolder, Environment.NewLine);
                this.Message.Hint = String.Format("\u2022 Permanently delete the backup files by deleting the {0} folder.{1}", Constant.File.DeletedFilesFolder, Environment.NewLine);
                if (deleteImageAndData == false)
                {
                    // Case 1: Delete the current image, but not its data.
                    this.Message.Title += "but not its data.";
                    this.Message.What += "but not its data.";
                    this.Message.Result += String.Format("\u2022 A placeholder {0} will be shown when you try to view a deleted {0}.", imageOrVideo);
                    this.Message.Hint += "\u2022 Restore this deleted file by manually moving it back to its original location." + Environment.NewLine;
                }
                else
                {
                    // Case 2: Delete the current image and its data
                    this.Message.Title += "and its data";
                    this.Message.What += String.Format("and the data associated with that {0}.", imageOrVideo);
                    this.Message.Result += String.Format("\u2022 However, the data associated with that {0} will be permanently deleted.", imageOrVideo);
                    this.Message.Hint += "\u2022 Restore this deleted file by manually moving it to a new sub-folder, and adding that sub-folder to the image set." + Environment.NewLine;
                }
            }
            else
            {
                int numberOfImagesToDelete = this.filesToDelete.Count;
                this.Message.Title = String.Format("Delete {0} image and/or video(s) marked for deletion ", numberOfImagesToDelete.ToString());
                this.Message.What = String.Format("Deletes {0} image and/or video(s) that are marked for deletion (shown below), ",  numberOfImagesToDelete.ToString());
                this.Message.Result = String.Empty;
                this.Message.Hint = String.Format("\u2022 Permanently delete the backup files by deleting the {0} folder.{1}", Constant.File.DeletedFilesFolder, Environment.NewLine);
                if (numberOfImagesToDelete > Constant.Images.LargeNumberOfDeletedImages)
                {
                    this.Message.Result += String.Format("Deleting {0} files will take a few moments. Please be patient.{1}", numberOfImagesToDelete.ToString(), Environment.NewLine);
                }
                this.Message.Result += String.Format("\u2022 The deleted file will be backed up in a sub-folder named {0}.{1}", Constant.File.DeletedFilesFolder, Environment.NewLine);
                if (deleteImageAndData == false)
                {
                    // Case 3: Delete the images that have the delete flag set, but not their data
                    this.Message.Title += "but not their data";
                    this.Message.What += "but not the data entered for them.";
                    this.Message.Result += "\u2022 A placeholder image will be shown when you try to view a deleted file.";
                    this.Message.Hint += "\u2022 Restore these deleted files by manually moving them back to their original location." + Environment.NewLine;
                }
                else
                {
                    // Case 4: Delete the images that have the delete flag set, and their data
                    this.Message.Title += "and their data";
                    this.Message.What += "along with the data entered for them.";
                    this.Message.Result += "\u2022 However, the data associated with those files will be permanently deleted.";
                    this.Message.Hint += "\u2022 Restore these deleted files by manually moving them to a new sub-folder, and adding that sub-folder to the image set." + Environment.NewLine;
                }
            }

            // load thumbnails of those images that are candidates for deletion
            Mouse.OverrideCursor = Cursors.Wait;
            this.GridGallery.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Auto) });
            this.GridGallery.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Auto) });
            int columnIndex = 0;
            int rowIndex = 0;
            foreach (ImageRow imageProperties in imagesToDelete)
            {
                Label imageLabel = new Label
                {
                    Content = imageProperties.FileName
                };
                imageLabel.ToolTip = imageLabel.Content;
                imageLabel.Height = 25;
                imageLabel.VerticalAlignment = VerticalAlignment.Top;

                Image imageControl = new Image
                {
                    Source = imageProperties.LoadBitmap(database.FolderPath, Constant.Images.ThumbnailWidth)
                };

                Grid.SetRow(imageLabel, rowIndex);
                Grid.SetRow(imageControl, rowIndex + 1);
                Grid.SetColumn(imageLabel, columnIndex);
                Grid.SetColumn(imageControl, columnIndex);
                this.GridGallery.Children.Add(imageLabel);
                this.GridGallery.Children.Add(imageControl);
                ++columnIndex;
                if (columnIndex == 5)
                {
                    // A new row is started every five columns
                    columnIndex = 0;
                    rowIndex += 2;
                    this.GridGallery.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Auto) });
                    this.GridGallery.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Auto) });
                }
            }
            Mouse.OverrideCursor = null;
            this.scroller.CanContentScroll = true;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Utilities.SetDefaultDialogPosition(this);
            Utilities.TryFitDialogWindowInWorkingArea(this);
        }

        /// <summary>
        /// Cancel button selected
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        private void ConfirmBox_Checked(object sender, RoutedEventArgs e)
        {
            this.OkButton.IsEnabled = (bool)this.chkboxConfirm.IsChecked;
        }

        /// <summary>
        /// Ok button selected
        /// </summary>
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }
    }
}
