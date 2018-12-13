using System.Windows;
using System.Windows.Controls;
using Timelapse.Images;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    public partial class AdvancedTimelapseOptions : Window
    {
        private MarkableCanvas markableCanvas;
        private TimelapseState timelapseState;

        public AdvancedTimelapseOptions(TimelapseState timelapseState, MarkableCanvas markableCanvas, Window owner)
        {
            this.InitializeComponent();
            this.Owner = owner;
            this.markableCanvas = markableCanvas;
            this.timelapseState = timelapseState;

            // Deletion Management
            this.RadioButtonDeletionManagement_Set(this.timelapseState.DeleteFolderManagement);

            // Throttles
            this.ImageRendersPerSecond.Minimum = Constant.ThrottleValues.DesiredMaximumImageRendersPerSecondLowerBound;
            this.ImageRendersPerSecond.Maximum = Constant.ThrottleValues.DesiredMaximumImageRendersPerSecondUpperBound;
            this.ImageRendersPerSecond.Value = this.timelapseState.Throttles.DesiredImageRendersPerSecond;
            this.ImageRendersPerSecond.ValueChanged += this.ImageRendersPerSecond_ValueChanged;
            this.ImageRendersPerSecond.ToolTip = this.timelapseState.Throttles.DesiredImageRendersPerSecond;

            // The Max Zoom Value
            this.MaxZoom.Value = this.markableCanvas.ZoomMaximum;
            this.MaxZoom.ToolTip = this.markableCanvas.ZoomMaximum;
            this.MaxZoom.Maximum = Constant.MarkableCanvas.ImageZoomMaximumRangeAllowed;
            this.MaxZoom.Minimum = 2;

            // Image Differencing Thresholds
            this.DifferenceThreshold.Value = this.timelapseState.DifferenceThreshold;
            this.DifferenceThreshold.ToolTip = this.timelapseState.DifferenceThreshold;
            this.DifferenceThreshold.Maximum = Constant.ImageValues.DifferenceThresholdMax;
            this.DifferenceThreshold.Minimum = Constant.ImageValues.DifferenceThresholdMin;

            // Showing Images
            this.CheckBoxSuppressThrottleWhenLoading.IsChecked = this.timelapseState.SuppressThrottleWhenLoading ? true : false;
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dialogs.SetDefaultDialogPosition(this);
            Dialogs.TryFitDialogWindowInWorkingArea(this);
        }

        #region Delete Folder Management
        // Check the appropriate radio button to match the state
            private void RadioButtonDeletionManagement_Set(DeleteFolderManagement deleteFolderManagement)
        {
            switch (deleteFolderManagement)
            {
                case DeleteFolderManagement.ManualDelete:
                    this.RadioButtonManualDelete.IsChecked = true;
                    break;
                case DeleteFolderManagement.AskToDeleteOnExit:
                    this.RadioButtonAskToDelete.IsChecked = true;
                    break;
                case DeleteFolderManagement.AutoDeleteOnExit:
                    this.RadioButtonAutoDeleteOnExit.IsChecked = true;
                    break;
                default:
                    break;
            }
        }

        // Set the state to match the radio button selection
        private void DeletedFileManagement_Click(object sender, RoutedEventArgs e)
        {
            RadioButton rb = (RadioButton)sender;
            switch (rb.Name)
            {
                case "RadioButtonManualDelete":
                    this.timelapseState.DeleteFolderManagement = DeleteFolderManagement.ManualDelete;
                    break;
                case "RadioButtonAskToDelete":
                    this.timelapseState.DeleteFolderManagement = DeleteFolderManagement.AskToDeleteOnExit;
                    break;
                case "RadioButtonAutoDeleteOnExit":
                    this.timelapseState.DeleteFolderManagement = DeleteFolderManagement.AutoDeleteOnExit;
                    break;
                default:
                    break;
            }
        }

        // Reset to the Default, i.e. manual deletion
        private void ResetDeletedFileManagement_Click(object sender, RoutedEventArgs e)
        {
            RadioButtonManualDelete.IsChecked = true;
            this.timelapseState.DeleteFolderManagement = DeleteFolderManagement.ManualDelete;
        }
        #endregion

        private void ImageRendersPerSecond_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            this.timelapseState.Throttles.SetDesiredImageRendersPerSecond(this.ImageRendersPerSecond.Value);
            this.ImageRendersPerSecond.ToolTip = this.timelapseState.Throttles.DesiredImageRendersPerSecond;
        }

        private void ResetThrottle_Click(object sender, RoutedEventArgs e)
        {
            this.timelapseState.Throttles.ResetToDefaults();
            this.ImageRendersPerSecond.Value = this.timelapseState.Throttles.DesiredImageRendersPerSecond;
            this.ImageRendersPerSecond.ToolTip = this.timelapseState.Throttles.DesiredImageRendersPerSecond;
            this.timelapseState.SuppressThrottleWhenLoading = false;
            this.CheckBoxSuppressThrottleWhenLoading.IsChecked = false;
        }

        // Reset the maximum zoom to the amount specified in Max Zoom;
        private void ResetMaxZoom_Click(object sender, RoutedEventArgs e)
        {
            this.markableCanvas.ResetMaximumZoom();
            this.MaxZoom.Value = this.markableCanvas.ZoomMaximum;
            this.MaxZoom.ToolTip = this.markableCanvas.ZoomMaximum;
        }

        // Callback: The user has changed the maximum zoom value
        private void MaxZoom_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            this.markableCanvas.ZoomMaximum = (int)this.MaxZoom.Value;
            this.MaxZoom.ToolTip = this.markableCanvas.ZoomMaximum;
        }

        private void ResetImageDifferencingButton_Click(object sender, RoutedEventArgs e)
        {
            this.timelapseState.DifferenceThreshold = Constant.ImageValues.DifferenceThresholdDefault;
            this.DifferenceThreshold.Value = this.timelapseState.DifferenceThreshold;
            this.DifferenceThreshold.ToolTip = this.timelapseState.DifferenceThreshold;
        }

        private void DifferenceThreshold_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            this.timelapseState.DifferenceThreshold = (byte)this.DifferenceThreshold.Value;
            this.DifferenceThreshold.ToolTip = this.timelapseState.DifferenceThreshold;
        }

        private void SuppressThrottleWhenLoading_Click(object sender, RoutedEventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            this.timelapseState.SuppressThrottleWhenLoading = (cb.IsChecked == true) ? true : false;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }
    }
}
