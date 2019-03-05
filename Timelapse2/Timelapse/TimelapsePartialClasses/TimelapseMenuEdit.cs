using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Timelapse.Controls;
using Timelapse.Database;
using Timelapse.Dialog;
using Timelapse.Enums;
using Timelapse.QuickPaste;
using Timelapse.Util;
using MessageBox = Timelapse.Dialog.MessageBox;

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
            this.MenuItemDeleteCurrentFile.IsEnabled = state;
            this.MenuItemDeleteCurrentFileAndData.IsEnabled = state;
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
            if (Utilities.TryGetFileFromUser("Import QuickPaste entries by selecting the Timelapse database (.ddb) file from the image folder where you had used them.",
                                             Path.Combine(this.dataHandler.FileDatabase.FolderPath, Constant.File.DefaultFileDatabaseFileName),
                                             String.Format("Database files (*{0})|*{0}", Constant.File.FileDatabaseFileExtension),
                                             Constant.File.FileDatabaseFileExtension,
                                             out string ddbFile) == true)
            {
                List<QuickPasteEntry> qpe = QuickPasteOperations.QuickPasteImportFromDB(this.dataHandler.FileDatabase, ddbFile);
                if (qpe.Count == 0)
                {
                    MessageBox messageBox = new MessageBox("Could not import QuickPaste entries", this);
                    messageBox.Message.Problem = "Timelapse could not find any QuickPaste entries in the selected database";
                    messageBox.Message.Reason = "When an analyst creates QuickPaste entries, those entries are stored in the database file " + Environment.NewLine;
                    messageBox.Message.Reason += "associated with the image set being analyzed. Since none where found, " + Environment.NewLine;
                    messageBox.Message.Reason += "its likely that no one had created any quickpaste entries when analyzing that image set.";
                    messageBox.Message.Hint = "Perhaps they are in a different database?";
                    messageBox.Message.Icon = MessageBoxImage.Information;
                    messageBox.ShowDialog();
                    return;
                }
                else
                {
                    this.quickPasteEntries = qpe;
                    this.dataHandler.FileDatabase.SyncImageSetToDatabase();
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
        private void MenuItemPopulateFieldFromMetadata_Click(object sender, RoutedEventArgs e)
        {
            // If we are not in the selection All view, or if its a corrupt image or deleted image, tell the person. Selecting ok will shift the selection.
            // We want to be on a valid image as otherwise the metadata of interest won't appear
            if (this.dataHandler.ImageCache.Current.IsDisplayable() == false)
            {
                int firstFileDisplayable = this.dataHandler.FileDatabase.GetCurrentOrNextDisplayableFile(this.dataHandler.ImageCache.CurrentRow);
                if (firstFileDisplayable == -1)
                {
                    // There are no displayable images, and thus no metadata to choose from, so abort
                    MessageBox messageBox = new MessageBox("Populate a data field with image metadata of your choosing.", this);
                    messageBox.Message.Problem = "Timelapse can't extract any metadata, as there are no valid displayable file." + Environment.NewLine;
                    messageBox.Message.Reason += "Timelapse must have at least one valid file in order to get its metadata. However, the image files are either missing (not available) or corrupted.";
                    messageBox.Message.Icon = MessageBoxImage.Error;
                    messageBox.ShowDialog();
                    return;
                }
            }

            if (this.MaybePromptToApplyOperationIfPartialSelection(this.state.SuppressSelectedPopulateFieldFromMetadataPrompt,
                                                               "'Populate a data field with image metadata...'",
                                                               (bool optOut) =>
                                                               {
                                                                   this.state.SuppressSelectedPopulateFieldFromMetadataPrompt = optOut;
                                                               }))
            {
                PopulateFieldWithMetadata populateField = new PopulateFieldWithMetadata(this.dataHandler.FileDatabase, this.dataHandler.ImageCache.Current.GetFilePath(this.FolderPath), this);
                this.ShowBulkImageEditDialog(populateField, false);
            }
        }

        // Delete sub-menu opening
        private void MenuItemDelete_SubmenuOpening(object sender, RoutedEventArgs e)
        {
            try
            {
                int deletedImages = this.dataHandler.FileDatabase.GetFileCount(FileSelectionEnum.MarkedForDeletion);
                this.MenuItemDeleteFiles.IsEnabled = deletedImages > 0;
                this.MenuItemDeleteFilesAndData.IsEnabled = deletedImages > 0;
                this.MenuItemDeleteCurrentFileAndData.IsEnabled = true;
                this.MenuItemDeleteCurrentFile.IsEnabled = this.dataHandler.ImageCache.Current.IsDisplayable() || this.dataHandler.ImageCache.Current.ImageQuality == FileSelectionEnum.Corrupted;
            }
            catch (Exception exception)
            {
                TraceDebug.PrintMessage(String.Format("Delete submenu failed to open in Delete_SubmenuOpening. {0}", exception.ToString()));

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
        private void MenuItemDeleteFiles_Click(object sender, RoutedEventArgs e)
        {
            MenuItem menuItem = sender as MenuItem;

            // This callback is invoked by DeleteImage (which deletes the current image) and DeleteImages (which deletes the images marked by the deletion flag)
            // Thus we need to use two different methods to construct a table containing all the images marked for deletion
            List<ImageRow> imagesToDelete;
            bool deleteCurrentImageOnly;
            bool deleteFilesAndData;
            if (menuItem.Name.Equals(this.MenuItemDeleteFiles.Name) || menuItem.Name.Equals(this.MenuItemDeleteFilesAndData.Name))
            {
                deleteCurrentImageOnly = false;
                deleteFilesAndData = menuItem.Name.Equals(this.MenuItemDeleteFilesAndData.Name);
                // get list of all images marked for deletion in the current seletion
                imagesToDelete = this.dataHandler.FileDatabase.GetFilesMarkedForDeletion().ToList();
                for (int index = imagesToDelete.Count - 1; index >= 0; index--)
                {
                    if (this.dataHandler.FileDatabase.FileTable.Find(imagesToDelete[index].ID) == null)
                    {
                        imagesToDelete.Remove(imagesToDelete[index]);
                    }
                }
            }
            else
            {
                // Delete current image case. Get the ID of the current image and construct a datatable that contains that image's datarow
                deleteCurrentImageOnly = true;
                deleteFilesAndData = menuItem.Name.Equals(this.MenuItemDeleteCurrentFileAndData.Name);
                imagesToDelete = new List<ImageRow>();
                if (this.dataHandler.ImageCache.Current != null)
                {
                    imagesToDelete.Add(this.dataHandler.ImageCache.Current);
                }
            }

            // If no images are selected for deletion. Warn the user.
            // Note that this should never happen, as the invoking menu item should be disabled (and thus not selectable)
            // if there aren't any images to delete. Still,...
            if (imagesToDelete == null || imagesToDelete.Count < 1)
            {
                MessageBox messageBox = new MessageBox("No files are marked for deletion", this);
                messageBox.Message.Problem = "You are trying to delete files marked for deletion, but no files have their 'Delete?' field checked.";
                messageBox.Message.Hint = "If you have files that you think should be deleted, check their Delete? field.";
                messageBox.Message.Icon = MessageBoxImage.Information;
                messageBox.ShowDialog();
                return;
            }

            DeleteImages deleteImagesDialog = new DeleteImages(this.dataHandler.FileDatabase, imagesToDelete, deleteFilesAndData, deleteCurrentImageOnly, this);
            bool? result = deleteImagesDialog.ShowDialog();
            if (result == true)
            {
                // cache the current ID as the current image may be invalidated
                long currentFileID = this.dataHandler.ImageCache.Current.ID;

                Mouse.OverrideCursor = Cursors.Wait;
                List<ColumnTuplesWithWhere> imagesToUpdate = new List<ColumnTuplesWithWhere>();
                List<long> imageIDsToDropFromDatabase = new List<long>();
                foreach (ImageRow image in imagesToDelete)
                {
                    // invalidate cache so Missing placeholder will be displayed
                    // release any handle open on the file so it can be moved
                    this.dataHandler.ImageCache.TryInvalidate(image.ID);
                    // SAULXXX Note that we should likely pop up a dialog box that displays non-missing files that we can't (for whatever reason) delete
                    // SAULXXX If we can't delete it, we may want to abort changing the various DeleteFlage and ImageQuality values. 
                    // SAULXXX A good way is to put an 'image.ImageFileExists' field in, and then do various tests on that.
                    image.TryMoveFileToDeletedFilesFolder(this.dataHandler.FileDatabase.FolderPath);

                    if (deleteFilesAndData)
                    {
                        // mark the image row for dropping
                        imageIDsToDropFromDatabase.Add(image.ID);
                    }
                    else
                    {
                        // as only the file was deleted, change image quality to FileNoLongerAvailable and clear the delete flag
                        image.DeleteFlag = false;
                        image.ImageQuality = FileSelectionEnum.Missing;
                        List<ColumnTuple> columnTuples = new List<ColumnTuple>()
                        {
                            new ColumnTuple(Constant.DatabaseColumn.DeleteFlag, Constant.BooleanValue.False),
                            new ColumnTuple(Constant.DatabaseColumn.ImageQuality, FileSelectionEnum.Missing.ToString())
                        };
                        imagesToUpdate.Add(new ColumnTuplesWithWhere(columnTuples, image.ID));
                    }
                }

                // Invalidate the overview cache as well, so Missing placeholder will be displayed.
                this.MarkableCanvas.ClickableImagesGrid.InvalidateCache();

                if (deleteFilesAndData)
                {
                    // drop images
                    this.dataHandler.FileDatabase.DeleteFilesAndMarkers(imageIDsToDropFromDatabase);

                    // Reload the file datatable. Then find and show the image closest to the last one shown
                    this.FilesSelectAndShow(currentFileID, this.dataHandler.FileDatabase.ImageSet.FileSelection);
                    if (this.dataHandler.FileDatabase.CurrentlySelectedFileCount > 0)
                    {
                        int nextImageRow = this.dataHandler.FileDatabase.GetFileOrNextFileIndex(currentFileID);
                        this.FileShow(nextImageRow);
                    }
                    else
                    {
                        this.EnableOrDisableMenusAndControls();
                    }
                }
                else
                {
                    // update image properties
                    this.dataHandler.FileDatabase.UpdateFiles(imagesToUpdate);
                    this.FilesSelectAndShow(currentFileID, this.dataHandler.FileDatabase.ImageSet.FileSelection);

                    // display the updated properties on the current image
                    int nextImageRow = this.dataHandler.FileDatabase.FindClosestImageRow(currentFileID);
                    this.FileShow(nextImageRow);
                }
                Mouse.OverrideCursor = null;
            }
        }

        // Re-read dates and times from files
        private void MenuItemRereadDateTimesfromFiles_Click(object sender, RoutedEventArgs e)
        {
            // If we are not in the selection All view, or if its a corrupt file, tell the person. Selecting ok will shift the views..
            if (this.MaybePromptToApplyOperationIfPartialSelection(this.state.SuppressSelectedRereadDatesFromFilesPrompt,
                                                               "'Reread dates from files...'",
                                                               (bool optOut) =>
                                                               {
                                                                   this.state.SuppressSelectedRereadDatesFromFilesPrompt = optOut;
                                                               }))
            {
                DateTimeRereadFromFiles rereadDates = new DateTimeRereadFromFiles(this.dataHandler.FileDatabase, this);
                this.ShowBulkImageEditDialog(rereadDates, true);
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

        // Correct for daylight savings time
        private void MenuItemDaylightSavingsTimeCorrection_Click(object sender, RoutedEventArgs e)
        {
            // If we are not in the selection All view, or if its a corrupt image, tell the person. Selecting ok will shift the views..
            if (this.dataHandler.ImageCache.Current.IsDisplayable() == false)
            {
                // Just a corrupted image
                MessageBox messageBox = new MessageBox("Can't correct for daylight savings time.", this);
                messageBox.Message.Problem = "This is a corrupted file.";
                messageBox.Message.Solution = "To correct for daylight savings time, you need to:" + Environment.NewLine;
                messageBox.Message.Solution += "\u2022 be displaying a file with a valid date ";
                messageBox.Message.Solution += "\u2022 where that file should be the one at the daylight savings time threshold.";
                messageBox.Message.Icon = MessageBoxImage.Exclamation;
                messageBox.ShowDialog();
                return;
            }

            if (this.MaybePromptToApplyOperationIfPartialSelection(this.state.SuppressSelectedDaylightSavingsCorrectionPrompt,
                                                               "'Correct for daylight savings time...'",
                                                               (bool optOut) =>
                                                               {
                                                                   this.state.SuppressSelectedDaylightSavingsCorrectionPrompt = optOut;
                                                               }))
            {
                DateDaylightSavingsTimeCorrection dateTimeChange = new DateDaylightSavingsTimeCorrection(this.dataHandler.FileDatabase, this.dataHandler.ImageCache, this);
                this.ShowBulkImageEditDialog(dateTimeChange, true);
            }
        }

        // Correct for cameras not set to the right date and time by specifying an offset
        private void MenuItemDateTimeFixedCorrection_Click(object sender, RoutedEventArgs e)
        {
            // Warn user that they are in a selected view, and verify that they want to continue
            if (this.MaybePromptToApplyOperationIfPartialSelection(this.state.SuppressSelectedDateTimeFixedCorrectionPrompt,
                                                               "'Add a fixed correction value to every date/time...'",
                                                               (bool optOut) =>
                                                               {
                                                                   this.state.SuppressSelectedDateTimeFixedCorrectionPrompt = optOut;
                                                               }))
            {
                DateTimeFixedCorrection fixedDateCorrection = new DateTimeFixedCorrection(this.dataHandler.FileDatabase, this.dataHandler.ImageCache.Current, this);
                this.ShowBulkImageEditDialog(fixedDateCorrection, true);
            }
        }

        // Correct for cameras whose clock runs fast or slow (clock drift). 
        // Note that the correction is applied only to images in the selected view.
        private void MenuItemDateTimeLinearCorrection_Click(object sender, RoutedEventArgs e)
        {
            // Warn user that they are in a selected view, and verify that they want to continue
            if (this.MaybePromptToApplyOperationIfPartialSelection(this.state.SuppressSelectedDateTimeLinearCorrectionPrompt,
                                                               "'Correct for camera clock drift'",
                                                               (bool optOut) =>
                                                               {
                                                                   this.state.SuppressSelectedDateTimeLinearCorrectionPrompt = optOut;
                                                               }))
            {
                DateTimeLinearCorrection linearDateCorrection = new DateTimeLinearCorrection(this.dataHandler.FileDatabase, this);
                this.ShowBulkImageEditDialog(linearDateCorrection, true);
            }
        }

        // Correct ambiguous dates dialog i.e. dates that could be read as either month/day or day/month
        private void MenuItemCorrectAmbiguousDates_Click(object sender, RoutedEventArgs e)
        {
            // Warn user that they are in a selection view, and verify that they want to continue
            if (this.MaybePromptToApplyOperationIfPartialSelection(this.state.SuppressSelectedAmbiguousDatesPrompt,
                                                               "'Correct ambiguous dates...'",
                                                               (bool optOut) =>
                                                               {
                                                                   this.state.SuppressSelectedAmbiguousDatesPrompt = optOut;
                                                               }))
            {
                DateCorrectAmbiguous dateCorrection = new DateCorrectAmbiguous(this.dataHandler.FileDatabase, this);
                if (dateCorrection.Abort)
                {
                    MessageBox messageBox = new MessageBox("No ambiguous dates found", this);
                    messageBox.Message.What = "No ambiguous dates found.";
                    messageBox.Message.Reason = "All of the images in this selected view have unambguous date fields." + Environment.NewLine;
                    messageBox.Message.Result = "No corrections needed, and no changes have been made." + Environment.NewLine;
                    messageBox.Message.Icon = MessageBoxImage.Information;
                    messageBox.ShowDialog();
                    messageBox.Close();
                    return;
                }
                this.ShowBulkImageEditDialog(dateCorrection, true);
            }
        }

        // Reassign a group of images to a particular time zone
        private void MenuItemSetTimeZone_Click(object sender, RoutedEventArgs e)
        {
            // Warn user that they are in a selecction view, and verify that they want to continue
            if (this.MaybePromptToApplyOperationIfPartialSelection(this.state.SuppressSelectedSetTimeZonePrompt,
                                                               "'Set the time zone of every date/time...'",
                                                               (bool optOut) =>
                                                               {
                                                                   this.state.SuppressSelectedSetTimeZonePrompt = optOut;
                                                               }))
            {
                DateTimeSetTimeZone fixedDateCorrection = new DateTimeSetTimeZone(this.dataHandler.FileDatabase, this.dataHandler.ImageCache.Current, this);
                this.ShowBulkImageEditDialog(fixedDateCorrection, false);
            }
        }

        // Identify or reclassify dark files.
        private void MenuItemEditClassifyDarkImages_Click(object sender, RoutedEventArgs e)
        {
            if (this.MaybePromptToApplyOperationIfPartialSelection(this.state.SuppressSelectedDarkThresholdPrompt,
                                                               "'(Re-) classify dark files...'",
                                                               (bool optOut) =>
                                                               {
                                                                   this.state.SuppressSelectedDarkThresholdPrompt = optOut; // SG TODO
                                                               }))
            {
                using (DarkImagesThreshold darkThreshold = new DarkImagesThreshold(this.dataHandler.FileDatabase, this.dataHandler.ImageCache.CurrentRow, this.state, this))
                {
                    darkThreshold.Owner = this;
                    darkThreshold.ShowDialog();
                }
            }
        }

        // Edit notes for this image set
        private void MenuItemLog_Click(object sender, RoutedEventArgs e)
        {
            EditLog editImageSetLog = new EditLog(this.dataHandler.FileDatabase.ImageSet.Log, this)
            {
                Owner = this
            };
            bool? result = editImageSetLog.ShowDialog();
            if (result == true)
            {
                this.dataHandler.FileDatabase.ImageSet.Log = editImageSetLog.Log.Text;
                this.dataHandler.FileDatabase.SyncImageSetToDatabase();
            }
        }

        // HELPER FUNCTION, only referenced by the above menu callbacks.
        // Various dialogs perform a bulk edit, after which various states have to be refreshed
        // This method shows the dialog and (if a bulk edit is done) refreshes those states.
        private void ShowBulkImageEditDialog(Window dialog, bool forceUpdate)
        {
            dialog.Owner = this;
            long currentFileID = this.dataHandler.ImageCache.Current.ID;
            bool? result = dialog.ShowDialog();
            if (result == true)
            {
                this.FilesSelectAndShow(forceUpdate);
            }
        }
    }
}
