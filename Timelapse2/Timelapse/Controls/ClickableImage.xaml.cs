using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Timelapse.Database;

namespace Timelapse.Controls
{
    /// <summary>
    /// Interaction logic for ClickableImage.xaml
    /// </summary>
    public partial class ClickableImage : UserControl
    {
        private const double DESIREDWIDTH = 256;
        private Point point = new Point(0, 0);
        private Brush unselectedBrush = Brushes.Black;
        private Brush selectedBrush = Brushes.LightBlue;

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

        public double TextFontSize 
        {
            set
            {
                this.TextBlock.FontSize = value;
            }
        }
        public ImageRow ImageRow { get; set; }

        public string RootFolder { get; set; }

        public int FileTableIndex { get; set; }

        // Path is RelativePath/FileName
        public string Path
        {
            get
            {
                return (this.ImageRow == null) ? String.Empty : System.IO.Path.Combine(this.ImageRow.RelativePath, this.ImageRow.FileName);
            }
        }

        // Whether the checkbox is checked
        private bool isSelected = false;
        public bool IsSelected
        {
            get
            {
                return isSelected;
            }
            set
            {
                isSelected = value;
                // Show or hide the checkmark 
                if (isSelected)
                {
                    this.Cell.Background = this.selectedBrush;
                    this.Checkmark.Text = "\U0001F5F9"; // Checkmark in unicode
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

        private Point mouseDownCoords = new Point(0, 0);

        // Constructors: If width isn't specified, it uses the internal constant DESIREDWIDTH
        public ClickableImage(double width)
        {
            this.InitializeComponent();
            this.DesiredRenderWidth = width;
            this.RootFolder = String.Empty;
        }
        public ClickableImage() : this(DESIREDWIDTH)
        {
        }

        public Double Rerender(double width)
        {
            this.DesiredRenderWidth = width;
            BitmapSource bf = this.ImageRow.LoadBitmap(this.RootFolder, Convert.ToInt32(this.DesiredRenderWidth), Images.ImageDisplayIntent.TransientLoading);
            this.Image.Source = bf;
            this.TextBlock.Text = this.ImageRow.FileName;

            // A bit of a hack to calculate the height on stock error images. When the loaded image is one of the ones held in the resource,
            // the size is in pixels rather than in device-independent pixels. To get the correct size,
            // we know that these images are 640x480, so we just multiple the desired width by .75 (i.e., 480/640)to get the desired height.
            if (bf == Constant.Images.FileNoLongerAvailable.Value || bf == Constant.Images.Corrupt.Value)
            {
                this.Image.Height = 0.75 * width; 
            }
            else
            {
                this.Image.Height = bf.PixelHeight;
            }
            return this.Image.Height; 
        }
    }
}
