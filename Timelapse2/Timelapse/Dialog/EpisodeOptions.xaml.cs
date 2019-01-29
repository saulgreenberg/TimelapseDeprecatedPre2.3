using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for EpisodeOptions.xaml
    /// </summary>
    public partial class EpisodeOptions : Window
    {
        private TimeSpan timeDifferenceThreshold;

        public EpisodeOptions(TimeSpan timeDifferenceThreshold, Window owner)
        {
            InitializeComponent();
            this.Owner = owner;
            this.timeDifferenceThreshold = timeDifferenceThreshold;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dialogs.SetDefaultDialogPosition(this);
            Dialogs.TryFitDialogWindowInWorkingArea(this);

            this.TimeThresholdSlider.Minimum = Constant.EpisodeDefaults.TimeThresholdMinimum;
            this.TimeThresholdSlider.Maximum = Constant.EpisodeDefaults.TimeThresholdMaximum; 
            this.TimeThresholdSlider.ValueChanged += TimeThresholdSlider_ValueChanged;
            this.TimeThresholdSlider.Value = this.timeDifferenceThreshold.TotalMinutes;
            DisplayFeedback();
        }

        private void TimeThresholdSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // this.state.FilePlayerSlowValue = this.SlowSpeedSlider.Value;
            DisplayFeedback();
            this.timeDifferenceThreshold = TimeSpan.FromMinutes(TimeThresholdSlider.Value);
        }

        private void DisplayFeedback()
        {
            this.TimeThresholdText.Text = String.Format("{0:m\\:ss} minutes", TimeSpan.FromMinutes(this.TimeThresholdSlider.Value));
        }

        private void ResetTimeThresholdSlider_Click(object sender, RoutedEventArgs e)
        {
            this.TimeThresholdSlider.Value = 2; //Constant.FilePlayerValues.PlaySlowDefault.TotalSeconds;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            Episodes.TimeDifferenceThreshold = this.timeDifferenceThreshold;
        }
    }
}
