﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Timelapse.Controls;
using Timelapse.Database;
using Timelapse.Images;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    /// <summary>
    /// This dialog box asks the user if he/she wants to delete the images (and possibly the data) of images rows as specified in the deletedImageTable
    /// What actually happens is that the image is replaced by a 'dummy' placeholder image,
    /// and the original image is copied into a subfolder called Deleted.
    /// </summary>
    public partial class DeleteImages : BusyableDialogWindow
    {
        #region Private Variables
        // these variables will hold the values of the passed in parameters
        private readonly FileDatabase fileDatabase;
        private readonly ImageCache imageCache;
        private readonly List<ImageRow> filesToDelete;
        private readonly bool deleteImage;
        private readonly bool deleteData;
        private readonly bool deleteCurrentImageOnly;

        private bool IsAnyDataUpdated;
        private int maxPathLength = 60;
        #endregion

        #region Constructor, Loaded
        /// <summary>
        /// Ask the user if he/she wants to delete one or more images and (depending on whether deleteData is set) the data associated with those images.
        /// Other parameters indicate various specifics of how the deletion was specified, which also determines what is displayed in the interface:
        /// -deleteData is true when the data associated with that image should be deleted.
        /// -useDeleteFlags is true when the user is trying to delete images with the deletion flag set, otherwise its the current image being deleted
        /// </summary>
        public DeleteImages(Window owner, FileDatabase fileDatabase, ImageCache imageCache, List<ImageRow> filesToDelete, bool deleteImage, bool deleteData, bool deleteCurrentImageOnly) : base(owner)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(fileDatabase, nameof(fileDatabase));
            ThrowIf.IsNullArgument(imageCache, nameof(imageCache));
            ThrowIf.IsNullArgument(filesToDelete, nameof(filesToDelete));

            this.InitializeComponent();

            this.fileDatabase = fileDatabase;
            this.imageCache = imageCache;
            this.filesToDelete = filesToDelete;
            //this.deleteImageAndData = deleteImageAndData;
            this.deleteImage = deleteImage;
            this.deleteData = deleteData;
            this.deleteCurrentImageOnly = deleteCurrentImageOnly;

            // Tracks whether any changes to the data or database are made
            this.IsAnyDataUpdated = false;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Set up a progress handler that will update the progress bar
            this.InitalizeProgressHandler(this.BusyCancelIndicator);

            Mouse.OverrideCursor = Cursors.Wait;

            // Construct the interface for either a single deletion, or for multiple deletions
            if (this.deleteCurrentImageOnly)
            {
                this.DeleteCurrentImageOnly();
            }
            else
            {
                this.DeleteMultipleImages();
            }

            // Depending upon what is being deleted,
            // set the visibility and enablement of various controls
            if (this.deleteData)
            {
                // Set the UI to require a further confirmation step to delete the data, 
                this.StartDoneButton.IsEnabled = false;
                this.chkboxConfirm.Visibility = Visibility.Visible;
            }
            else
            {
                this.StartDoneButton.IsEnabled = true;
                this.chkboxConfirm.Visibility = Visibility.Collapsed;
            }
            Mouse.OverrideCursor = null;
        }
        #endregion

        #region Build Initial Dialog Interfaces
        private void DeleteCurrentImageOnly()
        {
            // The single file to delete
            ImageRow imageRow = this.filesToDelete[0];

            // Show  the deleted file name and image in the interface
            this.ShowSingleFileView();
            this.maxPathLength = 70;
            string filePath = Path.Combine(imageRow.RelativePath, imageRow.File);
            if (string.IsNullOrEmpty(filePath) == false)
            {
                filePath = filePath.Length <= this.maxPathLength ? filePath : "..." + filePath.Substring(filePath.Length - this.maxPathLength, this.maxPathLength);
            }

            this.SingleImageViewer.Source = imageRow.LoadBitmap(this.fileDatabase.FolderPath, Constant.ImageValues.PreviewWidth480, out _);
            this.SingleFilePanel.ToolTip = Path.Combine(imageRow.RelativePath, imageRow.File);
            this.SingleImageViewer.ToolTip = Path.Combine(imageRow.RelativePath, imageRow.File);
            this.SingleFileNameRun.Text = filePath;

            // Populate the information pane
            string imageOrVideo = this.filesToDelete[0].IsVideo ? "video" : "image";

            if (this.deleteImage == true)
            {
                this.Message.Title = String.Format("Delete the current {0}", imageOrVideo);
                this.Message.What = String.Format("Deletes the current {0} if it exists", imageOrVideo);
                this.Message.Result = String.Format("\u2022 The deleted {0} will be backed up in a sub-folder named {1}.{2}", imageOrVideo, Constant.File.DeletedFilesFolder, Environment.NewLine);
                this.Message.Hint = String.Format("\u2022 Restore the deleted {0} by manually moving it ", imageOrVideo);
                if (this.deleteImage == true && this.deleteData == false)
                {
                    // Case 1: Delete the current image, but not its data.

                    this.Message.Title += " but not its data.";
                    this.Message.What += String.Format("{0}The data entered for the {1} IS NOT deleted.", Environment.NewLine, imageOrVideo);
                    this.Message.Result += String.Format("\u2022 A placeholder {0} will be shown when you try to view a deleted {0}.", imageOrVideo);
                    this.Message.Hint += "back to its original location." + Environment.NewLine;
                }
                else if (this.deleteImage == true && this.deleteData == true)
                {
                    // Case 2: Delete the current image and its data
                    this.Message.Title += " and its data";
                    this.Message.What += String.Format("{0}The data entered for the {1} IS deleted as well.", Environment.NewLine, imageOrVideo);
                    this.Message.Result += String.Format("\u2022 However, the data associated with that {0} will be permanently deleted.", imageOrVideo);
                    this.Message.Hint += "to a new sub-folder." + Environment.NewLine + "  Then add that sub-folder back to the image set." + Environment.NewLine;
                }
                this.Message.Hint += String.Format("\u2022 See Options|Preferences to manage how files in {0} are permanently deleted.", Constant.File.DeletedFilesFolder);
            }
            else
            {
                // Case: Delete the data only, leaving the image intact
                this.FileLabel.Text = "Affected file:";
                this.ConfirmCheckBoxText.Text = "Click to confirm deletion of data for the selected file";
                this.Message.Title = String.Format("Delete only the current {0}'s data", imageOrVideo);
                this.Message.What = String.Format("Deletes the data associated with the current {0}, but leaves the {0} intact", imageOrVideo);
                this.Message.Result = String.Format("\u2022 This data record will be removed.{0}", Environment.NewLine);
                this.Message.Result += String.Format("\u2022 The {0} is still intact, but it will not be displayed in Timelapse {1}", imageOrVideo, Environment.NewLine);
                this.Message.Result += String.Format("   unless a duplicate record exists.", imageOrVideo);
                this.Message.Hint = "Deleting only the data is useful for removing a previously-created duplicate record of a file.";
            }
        }

        // Deleting multiple images - set up the UI
        private void DeleteMultipleImages()
        {
            int numberOfImagesToDelete = this.filesToDelete.Count;

            // Load the files that are candidates for deletion as listbox items
            this.ShowMultipleFilesView();
            this.DeletedFilesListBox.Items.Clear();
            this.maxPathLength = 100;
            foreach (ImageRow imageProperties in this.filesToDelete)
            {
                string filePath = Path.Combine(imageProperties.RelativePath, imageProperties.File);
                if (string.IsNullOrEmpty(filePath) == false)
                {
                    filePath = filePath.Length <= this.maxPathLength ? filePath : "..." + filePath.Substring(filePath.Length - this.maxPathLength, this.maxPathLength);
                }

                ListBoxItem lbi = new ListBoxItem
                {
                    VerticalAlignment = System.Windows.VerticalAlignment.Top,
                    Height = 28,
                    Content = filePath,
                    Tag = imageProperties
                };
                lbi.MouseEnter += this.Lbi_MouseEnter;
                lbi.MouseLeave += this.Lbi_MouseLeave;
                this.DeletedFilesListBox.Items.Add(lbi);
            }

            // Populate the information pane
            if (this.deleteImage == true)
            {
                this.Message.Title = String.Format("Delete {0} files(s) ", numberOfImagesToDelete.ToString());
                this.Message.What = String.Format("Delete {0} image and/or video(s) - if they exist - marked for deletion.", numberOfImagesToDelete.ToString());
                this.Message.Result = String.Empty;
                this.Message.Hint = "\u2022 Restore deleted files by manually moving them ";
                this.Message.Result += String.Format("\u2022 The deleted file will be backed up in a sub-folder named {0}.{1}", Constant.File.DeletedFilesFolder, Environment.NewLine);
                if (deleteData == false)
                {
                    // Case : Delete the images that have the delete flag set, but not their data
                    this.Message.Title += "but not their data";
                    this.Message.What += Environment.NewLine + "The data entered for them IS NOT deleted.";
                    this.Message.Result += "\u2022 A placeholder image will be shown when you try to view a deleted file.";
                    this.Message.Hint += "back to their original location." + Environment.NewLine;
                }
                else // (deleteData == true)
                {
                    // Case : Delete the images that have the delete flag set, and their data
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
            else
            {
                // Case: Delete the data only, leaving the image intact
                //this.FileLabel.Text = "Affected file:";
                this.ConfirmCheckBoxText.Text = String.Format("Click to confirm deletion of data for the {0} selected files", numberOfImagesToDelete);
                this.Message.Title = String.Format("Delete only the data for {0} files(s) ", numberOfImagesToDelete);
                this.Message.What = String.Format("Deletes the data associated with {0} file(s), but leaves the files intact", numberOfImagesToDelete);
                this.Message.Result = String.Format("\u2022 These {0} data records will be permanently removed.{1}", numberOfImagesToDelete, Environment.NewLine);
                this.Message.Result += String.Format("\u2022 The {0} file(s) are still intact, but will not be displayed in Timelapse {1}", numberOfImagesToDelete, Environment.NewLine);
                this.Message.Result += String.Format("   unless a duplicate record exists.");
                this.Message.Hint = "Deleting only the data  is useful for removing a previously-created duplicate record of a file.";
            }
        }
        #endregion

        #region Do the actual file deletion
        // The (bool, int return value: true if the operation has been cancelled, and if so how many images were deleted before the cancel event
        private async Task<Tuple<bool, int>> DoDeleteFilesAsync(List<ImageRow> imagesToDelete, bool deleteFiles, bool deleteData)
        {
            // cache the current ID as the current image may be invalidated
            long currentFileID = this.imageCache.Current.ID;

            List<ColumnTuplesWithWhere> imagesToUpdate = new List<ColumnTuplesWithWhere>();
            List<long> imageIDsToDropFromDatabase = new List<long>();
            int fileIndex = 0;
            int filesDeleted = 0;
            bool isCancelled = false;

            await Task.Run(() =>
            {
                int count = imagesToDelete.Count;
                foreach (ImageRow image in imagesToDelete)
                {
                    if (Token.IsCancellationRequested)
                    {
                        isCancelled = true;
                        return;
                        //return new Tuple<bool,int>(true, filesDeleted);
                    }

                    if (deleteFiles)
                    {
                        // We need to release the file handle to various images as otherwise we won't be able to move them
                        // Release the image cache for this ID, if its actually in the cache  
                        this.imageCache.TryInvalidate(image.ID);
                        GC.Collect(); // See if this actually gets rid of the pointer to the image, as otherwise we get occassional exceptions
                                      // SAULXXX Note that we should likely pop up a dialog box that displays non-missing files that we can't (for whatever reason) delete
                                      // SAULXXX If we can't delete it, we may want to abort changing the various DeleteFlag and ImageQuality values. 
                                      // SAULXXX A good way is to put an 'image.ImageFileExists' field in, and then do various tests on that.
                        if (image.TryMoveFileToDeletedFilesFolder(this.fileDatabase.FolderPath))
                        {
                            // keep track of the number of files actually delted
                            filesDeleted++;
                        }
                    }
                    if (deleteData)
                    {
                        // mark the image row for dropping
                        imageIDsToDropFromDatabase.Add(image.ID);
                    }
                    else 
                    {
                        // as only the file was deleted, clear the delete flag
                        image.DeleteFlag = false;
                        List<ColumnTuple> columnTuples = new List<ColumnTuple>()
                        {
                            new ColumnTuple(Constant.DatabaseColumn.DeleteFlag, Constant.BooleanValue.False),
                        };
                        imagesToUpdate.Add(new ColumnTuplesWithWhere(columnTuples, image.ID));
                    }
                    fileIndex++;
                    if (this.ReadyToRefresh())
                    {
                        int percentDone = Convert.ToInt32(fileIndex / Convert.ToDouble(count) * 100.0);
                        this.Progress.Report(new ProgressBarArguments(percentDone, String.Format("Pass 1: Deleting {0} / {1} files", fileIndex, count), true, false));
                        Thread.Sleep(Constant.ThrottleValues.RenderingBackoffTime);
                    }
                }
                this.Progress.Report(new ProgressBarArguments(100, String.Format("Pass 2: Updating {0} files. Please wait...", count), false, true));
                Thread.Sleep(Constant.ThrottleValues.RenderingBackoffTime);

                if (deleteData)
                {
                    // drop images
                    this.fileDatabase.DeleteFilesAndMarkers(imageIDsToDropFromDatabase);
                }
                else
                {
                    // update image properties
                    this.fileDatabase.UpdateFiles(imagesToUpdate);
                }
                // A side effect of running this task is that the FileTable will be updated, which means that,
                // at the very least, the calling function will need to run FilesSelectAndShow to either
                // reload the FileTable with the updated data, or to reset the FileTable back to its original form
                // if the operation was cancelled.
                this.IsAnyDataUpdated = true;
            }).ConfigureAwait(true);

            return new Tuple<bool, int>(isCancelled, filesDeleted);
        }
        #endregion

        #region Listbox callbacks to display file image in thumbnails
        // When the user enters a listbox item, show the image
        private void Lbi_MouseEnter(object sender, MouseEventArgs e)
        {
            if (!(sender is ListBoxItem lbi))
            {
                return;
            }
            ImageRow ir = (ImageRow)lbi.Tag;
            Image image = new Image()
            {
                Source = ir.LoadBitmap(this.fileDatabase.FolderPath, Constant.ImageValues.PreviewWidth384, out _),
                Height = 300,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            lbi.ToolTip = image;
        }

        // When the user leaves a listbox item, remove the image
        private void Lbi_MouseLeave(object sender, MouseEventArgs e)
        {
            if (!(sender is ListBoxItem lbi))
            {
                return;
            }
            lbi.ToolTip = null;
        }
        #endregion

        #region Button callbacks
        // Set the confirm checkbox, which enables the ok button if the data deletions are confirmed. 
        private void ConfirmBox_Checked(object sender, RoutedEventArgs e)
        {
            this.StartDoneButton.IsEnabled = (bool)this.chkboxConfirm.IsChecked;
        }

        // Cancel button selected
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        // Ok button selected
        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            // Configure the UI's initial state
            this.CancelButton.IsEnabled = false;
            this.CancelButton.Visibility = Visibility.Hidden;
            this.StartDoneButton.Content = "_Done";
            this.StartDoneButton.Click -= this.StartButton_Click;
            this.StartDoneButton.Click += this.DoneButton_Click;
            this.StartDoneButton.IsEnabled = false;
            this.BusyCancelIndicator.IsBusy = true;
            this.WindowCloseButtonIsEnabled(false);

            Tuple<bool, int> isCancelledAndDeletedImagesCount = await DoDeleteFilesAsync(this.filesToDelete, this.deleteImage, this.deleteData).ConfigureAwait(true);

            // Hide the busy indicator and update the UI, e.g., to show how many files were deleted
            this.BusyCancelIndicator.IsBusy = false;
            this.StartDoneButton.IsEnabled = true;
            this.WindowCloseButtonIsEnabled(true);

            if (this.deleteData && this.deleteImage == false)
            {
                this.DoneMessagePanel.Content = String.Format("Data deleted for {0} files.{1}Otherwise, those files were left intact in their folders", this.filesToDelete.Count, Environment.NewLine);
            }
            else if (isCancelledAndDeletedImagesCount.Item1 == false)
            {
                this.DoneMessagePanel.Content = "Deleted ";
                this.DoneMessagePanel.Content += this.filesToDelete.Count == 1 ? this.filesToDelete[0].File : isCancelledAndDeletedImagesCount.Item2.ToString() + " files";
            }
            else
            {
                this.DoneMessagePanel.Content = "Cancelled, but ";
                this.DoneMessagePanel.Content += this.filesToDelete.Count == 1 ? this.filesToDelete[0].File : isCancelledAndDeletedImagesCount.Item2.ToString() + " files were already deleted." + Environment.NewLine;
                this.DoneMessagePanel.Content += "Deleted files are available in your Deleted folder." + Environment.NewLine;
                this.DoneMessagePanel.Content += "Data for these deleted images has not been changed." + Environment.NewLine;
            }
            this.ShowDoneMessageView();
        }

        private void DoneButton_Click(object sender, RoutedEventArgs e)
        {
            // We return false if the database was not altered, i.e., if this was all a no-op
            this.DialogResult = this.IsAnyDataUpdated;
        }
        #endregion

        #region Helper methods to show/hide various UI panels
        private void ShowSingleFileView()
        {
            this.SingleFilePanel.Visibility = Visibility.Visible;
            this.MultipleFilePanel.Visibility = Visibility.Collapsed;
            this.DoneMessagePanel.Visibility = Visibility.Collapsed;
        }

        private void ShowMultipleFilesView()
        {
            this.SingleFilePanel.Visibility = Visibility.Collapsed;
            this.MultipleFilePanel.Visibility = Visibility.Visible;
            this.DoneMessagePanel.Visibility = Visibility.Collapsed;
        }

        private void ShowDoneMessageView()
        {
            this.SingleFilePanel.Visibility = Visibility.Collapsed;
            this.MultipleFilePanel.Visibility = Visibility.Collapsed;
            this.DoneMessagePanel.Visibility = Visibility.Visible;
            this.chkboxConfirm.Visibility = Visibility.Collapsed;
        }
        #endregion
    }
}
