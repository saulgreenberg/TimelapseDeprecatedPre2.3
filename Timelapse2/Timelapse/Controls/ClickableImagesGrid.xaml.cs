using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Timelapse.Database;
using Timelapse.Enums;
using Timelapse.EventArguments;
using RowColumn = System.Drawing.Point;

namespace Timelapse.Controls
{
    /// <summary>
    /// Interaction logic for ClickableImagesGrid.xaml
    /// </summary>
    public partial class ClickableImagesGrid : UserControl
    {
        #region Public properties

        // DataEntryControls needs to be set externally
        public DataEntryControls DataEntryControls { private get; set; }
        
        // FileTable needs to be set externally
        public FileTable FileTable { private get; set; }

        // FileTableStartIndex needs to be set externally
        public int FileTableStartIndex { private get; set; }

        // FoldePath needs to be set externally
        // The root folder containing the template
        public string FolderPath { private get; set; }

        // The number of images that currently exist in a row
        public int ImagesInRow
        {
            get
            {
                return this.Grid.ColumnDefinitions.Count;
            }
        }

        // The number of rows that currently exist in the ClickableImagesGrid
        public int RowsInGrid
        {
            get
            {
                return this.Grid.RowDefinitions.Count;
            }
        }
        #endregion

        #region Private variables

        private List<ClickableImage> clickableImagesList;

        // Cache copies of the images we display plus associated information
        // This is done both to save existing image state and so we don't repeatedly rebuild that information
        private int cachedImagePathsStartIndex = -1;
        private List<ClickableImage> cachedImageList;

        // Track states between mouse down / move and up 
        private RowColumn cellChosenOnMouseDown;
        private bool modifierKeyPressedOnMouseDown = false;
        private RowColumn cellWithLastMouseOver = new RowColumn(-1, -1);
        #endregion

        #region Constructor
        public ClickableImagesGrid()
        {
            this.InitializeComponent();
            this.FileTableStartIndex = 0;
        }
        #endregion

        #region Public Refresh
        // Rebuild the grid, based on 
        // - fitting the image of a desired width into as many cells of the same size that can fit within the grid
        // - retaining information about images previously shown on this grid, which importantly includes its selection status.
        //   this means users can do some selections, then change the zoom level.
        //   Note that when a user navigates, previously selected images that no longer appear in the grid will be unselected
        public bool Refresh(double desiredWidth, Size availableSize, bool forceUpdate, int state)
        {
            // If nothing is loaded, or if there is no desiredWidth, then there is nothing to refresh
            if (FileTable == null || FileTable.Count() == 0 || desiredWidth == 0)
            {
                return false;
            }
            Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;

            // Clear the Grid so we can start afresh
            this.Grid.RowDefinitions.Clear();
            this.Grid.ColumnDefinitions.Clear();
            this.Grid.Children.Clear();

            // Calculated the number of columns that can fit into the available space,
            
            int columnCount = Convert.ToInt32(Math.Floor(availableSize.Width / desiredWidth));
            if (columnCount == 0)
            {
                // abort as there is not enough width available to display even a single image
                Mouse.OverrideCursor = null;
                return false;
            }

            // Add as many columns of the desired width as can fit into the grid's available space
            for (int thisColumn = 0; thisColumn < columnCount; thisColumn++)
            { 
                this.Grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition() { Width = new GridLength(desiredWidth, GridUnitType.Pixel) });
            }

            // Add images to successive columns, where we create an initial row and then additional row if needed after the columns are full
            // As we do this, check to see:
            // - if those images are in the image cache (as these will be in the same order, we can check as we add images and move through the cache) 
            // - if we have run out of images to add 
            // - the new row does't fit the available space
            int rowNumber = 0;
            int fileTableIndex = this.FileTableStartIndex;
            
            Double imageHeight = 0;
            Double maxImageHeight = 0;
            Double combinedRowHeight = 0;
            ClickableImage ci;
            this.clickableImagesList = new List<ClickableImage>();
            List<ClickableImage> clickableImagesRow = new List<ClickableImage>();
            
            // If forceUpdate is true, remove the cache so that images have to be regenerated
            if (forceUpdate && this.cachedImageList != null)
            {
                this.cachedImageList.Clear();
            }

            while (true)
            {
                // For each row: 
                // Collect potential images, while tracking the maximum height of each image, which is used to determine the row height needed
                for (int columnIndex = 0; columnIndex < columnCount && fileTableIndex < FileTable.Count(); columnIndex++)
                {
                    // Process each image. As we do this, 
                    // - check the cache to see if the image is already there, 
                    // - ensure that an image is available
                    string path = Path.Combine(this.FileTable[fileTableIndex].RelativePath, this.FileTable[fileTableIndex].FileName);
                    bool inCache = false;
                    int cachedImageListIndex = 0;
                    // Iterate through the cache to see if the image is in it
                    while (this.cachedImageList != null && cachedImageListIndex < this.cachedImageList.Count)
                    {
                        if (path == this.cachedImageList[cachedImageListIndex].Path)
                        {
                            // The image is in the cache.
                            ci = this.cachedImageList[cachedImageListIndex];
                            if (ci.DesiredRenderWidth < desiredWidth && ci.DesiredRenderSize.X < desiredWidth)
                            {
                                // Re-render the cached image, as its smaller than the resolution width 
                                imageHeight = ci.Rerender(desiredWidth, state, cachedImageListIndex);
                            }
                            else
                            {
                                // Reuse the cached image, as its at least of the same or greater resolution width. 
                                ci.Image.Width = desiredWidth; // Adjust the image width to the new size
                                imageHeight = ci.DesiredRenderSize.Y;
                            }
                            ci.FileTableIndex = fileTableIndex; // Update the filetableindex just in case
                            ci.TextFontSize = desiredWidth / 20;
                            ci.AdjustMargin(state);
                            clickableImagesRow.Add(ci);
                            inCache = true;
                            cachedImageListIndex++;
                            if (maxImageHeight < imageHeight)
                            {
                                maxImageHeight = imageHeight;
                            }
                            break;
                        }
                        cachedImageListIndex++;
                        inCache = false;
                    }
                    if (inCache == false)
                    {
                        // The image is not in the cache. Create a new clickable image
                        ci = new ClickableImage(desiredWidth)
                        {
                            RootFolder = this.FolderPath,
                            ImageRow = this.FileTable[fileTableIndex],
                            DesiredRenderWidth = desiredWidth
                        };
                        imageHeight = ci.Rerender(desiredWidth, state, fileTableIndex);
                        ci.FileTableIndex = fileTableIndex; // Set the filetableindex so we can retrieve it later
                        ci.TextFontSize = desiredWidth / 20;
                        ci.AdjustMargin(state);
                        clickableImagesRow.Add(ci);
                        if (maxImageHeight < imageHeight)
                        {
                            maxImageHeight = imageHeight;
                        }
                    }
                    fileTableIndex++;
                } // end Process and Retrieve potential images for each row, checking height as we go

                // We've reached the end of the row.
                // Check if there is enough space to add a new row
                if (combinedRowHeight + maxImageHeight > availableSize.Height)
                {
                    // Don't bother adding a new row, as there is not enough room
                    // Even so, we may as well add these images to the cache as they have been processed
                    foreach (ClickableImage clickableImage in clickableImagesRow)
                    {
                        this.clickableImagesList.Add(clickableImage);
                    }
                    clickableImagesRow.Clear();
                    break;
                }
                else
                {
                    // Create a new row
                    this.Grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(maxImageHeight, GridUnitType.Pixel) });
                    int columnNumber = 0;
                    foreach (ClickableImage clickableImage in clickableImagesRow)
                    {
                        this.clickableImagesList.Add(clickableImage);
                        Grid.SetRow(clickableImage, rowNumber);
                        Grid.SetColumn(clickableImage, columnNumber);
                        this.Grid.Children.Add(clickableImage);
                        columnNumber++;
                    }
                    rowNumber++;
                    combinedRowHeight += maxImageHeight;
                    // Cleanup
                    clickableImagesRow.Clear();
                    maxImageHeight = 0;
                }
                // If we've gone beyond the last image in the image set, then we are done.
                if (fileTableIndex >= FileTable.Count())
                {
                    break;
                }
            }

            // Reset the selection to the first image in the grid if the first displayable image isn't the same
            if (this.cachedImagePathsStartIndex != this.FileTableStartIndex)
            {
                this.SelectInitialCellOnly();
            }

            // save cache parameters if the cache has changed 
            if (this.cachedImageList == null || this.cachedImageList.Count < this.clickableImagesList.Count || this.cachedImagePathsStartIndex != this.FileTableStartIndex)
            {
                this.cachedImageList = this.clickableImagesList;
                this.cachedImagePathsStartIndex = this.FileTableStartIndex;
            }
            // It doesn't appear that this is needed here, as its invoked elsewhere. 
            // Currently commented out to avoid redundant updating of the controls
            // this.EnableOrDisableControlsAsNeeded();
            Mouse.OverrideCursor = null;

            // Return false if we can't even fit in a single row
            return (Grid.RowDefinitions.Count < 1) ? false : true;
        }

        // Invalidate the clickable images cache.
        // Used to force a redraw of images, e.g., such as when an image is deleted (but not its data) so that the missing image is shown in its place
        public void InvalidateCache()
        {
            this.cachedImageList = null;
        }
        #endregion

        #region Mouse callbacks
        // Mouse left down. Select images
        // The selection behaviours depend upon whether the CTL or SHIFT modifier key is pressed, or whether this is a double click 
        private void Grid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ClickableImage ci;
            this.cellChosenOnMouseDown = this.GetCellFromPoint(Mouse.GetPosition(Grid));
            RowColumn currentCell = GetCellFromPoint(Mouse.GetPosition(Grid));
            this.cellWithLastMouseOver = currentCell;

            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                // CTL mouse down: change that cell (and only that cell's) state
                this.modifierKeyPressedOnMouseDown = true;
                if (Equals(this.cellChosenOnMouseDown, currentCell))
                {
                    ci = GetClickableImageFromCell(currentCell);
                    if (ci != null)
                    {
                        ci.IsSelected = !ci.IsSelected;
                    }
                }
            }
            else if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
            {
                // SHIFT mouse down: extend the selection (if any) to this point.
                this.modifierKeyPressedOnMouseDown = true;
                this.SelectExtendSelectionFrom(currentCell);
            }
            else
            {
                // Left mouse down, no modifiers keys. 
                // Select only the current cell, unselecting others.
                ci = this.GetClickableImageFromCell(currentCell);
                if (ci != null)
                {
                    this.SelectNone();
                    ci.IsSelected = true; 
                }
            }

            // If this is a double click, raise the Double click event, e.g., so that the calling app can navigate to that image.
            if (e.ClickCount == 2)
            {
                ci = GetClickableImageFromCell(currentCell);
                ClickableImagesGridEventArgs eventArgs = new ClickableImagesGridEventArgs(this, ci?.ImageRow);
                this.OnDoubleClick(eventArgs);
                e.Handled = true; // Stops the double click from generating a marker on the MarkableImageCanvas
            }
            this.EnableOrDisableControlsAsNeeded();
            ClickableImagesGridEventArgs selectionEventArgs = new ClickableImagesGridEventArgs(this, null);
            this.OnSelectionChanged(selectionEventArgs);
        }

        // If a mouse-left drag movement, select all cells between the starting and current cell
        private void Grid_MouseMove(object sender, MouseEventArgs e)
        {
            // We only pay attention to mouse-left moves without any modifier keys pressed (i.e., a drag action).
            if (e.LeftButton != MouseButtonState.Pressed || this.modifierKeyPressedOnMouseDown)
            {
                return;
            }

            // Get the cell under the mouse pointer
            RowColumn currentCell = GetCellFromPoint(Mouse.GetPosition(Grid));

            // Ignore if the cell has already been handled in the last mouse down or move event,
            if (Equals(currentCell, this.cellWithLastMouseOver))
            {
                return;
            }

            // Select from the initial cell to the current cell
            this.SelectFromInitialCellTo(currentCell);
            this.cellWithLastMouseOver = currentCell;
            this.EnableOrDisableControlsAsNeeded();
        }

        // On the mouse up, select all cells contained by its bounding box. Note that this is needed
        // as well as the mouse move version, as otherwise a down/up on the same spot won't select the cell.
        private void Grid_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            this.cellWithLastMouseOver.X = -1;
            this.cellWithLastMouseOver.Y = -1;
            if (this.modifierKeyPressedOnMouseDown)
            {
                this.modifierKeyPressedOnMouseDown = false;
                return;
            }
        }
        #endregion

        #region Grid Selection 
        // Unselect all elements in the grid
        // Select the first (and only the first) image in the current grid
        public void SelectInitialCellOnly()
        {
            this.SelectNone(); // Clear the selections
            if (this.clickableImagesList.Count() > 0)
            {
                ClickableImage ci = this.clickableImagesList[0];
                ci.IsSelected = true;
            }
            ClickableImagesGridEventArgs eventArgs = new ClickableImagesGridEventArgs(this, null);
            this.OnSelectionChanged(eventArgs);
        }

        private void SelectNone()
        {
            // Unselect all clickable images
            foreach (ClickableImage ci in this.clickableImagesList)
            {
                ci.IsSelected = false;
            }
        }

        // Select all cells between the initial and currently selected cell
        private void SelectFromInitialCellTo(RowColumn currentCell)
        {
            // If the first selected cell doesn't exist, make it the same as the currently selected cell
            if (this.cellChosenOnMouseDown == null)
            {
                this.cellChosenOnMouseDown = currentCell;
            }

            this.SelectNone(); // Clear the selections

            // Determine which cell is 
            DetermineTopLeftBottomRightCells(cellChosenOnMouseDown, currentCell, out RowColumn startCell, out RowColumn endCell);

            // Select the cells defined by the cells running from the topLeft cell to the BottomRight cell
            RowColumn indexCell = startCell;

            ClickableImage ci;
            while (true)
            {
                ci = GetClickableImageFromCell(indexCell);
                // If the cell doesn't contain a ClickableImage, then we are at the end.
                if (ci == null)
                {
                    break;
                }
                ci.IsSelected = true;

                // If there is no next cell, then we are at the end.
                if (GridGetNextCell(indexCell, endCell, out RowColumn nextCell) == false)
                {
                   break;
                }
                indexCell = nextCell;
            }
            ClickableImagesGridEventArgs eventArgs = new ClickableImagesGridEventArgs(this, null);
            this.OnSelectionChanged(eventArgs);
        }

        // Select all cells between the initial and currently selected cell
        private void SelectFromTo(RowColumn cell1, RowColumn cell2)
        {
            DetermineTopLeftBottomRightCells(cell1, cell2, out RowColumn startCell, out RowColumn endCell);

            // Select the cells defined by the cells running from the topLeft cell to the BottomRight cell
            RowColumn indexCell = startCell;

            ClickableImage ci;
            while (true)
            {
                ci = GetClickableImageFromCell(indexCell);
                // This shouldn't happen, but ensure that the cell contains a ClickableImage.
                if (ci == null)
                {
                    break;
                }
                ci.IsSelected = true;

                // If there is no next cell, then we are at the end.
                if (GridGetNextCell(indexCell, endCell, out RowColumn nextCell) == false)
                {
                    break;
                }
                indexCell = nextCell;
            }
            ClickableImagesGridEventArgs eventArgs = new ClickableImagesGridEventArgs(this, null);
            this.OnSelectionChanged(eventArgs);
        }

        private void SelectExtendSelectionFrom(RowColumn currentCell)
        {
            // If there is no previous cell, then we are at the end.
            if (GridGetPreviousSelectedCell(currentCell, out RowColumn previousCell) == true)
            { 
                SelectFromTo(previousCell, currentCell);
            }
            else if (GridGetNextSelectedCell(currentCell, out RowColumn nextCell) == true)
            { 
                SelectFromTo(currentCell, nextCell);
            }
        }
      
        // Get the Selected times as a list of file table indexes to the current displayed selection of files (note these are not the IDs)
        public List<int> GetSelected()
        {
            List<int> selected = new List<int>();
            if (this.clickableImagesList == null)
            {
                return selected;
            }
            foreach (ClickableImage ci in this.clickableImagesList)
            {
                if (ci.IsSelected)
                {
                    int fileIndex = ci.FileTableIndex;
                    selected.Add(fileIndex);
                }
            }
            return selected;
        }

        public int SelectedCount()
        {
            return GetSelected().Count;
        }
        #endregion

        #region Cell Navigation methods
        private bool GridGetNextSelectedCell(RowColumn cell, out RowColumn nextCell)
        {
            RowColumn lastCell = new RowColumn(this.Grid.RowDefinitions.Count - 1, this.Grid.ColumnDefinitions.Count - 1);
            ClickableImage ci;

            while (GridGetNextCell(cell, lastCell, out nextCell))
            {
                ci = GetClickableImageFromCell(nextCell);

                // If there is no cell, we've reached the end, 
                if (ci == null) 
                {
                    return false;
                }
                // We've found a selected cell
                if (ci.IsSelected)
                {
                    return true;
                }
                cell = nextCell;
            }
            return false;
        }

        private bool GridGetPreviousSelectedCell(RowColumn cell, out RowColumn previousCell)
        {
            RowColumn lastCell = new RowColumn(0, 0);
            ClickableImage ci;

            while (GridGetPreviousCell(cell, lastCell, out previousCell))
            {
                ci = GetClickableImageFromCell(previousCell);

                // If there is no cell, terminate as we've reached the beginning
                if (ci == null)
                {
                    return false;
                }
                // We've found a selected cell
                if (ci.IsSelected)
                {
                    return true;
                }
                cell = previousCell;
            }
            return false;
        }
        // Get the next cell and return true
        // Return false if we hit the lastCell or the end of the grid.
        private bool GridGetNextCell(RowColumn cell, RowColumn lastCell, out RowColumn nextCell)
        {
            nextCell = new RowColumn(cell.X, cell.Y);
            // Try to go to the next column or wrap around to the next row if we are at the end of the row
            nextCell.Y++;
            if (nextCell.Y == this.Grid.ColumnDefinitions.Count())
            {
                // start a new row
                nextCell.Y = 0;
                nextCell.X++;
            }

            if (nextCell.X > lastCell.X || (nextCell.X == lastCell.X && nextCell.Y > lastCell.Y))
            {
                // We just went beyond the last cell, so we've reached the end.
                return false;
            }
            return true;
        }

        // Get the previous cell. Return true if we can, otherwise false.
        private bool GridGetPreviousCell(RowColumn cell, RowColumn firstCell, out RowColumn previousCell)
        {
            previousCell = new RowColumn(cell.X, cell.Y);
            // Try to go to the previous column or wrap around to the previous row if we are at the beginning of the row
            previousCell.Y--;
            if (previousCell.Y < 0)
            {
                // go to the previous row
                previousCell.Y = this.Grid.ColumnDefinitions.Count() - 1;
                previousCell.X--;
            }

            if (previousCell.X < firstCell.X || (previousCell.X == firstCell.X && previousCell.Y < firstCell.Y))
            {
                // We just went beyond the last cell, so we've reached the end.
                return false;
            }
            return true;
        }
        #endregion
  
        #region Cell Calculation methods

        // Given two cells, determine which one is the start vs the end cell
        private static void DetermineTopLeftBottomRightCells(RowColumn cell1, RowColumn cell2, out RowColumn startCell, out RowColumn endCell)
        {
            startCell = new RowColumn();
            endCell = new RowColumn();

            startCell = (cell1.X < cell2.X || (cell1.X == cell2.X && cell1.Y <= cell2.Y)) ? cell1 : cell2;
            endCell = Equals(startCell, cell1) ? cell2 : cell1;
        }
    
        // Given a mouse point, return a point that indicates the (row, column) of the grid that the mouse point is over
        private RowColumn GetCellFromPoint(Point mousePoint)
        {
            RowColumn cell = new RowColumn(0, 0);
            double accumulatedHeight = 0.0;
            double accumulatedWidth = 0.0;

            // Calculate which row the mouse was over
            foreach (var rowDefinition in Grid.RowDefinitions)
            {
                accumulatedHeight += rowDefinition.ActualHeight;
                if (accumulatedHeight >= mousePoint.Y)
                {
                    break;
                }
                cell.X++;
            }

            // Calculate which column the mouse was over
            foreach (var columnDefinition in Grid.ColumnDefinitions)
            {
                accumulatedWidth += columnDefinition.ActualWidth;
                if (accumulatedWidth >= mousePoint.X)
                { 
                    break;
                }
                cell.Y++;
            }
            return cell;
        }

        // Get the clickable image held by the Grid's specified row,column coordinates 
        private ClickableImage GetClickableImageFromCell(RowColumn cell)
        {
            return Grid.Children.Cast<ClickableImage>().FirstOrDefault(exp => Grid.GetColumn(exp) == cell.Y && Grid.GetRow(exp) == cell.X);
        }
        #endregion

        #region Enabling controls
        // Update the data entry controls to match the current selection(s)
        private void EnableOrDisableControlsAsNeeded()
        {
            if (this.Visibility == Visibility.Collapsed)
            {
                this.DataEntryControls.SetEnableState(ControlsEnableStateEnum.SingleImageView, -1);
            }
            else
            {
                this.DataEntryControls.SetEnableState(ControlsEnableStateEnum.MultipleImageView, this.SelectedCount());
            }
        }
        #endregion
        
        #region Events
        public event EventHandler<ClickableImagesGridEventArgs> DoubleClick;
        public event EventHandler<ClickableImagesGridEventArgs> SelectionChanged;

        protected virtual void OnDoubleClick(ClickableImagesGridEventArgs e)
        {
            this.DoubleClick?.Invoke(this, e);
        }

        protected virtual void OnSelectionChanged(ClickableImagesGridEventArgs e)
        {
            this.SelectionChanged?.Invoke(this, e);
        }
        #endregion
    }
}