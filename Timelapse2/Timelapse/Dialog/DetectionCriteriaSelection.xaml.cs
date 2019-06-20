using System.Collections.Generic;
using System.Windows;
using Timelapse.Database;
using Timelapse.Detection;
using Timelapse.Enums;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for DetectionCriteriaSelection.xaml
    /// </summary>
    public partial class DetectionCriteriaSelection : Window
    {
        private FileDatabase database;
        public DetectionSelections DetectionSelections {get; set;}

        public DetectionCriteriaSelection(FileDatabase database, Window owner, DetectionSelections detectionSelections)
        {
            InitializeComponent();
            this.Owner = owner;
            this.database = database;
            this.DetectionSelections = detectionSelections;

        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Adjust this dialog window position 
            Dialogs.SetDefaultDialogPosition(this);
            Dialogs.TryFitDialogWindowInWorkingArea(this);

            // Set the state of the detections to the last used ones (or to its defaults)
            this.UseDetectionCategoryCheckbox.IsChecked = this.DetectionSelections.UseDetectionCategory;
            this.UseDetectionConfidenceCheckbox.IsChecked = this.DetectionSelections.UseDetectionConfidenceThreshold;
            this.DetectionConfidenceSpinner.Value = this.DetectionSelections.DetectionConfidenceThreshold;

            // Put Detection categories in as human-readable labels and set it to the last used one.
            List<string> labels = this.database.GetDetectionLabels();
            foreach (string label in labels)
            {
                this.DetectionCategoryComboBox.Items.Add(label);
            }
            this.DetectionCategoryComboBox.SelectedValue = database.GetDetectionLabelFromCategory(this.DetectionSelections.DetectionCategory);
            this.SetCriteria();
            this.ShowCount();
        }

        #region Ok/Cancel buttons
        // Apply the selection if the Ok button is clicked
        private void OkButton_Click(object sender, RoutedEventArgs args)
        {
            this.SetCriteria();
            this.DialogResult = true;
        }

        // Cancel - exit the dialog without doing anythikng.
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
        #endregion

        private void UseCriteria_CheckedChanged(object sender, RoutedEventArgs e)
        {
            this.DetectionConfidenceSpinner.IsEnabled = this.UseDetectionConfidenceCheckbox.IsChecked == true;
            this.DetectionCategoryComboBox.IsEnabled = this.UseDetectionCategoryCheckbox.IsChecked == true;
            this.SetCriteria();
            this.ShowCount();
        }

        private void SetCriteria()
        {
            if (this.IsLoaded == false)
            {
                return;
            }
            this.DetectionSelections.UseDetectionCategory = this.UseDetectionCategoryCheckbox.IsChecked == true;
            if (this.DetectionSelections.UseDetectionCategory)
            {
                this.DetectionSelections.DetectionCategory = this.database.GetDetectionCategoryFromLabel((string)this.DetectionCategoryComboBox.SelectedItem);
            }

            this.DetectionSelections.UseDetectionConfidenceThreshold = this.UseDetectionConfidenceCheckbox.IsChecked == true;
            if (this.DetectionSelections.UseDetectionConfidenceThreshold)
            {
                this.DetectionSelections.DetectionConfidenceThreshold = (double)this.DetectionConfidenceSpinner.Value;
            }
        }

        private void ShowCount()
        {
            if (this.IsLoaded == false)
            {
                return;
            }
            int count = this.database.GetFileCount(FileSelectionEnum.Custom);
            this.QueryMatches.Text = count > 0 ? count.ToString() : "0";
        }

        private void DetectionCategoryComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            this.SetCriteria();
            this.ShowCount();
        }

        private void DetectionConfidenceSpinner_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            this.SetCriteria();
            this.ShowCount();
        }
    }
}
