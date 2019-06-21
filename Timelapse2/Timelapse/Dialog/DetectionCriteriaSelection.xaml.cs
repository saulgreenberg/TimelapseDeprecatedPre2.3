using System.Collections.Generic;
using System.Linq;
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
        private bool dontInvoke = false;
        const string LessThan = "Less than";
        const string GreaterThan = "Greater than";
        const string Between = "Between";
        Dictionary<ComparisonEnum, string> ComparisonDictionary = new Dictionary<ComparisonEnum, string>();
        public DetectionSelections DetectionSelections {get; set;}

        public DetectionCriteriaSelection(FileDatabase database, Window owner, DetectionSelections detectionSelections)
        {
            InitializeComponent();
            this.Owner = owner;
            this.database = database;
            this.DetectionSelections = detectionSelections;
            ComparisonDictionary.Add(ComparisonEnum.LessThanEqual, LessThan);
            ComparisonDictionary.Add(ComparisonEnum.GreaterThan, GreaterThan);
            ComparisonDictionary.Add(ComparisonEnum.Between, Between);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Adjust this dialog window position 
            Dialogs.SetDefaultDialogPosition(this);
            Dialogs.TryFitDialogWindowInWorkingArea(this);

            this.DetectionRangeType.Items.Add(LessThan);
            this.DetectionRangeType.Items.Add(Between);
            this.DetectionRangeType.Items.Add(GreaterThan);

            // Set the state of the detections to the last used ones (or to its defaults)
            this.dontInvoke = true;
            this.UseDetectionCategoryCheckbox.IsChecked = this.DetectionSelections.UseDetectionCategory;
            this.UseDetectionConfidenceCheckbox.IsChecked = this.DetectionSelections.UseDetectionConfidenceThreshold;
            this.dontInvoke = false;

            this.DetectionRangeType.SelectedItem = this.ComparisonDictionary[this.DetectionSelections.DetectionComparison];

            //this.DetectionConfidenceSpinner.Value = this.DetectionSelections.DetectionConfidenceThreshold1;

            this.DetectionConfidenceSpinner1.Value = this.DetectionSelections.DetectionConfidenceThreshold1;
            this.DetectionConfidenceSpinner2.Value = this.DetectionSelections.DetectionConfidenceThreshold2;


            // Put Detection categories in as human-readable labels and set it to the last used one.
            List<string> labels = this.database.GetDetectionLabels();
            foreach (string label in labels)
            {
                this.DetectionCategoryComboBox.Items.Add(label);
            }
            this.DetectionCategoryComboBox.SelectedValue = database.GetDetectionLabelFromCategory(this.DetectionSelections.DetectionCategory);

            this.SetDetectionSpinnerVisibility(this.DetectionSelections.DetectionCategory);
            this.SetDetectionSpinnerEnable();

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
            if (this.dontInvoke) return;
            // Enable or disable the controls depending on the various checkbox states
            SetDetectionSpinnerEnable();

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
                this.DetectionSelections.DetectionConfidenceThreshold1 = (double)this.DetectionConfidenceSpinner1.Value;
            }
        }

        private void ShowCount()
        {
            if (this.IsLoaded == false)
            {
                return;
            }
            //int count = (this.DetectionSelections.UseDetectionConfidenceThreshold || this.DetectionSelections.UseDetectionCategory) ? this.database.GetFileCount(FileSelectionEnum.Custom) : this.database.GetFileCount(FileSelectionEnum.All);
            int count = this.database.GetFileCount(FileSelectionEnum.Custom);
            this.QueryMatches.Text = count > 0 ? count.ToString() : "0";
            this.OkButton.IsEnabled = true; // (count > 0); // Dusable OK button if there are no matches.
        }

        private void DetectionCategoryComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (this.IsLoaded == false)
            {
                return;
            }
            this.SetCriteria();
            this.ShowCount();
        }

        //private void DetectionConfidenceSpinner_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        //{
        //    if (this.IsLoaded == false)
        //    {
        //        return;
        //    }
        //    this.SetCriteria();
        //    this.ShowCount();
        //}

        private bool ignoreSpinnerUpdates = false;
        private void DetectionConfidenceSpinner1_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (this.IsLoaded == false || ignoreSpinnerUpdates)
            {
                return;
            }
            
            if (this.DetectionConfidenceSpinner1.Value > this.DetectionConfidenceSpinner2.Value)
            {
                ignoreSpinnerUpdates = true;
                this.DetectionConfidenceSpinner2.Value = this.DetectionConfidenceSpinner1.Value;
                ignoreSpinnerUpdates = false;
            }
            this.SetCriteria();
            this.ShowCount();
        }

        private void DetectionConfidenceSpinner2_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (this.IsLoaded == false || ignoreSpinnerUpdates)
            {
                return;
            }

            if (this.DetectionConfidenceSpinner2.Value < this.DetectionConfidenceSpinner1.Value)
            {
                ignoreSpinnerUpdates = true;
                this.DetectionConfidenceSpinner2.Value = this.DetectionConfidenceSpinner1.Value;
                ignoreSpinnerUpdates = false;
            }
            this.SetCriteria();
            this.ShowCount();
        }

        private void DetectionRangeType_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            this.SetDetectionSpinnerVisibility((string)this.DetectionRangeType.SelectedValue);
            this.DetectionSelections.DetectionComparison = ComparisonDictionary.FirstOrDefault(x => x.Value == (string)this.DetectionRangeType.SelectedValue).Key;
            this.SetCriteria();
            this.ShowCount();
        }

        #region Set and/or enable the visibility of Spinner controls
        // Depending on what comparision operator is used, set the visibility of particular spinners and labels
        private void SetDetectionSpinnerVisibility(ComparisonEnum comparisonEnum)
        {
            SetDetectionSpinnerVisibility(ComparisonDictionary[comparisonEnum]);
        }
        private void SetDetectionSpinnerVisibility (string comparison)
        {
            switch (comparison)
            {
                case LessThan:
                    this.DetectionConfidenceSpinner2.Visibility = Visibility.Hidden;
                    this.AndLabel.Visibility = Visibility.Hidden;
                    break;
                case Between:
                    this.DetectionConfidenceSpinner2.Visibility = Visibility.Visible;
                    this.AndLabel.Visibility = Visibility.Visible;
                    break;
                case GreaterThan:
                default:
                    this.DetectionConfidenceSpinner2.Visibility = Visibility.Hidden;
                    this.AndLabel.Visibility = Visibility.Hidden;
                    break;
            }
        }
        private void SetDetectionSpinnerEnable()
        {
            // Enable or disable the controls depending on the various checkbox states
            this.DetectionConfidenceSpinner1.IsEnabled = this.UseDetectionConfidenceCheckbox.IsChecked == true;
            this.DetectionConfidenceSpinner2.IsEnabled = this.UseDetectionConfidenceCheckbox.IsChecked == true;
            this.DetectionRangeType.IsEnabled = this.UseDetectionConfidenceCheckbox.IsChecked == true;
            this.DetectionCategoryComboBox.IsEnabled = this.UseDetectionCategoryCheckbox.IsChecked == true;
        }
        #endregion
    }
}
