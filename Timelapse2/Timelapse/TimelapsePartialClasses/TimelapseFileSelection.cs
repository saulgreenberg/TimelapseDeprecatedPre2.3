using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Timelapse.Controls;
using Timelapse.Database;
using Timelapse.Enums;
using Timelapse.Util;
using MessageBox = Timelapse.Dialog.MessageBox;

namespace Timelapse
{
    // File Selection which includes showing the current file
    public partial class TimelapseWindow : Window, IDisposable
    {
        // FilesSelectAndShow: various forms
        private void FilesSelectAndShow(bool forceUpdate)
        {
            if (this.dataHandler == null || this.dataHandler.FileDatabase == null)
            {
                TraceDebug.PrintMessage("FilesSelectAndShow: Expected a file database to be available.");
            }
            this.FilesSelectAndShow(this.dataHandler.FileDatabase.ImageSet.FileSelection, forceUpdate);
        }

        private void FilesSelectAndShow(FileSelectionEnum selection, bool forceUpdate)
        {
            long fileID = Constant.DatabaseValues.DefaultFileID;
            if (this.dataHandler != null && this.dataHandler.ImageCache != null && this.dataHandler.ImageCache.Current != null)
            {
                fileID = this.dataHandler.ImageCache.Current.ID;
            }
            this.FilesSelectAndShow(fileID, selection, forceUpdate);
        }

        // FilesSelectAndShow: Basic form doesn't force an update
        private void FilesSelectAndShow(long imageID, FileSelectionEnum selection)
        {
            FilesSelectAndShow(imageID, selection, false);
        }

        // FilesSelectAndShow: Full version
        private void FilesSelectAndShow(long imageID, FileSelectionEnum selection, bool forceUpdate)
        {
            // change selection
            // if the data grid is bound the file database automatically updates its contents on SelectFiles()
            if (this.dataHandler == null)
            {
                TraceDebug.PrintMessage("FilesSelectAndShow() should not be reachable with a null data handler.  Is a menu item wrongly enabled?");
                return;
            }
            if (this.dataHandler.FileDatabase == null)
            {
                TraceDebug.PrintMessage("FilesSelectAndShow() should not be reachable with a null database.  Is a menu item wrongly enabled?");
                return;
            }

            // We set forceUpdate to true because at least one imagequality status has changed
            // and we want the correct image to be shown
            if (CheckAndUpdateImageQualityForMissingFiles())
            {
                forceUpdate = true;
            }

            // Select the files according to the given selection
            this.dataHandler.FileDatabase.SelectFiles(selection);

            // explain to user if their selection has gone empty and change to all files
            if ((this.dataHandler.FileDatabase.CurrentlySelectedFileCount < 1) && (selection != FileSelectionEnum.All))
            {
                // These cases are reached when 
                // 1) datetime modifications result in no files matching a custom selection
                // 2) all files which match the selection get deleted
                MessageBox messageBox = new MessageBox("Resetting selection to All files (no files currently match the current selection)", this);
                messageBox.Message.Icon = MessageBoxImage.Information;
                messageBox.Message.Result = "The 'All files' selection will be applied, where all files in your image set are displayed.";

                switch (selection)
                {
                    case FileSelectionEnum.Corrupted:
                        messageBox.Message.Problem = "Corrupted files were previously selected but no files are currently corrupted, so nothing can be shown.";
                        messageBox.Message.Reason = "No files have their 'ImageQuality' field set to Corrupted.";
                        messageBox.Message.Hint = "If you have files you think should be marked as 'Corrupted', set their 'ImageQuality' field to 'Corrupted' and then reselect corrupted files.";
                        break;

                    case FileSelectionEnum.Custom:
                        messageBox.Message.Problem = "No files currently match the custom selection so nothing can be shown.";
                        messageBox.Message.Reason = "No files match the criteria set in the current Custom selection.";
                        messageBox.Message.Hint = "Create a different custom selection and apply it view the matching files.";
                        break;
                    case FileSelectionEnum.Dark:
                        messageBox.Message.Problem = "Dark files were previously selected but no files are currently dark so nothing can be shown.";
                        messageBox.Message.Reason = "No files have their 'ImageQuality' field set to Dark.";
                        messageBox.Message.Hint = "If you have files you think should be marked as 'Dark', set their 'ImageQuality' field to 'Dark' and then reselect dark files.";
                        break;
                    case FileSelectionEnum.Missing:
                        messageBox.Message.Problem = "Missing files were previously selected. However, none of the files are marked as missing, so nothing can be shown.";
                        messageBox.Message.Reason = "No files have their 'ImageQuality' field set to Missing.";
                        messageBox.Message.Hint = "If you have files that you think should be marked as 'Missing' (i.e., whose images are no longer available as shown by the displayed graphic), set their 'ImageQuality' field to 'Missing' and then reselect 'Missing' files.";
                        break;
                    case FileSelectionEnum.MarkedForDeletion:
                        messageBox.Message.Problem = "Files marked for deletion were previously selected but no files are currently marked so nothing can be shown.";
                        messageBox.Message.Reason = "No files have their 'Delete?' field checked.";
                        messageBox.Message.Hint = "If you have files you think should be marked for deletion, check their 'Delete?' field and then reselect files marked for deletion.";
                        break;
                    case FileSelectionEnum.Ok:
                        messageBox.Message.Problem = "Ok files were previously selected but no files are currently OK so nothing can be shown.";
                        messageBox.Message.Reason = "No files have their 'ImageQuality' field set to Ok.";
                        messageBox.Message.Hint = "If you have files you think should be marked as 'Ok', set their 'ImageQuality' field to 'Ok' and then reselect Ok files.";
                        break;
                    default:
                        throw new NotSupportedException(String.Format("Unhandled selection {0}.", selection));
                }
                this.StatusBar.SetMessage("Resetting selection to All files.");
                messageBox.ShowDialog();

                selection = FileSelectionEnum.All;
                this.dataHandler.FileDatabase.SelectFiles(selection);
            }

            // Change the selection to reflect what the user selected. Update the menu state accordingly
            // Set the checked status of the radio button menu items to the selection.
            string status;
            switch (selection)
            {
                case FileSelectionEnum.All:
                    status = "All files";
                    break;
                case FileSelectionEnum.Corrupted:
                    status = "Corrupted files";
                    break;
                case FileSelectionEnum.Custom:
                    status = "Custom selection";
                    break;
                case FileSelectionEnum.Dark:
                    status = "Dark files";
                    break;
                case FileSelectionEnum.MarkedForDeletion:
                    status = "Files marked for deletion";
                    break;
                case FileSelectionEnum.Missing:
                    status = "Missing files";
                    break;
                case FileSelectionEnum.Ok:
                    status = "Light and Okay files";
                    break;
                default:
                    throw new NotSupportedException(String.Format("Unhandled file selection {0}.", selection));
            }
            // Show feedback of the status description in both the status bar and the data entry control panel title
            this.StatusBar.SetView(status);
            this.DataEntryControlPanel.Title = "Data entry for " + status;

            // Reset the Episodes, as it may change based on the current selection
            Episodes.Reset();

            // Display the specified file or, if it's no longer selected, the next closest one
            // FileShow() handles empty image sets, so those don't need to be checked for here.
            // After a selection changes, set the slider to represent the index and the count of the current selection
            this.FileNavigatorSlider_EnableOrDisableValueChangedCallback(false);
            this.FileNavigatorSlider.Maximum = this.dataHandler.FileDatabase.CurrentlySelectedFileCount;  // Reset the slider to the size of images in this set
            if (this.FileNavigatorSlider.Maximum <= 50)
            {
                this.FileNavigatorSlider.IsSnapToTickEnabled = true;
                this.FileNavigatorSlider.TickFrequency = 1.0;
            }
            else
            {
                this.FileNavigatorSlider.IsSnapToTickEnabled = false;
                this.FileNavigatorSlider.TickFrequency = 0.02 * this.FileNavigatorSlider.Maximum;
            }

            // Reset the clickable grid selection after every change in the selection
            if (this.IsDisplayingMultipleImagesInOverview())
            {
                this.MarkableCanvas.ClickableImagesGrid.SelectInitialCellOnly();
            }

            // Always force an update after a selection
            this.FileShow(this.dataHandler.FileDatabase.GetFileOrNextFileIndex(imageID), true);

            // Update the status bar accordingly
            this.StatusBar.SetCurrentFile(this.dataHandler.ImageCache.CurrentRow + 1);  // We add 1 because its a 0-based list
            this.StatusBar.SetCount(this.dataHandler.FileDatabase.CurrentlySelectedFileCount);
            this.FileNavigatorSlider_EnableOrDisableValueChangedCallback(true);
            this.dataHandler.FileDatabase.ImageSet.FileSelection = selection;    // Remember the current selection
        }

        // Helper methods - used only by above

        // Check every file to see if:
        // - file exists but ImageQuality is Missing, or
        // - file does not exist but ImageQuality is anything other than missing
        // If there is a mismatch, change the ImageQuality to reflect the Files' actual status.
        // The downfall is that prior ImageQuality information will be lost if a change is made
        // Another issue is that the current version only checks the currently selected files vs. all files
        public bool CheckAndUpdateImageQualityForMissingFiles()
        {
            string filepath = String.Empty;
            string message = String.Empty;
            List<ColumnTuplesWithWhere> imagesToUpdate = new List<ColumnTuplesWithWhere>();
            ColumnTuplesWithWhere imageUpdate;

            // Get all files, regardless of the selection
            FileTable allFiles = this.dataHandler.FileDatabase.GetAllFiles();
            foreach (ImageRow image in allFiles)
            {
                filepath = Path.Combine(this.FolderPath, image.RelativePath, image.FileName);
                if (File.Exists(filepath) && image.ImageQuality == FileSelectionEnum.Missing)
                {
                    // The File exists but image quality is set to missing. Reset it to OK
                    // Note that the file may be corrupt, dark, etc., but we don't check for that.
                    // SAULXXX Perhaps we should?
                    image.ImageQuality = FileSelectionEnum.Ok;
                    imageUpdate = new ColumnTuplesWithWhere(new List<ColumnTuple>() { new ColumnTuple(Constant.DatabaseColumn.ImageQuality, image.ImageQuality.ToString()) }, image.ID);
                    imagesToUpdate.Add(imageUpdate);
                }
                else if (File.Exists(filepath) == false && image.ImageQuality != FileSelectionEnum.Missing)
                {
                    // The File does not exist anymore, but the image quality is not set to missing. Reset it to Missing
                    // Note that this could lose information,  as the file may be marked as corrupt or dark, etc., but we don't check for that.
                    // SAULXXX Not sure how to fix this, except to separate image quality information into other columns.
                    message = "Missing " + filepath;
                    image.ImageQuality = FileSelectionEnum.Missing;
                    imageUpdate = new ColumnTuplesWithWhere(new List<ColumnTuple>() { new ColumnTuple(Constant.DatabaseColumn.ImageQuality, image.ImageQuality.ToString()) }, image.ID);
                    imagesToUpdate.Add(imageUpdate);
                }
            }
            if (imagesToUpdate.Count > 0)
            {
                this.dataHandler.FileDatabase.UpdateFiles(imagesToUpdate);
                return true;
            }
            return false;
        }
    }
}
