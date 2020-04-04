using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Threading;
using Timelapse.Controls;
using Timelapse.EventArguments;

namespace Timelapse.Images
{
    // This portion of the Markable Canvas 
    // - handles image procesing adjustments as requested by events sent via the ImageAdjuster.
    // - generates events indicating image state to be consumed by the Image Adjuster to adjust its own state (e.g., disabled, reset, etc).
    public partial class MarkableCanvas : Canvas
    {
        #region EventHandler definitions
        // Whenever an image state is changed, raise an event (to be consumed by ImageAdjuster)
        public event EventHandler<ImageStateEventArgs> ImageStateChanged; // raise when an image state is changed (to be consumed by ImageAdjuster)
        #endregion

        #region Private variables
        // State information - whether the current image is being processed
        private bool Processing = false;

        // When started, the timer tries to updates image processing to ensure that the last image processing values are applied
        private readonly DispatcherTimer timerImageProcessingUpdate = new DispatcherTimer();

        // image processing parameters
        private int contrast;
        private int brightness;
        private bool detectEdges;
        private bool sharpen;

        // We track the last parameters used, as if they haven't changed we won't update the image
        private int lastContrast = 0;
        private int lastBrightness = 0;
        private bool lastDetectEdges = false;
        private bool lastSharpen = false;
        #endregion

        #region Consume and handle image processing events
        // This should be invoked by the Constructor to initialize aspects of this partial class
        private void InitializeImageAdjustment()
        {
            // When started, ensures that the final image processing parameters are applied to the image
            this.timerImageProcessingUpdate.Interval = TimeSpan.FromSeconds(0.1);
            this.timerImageProcessingUpdate.Tick += this.TimerImageProcessingUpdate_Tick;
        }

        // Receive an event containing new image processing parameters.
        // Store these parameters and then try to update the image
        public async void AdjustImage_EventHandler(object sender, ImageAdjusterEventArgs e)
        {
            if (e == null)
            {
                return;
            }
            if (e.Contrast == this.lastContrast && e.Brightness == this.lastBrightness && e.DetectEdges == this.lastDetectEdges && e.Sharpen == this.lastSharpen)
            {
                // No change from the last time we processed an image, so don't bother doing anything
                return;
            }
            this.contrast = e.Contrast;
            this.brightness = e.Brightness;
            this.detectEdges = e.DetectEdges;
            this.sharpen = e.Sharpen;
            this.timerImageProcessingUpdate.Start();
            await UpdateAndProcessImage().ConfigureAwait(true);
        }

        // Because an event may come in while an image is being processed, the timer
        // will try to continue the processing the image with the latest image processing parameters (if any) 
        private async void TimerImageProcessingUpdate_Tick(object sender, EventArgs e)
        {
            if (this.Processing)
            {
                return;
            }
            if (this.contrast != this.lastContrast || this.brightness != this.lastBrightness || this.detectEdges != this.lastDetectEdges || this.sharpen != this.lastSharpen)
            {
                await this.UpdateAndProcessImage().ConfigureAwait(true);
            }
            this.timerImageProcessingUpdate.Stop();
        }

        // Update the image according to the image processing parameters.
        private async Task UpdateAndProcessImage()
        {
            try
            {
                // If its processing, or if anything is null, we defer resetting anything. Note that we may get an update later (e.g., via the timer)
                DataEntryHandler handler = Util.GlobalReferences.MainWindow?.DataHandler;
                if (this.Processing || handler?.ImageCache?.CurrentDifferenceState == null || handler?.FileDatabase == null)
                {
                    return;
                }

                string path = handler.ImageCache.Current.GetFilePath(handler.FileDatabase.FolderPath);
                if (File.Exists(path) == false)
                {
                    this.OnImageStateChanged(new ImageStateEventArgs(false, false)); //  Signal change in image state (consumed by ImageAdjuster)
                    return;
                }

                this.Processing = true;
                using (MemoryStream imageStream = new MemoryStream(File.ReadAllBytes(path)))
                {
                    this.lastBrightness = this.brightness;
                    this.lastContrast = this.contrast;
                    this.lastSharpen = this.sharpen;
                    this.lastDetectEdges = this.detectEdges;
                    this.ImageToDisplay.Source = await ImageProcess.StreamToImageProcessedBitmap(imageStream, this.brightness, this.contrast, this.sharpen, this.detectEdges).ConfigureAwait(true);
                }
            }
            catch
            {
                // Disable the ImageAdjuster for this image if there is a problem
                this.OnImageStateChanged(new ImageStateEventArgs(false, false)); //  Signal change in image state (consumed by ImageAdjuster)
            }
            this.Processing = false;
        }
        #endregion 

        #region Generate ImageStateChange event
        // Generate an event indicating the image state. To be consumed by the Image Adjuster to adjust its own state (e.g., disabled, reset, etc).
        private void GenerateImageStateChangeEvent(bool isNewImage, bool isPrimaryImage)
        {
            this.OnImageStateChanged(new ImageStateEventArgs(isNewImage, isPrimaryImage)); //  Signal change in image state (consumed by ImageAdjuster)
        }
        protected virtual void OnImageStateChanged(ImageStateEventArgs e)
        {
            ImageStateChanged?.Invoke(this, e);
        }
        #endregion
    }
}
