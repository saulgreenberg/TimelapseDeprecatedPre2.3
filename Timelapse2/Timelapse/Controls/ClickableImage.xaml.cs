using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

        public void Rerender(double width)
        {
            this.DesiredRenderWidth = width;
            this.Image.Source = this.ImageRow.LoadBitmap(this.RootFolder, Convert.ToInt32(this.DesiredRenderWidth), Images.ImageDisplayIntent.Persistent);
            this.TextBlock.Text = this.ImageRow.FileName;
        }
        private void Grid_LeftMouseButtonUp(object sender, MouseButtonEventArgs e)
        {
            return;
            int tolerance = 10;
            if (e.GetPosition(this).X > this.mouseDownCoords.X - tolerance && e.GetPosition(this).X < this.mouseDownCoords.X + tolerance && e.GetPosition(this).Y > this.mouseDownCoords.Y - tolerance && e.GetPosition(this).Y < this.mouseDownCoords.Y + tolerance)
            {
                this.CheckBox.IsChecked = !this.CheckBox.IsChecked;
            }
        }

        private void Grid_LeftMouseButtonDown(object sender, MouseButtonEventArgs e)
        {
            return;
            if (e.ClickCount == 2)
            {
                System.Diagnostics.Debug.Print("DoubleClick! " + this.ImageRow.FileName);
            }
            this.mouseDownCoords = e.GetPosition(this);
        }
    }
}
