﻿using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Timelapse.EventArguments;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    ///// <summary>
    ///// Interaction logic for ImageAdjuster.xaml
    /// </summary>
    public partial class ImageAdjuster : Window
    {
        #region Private variables
        // Store the various parameters that indicate how the image should be adjusted
        private int Contrast = 0;
        private int Brightness = 0;
        private bool DetectEdges = false;
        private bool Sharpen = false;
        private bool UseGamma = false;
        private float GammaValue = 1;

        // State information
        private bool AbortUpdate = false;
        #endregion

        #region Consructor, Loading and Closing
        public ImageAdjuster(Window owner)
        {
            InitializeComponent();
            this.Owner = owner;
        }

        // Position the window on the display 
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {

            // Position the window
            this.Left = this.Owner.Left + this.Owner.Width - this.Width - 10;
            this.Top = this.Owner.Top + 3;

            // Configure the sliders and timer
            int sliderMinMax = 75;
            ContrastSlider.Maximum = sliderMinMax;
            ContrastSlider.Minimum = -sliderMinMax;
            BrightnessSlider.Maximum = sliderMinMax;
            BrightnessSlider.Minimum = -sliderMinMax;
            GammaSlider.Minimum = .1;
            GammaSlider.Maximum = 1.9;
            GammaSlider.Value = GammaValue;

            // Register the various control callbacks. 
            CBEdges.Checked += RadioButtons_CheckChanged;
            CBSharpen.Checked += RadioButtons_CheckChanged;
            CBNone.Checked += RadioButtons_CheckChanged;
            ContrastSlider.ValueChanged += ImageSliders_ValueChanged;
            BrightnessSlider.ValueChanged += ImageSliders_ValueChanged;
            GammaSlider.ValueChanged += ImageSliders_ValueChanged;
        }

        // Reuse the window by changing closing to hiding
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            this.Hide();
        }
        #endregion

        #region Public Methods - Show/Hide
        // Showing the window activates everything as needed
        public new void Show()
        {
            // Receipt of this event from the Markable Canvase provides information used to decide how this control should appear e.g., reset, activated, etc.
            Util.GlobalReferences.MainWindow.MarkableCanvas.ImageStateChanged += this.ConfigureWindowState;
            base.Show();
        }

        // Hiding the window essentially resets everything and deactivates the primary event handler, 
        public new void Hide()
        {
            this.ResetControlsToNeutralValues();
            this.UpdateImageParametersAndGenerateEvent();
            Util.GlobalReferences.MainWindow.MarkableCanvas.ImageStateChanged -= this.ConfigureWindowState;
            base.Hide();
        }
        #endregion

        #region Receive Event and adjust window appearance state- generated by MarkableCanvas
        private void ConfigureWindowState(object sender, ImageStateEventArgs e)
        {
            this.EnableControls(e.IsImageView);
        }

        // Enabel/disable controls as needed and/or change the various control colorings to indicate the primary sections being used
        private void EnableControls(bool enabledState)
        {
            this.IsEnabled = enabledState;

            // Set up the brushes
            Brush enabledGammaBrush = enabledState && (CBGamma.IsChecked == true) ? Brushes.Black : Brushes.Gray;
            Brush enabledOtherBrush = enabledState && (CBGamma.IsChecked == true) ? Brushes.Gray : Brushes.Black;
            Brush isEnabledBrush = enabledState ? Brushes.Black : Brushes.Gray;

            // Provide a more disabled appearance to radio buttons, checkboxes and slider labels 
            this.CBNone.Foreground = enabledState ? enabledOtherBrush : isEnabledBrush;
            this.CBEdges.Foreground = this.CBNone.Foreground;
            this.CBSharpen.Foreground = this.CBNone.Foreground;
            this.BrightnessLabel.Foreground = this.CBNone.Foreground;
            this.ContrastLabel.Foreground = this.CBNone.Foreground;

            this.OtherControlsArea.Background = enabledState && (CBGamma.IsChecked == false) ? Brushes.White : Brushes.WhiteSmoke;
            this.GammaArea.Background = enabledState && (CBGamma.IsChecked == true) ? Brushes.White : Brushes.WhiteSmoke;
            this.CBGamma.Foreground = enabledState ? enabledGammaBrush : isEnabledBrush;

            this.ButtonArea.Background = enabledState ? Brushes.White : Brushes.WhiteSmoke;
            this.ButtonReset.IsEnabled = !IsNeutral();
        }
        #endregion

        #region Update Image Parameters
        // Update the image processing parameters to those in the checkboxes and sliders
        // Then generate an event to inform the Markable Canvase to update the image according to those paraemeters
        private void UpdateImageParametersAndGenerateEvent()
        {
            this.UpdateImageParametersAndGenerateEvent(false);
        }
        private void UpdateImageParametersAndGenerateEvent(bool forceUpdate)
        {
            //this.AdjustLook();
            this.EnableControls(true);
            if (this.AbortUpdate)
            {
                return;
            }

            // We only update everything and send the event if the final values differ from the current values
            if (forceUpdate || (this.Contrast != Convert.ToInt32(ContrastSlider.Value) || this.Brightness != Convert.ToInt32(BrightnessSlider.Value) || this.GammaValue != this.GammaSlider.Value
                || this.DetectEdges != CBEdges.IsChecked || this.Sharpen != CBSharpen.IsChecked || this.UseGamma != this.CBGamma.IsChecked))
            {
                this.Contrast = Convert.ToInt32(ContrastSlider.Value);
                this.Brightness = Convert.ToInt32(BrightnessSlider.Value);
                this.DetectEdges = CBEdges.IsChecked == true;
                this.Sharpen = CBSharpen.IsChecked == true;
                this.UseGamma = CBGamma.IsChecked == true;
                this.GammaValue = (float)(this.GammaSlider.Maximum - this.GammaSlider.Value);

                // Generate an event to inform the Markable Canvase to update the image. 
                // Note that the last argument (to invoke an external image viewer) is always false, as that is handeld separately
                this.OnImageProcessingParametersChanged(new ImageAdjusterEventArgs(this.Brightness, this.Contrast, this.Sharpen, this.DetectEdges, this.UseGamma, this.GammaValue, false, forceUpdate));
            }
            this.ButtonReset.IsEnabled = !IsNeutral();
        }

        // Reset the controls  to their neutral values (i.e. to restore the original image)
        private void ResetControlsToNeutralValues()
        {
            // We don't update anything until after we reset the sliders and checkbox, as otherwise it would generate an event for each change
            this.AbortUpdate = true;
            this.BrightnessSlider.Value = 0;
            this.ContrastSlider.Value = 0;
            this.CBNone.IsChecked = true;
            this.GammaSlider.Value = 1;
            this.CBGamma.IsChecked = false;
            this.AbortUpdate = false;
            this.EnableControls(true);
            //this.AdjustLook();
        }
        #endregion

        #region UI Callbacks - image processing parameters altered in the UI
        // Send keboard events to the markable canvas, mostly so that the navigation keys will work.
        private void Control_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            Keyboard.Focus(GlobalReferences.MainWindow.MarkableCanvas);
            GlobalReferences.MainWindow.MarkableCanvas.RaiseEvent(e);
        }

        private void ButtonImageViewer_Click(object sender, RoutedEventArgs e)
        {
            // Generate an event to inform the Markable Canvas, in this case to invoke the file viewer 
            // The only thing of importance in this call is that the final argument (openExternalViewer) is true. The other values will be ignored. 
            this.OnImageProcessingParametersChanged(new ImageAdjusterEventArgs(this.Brightness, this.Contrast, this.Sharpen, this.DetectEdges, this.UseGamma, this.GammaValue, true, false));
        }

        // Update allimage processing parameters whenever the user changes any of them
        private void RadioButtons_CheckChanged(object sender, RoutedEventArgs e)
        {
            // Set the gamma checkbox to reflect that a radiobutton option was pressed
            this.CBGamma.IsChecked = false;
            this.UpdateImageParametersAndGenerateEvent();
        }

        // Update all image processing parameters and then update the image based on that
        private void ImageSliders_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Set the gamma checkbox to reflect what slider was moved
            this.CBGamma.IsChecked = (sender is Slider slider && slider == this.GammaSlider);
            this.UpdateImageParametersAndGenerateEvent();
        }
        private void CBGamma_CheckedChanged(object sender, RoutedEventArgs e)
        {
            this.UpdateImageParametersAndGenerateEvent();
        }

        private void ButtonReset_Click(object sender, RoutedEventArgs e)
        {
            this.ResetControlsToNeutralValues();
            this.UpdateImageParametersAndGenerateEvent();
        }

        private void ButtonApply_Click(object sender, RoutedEventArgs e)
        {
            this.UpdateImageParametersAndGenerateEvent(true);
        }
        #endregion

        #region Event Generation - Generate an event whenever the parameters change
        // Whenever an image is changed, raise an event (to be consumed by MarkableCanvas)
        public event EventHandler<ImageAdjusterEventArgs> ImageProcessingParametersChanged;

        protected virtual void OnImageProcessingParametersChanged(ImageAdjusterEventArgs e)
        {
            this.ImageProcessingParametersChanged?.Invoke(this, e);
        }

        #endregion

        #region Helpers
        private bool IsNeutral()
        {
            return ((this.CBGamma.IsChecked == false && this.CBNone.IsChecked == true && this.BrightnessSlider.Value == 0 && this.ContrastSlider.Value == 0)
                     || (this.CBGamma.IsChecked == true && this.GammaSlider.Value == 1));
        }
        #endregion


    }
}
