using System.Windows;
using System.Windows.Controls;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Lets the user Hide or Show various informational dialog boxes.
    /// </summary>
    public partial class DialogsHideOrShow : Window
    {
        private readonly TimelapseState state;
        public DialogsHideOrShow(TimelapseState state, Window owner)
        {
            this.InitializeComponent();
            this.Owner = owner;
            this.state = state;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Adjust this dialog window position 
            Dialogs.TryPositionAndFitDialogIntoWindow(this);

            this.SuppressAmbiguousDatesDialog.IsChecked = this.state.SuppressAmbiguousDatesDialog;
            this.SuppressCsvExportDialog.IsChecked = this.state.SuppressCsvExportDialog;
            this.SuppressCsvExportDialog.IsChecked = this.state.SuppressCsvExportDialog;
            this.SuppressCsvImportPrompt.IsChecked = this.state.SuppressCsvImportPrompt;
            this.SuppressSelectedAmbiguousDatesPrompt.IsChecked = this.state.SuppressSelectedAmbiguousDatesPrompt;
            this.SuppressMergeDatabasesPrompt.IsChecked = this.state.SuppressMergeDatabasesPrompt;
            this.SuppressSelectedCsvExportPrompt.IsChecked = this.state.SuppressSelectedCsvExportPrompt;
            this.SuppressSelectedDarkThresholdPrompt.IsChecked = this.state.SuppressSelectedDarkThresholdPrompt;
            this.SuppressSelectedDateTimeFixedCorrectionPrompt.IsChecked = this.state.SuppressSelectedDateTimeFixedCorrectionPrompt;
            this.SuppressSelectedDateTimeLinearCorrectionPrompt.IsChecked = this.state.SuppressSelectedDateTimeLinearCorrectionPrompt;
            this.SuppressSelectedDaylightSavingsCorrectionPrompt.IsChecked = this.state.SuppressSelectedDaylightSavingsCorrectionPrompt;
            this.SuppressSelectedPopulateFieldFromMetadataPrompt.IsChecked = this.state.SuppressSelectedPopulateFieldFromMetadataPrompt;
            this.SuppressSelectedRereadDatesFromFilesPrompt.IsChecked = this.state.SuppressSelectedRereadDatesFromFilesPrompt;
            this.SuppressSelectedSetTimeZonePrompt.IsChecked = this.state.SuppressSelectedSetTimeZonePrompt;
        }

        private void SuppressAmbiguousDatesDialog_Click(object sender, RoutedEventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            this.state.SuppressAmbiguousDatesDialog = (cb.IsChecked == true) ? true : false;
        }

        private void SuppressCsvExportDialog_Click(object sender, RoutedEventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            this.state.SuppressCsvExportDialog = (cb.IsChecked == true) ? true : false;
        }

        private void SuppressCsvImportPrompt_Click(object sender, RoutedEventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            this.state.SuppressCsvImportPrompt = (cb.IsChecked == true) ? true : false;
        }

        private void SuppressSelectedAmbiguousDatesPrompt_Click(object sender, RoutedEventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            this.state.SuppressSelectedAmbiguousDatesPrompt = (cb.IsChecked == true) ? true : false;
        }

        private void SuppressMergeDatabasesPrompt_Click(object sender, RoutedEventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            this.state.SuppressMergeDatabasesPrompt = (cb.IsChecked == true) ? true : false;
        }
        
        private void SuppressSelectedCsvExportPrompt_Click(object sender, RoutedEventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            this.state.SuppressSelectedCsvExportPrompt = (cb.IsChecked == true) ? true : false;
        }

        private void SuppressSelectedDarkThresholdPrompt_Click(object sender, RoutedEventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            this.state.SuppressSelectedDarkThresholdPrompt = (cb.IsChecked == true) ? true : false;
        }

        private void SuppressSelectedDateTimeFixedCorrectionPrompt_Click(object sender, RoutedEventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            this.state.SuppressSelectedDateTimeFixedCorrectionPrompt = (cb.IsChecked == true) ? true : false;
        }

        private void SuppressSelectedDateTimeLinearCorrectionPrompt_Click(object sender, RoutedEventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            this.state.SuppressSelectedDateTimeLinearCorrectionPrompt = (cb.IsChecked == true) ? true : false;
        }

        private void SuppressSelectedDaylightSavingsCorrectionPrompt_Click(object sender, RoutedEventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            this.state.SuppressSelectedDaylightSavingsCorrectionPrompt = (cb.IsChecked == true) ? true : false;
        }

        private void SuppressSelectedPopulateFieldFromMetadataPrompt_Click(object sender, RoutedEventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            this.state.SuppressSelectedPopulateFieldFromMetadataPrompt = (cb.IsChecked == true) ? true : false;
        }

        private void SuppressSelectedRereadDatesFromFilesPrompt_Click(object sender, RoutedEventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            this.state.SuppressSelectedRereadDatesFromFilesPrompt = (cb.IsChecked == true) ? true : false;
        }

        private void SuppressSelectedSetTimeZonePrompt_Click(object sender, RoutedEventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            this.state.SuppressSelectedSetTimeZonePrompt = (cb.IsChecked == true) ? true : false;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }
    }
}
