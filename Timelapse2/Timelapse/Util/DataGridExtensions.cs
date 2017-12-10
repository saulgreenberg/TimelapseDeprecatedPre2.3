using System;
using System.ComponentModel;
using System.Data;
using System.Windows.Controls;

namespace Timelapse.Util
{
    // Methods to manipulate a datagrid. 
    public static class DataGridExtensions
    {
        // Select the row with the given ID, discover its rowIndex, and then scroll that row into view
        public static void SelectAndScrollIntoView(this DataGrid dataGrid, long ID, int possibleRowIndex)
        {
            // Check to see if the ID is at the spot indicated by fileIndex, as there is a reasonable chance that this is the case
            // unless the user has resorted the data grid. If so, it minimizes going through every row.
            int rowIndex = 0;
            DataRowView drv = dataGrid.Items[possibleRowIndex] as DataRowView;
            if ((long)drv.Row.ItemArray[0] == ID)
            {
                rowIndex = possibleRowIndex;
            }
            else
            {
                // Nope. So we have to find the file index of the row containing the ID
                for (int i = 0; i < dataGrid.Items.Count; i++)
                {
                    drv = dataGrid.Items[i] as DataRowView;
                    if ((long)drv.Row.ItemArray[0] == ID)
                    {
                        rowIndex = i;
                        break;
                    }
                }
            }
            bool indexIncreasing = rowIndex > dataGrid.SelectedIndex;
            dataGrid.SelectedIndex = rowIndex;

            // try to scroll so at least 5 rows are visible beyond the selected row
            int scrollIndex;
            if (indexIncreasing)
            {
                scrollIndex = Math.Min(rowIndex + 5, dataGrid.Items.Count - 1);
            }
            else
            {
                scrollIndex = Math.Max(rowIndex - 5, 0);
            }
            dataGrid.ScrollIntoView(dataGrid.Items[scrollIndex]);
        }

        // Sort the given data grid by the given column number in ascending order
        public static void SortByColumnAscending(this DataGrid dataGrid, int columnNumber)
        {
            // Clear current sort descriptions
            dataGrid.Items.SortDescriptions.Clear();

            // Add the new sort description
            DataGridColumn firstColumn = dataGrid.Columns[columnNumber];
            ListSortDirection sortDirection = ListSortDirection.Ascending;
            dataGrid.Items.SortDescriptions.Add(new SortDescription(firstColumn.SortMemberPath, sortDirection));

            // Apply sort
            foreach (DataGridColumn column in dataGrid.Columns)
            {
                column.SortDirection = null;
            }
            firstColumn.SortDirection = sortDirection;

            // Refresh items to display sort
            dataGrid.Items.Refresh();
        }
    }
}
