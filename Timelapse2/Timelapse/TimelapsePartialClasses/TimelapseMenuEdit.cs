﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Timelapse.Database;
using Timelapse.Dialog;
using Timelapse.Enums;
using Timelapse.QuickPaste;
using Timelapse.Util;

// Edit Menu Callbacks
namespace Timelapse
{
    public partial class TimelapseWindow : Window, IDisposable
    {
        // Edit Submenu Opening 
        private void Edit_SubmenuOpening(object sender, RoutedEventArgs e)
        {
            this.FilePlayer_Stop(); // In case the FilePlayer is going

            // Enable / disable various edit menu items depending on whether we are looking at the single image view or overview
            bool state = this.IsDisplayingSingleImage();
            this.MenuItemCopyPreviousValues.IsEnabled = state;

            // Enable the FindMissingImage menu only if the current image is missing
            ImageRow currentImage = this.DataHandler?.ImageCache?.Current;
            this.MenuItemFindMissingImage.IsEnabled =
                this.DataHandler?.ImageCache?.Current != null
                && false == File.Exists(FilesFolders.GetFullPath(this.DataHandler.FileDatabase, currentImage));
        }

        // Find image 
        private void MenuItemFindByFileName_Click(object sender, RoutedEventArgs e)
        {
            this.FindBoxSetVisibility(true);
        }

        // Show QuickPaste Window 
        private void MenuItemQuickPasteWindowShow_Click(object sender, RoutedEventArgs e)
        {
            if (this.quickPasteWindow == null)
            {
                // create the quickpaste window if it doesn't already exist.
                this.QuickPasteWindowShow();
            }
            this.QuickPasteRefreshWindowAndXML();
            this.QuickPasteWindowShow();
        }

        // Import QuickPaste Items from .ddb file
        private void MenuItemQuickPasteImportFromDB_Click(object sender, RoutedEventArgs e)
        {
            if (Dialogs.TryGetFileFromUserUsingOpenFileDialog("Import QuickPaste entries by selecting the Timelapse database (.ddb) file from the image folder where you had used them.",
                                             Path.Combine(this.DataHandler.FileDatabase.FolderPath, Constant.File.DefaultFileDatabaseFileName),
                                             String.Format("Database files (*{0})|*{0}", Constant.File.FileDatabaseFileExtension),
                                             Constant.File.FileDatabaseFileExtension,
                                             out string ddbFile) == true)
            {
                List<QuickPasteEntry> qpe = QuickPasteOperations.QuickPasteImportFromDB(this.DataHandler.FileDatabase, ddbFile);
                if (qpe.Count == 0)
                {
                    Dialogs.MenuEditCouldNotImportQuickPasteEntriesDialog(this);
                    return;
                }
                else
                {
                    this.quickPasteEntries = qpe;
                    this.DataHandler.FileDatabase.UpdateSyncImageSetToDatabase();
                    this.QuickPasteRefreshWindowAndXML();
                    this.QuickPasteWindowShow();
                }
            }
        }

        // Copy Previous Values
        private void MenuItemCopyPreviousValues_Click(object sender, RoutedEventArgs e)
        {
            this.CopyPreviousValues_Click();
        }

        // Populate a data field from metadata (example metadata displayed from the currently selected image)
        private async void MenuItemPopulateFieldFromMetadata_Click(object sender, RoutedEventArgs e)
        {
            // If we are not in the selection All view, or if its a corrupt image or deleted image, or if its a video that no longer exists, tell the person. Selecting ok will shift the selection.
            // We want to be on a valid image as otherwise the metadata of interest won't appear
            if (this.DataHandler.ImageCache.Current.IsDisplayable(this.FolderPath) == false)
            {
                // There are no displayable images, and thus no metadata to choose from, so abort
                Dialogs.MenuEditPopulateDataFieldWithMetadataDialog(this);
                return;
            }

            // Warn the user that they are currently in a selection displaying only a subset of files, and make sure they want to continue.
            if (Dialogs.MaybePromptToApplyOperationOnSelectionDialog(this, this.DataHandler.FileDatabase, this.State.SuppressSelectedPopulateFieldFromMetadataPrompt,
                                                                           "'Populate a data field with image metadata...'",
                                                               (bool optOut) =>
                                                               {
                                                                   this.State.SuppressSelectedPopulateFieldFromMetadataPrompt = optOut;
                                                               }))
            {
                using (PopulateFieldWithMetadata populateField = new PopulateFieldWithMetadata(this, this.DataHandler.FileDatabase, this.DataHandler.ImageCache.Current.GetFilePath(this.FolderPath)))
                {
                    if (this.ShowDialogAndCheckIfChangesWereMade(populateField))
                    {
                        await this.FilesSelectAndShowAsync().ConfigureAwait(true);
                    };
                }
            }
        }

        // Delete sub-menu opening
        private void MenuItemDelete_SubmenuOpening(object sender, RoutedEventArgs e)
        {
            try
            {
                bool deletedImages = this.DataHandler.FileDatabase.RowExistsWhere(FileSelectionEnum.MarkedForDeletion);
                this.MenuItemDeleteFiles.IsEnabled = deletedImages;
                this.MenuItemDeleteFilesAndData.IsEnabled = deletedImages;

                // Enable the delete current file option only if we are not on the thumbnail grid 
                this.MenuItemDeleteCurrentFileAndData.IsEnabled = this.MarkableCanvas.IsThumbnailGridVisible == false; // Only show this option if the thumbnail grid is visible
                this.MenuItemDeleteCurrentFile.IsEnabled = this.MarkableCanvas.IsThumbnailGridVisible == false && this.DataHandler.ImageCache.Current.IsDisplayable(this.FolderPath);
            }
            catch (Exception exception)
            {
                TracePrint.PrintMessage(String.Format("Delete submenu failed to open in Delete_SubmenuOpening. {0}", exception.ToString()));

                // This function was blowing up on one user's machine, but not others.
                // I couldn't figure out why, so I just put this fallback in here to catch that unusual case.
                this.MenuItemDeleteFiles.IsEnabled = true;
                this.MenuItemDeleteFilesAndData.IsEnabled = true;
                this.MenuItemDeleteCurrentFile.IsEnabled = true;
                this.MenuItemDeleteCurrentFileAndData.IsEnabled = true;
            }
        }

        // Delete callback manages all deletion menu choices where: 
        // - the current image or all images marked for deletion are deleted
        // - the data associated with those images may be delted.
        // - deleted images are moved to a backup folder.
        private async void MenuItemDeleteFiles_Click(object sender, RoutedEventArgs e)
        {
            MenuItem menuItem = sender as MenuItem;

            // This callback is invoked by DeleteImage (which deletes the current image) and DeleteImages (which deletes the images marked by the deletion flag)
            // Thus we need to use two different methods to construct a table containing all the images marked for deletion
            List<ImageRow> filesToDelete;
            bool deleteCurrentImageOnly;
            bool deleteFilesAndData;
            if (menuItem.Name.Equals(this.MenuItemDeleteFiles.Name) || menuItem.Name.Equals(this.MenuItemDeleteFilesAndData.Name))
            {
                deleteCurrentImageOnly = false;
                deleteFilesAndData = menuItem.Name.Equals(this.MenuItemDeleteFilesAndData.Name);
                // get list of all images marked for deletion in the current seletion
                using (FileTable filetable = this.DataHandler.FileDatabase.SelectFilesMarkedForDeletion())
                {
                    filesToDelete = filetable.ToList();
                }

                for (int index = filesToDelete.Count - 1; index >= 0; index--)
                {
                    if (this.DataHandler.FileDatabase.FileTable.Find(filesToDelete[index].ID) == null)
                    {
                        filesToDelete.Remove(filesToDelete[index]);
                    }
                }
            }
            else
            {
                // Delete current image case. Get the ID of the current image and construct a datatable that contains that image's datarow
                deleteCurrentImageOnly = true;
                deleteFilesAndData = menuItem.Name.Equals(this.MenuItemDeleteCurrentFileAndData.Name);
                filesToDelete = new List<ImageRow>();
                if (this.DataHandler.ImageCache.Current != null)
                {
                    filesToDelete.Add(this.DataHandler.ImageCache.Current);
                }
            }

            // If no images are selected for deletion. Warn the user.
            // Note that this should never happen, as the invoking menu item should be disabled (and thus not selectable)
            // if there aren't any images to delete. Still,...
            if (filesToDelete == null || filesToDelete.Count < 1)
            {
                Dialogs.MenuEditNoFilesMarkedForDeletionDialog(this);
                return;
            }
            long currentFileID = this.DataHandler.ImageCache.Current.ID;
            DeleteImages deleteImagesDialog = new DeleteImages(this, this.DataHandler.FileDatabase, this.DataHandler.ImageCache, filesToDelete, deleteFilesAndData, deleteCurrentImageOnly);
            bool? result = deleteImagesDialog.ShowDialog();
            if (result == true)
            {
                // Delete the files
                Mouse.OverrideCursor = Cursors.Wait;
                // Reload the file datatable. 
                await this.FilesSelectAndShowAsync(currentFileID, this.DataHandler.FileDatabase.ImageSet.FileSelection).ConfigureAwait(true);

                if (deleteFilesAndData)
                {
                    // Find and show the image closest to the last one shown
                    if (this.DataHandler.FileDatabase.CountAllCurrentlySelectedFiles > 0)
                    {
                        int nextImageRow = this.DataHandler.FileDatabase.GetFileOrNextFileIndex(currentFileID);
                        this.FileShow(nextImageRow);
                    }
                    else
                    {
                        // No images left, so disable everything
                        this.EnableOrDisableMenusAndControls();
                    }
                }
                else
                {
                    // display the updated properties on the current image, or the closest one to it.
                    int nextImageRow = this.DataHandler.FileDatabase.FindClosestImageRow(currentFileID);
                    this.FileShow(nextImageRow);
                }
                Mouse.OverrideCursor = null;
            }
        }

        // Date Correction sub-menu opening
        private void MenuItemDateCorrection_SubmenuOpened(object sender, RoutedEventArgs e)
        {
            if (this.IsUTCOffsetControlHidden())
            {
                this.MenuItemSetTimeZone.IsEnabled = false;
            }
        }

        // Re-read dates and times from files
        private async void MenuItemRereadDateTimesfromFiles_Click(object sender, RoutedEventArgs e)
        {
            // Warn the user that they are currently in a selection displaying only a subset of files, and make sure they want to continue.
            if (Dialogs.MaybePromptToApplyOperationOnSelectionDialog(
                this,
                this.DataHandler.FileDatabase,
                this.State.SuppressSelectedRereadDatesFromFilesPrompt,
                "'Reread dates and times from files...'",
                (bool optOut) => { this.State.SuppressSelectedRereadDatesFromFilesPrompt = optOut; }
                ))
            {
                DateTimeRereadFromFiles rereadDates = new DateTimeRereadFromFiles(this, this.DataHandler.FileDatabase);
                if (this.ShowDialogAndCheckIfChangesWereMade(rereadDates))
                {
                    await this.FilesSelectAndShowAsync().ConfigureAwait(true);
                };
            }
        }

        // Correct for daylight savings time
        private async void MenuItemDaylightSavingsTimeCorrection_Click(object sender, RoutedEventArgs e)
        {
            // Warn the user that they are currently in a selection displaying only a subset of files, and make sure they want to continue.
            if (Dialogs.MaybePromptToApplyOperationOnSelectionDialog(
                this,
                this.DataHandler.FileDatabase,
                this.State.SuppressSelectedDaylightSavingsCorrectionPrompt,
                "'Correct for daylight savings time...'",
                (bool optOut) => { this.State.SuppressSelectedDaylightSavingsCorrectionPrompt = optOut; }
                ))
            {
                DateDaylightSavingsTimeCorrection dateTimeChange = new DateDaylightSavingsTimeCorrection(this, this.DataHandler.FileDatabase, this.DataHandler.ImageCache);
                if (this.ShowDialogAndCheckIfChangesWereMade(dateTimeChange))
                {
                    await this.FilesSelectAndShowAsync().ConfigureAwait(true);
                };
            }
        }

        // Correct for cameras not set to the right date and time by specifying an offset
        private async void MenuItemDateTimeFixedCorrection_Click(object sender, RoutedEventArgs e)
        {
            // Warn the user that they are currently in a selection displaying only a subset of files, and make sure they want to continue.
            if (Dialogs.MaybePromptToApplyOperationOnSelectionDialog(this, this.DataHandler.FileDatabase, this.State.SuppressSelectedDateTimeFixedCorrectionPrompt,
                                                                           "'Add a fixed correction value to every date/time...'",
                                                               (bool optOut) =>
                                                               {
                                                                   this.State.SuppressSelectedDateTimeFixedCorrectionPrompt = optOut;
                                                               }))
            {
                DateTimeFixedCorrection fixedDateCorrection = new DateTimeFixedCorrection(this, this.DataHandler.FileDatabase, this.DataHandler.ImageCache.Current);
                if (this.ShowDialogAndCheckIfChangesWereMade(fixedDateCorrection))
                {
                    await this.FilesSelectAndShowAsync().ConfigureAwait(true);
                }
            }
        }

        // Correct for cameras whose clock runs fast or slow (clock drift). 
        // Note that the correction is applied only to images in the selected view.
        private async void MenuItemDateTimeLinearCorrection_Click(object sender, RoutedEventArgs e)
        {
            // Warn the user that they are currently in a selection displaying only a subset of files, and make sure they want to continue.
            if (Dialogs.MaybePromptToApplyOperationOnSelectionDialog(
                this,
                this.DataHandler.FileDatabase,
                this.State.SuppressSelectedDateTimeLinearCorrectionPrompt,
                "'Correct for camera clock drift'",
                (bool optOut) => { this.State.SuppressSelectedDateTimeLinearCorrectionPrompt = optOut; }
                ))
            {
                DateTimeLinearCorrection linearDateCorrection = new DateTimeLinearCorrection(this, this.DataHandler.FileDatabase);
                if (this.ShowDialogAndCheckIfChangesWereMade(linearDateCorrection))
                {
                    await this.FilesSelectAndShowAsync().ConfigureAwait(true);
                }
            }
        }

        // Correct ambiguous dates dialog i.e. dates that could be read as either month/day or day/month
        private async void MenuItemCorrectAmbiguousDates_Click(object sender, RoutedEventArgs e)
        {
            // Warn the user that they are currently in a selection displaying only a subset of files, and make sure they want to continue.
            if (Dialogs.MaybePromptToApplyOperationOnSelectionDialog(
                this, this.DataHandler.FileDatabase, this.State.SuppressSelectedAmbiguousDatesPrompt,
                "'Correct ambiguous dates...'",
                (bool optOut) =>
                 {
                     this.State.SuppressSelectedAmbiguousDatesPrompt = optOut;
                 }))
            {
                DateCorrectAmbiguous dateCorrection = new DateCorrectAmbiguous(this, this.DataHandler.FileDatabase);
                if (this.ShowDialogAndCheckIfChangesWereMade(dateCorrection))
                {
                    await this.FilesSelectAndShowAsync().ConfigureAwait(true);
                }
            }
        }

        // Reassign a group of images to a particular time zone
        private async void MenuItemSetTimeZone_Click(object sender, RoutedEventArgs e)
        {
            // Warn the user that they are currently in a selection displaying only a subset of files, and make sure they want to continue.
            if (Dialogs.MaybePromptToApplyOperationOnSelectionDialog(this, this.DataHandler.FileDatabase, this.State.SuppressSelectedSetTimeZonePrompt,
                                                                           "'Set the time zone of every date/time...'",
                                                               (bool optOut) =>
                                                               {
                                                                   this.State.SuppressSelectedSetTimeZonePrompt = optOut;
                                                               }))
            {
                DateTimeSetTimeZone fixedDateCorrection = new DateTimeSetTimeZone(this.DataHandler.FileDatabase, this.DataHandler.ImageCache.Current, this);
                if (this.ShowDialogAndCheckIfChangesWereMade(fixedDateCorrection))
                {
                    await this.FilesSelectAndShowAsync().ConfigureAwait(true);
                }
            }
        }

        private async void MenuItemEditFindMissingFolder_Click(object sender, RoutedEventArgs e)
        {
            bool? result = TimelapseWindow.CheckAndCorrectForMissingFolders(this, this.DataHandler.FileDatabase);
            if (true == result)
            {
                await this.FilesSelectAndShowAsync().ConfigureAwait(true);
                return;
            }
            if (result != null && false == result.Value)
            {
                Dialogs.MenuEditNoFoldersAreMissing(this);
            }
            // if result is null, it means that folders were missing but not updated.
        }

        //Try to find a missing image
        private async void MenuItemEditFindMissingImage_Click(object sender, RoutedEventArgs e)
        {
            // Don't do anything if the image actually exists. This should not fire, as this menu item is only enabled 
            // if there is a current image that doesn't exist. But just in case...
            if (null == this.DataHandler?.ImageCache?.Current || File.Exists(FilesFolders.GetFullPath(this.DataHandler.FileDatabase.FolderPath, this.DataHandler?.ImageCache?.Current)))
            {
                return;
            }

            string folderPath = this.DataHandler.FileDatabase.FolderPath;
            ImageRow currentImage = this.DataHandler?.ImageCache?.Current;

            // Search for - and return as list of relative path / filename tuples -  all folders under the root folder for files with the same name as the fileName.
            List<Tuple<string, string>> matchingRelativePathFileNameList = Util.FilesFolders.SearchForFoldersContainingFileName(folderPath, currentImage.File);

            // Remove any of the tuples that are spoken for i.e., that are already associated with a row in the database
            for (int i = matchingRelativePathFileNameList.Count - 1; i >= 0; i--)
            {
                if (this.DataHandler.FileDatabase.ExistsRelativePathAndFileInDataTable(matchingRelativePathFileNameList[i].Item1, matchingRelativePathFileNameList[i].Item2))
                {
                    // We only want matching files that are not already assigned to another datafield in the database
                    matchingRelativePathFileNameList.RemoveAt(i);
                }
            }

            // If there are no remaining tuples, it means no potential matches were found. 
            // Display a message saying so and abort.
            if (matchingRelativePathFileNameList.Count == 0)
            {
                Dialogs.MissingFileSearchNoMatchesFoundDialog(this, currentImage.File);
                return;
            }

            // Now retrieve a list of all filenames located in the same folder (i.e., that have the same relative path) as the missing file.
            List<string> otherMissingFiles = this.DataHandler.FileDatabase.SelectFileNamesWithRelativePathFromDatabase(currentImage.RelativePath);

            // Remove the current missing file from the list, as well as any file that exists i.e., that is not missing.
            for (int i = otherMissingFiles.Count - 1; i >= 0; i--)
            {
                if (String.Equals(otherMissingFiles[i], currentImage.File) || File.Exists(Path.Combine(folderPath, currentImage.RelativePath, otherMissingFiles[i])))
                {
                    otherMissingFiles.RemoveAt(i);
                }
            }

            // For those that are left (if any), see if other files in the returned locations are in each path. Get their count, save them, and pass the count as a parameter e.g., a Dict with matching files, etc. 
            // Or perhapse even better, a list of file names for each path Dict<string, List<string>>
            // As we did above, go through the other missing files and remove those that are spoken for i.e., that are already associated with a row in the database.
            // What remains will be a list of  root paths, each with a list of  missing (unassociated) files that could be candidates for locating
            Dictionary<string, List<string>> otherMissingFileCandidates = new Dictionary<string, List<string>>();
            foreach (Tuple<string, string> matchingPath in matchingRelativePathFileNameList)
            {
                List<string> orphanMissingFiles = new List<string>();
                foreach (string otherMissingFile in otherMissingFiles)
                {
                    // Its a potential candidate if its not already referenced but it exists in that relative path folder
                    if (false == this.DataHandler.FileDatabase.ExistsRelativePathAndFileInDataTable(matchingPath.Item1, otherMissingFile) 
                        && File.Exists(FilesFolders.GetFullPath(FolderPath, matchingPath.Item1, otherMissingFile)))
                    {
                        orphanMissingFiles.Add(otherMissingFile);
                    }
                }
                otherMissingFileCandidates.Add(matchingPath.Item1, orphanMissingFiles);
            }

            Dialog.MissingImageLocateRelativePaths dialog = new Dialog.MissingImageLocateRelativePaths(this, this.DataHandler.FileDatabase, currentImage.RelativePath, currentImage.File, otherMissingFileCandidates);

            bool? result = dialog.ShowDialog();
            if (result == true)
            {
                Tuple<string, string> locatedMissingFile = dialog.LocatedMissingFile;
                if (String.IsNullOrEmpty(locatedMissingFile.Item2))
                {
                    return;
                }

                // Update the original missing file
                List<ColumnTuplesWithWhere> columnTuplesWithWhereList = new List<ColumnTuplesWithWhere>();
                ColumnTuplesWithWhere columnTuplesWithWhere = new ColumnTuplesWithWhere();
                columnTuplesWithWhere.Columns.Add(new ColumnTuple(Constant.DatabaseColumn.RelativePath, locatedMissingFile.Item1)); // The new relative path
                columnTuplesWithWhere.SetWhere(currentImage.RelativePath, currentImage.File); // Where the original relative path/file terms are met
                columnTuplesWithWhereList.Add(columnTuplesWithWhere);

                // Update the other missing files in the database, if any
                foreach (string otherMissingFileName in otherMissingFileCandidates[locatedMissingFile.Item1])
                {
                    columnTuplesWithWhere = new ColumnTuplesWithWhere();
                    columnTuplesWithWhere.Columns.Add(new ColumnTuple(Constant.DatabaseColumn.RelativePath, locatedMissingFile.Item1)); // The new value
                    columnTuplesWithWhere.SetWhere(currentImage.RelativePath, otherMissingFileName); // Where the original relative path/file terms are met
                    columnTuplesWithWhereList.Add(columnTuplesWithWhere);
                }

                // Now update the database
                this.DataHandler.FileDatabase.UpdateFiles(columnTuplesWithWhereList);
                await this.FilesSelectAndShowAsync().ConfigureAwait(true);
            }
        }

        // Identify or reclassify dark files.
        private void MenuItemEditClassifyDarkImages_Click(object sender, RoutedEventArgs e)
        {
            // Warn the user that they are currently in a selection displaying only a subset of files, and make sure they want to continue.
            if (Dialogs.MaybePromptToApplyOperationOnSelectionDialog(this, this.DataHandler.FileDatabase, this.State.SuppressSelectedDarkThresholdPrompt,
                                                                           "'(Re-) classify dark files...'",
                                                               (bool optOut) =>
                                                               {
                                                                   this.State.SuppressSelectedDarkThresholdPrompt = optOut; // SG TODO
                                                               }))
            {
                using (DarkImagesThreshold darkThreshold = new DarkImagesThreshold(this, this.DataHandler.FileDatabase, this.State, this.DataHandler.ImageCache.CurrentRow))
                {
                    darkThreshold.Owner = this;
                    if (darkThreshold.ShowDialog() == true)
                    {
                        // Force an update of the current image in case the current values have changed
                        System.Diagnostics.Debug.Print("Updating Files with Dark");
                        this.FileShow(this.DataHandler.ImageCache.CurrentRow, true);
                    }
                }
            }
        }

        // Edit notes for this image set
        private void MenuItemLog_Click(object sender, RoutedEventArgs e)
        {
            EditLog editImageSetLog = new EditLog(this.DataHandler.FileDatabase.ImageSet.Log, this)
            {
                Owner = this
            };
            bool? result = editImageSetLog.ShowDialog();
            if (result == true)
            {
                this.DataHandler.FileDatabase.ImageSet.Log = editImageSetLog.Log.Text;
                this.DataHandler.FileDatabase.UpdateSyncImageSetToDatabase();
            }
        }

        // HELPER FUNCTION, only referenced by the above menu callbacks.
        // Various dialogs perform a bulk edit, after which various states have to be refreshed
        // This method shows the dialog and (if a bulk edit is done) refreshes those states.
        private bool ShowDialogAndCheckIfChangesWereMade(Window dialog)
        {
            dialog.Owner = this;
            return (dialog.ShowDialog() == true);
        }
    }
}
