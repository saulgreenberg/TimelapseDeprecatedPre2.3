
using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Timelapse
{
    // File Navigation Slider (including Timer) callbacks and related
    public partial class TimelapseWindow : Window, IDisposable
    {
        // Drag Started callback
        private void FileNavigatorSlider_DragStarted(object sender, DragStartedEventArgs args)
        {
            FilePlayer_Stop(); // In case the FilePlayer is going
            this.timerFileNavigator.Start(); // The timer forces an image display update to the current slider position if the user pauses longer than the timer's interval. 
            this.state.FileNavigatorSliderDragging = true;
        }

        // Drag Completed callback
        private void FileNavigatorSlider_DragCompleted(object sender, DragCompletedEventArgs args)
        {
            FilePlayer_Stop(); // In case the FilePlayer is going
            this.state.FileNavigatorSliderDragging = false;
            this.ShowFile(this.FileNavigatorSlider);
            this.timerFileNavigator.Stop();
        }

        // Value Changed callback
        private void FileNavigatorSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> args)
        {
            // since the minimum value is 1 there's a value change event during InitializeComponent() to ignore
            if (this.state == null)
            {
                return;
            }

            // Stop the timer, but restart it if we are dragging
            this.timerFileNavigator.Stop();
            if (this.state.FileNavigatorSliderDragging == true)
            {
                this.timerFileNavigator.Interval = this.state.Throttles.DesiredIntervalBetweenRenders; // Throttle values may have changed, so we reset it just in case.
                this.timerFileNavigator.Start();
            }

            DateTime utcNow = DateTime.UtcNow;
            if ((this.state.FileNavigatorSliderDragging == false) || (utcNow - this.state.MostRecentDragEvent > this.timerFileNavigator.Interval))
            {
                this.ShowFile(this.FileNavigatorSlider);
                this.state.MostRecentDragEvent = utcNow;
                this.FileNavigatorSlider.AutoToolTipContent = this.dataHandler.ImageCache.Current.FileName;
            }
        }

        // Timer callback that forces image update to the current slider position. Invoked as the user pauses dragging the image slider 
        private void TimerFileNavigator_Tick(object sender, EventArgs e)
        {
            this.timerFileNavigator.Stop();
            this.ShowFile(this.FileNavigatorSlider);
            this.FileNavigatorSlider.AutoToolTipContent = this.dataHandler.ImageCache.Current.FileName;
        }

        private void FileNavigatorSlider_EnableOrDisableValueChangedCallback(bool enableCallback)
        {
            if (enableCallback)
            {
                this.FileNavigatorSlider.ValueChanged += new RoutedPropertyChangedEventHandler<double>(this.FileNavigatorSlider_ValueChanged);
            }
            else
            {
                this.FileNavigatorSlider.ValueChanged -= new RoutedPropertyChangedEventHandler<double>(this.FileNavigatorSlider_ValueChanged);
            }
        }

        // Reset the slider: usually done to disable the FileNavigator when there is no image set to display.
        private void FileNavigatorSliderReset()
        {
            bool filesSelected = (this.IsFileDatabaseAvailable() && this.dataHandler.FileDatabase.CurrentlySelectedFileCount > 0) ? true : false;

            this.timerFileNavigator.Stop();
            this.FileNavigatorSlider_EnableOrDisableValueChangedCallback(filesSelected);
            this.FileNavigatorSlider.IsEnabled = filesSelected;
            this.FileNavigatorSlider.Maximum = filesSelected ? this.dataHandler.FileDatabase.CurrentlySelectedFileCount : 0;
        }

        #region Depracated
        //// Create a semi-transparent visible blue border around the slider when it has the focus. Its semi-transparent to mute it somewhat...
        //private void FileNavigatorSlider_GotFocus(object sender, RoutedEventArgs e)
        //{
        //    SolidColorBrush brush = Constant.Control.BorderColorHighlight.Clone();
        //    brush.Opacity = .5;
        //    this.AutoToolTipSliderBorder.BorderBrush = brush;
        //}

        //private void FileNavigatorSlider_LostFocus(object sender, RoutedEventArgs e)
        //{
        //    this.AutoToolTipSliderBorder.BorderBrush = Brushes.Transparent;
        //}
        #endregion Depracated
    }
}
