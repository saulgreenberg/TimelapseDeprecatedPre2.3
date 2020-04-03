using System;

using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using ImageProcessor;
using ImageProcessor.Imaging.Formats;

namespace Timelapse.Dialog
{
    ///// <summary>
    ///// Interaction logic for ImageAdjuster.xaml
    /// </summary>
    public partial class ImageAdjuster : Window, IDisposable
    {
        private byte[] photoBytes;
        MemoryStream inStream;
        private int Contrast = 0;
        private int Brightness = 0;
        private bool IsEdgeDetetection = false;
        private bool IsSharpened = false;
        private bool Processing = false;
        private bool isDisposed;
        public Image ManipulatedImage { get; set; }

        string manipulatedImagePath = String.Empty;

        public string ManipulatedImagePath
        {
            get
            {
                return manipulatedImagePath;
            }
            set
            {
                manipulatedImagePath = value;
                if (String.IsNullOrEmpty(value))
                {
                    this.photoBytes = null;
                    this.inStream = null;
                }
                else
                {
                    this.photoBytes = File.ReadAllBytes(ManipulatedImagePath);
                    this.inStream = new MemoryStream(this.photoBytes);
                    this.UpdateImage();
                }
            }
        }

        public ImageAdjuster(Window owner)
        {
            InitializeComponent();
            this.Owner = owner;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.Left = this.Owner.Left + this.Owner.Width - this.Width + 20;
            this.Top = this.Owner.Top - 20;
            LoadFiles();
            await UpdateImage().ConfigureAwait(true);
        }

        private async Task<BitmapFrame> Doit()
        {
            if (inStream == null)
            {
                return null;
            }
            return await Task.Run(() =>
            {
                using (MemoryStream outStream = new MemoryStream())
                {
                    // Initialize the ImageFactory using the overload to preserve EXIF metadata.
                    using (ImageFactory imageFactory = new ImageFactory(preserveExifData: false))
                    {
                        if (this.IsEdgeDetetection)
                        {
                            // Load, resize, set the format and quality and save an image.
                            ImageProcessor.Imaging.Filters.EdgeDetection.ScharrEdgeFilter edger = new ImageProcessor.Imaging.Filters.EdgeDetection.ScharrEdgeFilter();
                            imageFactory.Load(inStream)
                                        .DetectEdges(edger)
                                        .Contrast(this.Contrast)
                                        .Brightness(this.Brightness)
                                        .Save(outStream);
                        }
                        else if (this.IsSharpened)
                        {
                            ImageProcessor.Imaging.GaussianLayer gaussian = new ImageProcessor.Imaging.GaussianLayer(5, 3, 0);
                            // Load, resize, set the format and quality and save an image.
                            imageFactory.Load(inStream)
                                        .GaussianSharpen(gaussian)
                                        .Contrast(this.Contrast)
                                        .Brightness(this.Brightness)
                                        .Save(outStream);
                        }
                        else
                        {
                            // Load, resize, set the format and quality and save an image.
                            imageFactory.Load(inStream)
                                        .Contrast(this.Contrast)
                                        .Brightness(this.Brightness)
                                        .Save(outStream);
                        }
                    }
                    // Return the stream as a bitmap that can be used in Image.Source
                    return BitmapFrame.Create(outStream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                }
            }).ConfigureAwait(true);
        }

        private async Task UpdateImage()
        {
            if (Processing)
            {
                return;
            }
            this.Processing = true;
            this.ManipulatedImage.Source = await Doit().ConfigureAwait(true);
            Processing = false;
        }

        private async void ImageSliders_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            this.Contrast = Convert.ToInt32(ContrastSlider.Value);
            this.Brightness = Convert.ToInt32(BrightnessSlider.Value);
            await UpdateImage().ConfigureAwait(true);
        }

        private void ImageSliders_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            this.ImageSliders_ValueChanged(null, null);
        }

        private async void RadioButtons_CheckChanged(object sender, RoutedEventArgs e)
        {
            this.IsEdgeDetetection = CBEdges.IsChecked == true;
            this.IsSharpened = CBSharpen.IsChecked == true;
            await UpdateImage().ConfigureAwait(true);
        }

        private void LoadFiles()
        {
            CBEdges.Checked += RadioButtons_CheckChanged;
            CBSharpen.Checked += RadioButtons_CheckChanged;
            CBNone.Checked += RadioButtons_CheckChanged;

            ContrastSlider.Maximum = 75;
            ContrastSlider.Minimum = -75;
            ContrastSlider.ValueChanged += ImageSliders_ValueChanged;

            BrightnessSlider.Maximum = 75;
            BrightnessSlider.Minimum = -75;
            BrightnessSlider.ValueChanged += ImageSliders_ValueChanged;

        }

        private async void Window_Unloaded(object sender, RoutedEventArgs e)
        {
            this.Brightness = 0;
            this.Contrast = 0;
            this.IsEdgeDetetection = false;
            this.IsSharpened = false;
            await UpdateImage().ConfigureAwait(true);
            this.Dispose();
        }

        private void ButtonReset_Click(object sender, RoutedEventArgs e)
        {
            BrightnessSlider.Value = 0;
            ContrastSlider.Value = 0;
            CBNone.IsChecked = true;
        }


        // Dispose() calls Dispose(true)
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        // The bulk of the clean-up code is implemented in Dispose(bool)
        protected virtual void Dispose(bool disposing)
        {
            if (isDisposed) return;

            if (disposing)
            {
                // free managed resources
                this.inStream.Dispose();
            }
            isDisposed = true;
        }
        ~ImageAdjuster()
        {
            Dispose(false);
        }
    }
}
