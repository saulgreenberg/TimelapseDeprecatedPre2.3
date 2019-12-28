using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Timelapse.Controls;
using Timelapse.Enums;
using Timelapse.Util;
using MessageBox = Timelapse.Dialog.MessageBox;

namespace Timelapse
{
    // File Selection which includes showing the current file
    public partial class TimelapseWindow : Window, IDisposable
    {
        // FilesSelectAndShow: various forms
        private async Task FilesSelectAndShow()
        {
            if (this.dataHandler == null || this.dataHandler.FileDatabase == null)
            {
                TraceDebug.PrintMessage("FilesSelectAndShow: Expected a file database to be available.");
                return;
            }
            await this.FilesSelectAndShow(this.dataHandler.FileDatabase.ImageSet.FileSelection).ConfigureAwait(true);
        }

        private async Task FilesSelectAndShow(FileSelectionEnum selection)
        {
            long fileID = Constant.DatabaseValues.DefaultFileID;
            if (this.dataHandler != null && this.dataHandler.ImageCache != null && this.dataHandler.ImageCache.Current != null)
            {
                fileID = this.dataHandler.ImageCache.Current.ID;
            }
            await this.FilesSelectAndShow(fileID, selection).ConfigureAwait(true);
        }

        // FilesSelectAndShow: Full version
        // PEFORMANCE FILES SELECT AND SHOW CALLED TOO OFTEN, GIVEN THAT IT IS A SLOW OPERATION
        // Note. forceUpdate isn't currently used. However,
        // I kept it in in case I want to use it in the future.
#pragma warning disable IDE0060 // Remove unused parameter
        private async Task FilesSelectAndShow(long imageID, FileSelectionEnum selection)
        #pragma warning restore IDE0060 // Remove unused parameter
        {
            // change selection
            // if the data grid is bound the file database automatically updates its contents on SelectFiles()
            if (this.dataHandler == null)
            {
                TraceDebug.PrintMessage("FilesSelectAndShow() should not be reachable with a null data handler.  Is a menu item wrongly enabled?");
                return ;
            }
            if (this.dataHandler.FileDatabase == null)
            {
                TraceDebug.PrintMessage("FilesSelectAndShow() should not be reachable with a null database.  Is a menu item wrongly enabled?");
                return ;
            }
            this.BusyIndicator.IsBusy = true; // Display the busy indicator
            // Select the files according to the given selection
            // Note that our check for missing actually checks to see if the file exists,
            // which is why its a different operation
            // PEFORMANCE - TWO  SLOW OPERATIONS: FilesSelectAndShow invoking this.dataHandler.FileDatabase.SelectFile / .SelectMissingFilesFromCurrentlySelectedFiles
            Mouse.OverrideCursor = Cursors.Wait;
            if (selection == FileSelectionEnum.Missing)
            {
                // PERFORMANCE this can be slow if there are many files, as it checks every single file in the current database selection to see if it exists
                // However, it is not a mainstream operation so can be considered a lower priority place for optimization
                this.dataHandler.FileDatabase.SelectMissingFilesFromCurrentlySelectedFiles();
            }
            else
            {
                // If its a folder selection, record it so we can save it later in the image set table 
                this.dataHandler.FileDatabase.ImageSet.SelectedFolder = selection == FileSelectionEnum.Folders
                    ? this.dataHandler.FileDatabase.GetSelectedFolder()
                    : String.Empty;
                // PERFORMANCE Select Files is a very slow operation as it runs a query over all files and returns everything it finds as datatables stored in memory.
                await this.dataHandler.FileDatabase.SelectFiles(selection).ConfigureAwait(true);
                this.dataHandler.FileDatabase.FileTable.BindDataGrid(this.dataHandler.FileDatabase.boundGrid, this.dataHandler.FileDatabase.onFileDataTableRowChanged);
            }
            Mouse.OverrideCursor = null;

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
                    case FileSelectionEnum.Custom:
                        messageBox.Message.Problem = "No files currently match the custom selection so nothing can be shown.";
                        messageBox.Message.Reason = "No files match the criteria set in the current Custom selection.";
                        messageBox.Message.Hint = "Create a different custom selection and apply it view the matching files.";
                        break;
                    case FileSelectionEnum.Folders:
                        messageBox.Message.Problem = "No files and/or image data were found for the selected folder.";
                        messageBox.Message.Reason = "Perhaps they were deleted during this session?";
                        messageBox.Message.Hint = "Try other folders or another selection. ";
                        break;
                    case FileSelectionEnum.Dark:
                        messageBox.Message.Problem = "Dark files were previously selected but no files are currently dark so nothing can be shown.";
                        messageBox.Message.Reason = "No files have their 'ImageQuality' field set to Dark.";
                        messageBox.Message.Hint = "If you have files you think should be marked as 'Dark', set their 'ImageQuality' field to 'Dark' and then reselect dark files.";
                        break;
                    case FileSelectionEnum.Missing:
                        messageBox.Message.Problem = "Missing files were previously selected. However, none of the files appear to be missing, so nothing can be shown.";
                        break;
                    case FileSelectionEnum.MarkedForDeletion:
                        messageBox.Message.Problem = "Files marked for deletion were previously selected but no files are currently marked so nothing can be shown.";
                        messageBox.Message.Reason = "No files have their 'Delete?' field checked.";
                        messageBox.Message.Hint = "If you have files you think should be marked for deletion, check their 'Delete?' field and then reselect files marked for deletion.";
                        break;
                    case FileSelectionEnum.Ok:
                        messageBox.Message.Problem = "Light files were previously selected but no files are currently marked 'Light' so nothing can be shown.";
                        messageBox.Message.Reason = "No files have their 'ImageQuality' field set to Light.";
                        messageBox.Message.Hint = "If you have files you think should be marked as 'Light', set their 'ImageQuality' field to 'Light' and then reselect Light files.";
                        break;
                    default:
                        throw new NotSupportedException(String.Format("Unhandled selection {0}.", selection));
                }
                this.StatusBar.SetMessage("Resetting selection to All files.");
                messageBox.ShowDialog();

                selection = FileSelectionEnum.All;
                // PEFORMANCE: The standard select files operation in FilesSelectAndShow
                await this.dataHandler.FileDatabase.SelectFiles(selection).ConfigureAwait(true);
                this.dataHandler.FileDatabase.FileTable.BindDataGrid(this.dataHandler.FileDatabase.boundGrid, this.dataHandler.FileDatabase.onFileDataTableRowChanged);
            }

            // Change the selection to reflect what the user selected. Update the menu state accordingly
            // Set the checked status of the radio button menu items to the selection.
            string status;
            switch (selection)
            {
                case FileSelectionEnum.All:
                    status = "All files";
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
                case FileSelectionEnum.Folders:
                    status = "Files in a specific folder";
                    break;
                case FileSelectionEnum.Missing:
                    status = "Missing files";
                    break;
                case FileSelectionEnum.Ok:
                    status = "Non-dark files";
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

            this.DataEntryControls.AutocompletionPopulateAllNotesWithFileTableValues(this.dataHandler.FileDatabase);
            // Always force an update after a selection
            this.FileShow(this.dataHandler.FileDatabase.GetFileOrNextFileIndex(imageID), true);

            // Update the status bar accordingly
            this.StatusBar.SetCurrentFile(this.dataHandler.ImageCache.CurrentRow + 1);  // We add 1 because its a 0-based list
            this.StatusBar.SetCount(this.dataHandler.FileDatabase.CurrentlySelectedFileCount);
            this.FileNavigatorSlider_EnableOrDisableValueChangedCallback(true);
            this.dataHandler.FileDatabase.ImageSet.FileSelection = selection;    // Remember the current selection
            this.BusyIndicator.IsBusy = false; // Display the busy indicator
            return;
        }
    }
}
