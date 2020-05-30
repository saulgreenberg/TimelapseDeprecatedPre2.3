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
            bool exists = this.DataHandler.FileDatabase.RowExistsWhere(FileSelectionEnum.MarkedForDeletion);
            this.MenuItemSelectFilesMarkedForDeletion.Header = "Files marked for d_eletion";
            if (!exists)
            {
                this.MenuItemSelectFilesMarkedForDeletion.Header += " (0)";
            }
            this.MenuItemSelectFilesMarkedForDeletion.IsEnabled = exists;

            // Put a checkmark next to the menu item that matches the stored selection criteria
            FileSelectionEnum selection = this.DataHandler.FileDatabase.ImageSet.FileSelection;

            this.MenuItemSelectAllFiles.IsChecked = selection == FileSelectionEnum.All;

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
            bool existsDark = this.DataHandler.FileDatabase.RowExistsWhere(FileSelectionEnum.Dark);
            bool existsOk = this.DataHandler.FileDatabase.RowExistsWhere(FileSelectionEnum.Ok);

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

            // find out which selection was selected
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
            else if (item == this.MenuItemSelectByFolder)
            {
                // MenuItemSelectByFolder and its child folders should not be activated from here, but we add this test just as a reminder
                return;
            }
            else
            {
                selection = FileSelectionEnum.All;   // Just in case
            }
            this.MenuItemSelectByFolder_ClearAllCheckmarks();

            // Treat the checked status as a radio button i.e., toggle their states so only the clicked menu item is checked.
            if (this.DataHandler.ImageCache.Current == null)
            {
                await this.FilesSelectAndShowAsync(selection).ConfigureAwait(true);
            }
            else
            {
                await this.FilesSelectAndShowAsync(this.DataHandler.ImageCache.Current.ID, selection).ConfigureAwait(true);  // Go to the first result (i.e., index 0) in the given selection set
            }
        }
        #endregion
        
        #region Select by Folder Submenu(including submenu opening)
        private void MenuItemSelectByFolder_SubmenuOpening(object sender, RoutedEventArgs e)
        {
            // We don't have to do anything if the folder menu item list has previously been populated
            if (!(sender is MenuItem menu) || menu.Items.Count != 1)
            {
                return;
            }

            // Repopulate the menu if needed. Get the folders from the database, and create a menu item representing it
            this.MenuItemSelectByFolder_ResetFolderList();
        }

        // Populate the menu. Get the folders from the database, and create a menu item representing it
        private void MenuItemSelectByFolder_ResetFolderList()
        {
            // Clear the list, excepting the first menu item all folders, which should be kept.
            MenuItem item = (MenuItem)this.MenuItemSelectByFolder.Items[0];
            this.MenuItemSelectByFolder.Items.Clear();
            this.MenuItemSelectByFolder.Items.Add(item);

            // Populate the menu . Get the folders from the database, and create a menu item representing it
            int i = 1;
            // PERFORMANCE. THIS introduces a delay when there are a large number of files. It is invoked when the user loads images for the first time. 
            // PROGRESSBAR - at the very least, show a progress bar if needed.
            List<object> folderList = this.DataHandler.FileDatabase.GetDistinctValuesInColumn(Constant.DBTables.FileData, Constant.DatabaseColumn.RelativePath);
            foreach (string header in folderList)
            {
                if (string.IsNullOrEmpty(header))
                {
                    // An empty header is actually the root folder. Since we already have an entry representng all files, we don't need it.
                    continue;
                }
                // Create a menu item for each folder
                MenuItem menuitemFolder = new MenuItem
                {
                    Header = header,
                    IsCheckable = true,
                    ToolTip = "Show only files in the folder: " + header
                };
                menuitemFolder.Click += this.MenuItemSelectFolder_Click;
                this.MenuItemSelectByFolder.Items.Insert(i++, menuitemFolder);
            }
        }

        // A specific folder was selected.
        private async void MenuItemSelectFolder_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is MenuItem mi))
            {
                return;
            }

            // If its select all folders, then just set the selection to all
            if (mi == this.MenuItemSelectAllFolders)
            {
                await this.FilesSelectAndShowAsync(this.DataHandler.ImageCache.Current.ID, FileSelectionEnum.All).ConfigureAwait(true);
                return;
            }

            // Set the search terms to the designated relative path
            this.DataHandler.FileDatabase.CustomSelection.SetRelativePathSearchTerm((string)mi.Header);
            int count = this.DataHandler.FileDatabase.CountAllFilesMatchingSelectionCondition(FileSelectionEnum.Custom);
            if (count <= 0)
            {
                Timelapse.Dialog.MessageBox messageBox = new Timelapse.Dialog.MessageBox("No files in this folder", Application.Current.MainWindow);
                messageBox.Message.Icon = MessageBoxImage.Exclamation;
                messageBox.Message.Reason = String.Format("While the folder {0} exists, no image data is associated with any files in it.", mi.Header);
                messageBox.Message.Hint = String.Format("Perhaps you removed these files and its data during this session?");
                messageBox.ShowDialog();
            }
            this.MenuItemSelectByFolder_ClearAllCheckmarks();
            this.MenuItemSelectByFolder.IsChecked = true;
            mi.IsChecked = true;
            await this.FilesSelectAndShowAsync(this.DataHandler.ImageCache.Current.ID, FileSelectionEnum.Folders).ConfigureAwait(true);  // Go to the first result (i.e., index 0) in the given selection set
        }

        private void MenuItemSelectByFolder_ClearAllCheckmarks()
        {
            this.MenuItemSelectByFolder.IsChecked = false;
            foreach (MenuItem mi in this.MenuItemSelectByFolder.Items)
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
                    this.MenuItemSelectByFolder_ClearAllCheckmarks();
                }
            }
            else
            {
                // Since we canceled the custom selection, uncheck the item (but only if another menu item is shown checked)
                bool otherMenuItemIsChecked =
                    this.MenuItemSelectAllFiles.IsChecked ||
                    this.MenuItemSelectMissingFiles.IsChecked ||
                    this.MenuItemSelectByFolder.IsChecked ||
                    this.MenuItemSelectFilesMarkedForDeletion.IsChecked;
                this.MenuItemSelectCustomSelection.IsChecked = otherMenuItemIsChecked ? false : true;
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
    }
}
