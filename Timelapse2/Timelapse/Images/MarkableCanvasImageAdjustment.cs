using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Threading;
using Timelapse.Controls;
using Timelapse.Dialog;
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
        private bool useGamma;
        private float gammaValue;

        // We track the last parameters used, as if they haven't changed we won't update the image
        private int lastContrast = 0;
        private int lastBrightness = 0;
        private bool lastDetectEdges = false;
        private bool lastSharpen = false;
        private bool lastUseGamma = false;
        private float lastGammaValue = 1;
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
                // Shouldn't happen, but...
                return;
            }

            string path = MarkableCanvas.GetFileFromDataHandlerIfExists();
            if (String.IsNullOrEmpty(path))
            {
                // The file cannot be opened or is not displayable. 
                // Signal change in image state, which essentially says there is no displayable image to adjust (consumed by ImageAdjuster)
                this.OnImageStateChanged(new ImageStateEventArgs(false, false)); //  Signal change in image state (consumed by ImageAdjuster)
                return;
            }

            if (e.OpenExternalViewer)
            {
                // If its a command to open the external viewer, we do it regardless of the processing state.
                this.OpenExternalViewer(path);
                return;
            }

            if (e.Contrast == this.lastContrast && e.Brightness == this.lastBrightness && e.DetectEdges == this.lastDetectEdges && e.Sharpen == this.lastSharpen && e.UseGamma == this.lastUseGamma && e.GammaValue == this.lastGammaValue)
            {
                // No change from the last time we processed an image, so don't bother doing anything
                System.Diagnostics.Debug.Print("No change in image");
                return;
            }
            this.contrast = e.Contrast;
            this.brightness = e.Brightness;
            this.detectEdges = e.DetectEdges;
            this.sharpen = e.Sharpen;
            this.useGamma = e.UseGamma;
            this.gammaValue = e.GammaValue;
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
            if (this.contrast != this.lastContrast || this.brightness != this.lastBrightness || this.detectEdges != this.lastDetectEdges || this.sharpen != this.lastSharpen || this.lastUseGamma != this.useGamma || this.lastGammaValue != this.gammaValue)
            {
                await this.UpdateAndProcessImage().ConfigureAwait(true);
            }
            this.timerImageProcessingUpdate.Stop();
        }

        // Update the image according to the image processing parameters.
        private async Task UpdateAndProcessImage()
        {
            // If its processing the image, try again later (via the time),
            if (this.Processing)
            {
                return;
            }
            try
            {
                string path = MarkableCanvas.GetFileFromDataHandlerIfExists();
                if (String.IsNullOrEmpty(path))
                {
                    // If we cannot get a valid file, there is no image to manipulate. 
                    // So abort and signal a change in image state that says there is no displayable image to adjust (consumed by ImageAdjuster)
                    this.OnImageStateChanged(new ImageStateEventArgs(false, false));
                }

                this.Processing = true;
                using (MemoryStream imageStream = new MemoryStream(File.ReadAllBytes(path)))
                {
                    // Remember the currently selected image processing states, so we can compare them later for changes
                    this.lastBrightness = this.brightness;
                    this.lastContrast = this.contrast;
                    this.lastSharpen = this.sharpen;
                    this.lastDetectEdges = this.detectEdges;
                    this.lastUseGamma = this.useGamma;
                    this.lastGammaValue = this.gammaValue;
                    this.ImageToDisplay.Source = await ImageProcess.StreamToImageProcessedBitmap(imageStream, this.brightness, this.contrast, this.sharpen, this.detectEdges, this.useGamma, this.gammaValue).ConfigureAwait(true);
                }
            }
            catch
            {
                // We failed on this image. To avoid this happening again,
                // Signal change in image state, which essentially says there is no adjustable image (consumed by ImageAdjuster)
                this.OnImageStateChanged(new ImageStateEventArgs(false, false));
            }
            this.Processing = false;
        }

        private void OpenExternalViewer(string path)
        {
            // Open the file in a file viewer
            try
            {
                // Show the file in a picture viewer
                // Create a process that will try to show the file
                using (Process process = new Process())
                {
                    process.StartInfo.UseShellExecute = true;
                    process.StartInfo.RedirectStandardOutput = false;
                    process.StartInfo.FileName = path;
                    process.Start();
                }
            }
            catch
            {
                // Can't open the image file
                MessageBox messageBox = new MessageBox("Can't open a photo viewer.", Util.GlobalReferences.MainWindow);
                messageBox.Message.Icon = System.Windows.MessageBoxImage.Error;
                messageBox.Message.Problem = "You don't have a default program set up to display a photo viewer  " + path;
                messageBox.Message.Solution = "Set up a photo viewer in your Windows Settings." + Environment.NewLine;
                messageBox.Message.Solution += "Go to 'Default apps', select 'Photo Viewer' and choose a desired photo viewer.";
                messageBox.ShowDialog();
            }
            return;
        }

        // Get a file path from the datahandler. 
        // If we can't, or if it does not exist, return String.Empty
        private static string GetFileFromDataHandlerIfExists()
        {
            string path = String.Empty;
            // If anything is null, we defer resetting anything. Note that we may get an update later (e.g., via the timer)
            DataEntryHandler handler = Util.GlobalReferences.MainWindow?.DataHandler;
            if (handler?.ImageCache?.CurrentDifferenceState != null && handler?.FileDatabase != null)
            {
                // Get the path
                path = handler.ImageCache.Current.GetFilePath(handler.FileDatabase.FolderPath);
            }
            return File.Exists(path) ? path : String.Empty;
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
