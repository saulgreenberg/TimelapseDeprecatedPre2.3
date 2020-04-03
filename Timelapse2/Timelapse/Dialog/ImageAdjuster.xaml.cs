using System;

using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ImageProcessor;
using ImageProcessor.Imaging.Formats;
using Timelapse.EventArguments;
using Timelapse.Images;

namespace Timelapse.Dialog
{
    ///// <summary>
    ///// Interaction logic for ImageAdjuster.xaml
    /// </summary>
    public partial class ImageAdjuster : Window, IDisposable
    {



        // Holds the image as a reusable stream so we don't have to regenerate it every time
        MemoryStream inImageStream;

        // Store the various parameters that indicate how the image should be adjusted
        private int Contrast = 0;
        private int Brightness = 0;
        private bool DetectEdges = false;
        private bool Sharpen = false;

        // State information
        private bool Processing = false;
        private bool AbortUpdate = false;
        private bool isDisposed;

        DispatcherTimer SheduleImageProcessingUpdate = new DispatcherTimer();
        


        // A pointer to the image that should be displayed on the Markable Canvas
        // The image's source will be replaced after the manipulation
        public Image ManipulatedImage { get; set; }

        // The path to the image to manipulate, which we will use to get the image as a stream.
        // Ideally, we would do this from the ManipulatedImage source, but I don't know how to do that.
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
                    this.inImageStream = null;
                }
                else
                {
                    this.inImageStream = new MemoryStream(File.ReadAllBytes(ManipulatedImagePath));
                    // TODO THIS SHOULD NOT BE HERE
                    // TODO THIS SHOULD NOT BE HERE
                    // TODO THIS SHOULD NOT BE HERE
                    // TODO THIS SHOULD NOT BE HERE
                    this.UpdateImage();
                }
            }
        }

        // Constructor.
        public ImageAdjuster(Window owner)
        {
            InitializeComponent();
            this.Owner = owner;
        }

        // TODO Position the window on the display 
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            int sliderMinMax = 75;
            // Position the window
            this.Left = this.Owner.Left + this.Owner.Width - this.Width - 10;
            this.Top = this.Owner.Top + 3;

            // Configure the sliders and timer
            ContrastSlider.Maximum = sliderMinMax;
            ContrastSlider.Minimum = -sliderMinMax;
            BrightnessSlider.Maximum = sliderMinMax;
            BrightnessSlider.Minimum = -sliderMinMax;
            this.SheduleImageProcessingUpdate.Interval = TimeSpan.FromSeconds(0.1);

            // Register the various control callbacks. 
            CBEdges.Checked += RadioButtons_CheckChanged;
            CBSharpen.Checked += RadioButtons_CheckChanged;
            CBNone.Checked += RadioButtons_CheckChanged;
            ContrastSlider.ValueChanged += ImageSliders_ValueChanged;
            BrightnessSlider.ValueChanged += ImageSliders_ValueChanged;
            this.SheduleImageProcessingUpdate.Tick += this.SheduleImageProcessingUpdate_Tick;

            // TO DO: EVENTS IMAGE CHANGED EVENT RECEIVED FROM MARKABLE CANVAS
            // TO DO: EVENTS
            // TO DO: EVENTS
            Util.GlobalReferences.MainWindow.MarkableCanvas.ImageChanged += this.ImageToStream;

            // Update the image to the current parameters
            // TO  DO: DO WE NEED THIS HERE? 
            // TO  DO: DO WE NEED THIS HERE
            // TO  DO: DO WE NEED THIS HERE
            // TO  DO: DO WE NEED THIS HERE
            await UpdateImage().ConfigureAwait(true);
        }

        private async void SheduleImageProcessingUpdate_Tick(object sender, EventArgs e)
        {
            if (this.Processing)
            {
                return;
            }
            System.Diagnostics.Debug.Print("Tick");
            await this.UpdateAndProcessImage().ConfigureAwait(true);
            this.SheduleImageProcessingUpdate.Stop();
        }

        // TO DO: EVENTS IMAGE CHANGED EVENT RECEIVED FROM MARKABLE CANVAS
        // TO DO: EVENTS IMAGE CHANGED EVENT RECEIVED FROM MARKABLE CANVAS
        // TO DO: EVENTS IMAGE CHANGED EVENT RECEIVED FROM MARKABLE CANVAS
        private void ImageToStream(object sender, ImageChangedEventArgs e)
        {
            System.Diagnostics.Debug.Print("New Image " + e.ImagePath + " " + e.IsAnImageDisplayed.ToString());
        }

        // Process the current image (held in inImageStream) using the various image processing parameters
        private async Task UpdateImage()
        {
            // If we are already processing the image, abort.
            if (this.Processing)
            {
                return;
            }
            System.Diagnostics.Debug.Print(BrightnessSlider.Value.ToString());
            this.Processing = true;
            this.ManipulatedImage.Source = await ImageProcess.StreamToImageProcessedBitmap(this.inImageStream, this.Brightness, this.Contrast, this.Sharpen, this.DetectEdges).ConfigureAwait(true);
            this.Processing = false;
        }

        // Update the image processing parameters to those in the checkboxes and sliders
        // Then update the image according to those parameters.
        private async Task UpdateAndProcessImage()
        {
            // If its processing, we defer resetting anything as we may get an update later (e.g., via the timer)
            if (this.AbortUpdate || this.Processing)
            {
                // Don't do any updating.
                return;
            }

            // We only have to update if the final values are actually different from the current values
            if (this.Contrast != Convert.ToInt32(ContrastSlider.Value) || this.Brightness != Convert.ToInt32(BrightnessSlider.Value) || this.DetectEdges != CBEdges.IsChecked || this.Sharpen != CBSharpen.IsChecked)
            {
                this.Contrast = Convert.ToInt32(ContrastSlider.Value);
                this.Brightness = Convert.ToInt32(BrightnessSlider.Value);
                this.DetectEdges = CBEdges.IsChecked == true;
                this.Sharpen = CBSharpen.IsChecked == true;
                await UpdateImage().ConfigureAwait(true);
            }
        }

        // Reset the controls  to their defaults, and then update the image processing parameters and image to match the controls
        private async Task ResetControls()
        {
            // We don't update anything until after we reset the sliders and checkbox, as otherwise 
            // it would lead to an update for each change
            this.AbortUpdate = true;
            BrightnessSlider.Value = 0;
            ContrastSlider.Value = 0;
            CBNone.IsChecked = true;
            this.AbortUpdate = false;

            await UpdateAndProcessImage().ConfigureAwait(true);
        }

        #region Callbacks - image processing parameters altered
        // Update allimage processing parameters whenever the user changes any of them
        private async void RadioButtons_CheckChanged(object sender, RoutedEventArgs e)
        {
            await UpdateAndProcessImage().ConfigureAwait(true);
        }

        // Update all image processing parameters and then update the image based on that
        private async void ImageSliders_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            this.SheduleImageProcessingUpdate.Start();
            await UpdateAndProcessImage().ConfigureAwait(true);
        }

        private async void ImageSliders_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            this.SheduleImageProcessingUpdate.Start();
            await UpdateAndProcessImage().ConfigureAwait(true);
        }

        private async void ButtonReset_Click(object sender, RoutedEventArgs e)
        {
            await ResetControls().ConfigureAwait(true);
        }

        private async void Window_Unloaded(object sender, RoutedEventArgs e)
        {
            await ResetControls().ConfigureAwait(true);
            this.Dispose();
        }
        #endregion

        #region Disposing
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
                this.inImageStream.Dispose();
            }
            isDisposed = true;
        }

        ~ImageAdjuster()
        {
            Dispose(false);
        }
        #endregion 
    }
}
