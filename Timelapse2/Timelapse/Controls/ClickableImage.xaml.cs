using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Timelapse.Database;
using Timelapse.Enums;

namespace Timelapse.Controls
{
    /// <summary>
    /// Clickable Image User Control, which is used to fill each cell in the Clickable ImagesGrid
    /// </summary>
    public partial class ClickableImage : UserControl
    {
        #region Public Properties
        public double DesiredRenderWidth
        {
            get
            {
                return this.Image.Width;
            }
            set
            {
                this.Image.Width = value;
                this.Image.MinWidth = value;
                this.Image.MaxWidth = value;
            }
        }
        public Point DesiredRenderSize
        {
            get
            {
                if (this.Image == null || this.Image.Source == null)
                {
                    this.point.X = 0;
                    this.point.Y = 0;
                }
                else
                {
                    this.point.X = this.Image.Source.Width;
                    if (this.Image.Source.Width != 0)
                    {
                        this.point.Y = this.Image.Width * this.Image.Source.Height / this.Image.Source.Width;
                    }
                }
                return this.point;
            }
        }

        public int FileTableIndex { get; set; }

        public ImageRow ImageRow { get; set; }
        // Whether the Checkbox is checked

        private bool isSelected = false;
        public bool IsSelected
        {
            get
            {
                return this.isSelected;
            }
            set
            {
                this.isSelected = value;
                // Show or hide the checkmark 
                if (this.isSelected)
                {
                    this.Cell.Background = this.selectedBrush;
                    this.Checkmark.Text = "\u2713"; // Checkmark in unicode
                    this.Checkmark.Background.Opacity = 0.7;
                }
                else
                {
                    this.Cell.Background = this.unselectedBrush;
                    this.Checkmark.Text = "   ";
                    this.Checkmark.Background.Opacity = 0.35;
                }
            }
        }

        // Path is the RelativePath/FileName of the image file
        public string Path
        {
            get
            {
                return (this.ImageRow == null) ? String.Empty : System.IO.Path.Combine(this.ImageRow.RelativePath, this.ImageRow.File);
            }
        }

        public string RootFolder { get; set; }

        public double TextFontSize
        {
            set
            {
                this.ImageNameText.FontSize = value;
                this.EpisodeText.FontSize = value;
            }
        }
        #endregion

        #region Private Variables
        private Point point = new Point(0, 0);
        private readonly Brush unselectedBrush = Brushes.Black;
        private readonly Brush selectedBrush = Brushes.LightBlue;
        #endregion

        // Constructors: Width is the desired width of the image
        public ClickableImage(double width)
        {
            this.InitializeComponent();
            this.DesiredRenderWidth = width;
            this.RootFolder = String.Empty;
        }

        // Rerender the image to the given width
        public Double Rerender(FileTable fileTable, double width, int state, int fileIndex)
        {
            this.DesiredRenderWidth = width;
            BitmapSource bf = this.ImageRow.GetBitmapFromFile(this.RootFolder, Convert.ToInt32(this.DesiredRenderWidth), ImageDisplayIntentEnum.Persistent, out bool isCorruptOrMissing);
            this.Image.Source = bf;

            // Render the episode text if needed
            this.DisplayEpisodeTextIfWarranted(fileTable, fileIndex, state);

            // A bit of a hack to calculate the height on stock error images. When the loaded image is one of the ones held in the resource,
            // the size is in pixels rather than in device-independent pixels. To get the correct size,
            // we know that these images are 640x480, so we just multiple the desired width by .75 (i.e., 480/640)to get the desired height.
            if (bf == Constant.ImageValues.FileNoLongerAvailable.Value || bf == Constant.ImageValues.Corrupt.Value)
            {
                this.Image.Height = 0.75 * width;
            }
            else
            {
                this.Image.Height = bf.PixelHeight;
            }
            return this.Image.Height;
        }

        // Get and display the episode text if various conditions are met
        public void DisplayEpisodeTextIfWarranted(FileTable fileTable, int fileIndex, int state)
        {
            if (Episodes.ShowEpisodes)
            {
                // A descriptive string: the filename without the extention, plus the time in HH:MM
                // This was on request from a user, who needed to scan for the first/last image in a timelapse capture sequence
                string timeInHHMM = (this.ImageRow.Time.Length > 3) ? this.ImageRow.Time.Remove(this.ImageRow.Time.Length - 3) : String.Empty;

                string filename = System.IO.Path.GetFileNameWithoutExtension(this.ImageRow.File);
                filename = this.ShortenFileNameIfNeeded(filename, state);
                this.ImageNameText.Text = filename + " (" + timeInHHMM + ")";

                if (Episodes.EpisodesDictionary.ContainsKey(fileIndex) == false)
                {
                    Episodes.EpisodeGetEpisodesInRange(fileTable, fileIndex);
                }
                Tuple<int, int> episode = Episodes.EpisodesDictionary[fileIndex];
                if (episode.Item1 == int.MaxValue)
                {
                    this.EpisodeText.Text = "\u221E";
                }
                else
                {
                    this.EpisodeText.Text = (episode.Item2 == 1) ? "Single" : String.Format("{0}/{1}", episode.Item1, episode.Item2);
                }
                this.EpisodeText.Foreground = (episode.Item1 == 1) ? Brushes.Red : Brushes.Black;
                this.EpisodeText.FontWeight = (episode.Item1 == 1 && episode.Item2 != 1) ? FontWeights.Bold : FontWeights.Normal;
            }
            this.EpisodeText.Visibility = Episodes.ShowEpisodes ? Visibility.Visible : Visibility.Hidden;
            this.ImageNameText.Visibility = this.EpisodeText.Visibility;
        }

        // Most images have a black bar at its bottom and top. We want to aligh 
        // the checkbox / text label to be just outside that black bar. This is guesswork, 
        // but the margin should line up within reason most of the time
        // Also, values are hard-coded vs. dynamic. Ok until we change the standard width or layout of the display space.
        public void AdjustMargin(int state)
        {
            int margin = 0;
            switch (state)
            {
                case 2:
                    margin = 8;
                    break;
                case 3:
                    margin = 6;
                    break;
                case 1:
                default:
                    margin = 12;
                    break;
            }
            this.ImageNameText.Margin = new Thickness(0, margin, margin, 0);
            this.EpisodeText.Margin = this.ImageNameText.Margin;
            this.CheckboxViewbox.Margin = new Thickness(margin, margin, 0, 0);
        }

        // Return a shortened version of the file name so that it fits in the available space 
        // Note that we left trim it, and we show an ellipsis on the left side if it doesn't fit.
        // Also, values are hard-coded vs. dynamic. Ok until we change the standard width or layout of the display space.
        public string ShortenFileNameIfNeeded(string filename, int state)
        {
            string ellipsis = "\u2026";
            switch (state)
            {
                case 2:
                case 3:
                    return filename.Length <= 10 ? filename : ellipsis + filename.Remove(0, filename.Length - 10);
                case 1:
                default:
                    return filename.Length <= 20 ? filename : ellipsis + filename.Remove(0, filename.Length - 20);
            }
        }
    }
}
