using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Database;
using Timelapse.Enums;

// Selection Menu Callbacks
namespace Timelapse
{
    // Select Menu Callbacks
    public partial class TimelapseWindow : Window, IDisposable
    {
        // Select sub-menu opening
        private void MenuItemSelect_SubmenuOpening(object sender, RoutedEventArgs e)
        {
            int count = 0;
            FilePlayer_Stop(); // In case the FilePlayer is going

            this.MenuItemSelectMissingFiles.IsEnabled = true;

            count = this.dataHandler.FileDatabase.GetFileCount(FileSelectionEnum.MarkedForDeletion);
            this.MenuItemSelectFilesMarkedForDeletion.Header = String.Format("Files marked for d_eletion [{0}]", count);
            this.MenuItemSelectFilesMarkedForDeletion.IsEnabled = count > 0;

            // Put a checkmark next to the menu item that matches the stored selection criteria
            FileSelectionEnum selection = this.dataHandler.FileDatabase.ImageSet.FileSelection;

            this.MenuItemSelectAllFiles.IsChecked = selection == FileSelectionEnum.All;

            this.MenuItemSelectDarkFiles.IsChecked = selection == FileSelectionEnum.Dark;
            this.MenuItemSelectLightFiles.IsChecked = selection == FileSelectionEnum.Light;
            this.MenuItemSelectUnknownFiles.IsChecked = selection == FileSelectionEnum.Unknown;
            this.MenuItemSelectByImageQuality.IsChecked = this.MenuItemSelectLightFiles.IsChecked || this.MenuItemSelectDarkFiles.IsChecked || this.MenuItemSelectUnknownFiles.IsChecked;

            this.MenuItemSelectMissingFiles.IsChecked = selection == FileSelectionEnum.Missing;
            this.MenuItemSelectFilesMarkedForDeletion.IsChecked = selection == FileSelectionEnum.MarkedForDeletion;
            this.MenuItemSelectCustomSelection.IsChecked = selection == FileSelectionEnum.Custom;
        }

        private void MenuItemSelectImageQuality_SubmenuOpening(object sender, RoutedEventArgs e)
        {
            Dictionary<FileSelectionEnum, int> counts = this.dataHandler.FileDatabase.GetFileCountsInAllFiles();
            int count;

            // Enable only the menu items that can select at least one potential image 
            count = counts[FileSelectionEnum.Light];
            this.MenuItemSelectLightFiles.IsEnabled = count > 0;
            this.MenuItemSelectLightFiles.Header = String.Format("_Light files [{0}]", count);

            count = counts[FileSelectionEnum.Dark];
            this.MenuItemSelectDarkFiles.Header = String.Format("_Dark files [{0}]", count);
            this.MenuItemSelectDarkFiles.IsEnabled = count > 0;

            count = counts[FileSelectionEnum.Unknown];
            this.MenuItemSelectUnknownFiles.Header = String.Format("_Unknown files [{0}]", count);
            this.MenuItemSelectUnknownFiles.IsEnabled = count > 0;
        }

        private void MenuItemSelectByFolder_SubmenuOpening(object sender, RoutedEventArgs e)
        {
            // We don't have to do anything if the folder menu item list has previously been populated
            if (!(sender is MenuItem menu) || menu.Items.Count != 1)
            {
                return;
            }

            // Repopulate the menu if needed. Get the folders from the database, and create a menu item representing it
            MenuItemSelectByFolder_ResetFolderList();
        }
        // Populate the menu. Get the folders from the database, and create a menu item representing it
        private void MenuItemSelectByFolder_ResetFolderList()
        {
            // Clear the list, excepting the first menu item all folders, which should be kept.
            MenuItem item = (MenuItem)MenuItemSelectByFolder.Items[0];
            MenuItemSelectByFolder.Items.Clear();
            MenuItemSelectByFolder.Items.Add(item);

            // Populate the menu . Get the folders from the database, and create a menu item representing it
            int i = 1;
            // PERFORMANCE. THIS ADDS ABOUT .250 ms. Since its invoked when loading images for the first time, it may be unneeded if the user doesn't use that menu
            // BUT TO FIX THAT, WE WOULD HAVE TO DISTINGUISH BETWEEN THE FIRST TIME VS subsequent times files were added.
            List<object> folderList = this.dataHandler.FileDatabase.GetDistinctValuesInColumn(Constant.DatabaseTable.FileData, Constant.DatabaseColumn.RelativePath);
            foreach (string header in folderList)
            {
                if (header == String.Empty)
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
                menuitemFolder.Click += MenuItemSelectFolder_Click;
                MenuItemSelectByFolder.Items.Insert(i++, menuitemFolder);
            }
        }

        // A specific folder was selected.
        private void MenuItemSelectFolder_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is MenuItem mi))
            {
                return;
            }

            // If its select all folders, then just set the selection to all
            if (mi == this.MenuItemSelectAllFolders)
            {
                this.FilesSelectAndShow(this.dataHandler.ImageCache.Current.ID, FileSelectionEnum.All);
                return;
            }

            // Set the search terms to the designated relative path
            this.dataHandler.FileDatabase.CustomSelection.SetRelativePathSearchTerm((string)mi.Header);
            int count = this.dataHandler.FileDatabase.GetFileCount(FileSelectionEnum.Custom);
            if (count <= 0)
            {
                Timelapse.Dialog.MessageBox messageBox = new Timelapse.Dialog.MessageBox("No files in this folder", Application.Current.MainWindow);
                messageBox.Message.Icon = MessageBoxImage.Exclamation;
                messageBox.Message.Reason = String.Format("While the folder {0} exists, no image data is associated with any files in it.", mi.Header);
                messageBox.Message.Hint = String.Format("Perhaps you removed these files and its data during this session?", mi.Header);
                messageBox.ShowDialog();
 //               return;
            }
            MenuItemSelectByFolder_ClearAllCheckmarks();
            this.MenuItemSelectByFolder.IsChecked = true;
            mi.IsChecked = true;
            this.FilesSelectAndShow(this.dataHandler.ImageCache.Current.ID, FileSelectionEnum.Folders);  // Go to the first result (i.e., index 0) in the given selection set
        }

        private void MenuItemSelectByFolder_ClearAllCheckmarks()
        {
            this.MenuItemSelectByFolder.IsChecked = false;
            foreach (MenuItem mi in MenuItemSelectByFolder.Items)
            {
                mi.IsChecked = false;
            }
        }

        // Select callback: handles all standard menu selection items
        private void MenuItemSelectFiles_Click(object sender, RoutedEventArgs e)
        {
            MenuItem item = (MenuItem)sender;
            FileSelectionEnum selection;

            // find out which selection was selected
            if (item == this.MenuItemSelectAllFiles)
            {
                selection = FileSelectionEnum.All;
            }
            else if (item == this.MenuItemSelectLightFiles)
            {
                selection = FileSelectionEnum.Light;
            }
            else if (item == this.MenuItemSelectUnknownFiles)
            {
                selection = FileSelectionEnum.Unknown;
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
            if (this.dataHandler.ImageCache.Current == null)
            {
                FilesSelectAndShow(selection, true);
            }
            else
            { 
                this.FilesSelectAndShow(this.dataHandler.ImageCache.Current.ID, selection);  // Go to the first result (i.e., index 0) in the given selection set
            }
        }

        // Custom Selection: raises a dialog letting the user specify their selection criteria
        private void MenuItemSelectCustomSelection_Click(object sender, RoutedEventArgs e)
        {
            // the first time the custom selection dialog is launched update the DateTime and UtcOffset search terms to the time of the current image
            SearchTerm firstDateTimeSearchTerm = this.dataHandler.FileDatabase.CustomSelection.SearchTerms.FirstOrDefault(searchTerm => searchTerm.DataLabel == Constant.DatabaseColumn.DateTime);
            if (firstDateTimeSearchTerm.GetDateTime() == Constant.ControlDefault.DateTimeValue.DateTime)
            {
                DateTimeOffset defaultDate = this.dataHandler.ImageCache.Current.DateTimeIncorporatingOffset;
                this.dataHandler.FileDatabase.CustomSelection.SetDateTimesAndOffset(defaultDate);
            }

            // show the dialog and process the resuls
            Dialog.CustomSelection customSelection = new Dialog.CustomSelection(this.dataHandler.FileDatabase, this.DataEntryControls, this, this.IsUTCOffsetControlHidden())
            {
                Owner = this
            };
            bool? changeToCustomSelection = customSelection.ShowDialog();
            // Set the selection to show all images and a valid image
            if (changeToCustomSelection == true)
            {
                this.FilesSelectAndShow(this.dataHandler.ImageCache.Current.ID, FileSelectionEnum.Custom);
                if (this.MenuItemSelectCustomSelection.IsChecked || this.MenuItemSelectCustomSelection.IsChecked)
                {
                    MenuItemSelectByFolder_ClearAllCheckmarks();
                }
            }
            else
            {
                // Since we canceled the custom selection, uncheck the item (but only if another menu item is shown checked)
                bool otherMenuItemIsChecked =
                    this.MenuItemSelectAllFiles.IsChecked ||
                    this.MenuItemSelectUnknownFiles.IsChecked ||
                    this.MenuItemSelectDarkFiles.IsChecked ||
                    this.MenuItemSelectLightFiles.IsChecked ||
                    this.MenuItemSelectMissingFiles.IsChecked ||
                    this.MenuItemSelectByFolder.IsChecked ||
                    this.MenuItemSelectFilesMarkedForDeletion.IsChecked;
                this.MenuItemSelectCustomSelection.IsChecked = otherMenuItemIsChecked ? false : true;
            }
        }

        // Refresh the selection: based on the current select criteria. 
        // Useful when, for example, the user has selected a view, but then changed some data values where items no longer match the current selection.
        private void MenuItemSelectReselect_Click(object sender, RoutedEventArgs e)
        {
            // Reselect the images, which re-sorts them to the current sort criteria. 
            this.FilesSelectAndShow(this.dataHandler.ImageCache.Current.ID, this.dataHandler.FileDatabase.ImageSet.FileSelection, true);
        }

        #region Depracated
        // There really is no reason to have a special menu item for this, as one can see the counts
        // when selecting. However, we keep the dialog box around as we still use that
        // for displaying the total numbers after a manual dark threshold operation is done.
        // Show file counts: how many images were loaded, types in categories, etc.
        // public void MenuItemImageCounts_Click(object sender, RoutedEventArgs e)
        // {
        //    this.MaybeFileShowCountsDialog(false, this);
        // }
        #endregion
    }
}
