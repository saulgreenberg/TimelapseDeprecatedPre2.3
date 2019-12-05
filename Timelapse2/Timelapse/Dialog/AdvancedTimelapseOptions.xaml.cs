﻿using System;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Enums;
using Timelapse.Images;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    public partial class AdvancedTimelapseOptions : Window
    {
        private readonly MarkableCanvas markableCanvas;
        private readonly TimelapseState timelapseState;

        public AdvancedTimelapseOptions(TimelapseState timelapseState, MarkableCanvas markableCanvas, Window owner)
        {
            this.InitializeComponent();
            this.Owner = owner;

            // Check the arguments for null 
            ThrowIf.IsNullArgument(timelapseState, nameof(timelapseState));
            ThrowIf.IsNullArgument(markableCanvas, nameof(markableCanvas));

            this.markableCanvas = markableCanvas;
            this.timelapseState = timelapseState;

            // Tab Order - set to current state.
            this.CheckBoxTabOrderDateTime.IsChecked = this.timelapseState.TabOrderIncludeDateTime;
            this.CheckBoxTabOrderDeleteFlag.IsChecked = this.timelapseState.TabOrderIncludeDeleteFlag;
            this.CheckBoxTabOrderImageQuality.IsChecked = this.timelapseState.TabOrderIncludeImageQuality;

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

            // Detections
            this.CheckBoxUseDetections.IsChecked = this.timelapseState.UseDetections;
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dialogs.SetDefaultDialogPosition(this);
            Dialogs.TryFitDialogWindowInWorkingArea(this);
            this.BoundingBoxDisplayThresholdSlider.IsEnabled = this.timelapseState.UseDetections;
            this.BoundingBoxDisplayThresholdSlider.Value = this.timelapseState.BoundingBoxDisplayThreshold;
        }

        #region Delete Folder Management
        // Check the appropriate radio button to match the state
        private void RadioButtonDeletionManagement_Set(DeleteFolderManagementEnum deleteFolderManagement)
        {
            switch (deleteFolderManagement)
            {
                case DeleteFolderManagementEnum.ManualDelete:
                    this.RadioButtonManualDelete.IsChecked = true;
                    break;
                case DeleteFolderManagementEnum.AskToDeleteOnExit:
                    this.RadioButtonAskToDelete.IsChecked = true;
                    break;
                case DeleteFolderManagementEnum.AutoDeleteOnExit:
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
                    this.timelapseState.DeleteFolderManagement = DeleteFolderManagementEnum.ManualDelete;
                    break;
                case "RadioButtonAskToDelete":
                    this.timelapseState.DeleteFolderManagement = DeleteFolderManagementEnum.AskToDeleteOnExit;
                    break;
                case "RadioButtonAutoDeleteOnExit":
                    this.timelapseState.DeleteFolderManagement = DeleteFolderManagementEnum.AutoDeleteOnExit;
                    break;
                default:
                    break;
            }
        }

        // Reset to the Default, i.e. manual deletion
        private void ResetDeletedFileManagement_Click(object sender, RoutedEventArgs e)
        {
            this.RadioButtonManualDelete.IsChecked = true;
            this.timelapseState.DeleteFolderManagement = DeleteFolderManagementEnum.ManualDelete;
        }
        #endregion
        #region Tab Controls to Include / Exclude
        private void CheckBoxTabOrder_Click(object sender, RoutedEventArgs e)
        {
            this.SetTabOrder();
        }

        private void ResetTabOrder_Click(object sender, RoutedEventArgs e)
        {
            this.CheckBoxTabOrderDateTime.IsChecked = false;
            this.CheckBoxTabOrderImageQuality.IsChecked = false;
            this.CheckBoxTabOrderDeleteFlag.IsChecked = false;
            this.SetTabOrder();
        }

        private void SetTabOrder()
        {
            this.timelapseState.TabOrderIncludeDateTime = this.CheckBoxTabOrderDateTime.IsChecked == true ? true : false;
            this.timelapseState.TabOrderIncludeDeleteFlag = this.CheckBoxTabOrderDeleteFlag.IsChecked == true ? true : false;
            this.timelapseState.TabOrderIncludeImageQuality = this.CheckBoxTabOrderImageQuality.IsChecked == true ? true : false;
        }

        #endregion
        private void ImageRendersPerSecond_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            this.timelapseState.Throttles.SetDesiredImageRendersPerSecond(this.ImageRendersPerSecond.Value);
            this.ImageRendersPerSecond.ToolTip = this.timelapseState.Throttles.DesiredImageRendersPerSecond;
        }

        #region Reset to defaults
        private void ResetThrottle_Click(object sender, RoutedEventArgs e)
        {
            this.timelapseState.Throttles.ResetToDefaults();
            this.ImageRendersPerSecond.Value = this.timelapseState.Throttles.DesiredImageRendersPerSecond;
            this.ImageRendersPerSecond.ToolTip = this.timelapseState.Throttles.DesiredImageRendersPerSecond;
        }

        // Reset the maximum zoom to the amount specified in Max Zoom;
        private void ResetMaxZoom_Click(object sender, RoutedEventArgs e)
        {
            this.markableCanvas.ResetMaximumZoom();
            this.MaxZoom.Value = this.markableCanvas.ZoomMaximum;
            this.MaxZoom.ToolTip = this.markableCanvas.ZoomMaximum;
        }
        #endregion
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

        // Detection settings
        private void CheckBoxUseDetections_Click(object sender, RoutedEventArgs e)
        {
            this.timelapseState.UseDetections = this.CheckBoxUseDetections.IsChecked == true;
            this.BoundingBoxDisplayThresholdSlider.IsEnabled = this.timelapseState.UseDetections;
        }

        private void ResetDetections_Click(object sender, RoutedEventArgs e)
        {
            this.CheckBoxUseDetections.IsChecked = false;
            this.timelapseState.UseDetections = false;
            this.BoundingBoxDisplayThresholdSlider.IsEnabled = false;
            this.BoundingBoxDisplayThresholdSlider.Value = Constant.MarkableCanvas.BoundingBoxDisplayThresholdDefault;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void BoundingBoxDisplayThreshold_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!(sender is Slider slider))
            {
                return;
            }
            this.BoundingBoxThresholdDisplayValue.Text = slider.Value.ToString("0.00");
            if (slider.Value == 0)
            {
                this.BoundingBoxThresholdDisplayText.Text = "This setting will display all bounding boxes";
            }
            else if (slider.Value == 1)
            {
                this.BoundingBoxThresholdDisplayText.Text = "This setting will never display bounding boxes";
            }
            else
            {
                this.BoundingBoxThresholdDisplayText.Text = "Always display bounding boxes above this confidence threshold";
            }
            this.timelapseState.BoundingBoxDisplayThreshold = slider.Value;
        }
    }
}
