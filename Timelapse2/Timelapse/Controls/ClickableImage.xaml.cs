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
                this.TextBlock.FontSize = (value >= 256) ? 16 : 10;
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

        public ImageRow ImageRow { get; set; }

        public string RootFolder { get; set; }

        // Path is RelativePath/FileName
        public string Path
        {
            get
            {
                return (this.ImageRow == null) ? String.Empty : System.IO.Path.Combine(this.ImageRow.RelativePath, this.ImageRow.FileName);
            }
        }

        // Whether the checkbox is checked
        public bool IsSelected
        {
            get
            {
                return this.CheckBox.IsChecked == true ? true : false;
            }
            set
            {
                this.CheckBox.IsChecked = value;
                this.Cell.Background = this.CheckBox.IsChecked == true ? this.selectedBrush : this.unselectedBrush;
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
            BitmapSource bf = this.ImageRow.LoadBitmap(this.RootFolder, Convert.ToInt32(this.DesiredRenderWidth), Images.ImageDisplayIntent.Persistent);
            this.Image.Source = bf;
            this.TextBlock.Text = this.ImageRow.FileName;
            this.Image.Height = bf.PixelHeight;
            return bf.PixelHeight;
        }

        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {
            this.IsSelected = this.CheckBox.IsChecked == true;
        }
    }
}
