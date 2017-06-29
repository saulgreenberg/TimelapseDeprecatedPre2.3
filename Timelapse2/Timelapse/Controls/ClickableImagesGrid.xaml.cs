using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

        private ObservableCollection<ClickableImage> imageList;

        // We cache copies of the images we display plus associated information
        private int cachedImagePathsStartIndex = -1;
        private string[] CachedImageFilePaths { get; set; }
        private ObservableCollection<ClickableImage> cachedImageList;

        public ClickableImagesGrid()
        {
            this.InitializeComponent();
            this.FileStartIndex = 0;
        }

        // Rebuild the grid, based on fitting the image of a desired width into as many cells of the same size that can fit 
        // the available size (width and height) of a grid
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
            this.imageList = new ObservableCollection<ClickableImage>();

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
                            this.imageList.Add(ci);
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
                    this.imageList.Add(ci);
                    ci.Rerender(desiredWidth);
                }
            }

            // Find the maximum image height. We will use this to determine the height of each row.
            Double maxImageHeight = 0;
            foreach (ClickableImage ci in this.imageList)
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
            for (int index = this.FileStartIndex; index < this.FileTable.RowCount && count < this.imageList.Count; index++)
            {
                clickableImage = this.imageList[count++];

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

            if (this.cachedImageList == null || this.cachedImageList.Count < this.imageList.Count || this.cachedImagePathsStartIndex != this.FileStartIndex)
            {
                this.cachedImageList = this.imageList;
                this.cachedImagePathsStartIndex = this.FileStartIndex;
            }
            Mouse.OverrideCursor = null;
        }

        private Tuple<int, int> CalculateRowsAndColumns(double imageWidth, double imageHeight, double availableWidth, double availableHeight)
        {
            // Calculate the number of rows and columns of a given height and width that we can fit into the available space
            int columns = Convert.ToInt32(Math.Floor(availableWidth / imageWidth));
            int rows = (imageHeight > 0) ? Convert.ToInt32(Math.Floor(availableHeight / imageHeight)) : 1;
            return new Tuple<int, int>(rows, columns);
        }
    }
}