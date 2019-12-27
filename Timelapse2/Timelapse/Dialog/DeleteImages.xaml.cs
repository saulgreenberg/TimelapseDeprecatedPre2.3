using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
        private readonly bool deleteImageAndData;
        private readonly List<ImageRow> filesToDelete;
        private readonly FileDatabase database;
        private int maxPathLength = 45;

        /// <summary>
        /// Ask the user if he/she wants to delete one or more images and (depending on whether deleteData is set) the data associated with those images.
        /// Other parameters indicate various specifics of how the deletion was specified, which also determines what is displayed in the interface:
        /// -deleteData is true when the data associated with that image should be deleted.
        /// -useDeleteFlags is true when the user is trying to delete images with the deletion flag set, otherwise its the current image being deleted
        /// </summary>
        public DeleteImages(FileDatabase database, List<ImageRow> filesToDelete, bool deleteImageAndData, bool deleteCurrentImageOnly, Window owner)
        {
            this.InitializeComponent();
            Mouse.OverrideCursor = Cursors.Wait;

            this.deleteImageAndData = deleteImageAndData;
            this.filesToDelete = filesToDelete;
            this.Owner = owner;
            this.database = database;

            // Construct the interface for either a single or for multiple images to delete
            if (deleteCurrentImageOnly)
            {
                this.DeleteCurrentImageOnly();
            }
            else
            {
                this.DeleteMultipleImages();
            }

            // Depending upon what is being deleted,
            // set the visibility and enablement of various controls
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
            Mouse.OverrideCursor = null;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dialogs.TryPositionAndFitDialogIntoWindow(this);
        }

        private void DeleteCurrentImageOnly()
        {
            ImageRow imageRow = this.filesToDelete[0];
            // Set various visibility and size attributes
            this.DeletedFilesListBox.Visibility = Visibility.Collapsed;
            this.SingleFileTextBlock.Visibility = Visibility.Visible;
            this.MouseOverMessageTextBlock.Visibility = Visibility.Collapsed;
            this.ImageViewer.Visibility = Visibility.Visible;

            this.Height = 600;
            this.MinHeight = 600;
            this.ImageViewer.Margin = new Thickness(20, 0, 0, 0);
            this.ImageViewer.Source = imageRow.LoadBitmap(this.database.FolderPath, Constant.ImageValues.ThumbnailWidthWhenNavigating, out bool isCorruptOrMissing);

            // Show  the deleted file name and image in the interface
            this.maxPathLength = 70;
            string filePath = Path.Combine(imageRow.RelativePath, imageRow.File);
            if (string.IsNullOrEmpty(filePath) == false)
            {
                filePath = filePath.Length <= this.maxPathLength ? filePath : "..." + filePath.Substring(filePath.Length - this.maxPathLength, this.maxPathLength);
            }
            this.SingleFileTextBlock.ToolTip = Path.Combine(imageRow.RelativePath, imageRow.File);
            this.ImageViewer.ToolTip = Path.Combine(imageRow.RelativePath, imageRow.File);
            this.SingleFileNameRun.Text = filePath;

            string imageOrVideo = this.filesToDelete[0].IsVideo ? "video" : "image";
            this.Message.Title = String.Format("Delete the current {0}", imageOrVideo);
            this.Message.What = String.Format("Deletes the current {0} if it exists", imageOrVideo);
            this.Message.Result = String.Format("\u2022 The deleted {0} will be backed up in a sub-folder named {1}.{2}", imageOrVideo, Constant.File.DeletedFilesFolder, Environment.NewLine);
            this.Message.Hint = String.Format("\u2022 Restore the deleted {0} by manually moving it ", imageOrVideo);
            if (this.deleteImageAndData == false)
            {
                // Case 1: Delete the current image, but not its data.
                this.Message.Title += " but not its data.";
                this.Message.What += String.Format("{0}The data entered for the {1} IS NOT deleted.", Environment.NewLine, imageOrVideo);
                this.Message.Result += String.Format("\u2022 A placeholder {0} will be shown when you try to view a deleted {0}.", imageOrVideo);
                this.Message.Hint += "back to its original location." + Environment.NewLine;
            }
            else
            {
                // Case 2: Delete the current image and its data
                this.Message.Title += " and its data";
                this.Message.What += String.Format("{0}The data entered for the {1} IS deleted as well.", Environment.NewLine, imageOrVideo);
                this.Message.Result += String.Format("\u2022 However, the data associated with that {0} will be permanently deleted.", imageOrVideo);
                this.Message.Hint += "to a new sub-folder." + Environment.NewLine + "  Then add that sub-folder back to the image set." + Environment.NewLine;
            }
            this.Message.Hint += String.Format("\u2022 See Options|Preferences to manage how files in {0} are permanently deleted.", Constant.File.DeletedFilesFolder);
        }

        private void DeleteMultipleImages()
        {
            int numberOfImagesToDelete = this.filesToDelete.Count;

            // Set various visibility and size attributes
            this.DeletedFilesListBox.Visibility = Visibility.Visible;
            this.SingleFileTextBlock.Visibility = Visibility.Collapsed;
            this.Height = 780;
            this.MinHeight = 680;
            this.ImageViewer.Width = Constant.ImageValues.ThumbnailWidth * 2; // Set the size of the image to display on mouse-over

            // Deleting multiple images
            // load the files that are candidates for deletion as listbox items
            this.DeletedFilesListBox.Items.Clear();
            this.maxPathLength = 45;
            foreach (ImageRow imageProperties in this.filesToDelete)
            {
                string filePath = Path.Combine(imageProperties.RelativePath, imageProperties.File);
                if (string.IsNullOrEmpty(filePath) == false)
                {
                    filePath = filePath.Length <= this.maxPathLength ? filePath : "..." + filePath.Substring(filePath.Length - this.maxPathLength, this.maxPathLength);
                }

                ListBoxItem lbi = new ListBoxItem
                {
                    VerticalAlignment = VerticalAlignment.Top,
                    Height = 28,
                    Content = filePath,
                    ToolTip = Path.Combine(imageProperties.RelativePath, imageProperties.File),
                    Tag = imageProperties
                };
                lbi.MouseEnter += this.Lbi_MouseEnter;
                lbi.MouseLeave += this.Lbi_MouseLeave;
                this.DeletedFilesListBox.Items.Add(lbi);
            }

            // Override how the mouse wheel works with the listbox scrollviewer
            ScrollViewer sv = Utilities.GetVisualChild<ScrollViewer>(this.DeletedFilesListBox);
            if (sv != null)
            {
                sv.PreviewMouseWheel += this.Sv_PreviewMouseWheel;
            }

            this.Message.Title = String.Format("Delete {0} files(s) ", numberOfImagesToDelete.ToString());
            this.Message.What = String.Format("Delete {0} image and/or video(s) - if they exist - marked for deletion.", numberOfImagesToDelete.ToString());
            this.Message.Result = String.Empty;
            this.Message.Hint = "\u2022 Restore deleted files by manually moving them ";
            this.Message.Result += String.Format("\u2022 The deleted file will be backed up in a sub-folder named {0}.{1}", Constant.File.DeletedFilesFolder, Environment.NewLine);
            if (this.deleteImageAndData == false)
            {
                // Case 3: Delete the images that have the delete flag set, but not their data
                this.Message.Title += "but not their data";
                this.Message.What += Environment.NewLine + "The data entered for them IS NOT deleted.";
                this.Message.Result += "\u2022 A placeholder image will be shown when you try to view a deleted file.";
                this.Message.Hint += "back to their original location." + Environment.NewLine;
            }
            else
            {
                // Case 4: Delete the images that have the delete flag set, and their data
                this.Message.Title += "and their data";
                this.Message.What += Environment.NewLine + "The data entered for them IS deleted as well.";
                this.Message.Result += "\u2022 However, the data associated with those files will be permanently deleted.";
                this.Message.Hint += "to a new sub-folder." + Environment.NewLine + "  Then add that sub-folder back to the image set." + Environment.NewLine;
            }
            if (numberOfImagesToDelete > Constant.ImageValues.LargeNumberOfDeletedImages)
            {
                this.Message.Result += String.Format("{0}\u2022 Deleting {1} files takes time. Please be patient.", Environment.NewLine, numberOfImagesToDelete.ToString());
            }
            this.Message.Hint += String.Format("\u2022 See Options|Preferences to manage how files in {0} are permanently deleted.", Constant.File.DeletedFilesFolder);
        }

        // When the user enters a listbox item, show the image
        private void Lbi_MouseEnter(object sender, MouseEventArgs e)
        {
            ListBoxItem lbi = sender as ListBoxItem;
            ImageRow ir = (ImageRow)lbi.Tag;
            this.ImageViewer.Source = ir.LoadBitmap(this.database.FolderPath, Constant.ImageValues.ThumbnailWidth, out bool isCorruptOrMissing);
            this.MouseOverMessageTextBlock.Visibility = Visibility.Collapsed;
            this.ImageViewer.Visibility = Visibility.Visible;
        }

        // When the user leaves a listbox item, remove the image
        private void Lbi_MouseLeave(object sender, MouseEventArgs e)
        {
            this.ImageViewer.Source = null;
            this.MouseOverMessageTextBlock.Visibility = Visibility.Visible;
            this.ImageViewer.Visibility = Visibility.Collapsed;
        }

        // Scroll one line (and thus show one image) at a time
        // Otherwise it would scroll 3 lines at a time (the default)
        private void Sv_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            ScrollViewer sv = sender as ScrollViewer;
            e.Handled = true;
            if (e.Delta > 0)
            {
                sv.LineUp();
            }
            else
            {
                sv.LineDown();
            }
        }

        // Set the confirm checkbox, which enables the ok button if the data deletions are confirmed. 
        private void ConfirmBox_Checked(object sender, RoutedEventArgs e)
        {
            this.OkButton.IsEnabled = (bool)this.chkboxConfirm.IsChecked;
        }

        // Cancel button selected
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        // Ok button selected
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }
    }
}
