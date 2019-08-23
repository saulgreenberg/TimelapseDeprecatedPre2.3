using System;
using System.Windows;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for EpisodeOptions.xaml
    /// </summary>
    public partial class EpisodeOptions : Window
    {
        public TimeSpan EpisodeTimeThreshold { get; set; }

        public EpisodeOptions(TimeSpan timeDifferenceThreshold, Window owner)
        {
            InitializeComponent();
            this.Owner = owner;
            this.EpisodeTimeThreshold = timeDifferenceThreshold;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dialogs.SetDefaultDialogPosition(this);
            Dialogs.TryFitDialogWindowInWorkingArea(this);

            this.TimeThresholdSlider.Minimum = Constant.EpisodeDefaults.TimeThresholdMinimum;
            this.TimeThresholdSlider.Maximum = Constant.EpisodeDefaults.TimeThresholdMaximum;
            this.TimeThresholdSlider.ValueChanged += TimeThresholdSlider_ValueChanged;
            this.TimeThresholdSlider.Value = this.EpisodeTimeThreshold.TotalMinutes;
            DisplayFeedback();
        }

        private void TimeThresholdSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // this.state.FilePlayerSlowValue = this.SlowSpeedSlider.Value;
            DisplayFeedback();
            this.EpisodeTimeThreshold = TimeSpan.FromMinutes(TimeThresholdSlider.Value);
        }

        private void DisplayFeedback()
        {
            TimeSpan duration = TimeSpan.FromMinutes(this.TimeThresholdSlider.Value);
            string label = (duration >= TimeSpan.FromMinutes(1)) ? "minutes" : "seconds";
            this.TimeThresholdText.Text = String.Format("{0:m\\:ss} {1}", duration, label);
        }

        private void ResetTimeThresholdSlider_Click(object sender, RoutedEventArgs e)
        {
            this.TimeThresholdSlider.Value = Constant.EpisodeDefaults.TimeThresholdDefault;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }
    }
}
