using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace Timelapse.Controls
{
    /// <summary>
    /// Interaction logic for ClickableImage.xaml
    /// </summary>
    public partial class ClickableImage : UserControl
    {
        private const double DESIREDWIDTH = 256;
        private Point point = new Point(0, 0);

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
                    this.point.X = this.Image.Width;
                    if (this.Image.Source.Width != 0)
                    {
                        this.point.Y = this.Image.Width * this.Image.Source.Height / this.Image.Source.Width;
                    }
                }
                return this.point;
            }
        }

        public string Path { get; set; }

        // Text associated with the image will be overlayed atop the image
        public string Text
        {
            get { return this.TextBlock.Text; }
            set { this.TextBlock.Text = value; }
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
            }
        }

        private Point mouseDownCoords = new Point(0, 0);

        // Constructors: If width isn't specified, it uses the internal constant DESIREDWIDTH
        public ClickableImage(double width)
        {
            this.InitializeComponent();
            this.DesiredRenderWidth = width;
        }
        public ClickableImage() : this(DESIREDWIDTH)
        {
        }

        // Set the source image
        public void Source(string uri)
        {
            BitmapImage b = new BitmapImage();
            b.BeginInit();
            b.DecodePixelWidth = Convert.ToInt32(this.DesiredRenderWidth);
            b.CacheOption = BitmapCacheOption.OnLoad;
            b.UriSource = new Uri(uri);
            b.EndInit();

            this.Image.Source = b;
        }

        public void Rerender(double width)
        {
            this.DesiredRenderWidth = width;
            this.Source(this.Path);
        }
        private void Grid_LeftMouseButtonUp(object sender, MouseButtonEventArgs e)
        {
            int tolerance = 10;
            if (e.GetPosition(this).X > this.mouseDownCoords.X - tolerance && e.GetPosition(this).X < this.mouseDownCoords.X + tolerance && e.GetPosition(this).Y > this.mouseDownCoords.Y - tolerance && e.GetPosition(this).Y < this.mouseDownCoords.Y + tolerance)
            {
                this.CheckBox.IsChecked = !this.CheckBox.IsChecked;
            }
        }

        private void Grid_LeftMouseButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.mouseDownCoords = e.GetPosition(this);
        }
    }
}
