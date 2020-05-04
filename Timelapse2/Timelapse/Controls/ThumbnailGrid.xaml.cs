using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Timelapse.Database;
using Timelapse.Enums;
using Timelapse.EventArguments;
using Timelapse.Util;
using RowColumn = System.Drawing.Point;

namespace Timelapse.Controls
{
    // Thumbnail Grid Overview
    // A user can use the mouse wheel to not only zoom into an image, but also to zoom out into an overview that displays 
    // multiple thumbnails at the same time in a grid. There are currently three levels of overviews, where the largest overview can 
    // – depending on the size of the display – display a good number of images (e.g., ~100) and let the user choose between them 
    // (e.g., any data entered will be applied to the images the user has checked).  However, I implemented this by brute force: 
    // I construct a fixed size grid, read images into it, and then display the grid. I don’t use infinite scroll. 
    // Nor do I display images asynchronously. This means that there could be a noticeable delay (particularly on slower computers) 
    // when switching into the overview, and when navigating images in the overview. I do cache images, but that’s a somewhat 
    // so-so solution. We are not talking about large delays here – perhaps a few seconds when switching between pages of images. 
    // Even so, it can disrupt the interactive feel of this. I suspect this simplest solution is to load images asynchronously, 
    // so users can start looking at images as they are being loaded. However, there may be better approaches. 
    // Another approach could use infinite scroll, but that could introduce some issues  in how user selections are done, 
    // where mis-selections are possible.
    // The ThumbnailGrid class does all the above.I suspect it could be completely re-implemented 
    // as an infinite scroll.However, if it is possible to change the existing class to load images asynchronously, 
    // that would help too.The catch is that the current implementation checks the size of each image to determine the size of the grid, 
    // so I am not sure how to get around that (except by using a heuristic).

    public partial class ThumbnailGrid : UserControl
    {
        #region Public properties

        // DataEntryControls needs to be set externally
        public DataEntryControls DataEntryControls { get; set; }

        // FileTable needs to be set externally
        public FileTable FileTable { set; get; }

        // FileTableStartIndex needs to be set externally
        public int FileTableStartIndex { get; set; }

        // FoldePath needs to be set externally
        // The root folder containing the template
        public string FolderPath { get; set; }

        // The number of images that currently exist in a row
        public int ImagesInRow
        {
            get
            {
                return this.Grid.ColumnDefinitions.Count;
            }
        }

        // The number of rows that currently exist in the ThumbnailGrid
        public int RowsInGrid
        {
            get
            {
                return this.Grid.RowDefinitions.Count;
            }
        }
        #endregion

        #region Private variables

        private List<ThumbnailInCell> thumbnailInCellsList;

        // Cache copies of the images we display plus associated information
        // This is done both to save existing image state and so we don't repeatedly rebuild that information
        private int cachedImagePathsStartIndex = -1;
        private List<ThumbnailInCell> cachedImageList;

        // Track states between mouse down / move and up 
        private RowColumn cellChosenOnMouseDown;
        private bool modifierKeyPressedOnMouseDown = false;
        private RowColumn cellWithLastMouseOver = new RowColumn(-1, -1);
        #endregion

        #region Constructor
        public ThumbnailGrid()
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
        // However, when zooming back out to a grid with fewer images displayed, previously selected images wil remain selected as their state is retained in the cache.
        public bool Refresh(double desiredHeight, double gridWidth, double gridHeight, bool forceUpdate)
        {
            // If nothing is loaded, or if there is no desiredWidth, then there is nothing to refresh
            if (this.FileTable == null || !this.FileTable.Any() || desiredHeight == 0)
            {
                return false;
            }
            Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;

            // Get the first image as a sample to determine the apect ration, which we will use the set the width of all columns. 
            // It may not be a representative aspect ration of all images, but its a reasonably heuristic. 
            // Note that the choice for getting the aspect ratio this way is a bit complicated. We can't just get the 'imageToDisplay' as it may
            // not be the correct one if we are navigating on the thumbnailGrid, or if it happens to be a video. So the easiest - albeit slightly less efficient -
            // way to do it is to grab the aspect ratio of the first image that will be displayed in the Thumbnail Grid. If it doesn't exist, we just use a default aspect ratio
            // ANother option - to avoid the cost of gettng a bitmap on a video - is to check if its a video (jut check the path suffix) and if so use the default aspect ratio OR
            // use FFMPEG Probe (but that may mean another dll?)
            BitmapSource bm = this.FileTable[FileTableStartIndex].GetBitmapFromFile(this.FolderPath, 64, ImageDisplayIntentEnum.TransientNavigating, out _);
            double desiredWidth = (bm == null || bm.PixelHeight == 0) ? desiredHeight * Constant.ThumbnailGrid.AspectRatioDefault : desiredHeight * bm.PixelWidth / bm.PixelHeight;

            // Reconstruct the Grid with the appropriate rows/columns 
            this.ReconstructGrid(desiredWidth, desiredHeight, gridWidth, gridHeight, FileTableStartIndex, forceUpdate);

            //Tuple<int, int> RowsColumns = ReconstructGrid(desiredWidth, desiredHeight, availableSize, FileTableStartIndex, state);
            //if (RowsColumns.Item1 == 0 || RowsColumns.Item2 == 0)
            //{
            //    // Abort as the grid cannot  display even a single image
            //    Mouse.OverrideCursor = null;
            //    return false;
            //}

            //// If forceUpdate is true, remove the cache so that images have to be regenerated
            //if (forceUpdate && this.cachedImageList != null)
            //{
            //    this.cachedImageList.Clear();
            //}

            //// Reset the selection to the first image in the grid if the first displayable image isn't the same
            //if (this.cachedImagePathsStartIndex != this.FileTableStartIndex)
            //{
            //    this.SelectInitialCellOnly();
            //}

            //// save cache parameters if the cache has changed 
            //if (this.cachedImageList == null || this.cachedImageList.Count < this.thumbnailInCellsList.Count || this.cachedImagePathsStartIndex != this.FileTableStartIndex)
            //{
            //    this.cachedImageList = this.thumbnailInCellsList;
            //    this.cachedImagePathsStartIndex = this.FileTableStartIndex;
            //}
            Mouse.OverrideCursor = null;
            return true;
        }
        #endregion

        #region Public Refresh
        // Rebuild the grid, based on 
        // - fitting the image of a desired width into as many cells of the same size that can fit within the grid
        // - retaining information about images previously shown on this grid, which importantly includes its selection status.
        //   this means users can do some selections, then change the zoom level.
        //   Note that when a user navigates, previously selected images that no longer appear in the grid will be unselected
        // However, when zooming back out to a grid with fewer images displayed, previously selected images wil remain selected as their state is retained in the cache.
        //public bool XRefresh(double desiredWidth, Size availableSize, bool forceUpdate, int state)
        //{
        //    // If nothing is loaded, or if there is no desiredWidth, then there is nothing to refresh
        //    if (this.FileTable == null || !this.FileTable.Any() || desiredWidth == 0)
        //    {
        //        return false;
        //    }
        //    Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
        //    // Reconstruct the Grid with the appropriate rows/columns 
        //    int columnCount = ReconstructGrid(desiredWidth, desiredWidth, availableSize);
        //    if (columnCount == 0)
        //    {
        //        // Abort as there is not enough width available to display even a single image
        //        Mouse.OverrideCursor = null;
        //        return false;
        //    }

        //    // Add images to successive columns, where we create an initial row and then additional row if needed after the columns are full
        //    // As we do this, check to see:
        //    // - if those images are in the image cache (as these will be in the same order, we can check as we add images and move through the cache) 
        //    // - if we have run out of images to add 
        //    // - the new row does't fit the available space
        //    int rowNumber = 0;
        //    int fileTableIndex = this.FileTableStartIndex;

        //    Double maxImageHeight = 0;
        //    Double combinedRowHeight = 0;
        //    ThumbnailInCell ci;
        //    this.thumbnailInCellsList = new List<ThumbnailInCell>();
        //    List<ThumbnailInCell> thumbnailGridRow = new List<ThumbnailInCell>();

        //    // If forceUpdate is true, remove the cache so that images have to be regenerated
        //    if (forceUpdate && this.cachedImageList != null)
        //    {
        //        this.cachedImageList.Clear();
        //    }

        //    while (true)
        //    {
        //        // For each row, collect potential images (if available), while tracking the maximum height across images (used to determine the needed row height)
        //        for (int columnIndex = 0; columnIndex < columnCount && fileTableIndex < this.FileTable.Count(); columnIndex++)
        //        {
        //            // Process each cell in a row. Use the image in the cache if available, otherwise create it.  
        //            string path = Path.Combine(this.FileTable[fileTableIndex].RelativePath, this.FileTable[fileTableIndex].File);

        //            ci = TryGetCachedThumbnailInCell(path, desiredWidth, state, fileTableIndex, ref maxImageHeight);
        //            if (ci != null)
        //            {
        //                thumbnailGridRow.Add(ci);
        //            }
        //            else
        //            {
        //                ci = CreateThumbnailInCell(fileTableIndex, state, desiredWidth, ref maxImageHeight);
        //                thumbnailGridRow.Add(ci);
        //            }
        //            fileTableIndex++;
        //        }

        //        // We've reached the end of the row. Create a new row if there is space for it, otherwise abort
        //        if (false == CreateNewRowIfSpaceExists(thumbnailGridRow, availableSize, rowNumber, combinedRowHeight, maxImageHeight))
        //        {
        //            // We are done
        //            break;
        //        }

        //        // Initialize the (empty) row and adjust the various heights
        //        rowNumber++; // we are now on the next row
        //        combinedRowHeight += maxImageHeight; // The amount of consumed space
        //        thumbnailGridRow.Clear();
        //        maxImageHeight = 0; // as we will have to recalculate the max height for this row

        //        // If we've gone beyond the last image in the image set, then we are done.
        //        if (fileTableIndex >= this.FileTable.Count())
        //        {
        //            break;
        //        }
        //    }

        //    // Reset the selection to the first image in the grid if the first displayable image isn't the same
        //    if (this.cachedImagePathsStartIndex != this.FileTableStartIndex)
        //    {
        //        this.SelectInitialCellOnly();
        //    }

        //    // save cache parameters if the cache has changed 
        //    if (this.cachedImageList == null || this.cachedImageList.Count < this.thumbnailInCellsList.Count || this.cachedImagePathsStartIndex != this.FileTableStartIndex)
        //    {
        //        this.cachedImageList = this.thumbnailInCellsList;
        //        this.cachedImagePathsStartIndex = this.FileTableStartIndex;
        //    }
        //    Mouse.OverrideCursor = null;

        //    // Return false if we can't even fit in a single row
        //    return (this.Grid.RowDefinitions.Count < 1) ? false : true;
        //}

        // Invalidate the ThumbnailInCells cache.
        // Used to force a redraw of images, e.g., such as when an image is deleted (but not its data) so that the missing image is shown in its place
        public void InvalidateCache()
        {
            this.cachedImageList = null;
        }
        #endregion

        #region Public BoundingBoxes
        // For each ThumbnailInCell in the cache (i.e., those that are currently being displayed)
        // show or hide the bounding boxes 
        public void ShowOrHideBoundingBoxes(bool visibility)
        {
            foreach (ThumbnailInCell ci in this.cachedImageList)
            {
                ci.ShowOrHideBoundingBoxes(visibility);
            }
        }
        #endregion

        #region Mouse callbacks
        // Mouse left down. Select images
        // The selection behaviours depend upon whether the CTL or SHIFT modifier key is pressed, or whether this is a double click 
        private void Grid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ThumbnailInCell ci;
            this.cellChosenOnMouseDown = this.GetCellFromPoint(Mouse.GetPosition(this.Grid));
            RowColumn currentCell = this.GetCellFromPoint(Mouse.GetPosition(this.Grid));
            this.cellWithLastMouseOver = currentCell;

            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                // CTL mouse down: change that cell (and only that cell's) state
                this.modifierKeyPressedOnMouseDown = true;
                if (Equals(this.cellChosenOnMouseDown, currentCell))
                {
                    ci = this.GetThumbnailInCellFromCell(currentCell);
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
                ci = this.GetThumbnailInCellFromCell(currentCell);
                if (ci != null)
                {
                    this.SelectNone();
                    ci.IsSelected = true;
                }
            }

            // If this is a double click, raise the Double click event, e.g., so that the calling app can navigate to that image.
            if (e.ClickCount == 2)
            {
                ci = this.GetThumbnailInCellFromCell(currentCell);
                ThumbnailGridEventArgs eventArgs = new ThumbnailGridEventArgs(this, ci?.ImageRow);
                this.OnDoubleClick(eventArgs);
                e.Handled = true; // Stops the double click from generating a marker on the MarkableImageCanvas
            }
            this.EnableOrDisableControlsAsNeeded();
            ThumbnailGridEventArgs selectionEventArgs = new ThumbnailGridEventArgs(this, null);
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
            RowColumn currentCell = this.GetCellFromPoint(Mouse.GetPosition(this.Grid));

            // Ignore if the cell has already been handled in the last mouse down or move event,
            if (Equals(currentCell, this.cellWithLastMouseOver))
            {
                return;
            }
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
            if (this.thumbnailInCellsList.Any())
            {
                ThumbnailInCell ci = this.thumbnailInCellsList[0];
                ci.IsSelected = true;
            }
            ThumbnailGridEventArgs eventArgs = new ThumbnailGridEventArgs(this, null);
            this.OnSelectionChanged(eventArgs);
        }

        private void SelectNone()
        {
            // Unselect all ThumbnailInCells
            foreach (ThumbnailInCell ci in this.thumbnailInCellsList)
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
            DetermineTopLeftBottomRightCells(this.cellChosenOnMouseDown, currentCell, out RowColumn startCell, out RowColumn endCell);

            // Select the cells defined by the cells running from the topLeft cell to the BottomRight cell
            RowColumn indexCell = startCell;

            ThumbnailInCell ci;
            while (true)
            {
                ci = this.GetThumbnailInCellFromCell(indexCell);
                // If the cell doesn't contain a ThumbnailInCell, then we are at the end.
                if (ci == null)
                {
                    break;
                }
                ci.IsSelected = true;

                // If there is no next cell, then we are at the end.
                if (this.GridGetNextCell(indexCell, endCell, out RowColumn nextCell) == false)
                {
                    break;
                }
                indexCell = nextCell;
            }
            ThumbnailGridEventArgs eventArgs = new ThumbnailGridEventArgs(this, null);
            this.OnSelectionChanged(eventArgs);
        }

        // Select all cells between the initial and currently selected cell
        private void SelectFromTo(RowColumn cell1, RowColumn cell2)
        {
            DetermineTopLeftBottomRightCells(cell1, cell2, out RowColumn startCell, out RowColumn endCell);

            // Select the cells defined by the cells running from the topLeft cell to the BottomRight cell
            RowColumn indexCell = startCell;

            ThumbnailInCell ci;
            while (true)
            {
                ci = this.GetThumbnailInCellFromCell(indexCell);
                // This shouldn't happen, but ensure that the cell contains a ThumbnailInCell.
                if (ci == null)
                {
                    break;
                }
                ci.IsSelected = true;

                // If there is no next cell, then we are at the end.
                if (this.GridGetNextCell(indexCell, endCell, out RowColumn nextCell) == false)
                {
                    break;
                }
                indexCell = nextCell;
            }
            ThumbnailGridEventArgs eventArgs = new ThumbnailGridEventArgs(this, null);
            this.OnSelectionChanged(eventArgs);
        }

        private void SelectExtendSelectionFrom(RowColumn currentCell)
        {
            // If there is no previous cell, then we are at the end.
            if (this.GridGetPreviousSelectedCell(currentCell, out RowColumn previousCell) == true)
            {
                this.SelectFromTo(previousCell, currentCell);
            }
            else if (this.GridGetNextSelectedCell(currentCell, out RowColumn nextCell) == true)
            {
                this.SelectFromTo(currentCell, nextCell);
            }
        }

        // Get the Selected times as a list of file table indexes to the current displayed selection of files (note these are not the IDs)
        public List<int> GetSelected()
        {
            List<int> selected = new List<int>();
            if (this.thumbnailInCellsList == null)
            {
                return selected;
            }
            foreach (ThumbnailInCell ci in this.thumbnailInCellsList)
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
            return this.GetSelected().Count;
        }
        #endregion

        private List<ThumbnailInCell> emptyThumbnailInCells;
        private BackgroundWorker BackgroundWorker;
        // Return the number of rows/columns in the grid as a Tuple.
        // If either the row or column is 0, then there is no meaningful grid to display images
        #region Reconstruct the grid, including clearing it
        // private Tuple<int, int> ReconstructGrid(double desiredWidth, double desiredHeight, Size availableSize, int fileTableStartIndex, int state)

        private void ReconstructGrid(double cellWidth, double cellHeight, double gridWidth, double gridHeight, int fileTableStartIndex, bool forceUpdate)
        {
            int fileTableIndex;
            this.BackgroundWorker = new BackgroundWorker()
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = true
            };

            this.BackgroundWorker.DoWork += (ow, ea) =>
            {
                try
                {
                    fileTableIndex = fileTableStartIndex;
                    foreach (ThumbnailInCell emptyThumbnailInCell in emptyThumbnailInCells)
                    {
                        if (this.BackgroundWorker.CancellationPending == true)
                        {
                            ea.Cancel = true;
                            return;
                        }

                        BitmapSource bm = emptyThumbnailInCell.GetThumbnail(cellWidth, cellHeight);
                        LoadImageProgressStatus lip = new LoadImageProgressStatus
                        {
                            ThumbnailInCell = emptyThumbnailInCell,
                            BitmapSource = bm,
                            Position = emptyThumbnailInCell.Position,
                            DesiredWidth = cellWidth,
                            FileTableIndex = fileTableIndex,
                        };
                        this.BackgroundWorker.ReportProgress(0, lip);
                        this.thumbnailInCellsList.Add(emptyThumbnailInCell);
                        fileTableIndex++;
                    }

                    //foreach (ThumbnaillnCell thumbnailInCell in this.ThumbnailInCells)
                    //{

                    //}
                    //    // Now add each thumbnailInCell to the grid going in left/right order then down each row until we run out of either cells or files to display
                    //    for (int currentRow = 0; currentRow < rowCount; currentRow++)
                    //{
                    //    for (int currentColumn = 0; currentColumn < columnCount && fileTableIndex < this.FileTable.Count(); currentColumn++)
                    //    {
                    //        // Process each cell in a row. Use the image in the cache if available, otherwise create it.  
                    //        string path = Path.Combine(this.FileTable[fileTableIndex].RelativePath, this.FileTable[fileTableIndex].File);

                    //        LoadImageProgressStatus lip = new LoadImageProgressStatus
                    //        {
                    //            ThumbnailInCell = this.ThumbnailInCells[imageInCell.Position],
                    //            //BitmapImage = Thumbnailer.GetThumbnailFromFileAsync(imageInCell.Path, desiredWidth, desiredHeight, useFFMpeg).Result,
                    //            BitmapImage = Thumbnailer.GetThumbnailFromFile(imageInCell.Path, desiredWidth, desiredHeight, useFFMpeg),
                    //            Position = imageInCell.Position,
                    //        };

                    //        thumbnailInCell = TryGetCachedThumbnailInCell(path, desiredWidth, state, fileTableIndex);
                    //        if (thumbnailInCell == null)
                    //        {
                    //            thumbnailInCell = CreateThumbnailInCell(fileTableIndex, state, desiredWidth); //SAULXXX CHANGE TO DESIRED HEIGHT, ALTHOUGH IT WILL HAVE THE SAME EFFECT
                    //        }
                    //        //Grid.SetRow(thumbnailInCell, currentRow);
                    //        //Grid.SetColumn(thumbnailInCell, currentColumn);
                    //        //this.Grid.Children.Add(thumbnailInCell);
                    //        //this.thumbnailInCellsList.Add(thumbnailInCell);
                    //        fileTableIndex++;
                    //    }
                    //}
                    // Note that if the grid can't even fit a single row or a single column in one of these will be 0.
                    return; //rowsColumns;
                }
                catch
                {
                    // Uncomment to trace 
                    System.Diagnostics.Debug.Print("DoWork Aborted");
                    return;
                }
            };

            this.BackgroundWorker.ProgressChanged += (o, ea) =>
            {
                // this gets called on the UI thread
                LoadImageProgressStatus lip = (LoadImageProgressStatus)ea.UserState;
                this.UpdateThumbnailsLoadProgress(lip);
            };

            this.BackgroundWorker.RunWorkerCompleted += (o, ea) =>
            {
                // If forceUpdate is true, remove the cache so that images have to be regenerated
                if (forceUpdate && this.cachedImageList != null)
                {
                    this.cachedImageList.Clear();
                }

                // Reset the selection to the first image in the grid if the first displayable image isn't the same
                if (this.cachedImagePathsStartIndex != this.FileTableStartIndex)
                {
                    this.SelectInitialCellOnly();
                }

                // save cache parameters if the cache has changed 
                if (this.cachedImageList == null || this.cachedImageList.Count < this.thumbnailInCellsList.Count || this.cachedImagePathsStartIndex != this.FileTableStartIndex)
                {
                    this.cachedImageList = this.thumbnailInCellsList;
                    this.cachedImagePathsStartIndex = this.FileTableStartIndex;
                }
                this.BackgroundWorker.Dispose();
                return;
            };

            this.CancelThumbnailUpdate();

            // Calculated the number of columns that can fit into the available space,
            int columnCount = Convert.ToInt32(Math.Floor(gridWidth / cellWidth));
            int rowCount = Convert.ToInt32(Math.Floor(gridHeight / cellHeight));
            Tuple<int, int> rowsColumns = new Tuple<int, int>(rowCount, columnCount);
            this.emptyThumbnailInCells = new List<ThumbnailInCell>();
            this.thumbnailInCellsList = new List<ThumbnailInCell>();
            fileTableIndex = fileTableStartIndex;
            ThumbnailInCell emptyTumbnailInCell;

            // Clear the Grid so we can start afresh
            this.Grid.RowDefinitions.Clear();
            this.Grid.ColumnDefinitions.Clear();
            this.Grid.Children.Clear();
            this.thumbnailInCellsList = new List<ThumbnailInCell>();

            if (rowsColumns.Item1 == 0 || rowsColumns.Item2 == 0)
            {
                // We can't even fit a single row or column in, so no point in continuing.
                return; // rowsColumns;
            }

            // Add as many columns of the desired width, and rows of the desired height as can fit into the grid's available space
            for (int currentColumn = 0; currentColumn < columnCount; currentColumn++)
            {
                this.Grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition() { Width = new GridLength(cellWidth, GridUnitType.Pixel) });
            }
            for (int currentRow = 0; currentRow < rowCount; currentRow++)
            {
                this.Grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(cellHeight, GridUnitType.Pixel) });
            }

            int position = 0;
            // Now add an empty thumbnailInCell to the grid going in left/right order then down each row until we run out of either cells or files to display
            for (int currentRow = 0; currentRow < rowCount; currentRow++)
            {
                for (int currentColumn = 0; currentColumn < columnCount && fileTableIndex < this.FileTable.Count(); currentColumn++)
                {
                    // Process each cell in a row. Use the image in the cache if available, otherwise create it.  
                    string path = Path.Combine(this.FileTable[fileTableIndex].RelativePath, this.FileTable[fileTableIndex].File);
                    //emptyTumbnailInCell = TryGetCachedThumbnailInCell(path, cellWidth, state, fileTableIndex);
                    //if (emptyTumbnailInCell == null)
                    //{
                    emptyTumbnailInCell = CreateEmptyThumbnail(fileTableIndex, cellWidth, cellHeight, position, currentRow, currentColumn); //SAULXXX CHANGE TO DESIRED HEIGHT, ALTHOUGH IT WILL HAVE THE SAME EFFECT
                    //}
                    Grid.SetRow(emptyTumbnailInCell, currentRow);
                    Grid.SetColumn(emptyTumbnailInCell, currentColumn);
                    this.Grid.Children.Add(emptyTumbnailInCell);
                    this.thumbnailInCellsList.Add(emptyTumbnailInCell);

                    emptyThumbnailInCells.Add(emptyTumbnailInCell);
                    fileTableIndex++;
                    position++;
                }
            }
            this.BackgroundWorker.RunWorkerAsync();
        }
        private void CancelThumbnailUpdate()
        {
            this.CancelUpdate();
        }
        public void CancelUpdate()
        {
            try
            {
                if (this.BackgroundWorker != null)
                {
                    this.BackgroundWorker.CancelAsync();
                }
            }
            catch { }
        }
        private void UpdateThumbnailsLoadProgress(LoadImageProgressStatus lip)
        {
            try
            {
                if (lip.ThumbnailInCell.Position < this.emptyThumbnailInCells.Count && lip.BitmapSource != null)
                {
                    ThumbnailInCell etic = this.emptyThumbnailInCells[lip.Position];
                    etic.SetSource(lip.BitmapSource);
                    etic.DesiredRenderWidth = lip.DesiredWidth;
                    etic.DisplayEpisodeAndBoundingBoxesIfWarranted(this.FileTable, lip.FileTableIndex, 1);
                }
                // Uncomment for tracing purposes
                //else
                //{
                //    System.Diagnostics.Debug.Print(String.Format("UpdateThumbnailsLoadProgress Aborted | IndexoutOfRange={0}, BitmapSource is null={1}", lip.ThumbnailInCell.Position >= this.emptyThumbnailInCells.Count, lip.BitmapSource == null));
                //}
            }
            catch
            {
                //System.Diagnostics.Debug.Print("UpdateThumbnailsLoadProgress Aborted | Catch");
            }
        }
        #endregion

        #region Set the image to the cached image if it is available
        private ThumbnailInCell TryGetCachedThumbnailInCell(string path, double desiredWidth, int state, int fileTableIndex)
        {
            ThumbnailInCell ci;
            Double imageHeight;
            int cachedImageListIndex = 0;
            while (this.cachedImageList != null && cachedImageListIndex < this.cachedImageList.Count)
            {
                if (path == this.cachedImageList[cachedImageListIndex].Path)
                {
                    // The image is in the cache.
                    ci = this.cachedImageList[cachedImageListIndex];
                    if (ci.DesiredRenderWidth < desiredWidth && ci.DesiredRenderSize.Width < desiredWidth)
                    {
                        // Re-render the cached image, as its smaller than the resolution width 
                        double FOOBARBARFOOBARBARFOOBARBARFOOBARBARFOOBARBAR = 1;
                        imageHeight = ci.Rerender(this.FileTable, cachedImageListIndex, desiredWidth, state, FOOBARBARFOOBARBARFOOBARBARFOOBARBARFOOBARBAR, FOOBARBARFOOBARBARFOOBARBARFOOBARBARFOOBARBAR);
                    }
                    else
                    {
                        // Reuse the cached image, as its at least of the same or greater resolution width. 
                        ci.Image.Width = desiredWidth; // Adjust the image width to the new size
                        imageHeight = ci.DesiredRenderSize.Height;

                        // Rerender the episode text in case it has changed
                        ci.DisplayEpisodeTextIfWarranted(this.FileTable, fileTableIndex);
                        ci.ShowOrHideBoundingBoxes(true);
                    }
                    ci.FileTableIndex = fileTableIndex; // Update the filetableindex just in case
                    int fontSizeCorrectionFactor = (state == 1) ? 20 : 15;
                    ci.SetTextFontSize(desiredWidth / fontSizeCorrectionFactor);
                    ci.AdjustMargin(state);
                    return ci;
                }
                cachedImageListIndex++;
            }
            return null;
        }

        // Create a ThumbnailInCell for the file at fileTableIndex
        private ThumbnailInCell CreateThumbnailInCell(int fileTableIndex, int state, double desiredWidth)
        {
            // The image is not in the cache. Create a new ThumbnailInCell
            ThumbnailInCell ci = new ThumbnailInCell(desiredWidth)
            {
                RootFolder = this.FolderPath,
                ImageRow = this.FileTable[fileTableIndex],
                DesiredRenderWidth = desiredWidth,
                BoundingBoxes = Util.GlobalReferences.MainWindow.GetBoundingBoxesForCurrentFile(this.FileTable[fileTableIndex].ID)
            };
            double FOOBARBARFOOBARBARFOOBARBARFOOBARBARFOOBARBAR = 1;
            double imageHeight = ci.Rerender(this.FileTable, fileTableIndex, desiredWidth, state, FOOBARBARFOOBARBARFOOBARBARFOOBARBARFOOBARBAR, FOOBARBARFOOBARBARFOOBARBARFOOBARBARFOOBARBAR);
            ci.FileTableIndex = fileTableIndex; // Set the filetableindex so we can retrieve it later
            int fontSizeCorrectionFactor = (state == 1) ? 20 : 15;
            ci.SetTextFontSize(desiredWidth / fontSizeCorrectionFactor);
            ci.AdjustMargin(state);
            return ci;
        }

        private ThumbnailInCell CreateEmptyThumbnail(int fileTableIndex, double desiredWidth, double desiredHeight, int position, int row, int column)
        {
            ThumbnailInCell ci = new ThumbnailInCell(desiredWidth)
            {
                Position = position,
                Row = row,
                Column = column,
                RootFolder = this.FolderPath,
                ImageRow = this.FileTable[fileTableIndex],
                DesiredRenderWidth = desiredWidth,
                BoundingBoxes = Util.GlobalReferences.MainWindow.GetBoundingBoxesForCurrentFile(this.FileTable[fileTableIndex].ID)
            };
            //double imageHeight = ci.Rerender(this.FileTable, fileTableIndex, desiredWidth, state);
            ci.FileTableIndex = fileTableIndex; // Set the filetableindex so we can retrieve it later
            //int fontSizeCorrectionFactor = (state == 1) ? 20 : 15;
            //ci.SetTextFontSize(desiredWidth / fontSizeCorrectionFactor);
            //ci.AdjustMargin(state);
            double fontSizeCorrectionFactor = desiredHeight / 10 > 40 ? 40 : desiredHeight / 10;// = (state == 1) ? 20 : 15;
            ci.SetTextFontSize(fontSizeCorrectionFactor);
            return ci;
        }
        #endregion

        #region Cell Navigation methods
        private bool GridGetNextSelectedCell(RowColumn cell, out RowColumn nextCell)
        {
            RowColumn lastCell = new RowColumn(this.Grid.RowDefinitions.Count - 1, this.Grid.ColumnDefinitions.Count - 1);
            ThumbnailInCell ci;

            while (this.GridGetNextCell(cell, lastCell, out nextCell))
            {
                ci = this.GetThumbnailInCellFromCell(nextCell);

                // If there is no cell, we've reached the end, 
                if (ci == null)
                {
                    System.Diagnostics.Debug.Print("false");
                    return false;
                }
                // We've found a selected cell
                if (ci.IsSelected)
                {
                    System.Diagnostics.Debug.Print("true");
                    return true;
                }
                cell = nextCell;
            }
            return false;
        }

        private bool GridGetPreviousSelectedCell(RowColumn cell, out RowColumn previousCell)
        {
            RowColumn lastCell = new RowColumn(0, 0);
            ThumbnailInCell ci;

            while (this.GridGetPreviousCell(cell, lastCell, out previousCell))
            {
                ci = this.GetThumbnailInCellFromCell(previousCell);

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
            if (nextCell.Y == this.Grid.ColumnDefinitions.Count)
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
                previousCell.Y = this.Grid.ColumnDefinitions.Count - 1;
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
            foreach (var rowDefinition in this.Grid.RowDefinitions)
            {
                accumulatedHeight += rowDefinition.ActualHeight;
                if (accumulatedHeight >= mousePoint.Y)
                {
                    break;
                }
                cell.X++;
            }

            // Calculate which column the mouse was over
            foreach (var columnDefinition in this.Grid.ColumnDefinitions)
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

        // Get the ThumbnailInCell held by the Grid's specified row,column coordinates 
        private ThumbnailInCell GetThumbnailInCellFromCell(RowColumn cell)
        {
            return this.Grid.Children.Cast<ThumbnailInCell>().FirstOrDefault(exp => Grid.GetColumn(exp) == cell.Y && Grid.GetRow(exp) == cell.X);
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
        public event EventHandler<ThumbnailGridEventArgs> DoubleClick;
        public event EventHandler<ThumbnailGridEventArgs> SelectionChanged;

        protected virtual void OnDoubleClick(ThumbnailGridEventArgs e)
        {
            this.DoubleClick?.Invoke(this, e);
        }

        protected virtual void OnSelectionChanged(ThumbnailGridEventArgs e)
        {
            this.SelectionChanged?.Invoke(this, e);
        }
        #endregion
    }

    #region Class: LoadImageProgressStatus
    // Used by ReportProgress to pass specific values to Progress Changed as a parameter 
    internal class LoadImageProgressStatus
    {
        public ThumbnailInCell ThumbnailInCell { get; set; } = null;
        public BitmapSource BitmapSource { get; set; } = null;
        public int Position { get; set; } = 0;
        public double DesiredWidth { get; set; } = 0;
        public int FileTableIndex { get; set; }

        public LoadImageProgressStatus()
        {
        }
    }
    #endregion
}