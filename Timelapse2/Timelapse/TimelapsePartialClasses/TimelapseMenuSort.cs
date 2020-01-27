using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Controls;
using Timelapse.Database;

namespace Timelapse
{
    // Sort Menu Callbacks
    public partial class TimelapseWindow : Window, IDisposable
    {
        // Sort sub-menu opening
        private void Sort_SubmenuOpening(object sender, RoutedEventArgs e)
        {
            this.FilePlayer_Stop(); // In case the FilePlayer is going
        }

        // Sort callback: handles all standard menu sorting items
        private async void MenuItemSort_Click(object sender, RoutedEventArgs e)
        {

            // While this should never happen, don't do anything if we don's have any data
            if (this.dataHandler == null || this.dataHandler.FileDatabase == null)
            {
                return;
            }

            MenuItem mi = (MenuItem)sender;
            SortTerm sortTerm1 = new SortTerm();
            SortTerm sortTerm2 = new SortTerm();
            switch (mi.Name)
            {
                case "MenuItemSortByDateTime":
                    sortTerm1.DataLabel = Constant.DatabaseColumn.DateTime;
                    sortTerm1.DisplayLabel = Constant.DatabaseColumn.DateTime;
                    sortTerm1.ControlType = Constant.DatabaseColumn.DateTime;
                    sortTerm1.IsAscending = Constant.BooleanValue.True;
                    break;
                case "MenuItemSortByFileName":
                    sortTerm1.DataLabel = Constant.DatabaseColumn.File;
                    sortTerm1.DisplayLabel = Constant.DatabaseColumn.File;
                    sortTerm1.ControlType = Constant.DatabaseColumn.File;
                    sortTerm1.IsAscending = Constant.BooleanValue.True;
                    break;
                case "MenuItemSortById":
                    sortTerm1.DataLabel = Constant.DatabaseColumn.ID;
                    sortTerm1.DisplayLabel = Constant.DatabaseColumn.ID;
                    sortTerm1.ControlType = Constant.DatabaseColumn.ID;
                    sortTerm1.IsAscending = Constant.BooleanValue.True;
                    break;
                default:
                    break;
            }
            // Record the sort terms in the image set
            this.dataHandler.FileDatabase.ImageSet.SetSortTerm(sortTerm1, sortTerm2);

            // Do the sort, showing feedback in the status bar and by checking the appropriate menu item
            await this.DoSortAndShowSortFeedbackAsync(true).ConfigureAwait(true);
        }

        // Custom Sort: raises a dialog letting the user specify their sort criteria
        private async void MenuItemSortCustom_Click(object sender, RoutedEventArgs e)
        {
            // Raise a dialog where user can specify the sorting criteria
            Dialog.CustomSort customSort = new Dialog.CustomSort(this.dataHandler.FileDatabase)
            {
                Owner = this
            };
            if (customSort.ShowDialog() == true)
            {
                if (this.dataHandler != null && this.dataHandler.FileDatabase != null)
                {
                    // this.dataHandler.FileDatabase.ImageSet.SortTerms = customSort.SortTerms;
                    this.dataHandler.FileDatabase.ImageSet.SetSortTerm(customSort.SortTerm1, customSort.SortTerm2);
                }
                await this.DoSortAndShowSortFeedbackAsync(true).ConfigureAwait(true);
            }
            else
            {
                // Ensure the checkmark appears next to the correct menu item 
                this.ShowSortFeedback(true);
            }
        }

        // Refresh the sort: based on the current sort criteria. 
        // Useful when, for example, the user has sorted a view, but then changed some data values where items are no longer sorted correctly.
        private async void MenuItemSortResort_Click(object sender, RoutedEventArgs e)
        {
            await this.DoSortAndShowSortFeedbackAsync(false).ConfigureAwait(true);
        }

        #region Helper functions
        // Do the sort and show feedback to the user. 
        // Only invoked by the above menu functions 
        private async Task DoSortAndShowSortFeedbackAsync(bool updateMenuChecks)
        {
            // Sync the current sort settings into the actual database. While this is done
            // on closing Timelapse, this will save it on the odd chance that Timelapse crashes before it exits.
            this.dataHandler.FileDatabase.UpdateSyncImageSetToDatabase(); // SAULXXX CHECK IF THIS IS NEEDED

            this.BusyCancelIndicator.IsBusy = true;
            // Reselect the images, which re-sorts them to the current sort criteria. 
            await this.FilesSelectAndShowAsync(this.dataHandler.ImageCache.Current.ID, this.dataHandler.FileDatabase.ImageSet.FileSelection).ConfigureAwait(true);
            this.BusyCancelIndicator.IsBusy = false;

            // sets up various status indicators in the UI
            this.ShowSortFeedback(updateMenuChecks);
        }

        // Show feedback in the UI based on the sort selection
        // Record the current sort state
        // Note: invoked by the above menu functions AND the OnFolderLoadingComplete method
        // SAULXXX WE MAY WANT TO MOVE THIS ELSEWHERE
        private void ShowSortFeedback(bool updateMenuChecks)
        {
            // Get the two sort terms
            SortTerm[] sortTerm = new SortTerm[2];

            for (int i = 0; i <= 1; i++)
            {
                sortTerm[i] = this.dataHandler.FileDatabase.ImageSet.GetSortTerm(i);
            }

            // If instructed to do so, Reset menu item checkboxes based on the current sort terms.
            if (updateMenuChecks == false)
            {
                return;
            }

            this.MenuItemSortByDateTime.IsChecked = false;
            this.MenuItemSortByFileName.IsChecked = false;
            this.MenuItemSortById.IsChecked = false;
            this.MenuItemSortCustom.IsChecked = false;

            // Determine which selection best fits the sort terms (e.g., a custom selection on just ID will be ID rather than Custom)
            if (sortTerm[0].DataLabel == Constant.DatabaseColumn.DateTime && sortTerm[0].IsAscending == Constant.BooleanValue.True && string.IsNullOrEmpty(sortTerm[1].DataLabel))
            {
                this.MenuItemSortByDateTime.IsChecked = true;
            }
            else if (sortTerm[0].DataLabel == Constant.DatabaseColumn.ID && sortTerm[0].IsAscending == Constant.BooleanValue.True && string.IsNullOrEmpty(sortTerm[1].DataLabel))
            {
                this.MenuItemSortById.IsChecked = true;
            }
            else if (sortTerm[0].DataLabel == Constant.DatabaseColumn.File && sortTerm[0].IsAscending == Constant.BooleanValue.True && string.IsNullOrEmpty(sortTerm[1].DataLabel))
            {
                this.MenuItemSortByFileName.IsChecked = true;
            }
            else
            {
                this.MenuItemSortCustom.IsChecked = true;
            }
            // Provide feedback in the status bar of what sort terms are being used
            this.StatusBar.SetSort(sortTerm[0].DataLabel, sortTerm[0].IsAscending == Constant.BooleanValue.True, sortTerm[1].DataLabel, sortTerm[1].IsAscending == Constant.BooleanValue.True);
        }
        #endregion
    }
}
