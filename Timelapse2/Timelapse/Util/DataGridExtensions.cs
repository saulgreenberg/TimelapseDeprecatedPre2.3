using System;
using System.ComponentModel;
using System.Windows.Controls;

namespace Timelapse.Util
{
    // Methods to manipulate a datagrid. 
    public static class DataGridExtensions
    {
               // Select the row provded by rowIndex and scroll that row into view
        public static void SelectAndScrollIntoView(this DataGrid dataGrid, int rowIndex)
        {
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
