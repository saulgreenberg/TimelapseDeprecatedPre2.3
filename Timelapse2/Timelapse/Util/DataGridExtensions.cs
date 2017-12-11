using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Timelapse.Util
{
    // Methods to manipulate a datagrid. 
    public static class DataGridExtensions
    {
        // Select the row with the given ID, discover its rowIndex, and then scroll that row into view
        //public static void SelectAndScrollIntoView(this DataGrid dataGrid, long id, int possibleRowIndex)
        //{
        //    // Check to see if the ID is at the spot indicated by fileIndex, as there is a reasonable chance that this is the case
        //    // unless the user has resorted the data grid. If so, it minimizes going through every row.
        //    int rowIndex = 0;
        //    DataRowView drv = dataGrid.Items[possibleRowIndex] as DataRowView;
        //    if ((long)drv.Row.ItemArray[0] == id)
        //    {
        //        rowIndex = possibleRowIndex;
        //    }
        //    else
        //    {
        //        // Nope. So we have to find the file index of the row containing the ID
        //        for (int i = 0; i < dataGrid.Items.Count; i++)
        //        {
        //            drv = dataGrid.Items[i] as DataRowView;
        //            if ((long)drv.Row.ItemArray[0] == id)
        //            {
        //                rowIndex = i;
        //                break;
        //            }
        //        }
        //    }
        //    bool indexIncreasing = rowIndex > dataGrid.SelectedIndex;
        //    //dataGrid.SelectedIndex = rowIndex;  // This used to be for single selection. Replaced by below for multiple selections.
        //    List<int> selectedIndexes = new List<int>();
        //    selectedIndexes.Add(rowIndex);
        //    //selectedIndexes.Add(rowIndex+1);

        //    SelectRowByIndexes(dataGrid, selectedIndexes);

        //    // try to scroll so at least 5 rows are visible beyond the selected row
        //    int scrollIndex;
        //    if (indexIncreasing)
        //    {
        //        scrollIndex = Math.Min(rowIndex + 5, dataGrid.Items.Count - 1);
        //    }
        //    else
        //    {
        //        scrollIndex = Math.Max(rowIndex - 5, 0);
        //    }
        //    dataGrid.ScrollIntoView(dataGrid.Items[scrollIndex]);
        //}

        // Select the rows with the given IDs, discover its rowIndexes, and then scroll the first row into view
        public static void SelectAndScrollIntoView(this DataGrid dataGrid, List<long> ids, int possibleRowIndex)
        {
            if (ids.Count.Equals(0))
            {
                dataGrid.UnselectAll();
                return;
            }

            // Check to see if the ID is at the spot indicated by fileIndex, as there is a reasonable chance that this is the case
            // unless the user has resorted the data grid. If so, it minimizes going through every row.
            DataRowView drv;
            List<int> rowIndexes = new List<int>();

            foreach (long id in ids)
            {
                //drv = dataGrid.Items[possibleRowIndex] as DataRowView;
                //if ((long)drv.Row.ItemArray[0] == ids[0])
                //{
                //    rowIndex = possibleRowIndex;
                //}
                //else
                //{
                // Nope. So we have to find the file index of the row containing the ID
                for (int i = 0; i < dataGrid.Items.Count; i++)
                {
                    drv = dataGrid.Items[i] as DataRowView;
                    if ((long)drv.Row.ItemArray[0] == id)
                    {
                        rowIndexes.Add(i);
                        break;
                    }
                }
                //}
            }
            int firstRowIndex = rowIndexes[0];
            bool indexIncreasing = firstRowIndex > dataGrid.SelectedIndex;
            //dataGrid.SelectedIndex = rowIndex;  // This used to be for single selection. Replaced by below for multiple selections.
            //List<int> selectedIndexes = new List<int>();
            //selectedIndexes.Add(rowIndex);
            //selectedIndexes.Add(rowIndex+1);

            SelectRowByIndexes(dataGrid, rowIndexes);

            // try to scroll so at least 5 rows are visible beyond the selected row
            int scrollIndex;
            if (indexIncreasing)
            {
                scrollIndex = Math.Min(firstRowIndex + 5, dataGrid.Items.Count - 1);
            }
            else
            {
                scrollIndex = Math.Max(firstRowIndex - 5, 0);
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

        #region Code to enable multiple selections. Modified from https://blog.magnusmontin.net/2013/11/08/how-to-programmatically-select-and-focus-a-row-or-cell-in-a-datagrid-in-wpf/

        // Select the rows indicated by the (perhaps multple) row indexes
        private static void SelectRowByIndexes(DataGrid dataGrid, List<int> rowIndexes)
        {
            if (!dataGrid.SelectionUnit.Equals(DataGridSelectionUnit.FullRow) || !dataGrid.SelectionMode.Equals(DataGridSelectionMode.Extended))
            {
                // This should never be triggered
                throw new ArgumentException("DataGrid issue: SelectionUnit must be FullRow, and  Selection Mode must be  Extended");
            }

            // Clear all selections
            dataGrid.SelectedItems.Clear();

            // If 0 items are selected, there is nothing more to do
            if (rowIndexes.Count.Equals(0) || rowIndexes.Count > dataGrid.Items.Count)
            {
                return;
            }

            // if there is only one item, we can just set it directly
            if (rowIndexes.Count.Equals(1))
            {
                int rowIndex = rowIndexes[0];
                if (rowIndex < 0 || rowIndex > (dataGrid.Items.Count - 1))
                {
                    // This shouldn't happen, but...
                    throw new ArgumentException(string.Format("{0} is an invalid row index.", rowIndex));
                }
                dataGrid.SelectedIndex = rowIndex;  // This used to be for single selection. 
                return;
            }

            // Multiple indexes are selected
            foreach (int rowIndex in rowIndexes)
            {
                if (rowIndex < 0 || rowIndex > (dataGrid.Items.Count - 1))
                { 
                    // This shouldn't happen, but...
                    throw new ArgumentException(string.Format("{0} is an invalid row index.", rowIndex));
                }

                object item = dataGrid.Items[rowIndex]; 
                dataGrid.SelectedItems.Add(item);

                DataGridRow row = dataGrid.ItemContainerGenerator.ContainerFromIndex(rowIndex) as DataGridRow;
                if (row == null)
                {
                    // dataGrid.ScrollIntoView(item);  CHANGE ABOVE TO SCROLL THE FIRST ITEM
                    row = dataGrid.ItemContainerGenerator.ContainerFromIndex(rowIndex) as DataGridRow;
                }
                if (row != null)
                {
                    DataGridCell cell = GetCell(dataGrid, row, 0);
                    if (cell != null)
                        cell.Focus();
                }
            }
        }

        // Get a cell from the DataGrid
        private static DataGridCell GetCell(DataGrid dataGrid, DataGridRow rowContainer, int column)
        {
            if (rowContainer != null)
            {
                DataGridCellsPresenter presenter = FindVisualChild<DataGridCellsPresenter>(rowContainer);
                if (presenter == null)
                {
                    /* if the row has been virtualized away, call its ApplyTemplate() method 
                     * to build its visual tree in order for the DataGridCellsPresenter
                     * and the DataGridCells to be created */
                    rowContainer.ApplyTemplate();
                    presenter = FindVisualChild<DataGridCellsPresenter>(rowContainer);
                }
                if (presenter != null)
                {
                    DataGridCell cell = presenter.ItemContainerGenerator.ContainerFromIndex(column) as DataGridCell;
                    if (cell == null)
                    {
                        /* bring the column into view
                         * in case it has been virtualized away */
                        dataGrid.ScrollIntoView(rowContainer, dataGrid.Columns[column]);
                        cell = presenter.ItemContainerGenerator.ContainerFromIndex(column) as DataGridCell;
                    }
                    return cell;
                }
            }
            return null;
        }

        // Enumerate the members of a visual tree, in order to programmatic access objects in the visual tree.
        private static T FindVisualChild<T>(DependencyObject obj) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                if (child != null && child is T)
                    return (T)child;
                else
                {
                    T childOfChild = FindVisualChild<T>(child);
                    if (childOfChild != null)
                        return childOfChild;
                }
            }
            return null;
        }
        #endregion
    }
}
