using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Database;
using Timelapse.Enums;

namespace Timelapse
{
    // Select Menu Callbacks - runs database queries that creates a subset of images to display
    public partial class TimelapseWindow : Window, IDisposable
    {
        # region Select sub-menu opening
        private void MenuItemSelect_SubmenuOpening(object sender, RoutedEventArgs e)
        {
            this.FilePlayer_Stop(); // In case the FilePlayer is going

            this.MenuItemSelectMissingFiles.IsEnabled = true;

            // Enable menu if there are any files marked for deletion
            bool exists = this.DataHandler.FileDatabase.ExistsRowThatMatchesSelectionForAllFilesOrConstrainedRelativePathFiles(FileSelectionEnum.MarkedForDeletion);
            this.MenuItemSelectFilesMarkedForDeletion.Header = "All marked for d_eletion";
            if (!exists)
            {
                this.MenuItemSelectFilesMarkedForDeletion.Header += " (0)";
            }
            this.MenuItemSelectFilesMarkedForDeletion.IsEnabled = exists;

            // Put a checkmark next to the menu item that matches the stored selection criteria
            FileSelectionEnum selection = this.DataHandler.FileDatabase.ImageSet.FileSelection;

            this.MenuItemSelectAllFiles.IsChecked = selection == FileSelectionEnum.All;
            this.MenuItemSelectByRelativePath.IsChecked = selection == FileSelectionEnum.Folders;

            this.MenuItemSelectDarkFiles.IsChecked = selection == FileSelectionEnum.Dark;
            this.MenuItemSelectOkFiles.IsChecked = selection == FileSelectionEnum.Ok;
            this.MenuItemSelectByImageQuality.IsChecked = this.MenuItemSelectOkFiles.IsChecked || this.MenuItemSelectDarkFiles.IsChecked;

            this.MenuItemSelectMissingFiles.IsChecked = selection == FileSelectionEnum.Missing;
            this.MenuItemSelectFilesMarkedForDeletion.IsChecked = selection == FileSelectionEnum.MarkedForDeletion;
            this.MenuItemSelectCustomSelection.IsChecked = selection == FileSelectionEnum.Custom;
        }
        #endregion

        #region  ImageQuality_SubmenuOpening
        private void MenuItemSelectImageQuality_SubmenuOpening(object sender, RoutedEventArgs e)
        {
            bool existsDark = this.DataHandler.FileDatabase.ExistsRowThatMatchesSelectionForAllFilesOrConstrainedRelativePathFiles(FileSelectionEnum.Dark);
            bool existsOk = this.DataHandler.FileDatabase.ExistsRowThatMatchesSelectionForAllFilesOrConstrainedRelativePathFiles(FileSelectionEnum.Ok);

            // Enable only the menu items that can select at least one potential image 
            this.MenuItemSelectOkFiles.Header = "_Ok files";
            if (!existsOk)
            {
                this.MenuItemSelectOkFiles.Header += " (0)";
            }
            this.MenuItemSelectOkFiles.IsEnabled = existsOk;

            this.MenuItemSelectDarkFiles.Header = "_Dark files";
            if (!existsDark)
            {
                this.MenuItemSelectDarkFiles.Header += " (0)";
            }
            this.MenuItemSelectDarkFiles.IsEnabled = existsDark;
        }
        #endregion

        #region Select callback: All file, ImageQuality, Missing, Marked for Deletion
        private async void MenuItemSelectFiles_Click(object sender, RoutedEventArgs e)
        {
            MenuItem item = (MenuItem)sender;
            FileSelectionEnum selection;
            FileSelectionEnum oldSelection = this.DataHandler.FileDatabase.ImageSet.FileSelection;

            // Set the selection enum to match the menu selection 
            if (item == this.MenuItemSelectAllFiles)
            {
                selection = FileSelectionEnum.All;
            }
            else if (item == this.MenuItemSelectOkFiles)
            {
                selection = FileSelectionEnum.Ok;
            }
            else if (item == this.MenuItemSelectMissingFiles)
            {
                selection = FileSelectionEnum.Missing;
            }
            else if (item == this.MenuItemSelectDarkFiles)
            {
                selection = FileSelectionEnum.Dark;
            }
            else if (item == this.MenuItemSelectFilesMarkedForDeletion)
            {
                selection = FileSelectionEnum.MarkedForDeletion;
            }
            else if (item == this.MenuItemSelectByRelativePath)
            {
                // MenuItemSelectByFolder and its child folders should not be activated from here, 
                // but we add this test just as a reminder that we haven't forgotten it
                return;
            }
            else
            {
                selection = FileSelectionEnum.All;   // Just in case, this is the fallback operation
            }

            // Clear all the checkmarks from the Folder menu
            // But, not sure where we Treat the other menu checked status as a radio button i.e., we would want to toggle their states so only the clicked menu item is checked. 
            this.MenuItemSelectByRelativePath_ClearAllCheckmarks();

            // Select and show the files according to the selection made
            if (this.DataHandler.ImageCache.Current == null)
            {
                // Go to the first result (i.e., index 0) in the given selection set
                await this.FilesSelectAndShowAsync(selection).ConfigureAwait(true);
            }
            else
            {
                if (false == await this.FilesSelectAndShowAsync(this.DataHandler.ImageCache.Current.ID, selection).ConfigureAwait(true))
                {
                    this.DataHandler.FileDatabase.ImageSet.FileSelection = oldSelection;
                }
            }
        }
        #endregion

        #region Select by Folder Submenu(including submenu opening)
        private void MenuItemSelectByFolder_SubmenuOpening(object sender, RoutedEventArgs e)
        {
            // We don't have to do anything if the folder menu item list, except set its checkmark, if it has previously been populated
            if (!(sender is MenuItem menu))
            {
                // shouldn't happen
                return;
            }

            // Repopulate the menu if needed. 
            if (menu.Items.Count != 1)
            {
                // Gets the folders from the database, and created a menu item representing it
                this.MenuItemSelectByFolder_ResetFolderList();
            }
            // Set the checkmark to reflect the current search term for the relative path
            this.MenuItemFolderListSetCheckmark();
        }

        private void MenuItemFolderListSetCheckmark()
        {
            SearchTerm relativePathSearchTerm = this.DataHandler?.FileDatabase?.CustomSelection?.SearchTerms.First(term => term.DataLabel == Constant.DatabaseColumn.RelativePath);
            if (relativePathSearchTerm == null)
            {
                return;
            }

            foreach (MenuItem menuItem in this.MenuItemSelectByRelativePath.Items)
            {
                menuItem.IsChecked = relativePathSearchTerm.UseForSearching && String.Equals((string)menuItem.Header, relativePathSearchTerm.DatabaseValue);
            }
        }

        // Populate the menu. Get the folders from the database, and create a menu item representing it
        private void MenuItemSelectByFolder_ResetFolderList()
        {

            // Clear the list, excepting the first menu item all folders, which should be kept.
            MenuItem item = (MenuItem)this.MenuItemSelectByRelativePath.Items[0];
            this.MenuItemSelectByRelativePath.Items.Clear();
            this.MenuItemSelectByRelativePath.Items.Add(item);

            // Populate the menu . Get the folders from the database, and create a menu item representing it
            int i = 1;
            // PERFORMANCE. THIS introduces a delay when there are a large number of files. It is invoked when the user loads images for the first time. 
            // PROGRESSBAR - at the very least, show a progress bar if needed.

            List<string> folderList = this.DataHandler.FileDatabase.GetFoldersFromRelativePaths();//this.DataHandler.FileDatabase.GetDistinctValuesInColumn(Constant.DBTables.FileData, Constant.DatabaseColumn.RelativePath);
            foreach (string header in folderList)
            {
                if (string.IsNullOrEmpty(header))
                {
                    // An empty header is actually the root folder. Since we already have an entry representng all files, we don't need it.
                    continue;
                }

                // Add the folder to the menu only if it isn't constrained by the relative path arguments
                if (this.Arguments.ConstrainToRelativePath && !(header == this.Arguments.RelativePath || header.StartsWith(this.Arguments.RelativePath + @"\")))
                {
                    continue;
                }
                // Create a menu item for each folder
                MenuItem menuitemFolder = new MenuItem
                {
                    Header = header,
                    IsCheckable = true,
                    ToolTip = "Show only files in the folder (including its own sub-folders): " + header
                };
                menuitemFolder.Click += this.MenuItemSelectFolder_Click;
                this.MenuItemSelectByRelativePath.Items.Insert(i++, menuitemFolder);
            }
        }


        // A specific folder was selected.
        private async void MenuItemSelectFolder_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is MenuItem mi))
            {
                return;
            }

            // If its select all folders, then 
            if (mi == this.MenuItemSelectAllFolders)
            {
                // its all folders, so just select all folders
                await this.FilesSelectAndShowAsync(this.DataHandler.ImageCache.Current.ID, FileSelectionEnum.All).ConfigureAwait(true);
                return;
            }

            // Set and only use the relative path as a search term
            this.DataHandler.FileDatabase.CustomSelection.ClearCustomSearchUses();
            this.DataHandler.FileDatabase.CustomSelection.SetAndUseRelativePathSearchTerm((string)mi.Header);

            int count = this.DataHandler.FileDatabase.CountAllFilesMatchingSelectionCondition(FileSelectionEnum.Custom);
            if (count <= 0)
            {
                Timelapse.Dialog.MessageBox messageBox = new Timelapse.Dialog.MessageBox("No files in this folder", Application.Current.MainWindow);
                messageBox.Message.Icon = MessageBoxImage.Exclamation;
                messageBox.Message.Reason = String.Format("While the folder {0} exists, no image data is associated with any files in it.", mi.Header);
                messageBox.Message.Hint = String.Format("Perhaps you removed these files and its data during this session?");
                messageBox.ShowDialog();
            }
            this.MenuItemSelectByRelativePath_ClearAllCheckmarks();
            this.MenuItemSelectByRelativePath.IsChecked = true;
            mi.IsChecked = true;
            await this.FilesSelectAndShowAsync(this.DataHandler.ImageCache.Current.ID, FileSelectionEnum.Folders).ConfigureAwait(true);  // Go to the first result (i.e., index 0) in the given selection set
            //await this.FilesSelectAndShowAsync(this.DataHandler.ImageCache.Current.ID, FileSelectionEnum.Custom).ConfigureAwait(true);  // Go to the first result (i.e., index 0) in the given selection set
        }

        private void MenuItemSelectByRelativePath_ClearAllCheckmarks()
        {
            this.MenuItemSelectByRelativePath.IsChecked = false;
            foreach (MenuItem mi in this.MenuItemSelectByRelativePath.Items)
            {
                mi.IsChecked = false;
            }
        }
        #endregion

        #region Custom Selection: raises a dialog letting the user specify their selection criteria
        private async void MenuItemSelectCustomSelection_Click(object sender, RoutedEventArgs e)
        {
            // the first time the custom selection dialog is launched update the DateTime and UtcOffset search terms to the time of the current image
            SearchTerm firstDateTimeSearchTerm = this.DataHandler.FileDatabase.CustomSelection.SearchTerms.FirstOrDefault(searchTerm => searchTerm.DataLabel == Constant.DatabaseColumn.DateTime);
            if (firstDateTimeSearchTerm.GetDateTime() == Constant.ControlDefault.DateTimeValue.DateTime)
            {
                DateTimeOffset defaultDate = this.DataHandler.ImageCache.Current.DateTimeIncorporatingOffset;
                this.DataHandler.FileDatabase.CustomSelection.SetDateTimesAndOffset(defaultDate);
            }

            // show the dialog and process the resuls
            Dialog.CustomSelection customSelection = new Dialog.CustomSelection(this.DataHandler.FileDatabase, this.DataEntryControls, this, this.IsUTCOffsetControlHidden(), this.DataHandler.FileDatabase.CustomSelection.DetectionSelections)
            {
                Owner = this
            };
            bool? changeToCustomSelection = customSelection.ShowDialog();
            // Set the selection to show all images and a valid image
            if (changeToCustomSelection == true)
            {
                await this.FilesSelectAndShowAsync(this.DataHandler.ImageCache.Current.ID, FileSelectionEnum.Custom).ConfigureAwait(true);
                if (this.MenuItemSelectCustomSelection.IsChecked || this.MenuItemSelectCustomSelection.IsChecked)
                {
                    this.MenuItemSelectByRelativePath_ClearAllCheckmarks();
                }
            }
            else
            {
                // Since we canceled the custom selection, uncheck the item (but only if another menu item is shown checked)
                bool otherMenuItemIsChecked =
                    this.MenuItemSelectAllFiles.IsChecked ||
                    this.MenuItemSelectMissingFiles.IsChecked ||
                    this.MenuItemSelectByRelativePath.IsChecked ||
                    this.MenuItemSelectFilesMarkedForDeletion.IsChecked;
                this.MenuItemSelectCustomSelection.IsChecked = !otherMenuItemIsChecked;
            }
        }
        #endregion

        #region Refresh the Selection
        // Refresh the selection: based on the current select criteria. 
        // Useful when, for example, the user has selected a view, but then changed some data values where items no longer match the current selection.
        private async void MenuItemSelectReselect_Click(object sender, RoutedEventArgs e)
        {
            // Reselect the images, which re-sorts them to the current sort criteria. 
            await this.FilesSelectAndShowAsync(this.DataHandler.ImageCache.Current.ID, this.DataHandler.FileDatabase.ImageSet.FileSelection).ConfigureAwait(true);
        }
        #endregion


        private async void MenuItemSelectRandomSample_Click(object sender, RoutedEventArgs e)
        {
            Dialog.RandomSampleSelection customSelection = new Dialog.RandomSampleSelection(this, 1000);
            bool? useRandomSample = customSelection.ShowDialog();

            if (true == useRandomSample)
            {
                this.DataHandler.FileDatabase.CustomSelection.RandomSample = customSelection.SampleSize;
                await this.FilesSelectAndShowAsync(this.DataHandler.ImageCache.Current.ID, this.DataHandler.FileDatabase.ImageSet.FileSelection).ConfigureAwait(true);
                this.DataHandler.FileDatabase.CustomSelection.RandomSample = 0;
            }
        }
    }
}
