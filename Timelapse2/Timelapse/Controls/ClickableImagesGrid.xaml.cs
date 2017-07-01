using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RowColumn = System.Drawing.Point;
using Timelapse.Database;

namespace Timelapse.Controls
{
    /// <summary>
    /// Interaction logic for ClickableImagesGrid.xaml
    /// </summary>
    public partial class ClickableImagesGrid : UserControl
    {
        public string[] FilePaths { get; set; }
        public FileTable FileTable { get; set; }
        public int FileStartIndex { get; set; }

        // The root folder containing the template
        public string FolderPath { get; set; } 

        private ObservableCollection<ClickableImage> clickableImagesList;

        // We cache copies of the images we display plus associated information
        private int cachedImagePathsStartIndex = -1;
        private string[] CachedImageFilePaths { get; set; }
        private ObservableCollection<ClickableImage> cachedImageList;

        private List<ClickableImage> selectedClickableImages;
        private RowColumn initiallySelectedCell;

        private Brush unselectedBrush = Brushes.Black;
        private Brush selectedBrush = Brushes.LightBlue;

        public ClickableImagesGrid()
        {
            this.InitializeComponent();
            this.FileStartIndex = 0;
            this.selectedClickableImages = new List<ClickableImage>();
        }

        #region Public Refresh
        // Rebuild the grid, based on fitting the image of a desired width into as many cells of the same size that can fit 
        // the available size (width and height) of a grid
        // Also retains information about images previously shown on this grid (e.g., selection status).
        public void Refresh(double desiredWidth, Point availableSize)
        {
            Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
            // As we will rebuild the grid and its items, we need to clear it first
            this.Grid.RowDefinitions.Clear();
            this.Grid.ColumnDefinitions.Clear();
            this.Grid.Children.Clear();

            // Get an estimate about the number of cells, i.e., how many rows and columns we can fit into the available space.
            // We try a 16:9 aspect ratio, which will likely equal to or overestimate the number of cells. 
            // Using that, we will then read in those number of images and get the actual worst-case aspect ratio 
            Tuple<int, int> rowCol = this.CalculateRowsAndColumns(desiredWidth, desiredWidth * 9.0 / 16.0, availableSize.X, availableSize.Y);
            int numberOfCells = rowCol.Item1 * rowCol.Item2;

            // Create the image list based on the overestimate of the number of Cells.
            // We do this as we need to get the actual 'worst-case' aspect ratio 
            this.clickableImagesList = new ObservableCollection<ClickableImage>();

            int counted = 0;
            for (int index = this.FileStartIndex; index < this.FileTable.RowCount && counted <= numberOfCells; index++)
            {
                ClickableImage ci;
                counted++;
                string path = Path.Combine(this.FileTable[index].RelativePath, this.FileTable[index].FileName);

                // If we already have a copy of the image, reuse that instead of recreating it.
                bool skip = false;
                if (this.cachedImageList != null)
                {
                    // Search the cached imageList for the image by its path. Note that if its a lower resolution image, we re-render it.
                    // This also keeps the checkbox state. 
                    foreach (ClickableImage clickableImageTest in this.cachedImageList)
                    {
                        if (clickableImageTest.Path == path)
                        {
                            ci = clickableImageTest;
                            if (ci.DesiredRenderWidth < desiredWidth && ci.DesiredRenderSize.X < desiredWidth)
                            {
                                ci.Rerender(desiredWidth);
                            }
                            skip = true;
                            this.clickableImagesList.Add(ci);
                            break;
                        }
                    }
                }
                if (skip == false)
                {
                    ci = new ClickableImage(desiredWidth);
                    ci.RootFolder = this.FolderPath;
                    ci.ImageRow = this.FileTable[index];
                    ci.DesiredRenderWidth = desiredWidth;
                    this.clickableImagesList.Add(ci);
                    ci.Rerender(desiredWidth);
                }
            }

            // Find the maximum image height. We will use this to determine the height of each row.
            Double maxImageHeight = 0;
            foreach (ClickableImage ci in this.clickableImagesList)
            {
                ci.DesiredRenderWidth = desiredWidth;
                if (maxImageHeight < ci.DesiredRenderSize.Y)
                {
                    maxImageHeight = ci.DesiredRenderSize.Y;
                }
            }

            // Using these real dimensions, calculate the number of rows and columns of a given height and width that we can fit into the available space
            rowCol = this.CalculateRowsAndColumns(desiredWidth, maxImageHeight, availableSize.X, availableSize.Y);

            // Add that number of rows and columns to the grid
            for (int r = 0; r < rowCol.Item1; r++)
            {
                this.Grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(maxImageHeight, GridUnitType.Pixel) });
            }
            for (int c = 0; c < rowCol.Item2; c++)
            {
                this.Grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition() { Width = new GridLength(desiredWidth, GridUnitType.Pixel) });
            }

            // Add an image to each available cell, as long as there are images to add.
            int row = 0;
            int col = 0;
            int count = 0;
            ClickableImage clickableImage;
            for (int index = this.FileStartIndex; index < this.FileTable.RowCount && count < this.clickableImagesList.Count; index++)
            {
                clickableImage = this.clickableImagesList[count++];

                // When we have filled the row, start a new row
                if (col >= rowCol.Item2)
                {
                    col = 0;
                    row++;
                }

                // Stop when we have filled all the rows
                if (row >= rowCol.Item1)
                {
                    break;
                }

                Grid.SetRow(clickableImage, row);
                Grid.SetColumn(clickableImage, col);
                this.Grid.Children.Add(clickableImage);
                col++;
            }

            if (this.cachedImageList == null || this.cachedImageList.Count < this.clickableImagesList.Count || this.cachedImagePathsStartIndex != this.FileStartIndex)
            {
                this.cachedImageList = this.clickableImagesList;
                this.cachedImagePathsStartIndex = this.FileStartIndex;
            }
            Mouse.OverrideCursor = null;
        }
        #endregion

        #region Mouse callbacks
        // Remember the cell that corresponds to the left mouse down even
        private void Grid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.initiallySelectedCell = this.GetCellRowColumnFromPoint(Mouse.GetPosition(Grid));
        }

        // As the mouse down moves with the left button pressed, select all cells contained by its bounding box i.e.,
        // - between the initial cell selected on the mouse down and the current cell under this mouse move
        private void Grid_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.Grid_SelectWithinBoundingBox();
            }
        }

        // On the mouse up, select all cells contained by its bounding box. Note that this is needed
        // as well as the mouse move version, as otherwise a down/up on the same spot won't select the cell.
        private void Grid_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            this.Grid_SelectWithinBoundingBox();
        }
        #endregion

        #region Helper methods

        // Calculate the number of rows and columns of a given height and width that we can fit into the available space
        private Tuple<int, int> CalculateRowsAndColumns(double imageWidth, double imageHeight, double availableWidth, double availableHeight)
        {
            
            int columns = Convert.ToInt32(Math.Floor(availableWidth / imageWidth));
            int rows = (imageHeight > 0) ? Convert.ToInt32(Math.Floor(availableHeight / imageHeight)) : 1;
            return new Tuple<int, int>(rows, columns);
        }

        // Select all cells within the bounding box defined by the initial and currently selected cell
        private void Grid_SelectWithinBoundingBox()
        {
            // clear the selections as we will rebuild the selections from scratch
            this.selectedClickableImages.Clear();
            this.Grid_UnselectAll(); // Clear the selections

            // get the currently selected cell
            RowColumn currentlySelectedCell = GetCellRowColumnFromPoint(Mouse.GetPosition(Grid));

            // If the first selected cell doesn't exist, make it the same as the currently selected cell
            if (this.initiallySelectedCell == null)
            {
                this.initiallySelectedCell = currentlySelectedCell;
            }

            RowColumn topLeftCell;
            RowColumn bottomRightCell;
            this.DetermineTopLeftBottomRightCells(initiallySelectedCell, currentlySelectedCell, out topLeftCell, out bottomRightCell);

            // Select the cells defined within the topLeft/BottomRight bounding box
            int row = topLeftCell.X;
            int col = topLeftCell.Y;
            while (true)
            {
                ClickableImage ci = Grid.Children.Cast<ClickableImage>().FirstOrDefault(exp => Grid.GetColumn(exp) == col && Grid.GetRow(exp) == row);
                if (ci == null)
                {
                    break;
                }
                ci.IsSelected = true;
                ci.Cell.Background = this.selectedBrush;

                col = (col < bottomRightCell.Y) ? col + 1 : topLeftCell.Y;
                if (col == topLeftCell.Y)
                {
                    row++;
                }
                if (row > bottomRightCell.X || (row > bottomRightCell.X && col > bottomRightCell.Y)) // WHy first condition?
                {
                    break;
                }
            }
        }

        // Given two RowColumn cell positions, return which is in the top left and which is the bottom right position 
        private void DetermineTopLeftBottomRightCells(RowColumn startCell, RowColumn currentCell, out RowColumn topLeftCell, out RowColumn bottomRightCell)
        {
            // If its in the same row, then order is determined by column
            if (startCell.X == currentCell.X )
            {
                if (startCell.Y <= currentCell.Y)
                {
                    topLeftCell = startCell;
                    bottomRightCell = currentCell;
                }
                else
                {
                    topLeftCell = currentCell;
                    bottomRightCell = startCell;
                }
                return;
            }
            // Rows are different, so order is determined by row number
            if (startCell.X < currentCell.X)
            {
                topLeftCell = startCell;
                bottomRightCell = currentCell;
            }
            else
            {
                topLeftCell = currentCell;
                bottomRightCell = startCell;
            }
        }
    
        // Given a mouse point, return a point that indicates the (row, column) of the grid that the mouse point is over
        private RowColumn GetCellRowColumnFromPoint (Point mousePoint)
        {
            RowColumn cellPosition = new RowColumn(0,0);
            double accumulatedHeight = 0.0;
            double accumulatedWidth = 0.0;

            // Calculate which row the mouse was over
            foreach (var rowDefinition in Grid.RowDefinitions)
            {
                accumulatedHeight += rowDefinition.ActualHeight;
                if (accumulatedHeight >= mousePoint.Y)
                    break;
                cellPosition.X++;
            }

            // Calculate which column the mouse was over
            foreach (var columnDefinition in Grid.ColumnDefinitions)
            {
                accumulatedWidth += columnDefinition.ActualWidth;
                if (accumulatedWidth >= mousePoint.X)
                    break;
                cellPosition.Y++;
            }
            return cellPosition;
        }

        // Unselect all elements in the grid
        private void Grid_UnselectAll ()
        {
            // Unselect all clickable images
            foreach (ClickableImage ci in this.clickableImagesList)
            {
                ci.IsSelected = false;
                ci.Cell.Background = this.unselectedBrush;
            }
        }

        // Select the grid elements in the provided list
        private void Grid_Select(List<ClickableImage> selectedClickableImages)
        {
            foreach (ClickableImage ci in selectedClickableImages)
            {
                ci.IsSelected = true;
                ci.Cell.Background = this.selectedBrush;
            }
        }
        #endregion

    }
}