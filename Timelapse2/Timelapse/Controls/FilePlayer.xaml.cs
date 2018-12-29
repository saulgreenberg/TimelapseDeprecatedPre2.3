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
using System.Windows.Navigation;
using System.Windows.Shapes;
using Timelapse.Controls;
using Timelapse.Enums;

namespace Timelapse.Controls
{
    /// <summary>
    /// FilePlayer contains a set of media controls representing how one can navigate through files by:
    /// - going to the first and last file
    /// - stepping forwards and backwards through files one at a time
    /// - playing forwards and backwards through files at two different speeds
    /// It does not actually do anything except raise events signifying the user's intentions.
    /// </summary>
    public partial class FilePlayer : UserControl
    {
        public FilePlayerDirectionEnum Direction { get; set; }
        public FilePlayerSelectionEnum Selection { get; set; }

        public delegate void FilePlayerChangedHandler(object sender, FilePlayerEventArgs e);
        public event FilePlayerChangedHandler FilePlayerChange;

        public void OnFilePlayerChange(object sender, FilePlayerEventArgs e)
        {
            // If there exist any subscribers call the event
            this.FilePlayerChange?.Invoke(this, e);
        }

        public FilePlayer()
        {
            this.InitializeComponent();
            this.SwitchFileMode(true);
        }

        public void Stop()
        {
            this.StopButton.IsChecked = true;
        }

        // Enable or Disable the backwards controls
        public void BackwardsControlsEnabled(bool isEnabled)
        {
            this.FirstFile.IsEnabled = isEnabled;
            this.PlayBackwardsFast.IsEnabled = isEnabled;
            this.PlayBackwardsSlow.IsEnabled = isEnabled;
            this.StepBackwards.IsEnabled = isEnabled;
            this.RowUp.IsEnabled = isEnabled;
            this.PageUp.IsEnabled = isEnabled;
        }

        // Enable or Disable the forward controls
        public void ForwardsControlsEnabled(bool isEnabled)
        {
            this.StepForward.IsEnabled = isEnabled;
            this.PlayForwardFast.IsEnabled = isEnabled;
            this.PlayForwardSlow.IsEnabled = isEnabled;
            this.LastFile.IsEnabled = isEnabled;
            this.RowDown.IsEnabled = isEnabled;
            this.PageDown.IsEnabled = isEnabled;
        }

        public void SwitchFileMode(bool isSingleMode)
        {
            this.RowDown.Visibility = isSingleMode ? Visibility.Collapsed : Visibility.Visible;
            this.RowUp.Visibility = isSingleMode ? Visibility.Collapsed : Visibility.Visible;
            this.PageDown.Visibility = isSingleMode ? Visibility.Collapsed : Visibility.Visible;
            this.PageUp.Visibility = isSingleMode ? Visibility.Collapsed : Visibility.Visible;

            this.PlayForwardSlow.Visibility = isSingleMode ? Visibility.Visible : Visibility.Collapsed;
            this.PlayForwardFast.Visibility = isSingleMode ? Visibility.Visible : Visibility.Collapsed;
            this.PlayBackwardsSlow.Visibility = isSingleMode ? Visibility.Visible : Visibility.Collapsed;
            this.PlayBackwardsFast.Visibility = isSingleMode ? Visibility.Visible : Visibility.Collapsed;
        }
        private void FilePlayer_Click(object sender, RoutedEventArgs e)
        {
            RadioButton button = sender as RadioButton;
            switch ((string)button.Tag)
            {
                case "First":
                    this.Direction = FilePlayerDirectionEnum.Backward;
                    this.Selection = FilePlayerSelectionEnum.First;
                    break;
                case "PageUp":
                    this.Direction = FilePlayerDirectionEnum.Backward;
                    this.Selection = FilePlayerSelectionEnum.Page;
                    break;
                case "RowUp":
                    this.Direction = FilePlayerDirectionEnum.Backward;
                    this.Selection = FilePlayerSelectionEnum.Row;
                    break;
                case "PlayBackwardsFast":
                    this.Direction = FilePlayerDirectionEnum.Backward;
                    this.Selection = FilePlayerSelectionEnum.PlayFast;
                    break;
                case "PlayBackwardsSlow":
                    this.Direction = FilePlayerDirectionEnum.Backward;
                    this.Selection = FilePlayerSelectionEnum.PlaySlow;
                    break;
                case "StepBackwards":
                    this.Direction = FilePlayerDirectionEnum.Backward;
                    this.Selection = FilePlayerSelectionEnum.Step;
                    break;
                case "Stop":
                    this.Selection = FilePlayerSelectionEnum.Stop;
                    break;
                case "StepForward":
                    this.Direction = FilePlayerDirectionEnum.Forward;
                    this.Selection = FilePlayerSelectionEnum.Step;
                    break;
                case "PlayForwardSlow":
                    this.Direction = FilePlayerDirectionEnum.Forward;
                    this.Selection = FilePlayerSelectionEnum.PlaySlow;
                    break;
                case "PlayForwardFast":
                    this.Direction = FilePlayerDirectionEnum.Forward;
                    this.Selection = FilePlayerSelectionEnum.PlayFast;
                    break;
                case "PageDown":
                    this.Direction = FilePlayerDirectionEnum.Forward;
                    this.Selection = FilePlayerSelectionEnum.Page;
                    break;
                case "RowDown":
                    this.Direction = FilePlayerDirectionEnum.Forward;
                    this.Selection = FilePlayerSelectionEnum.Row;
                    break;
                case "Last":
                    this.Direction = FilePlayerDirectionEnum.Forward;
                    this.Selection = FilePlayerSelectionEnum.Last;
                    break;
                default:
                    this.Selection = FilePlayerSelectionEnum.Stop;
                    break;
            }
            // Raise the event
            this.OnFilePlayerChange(this, new FilePlayerEventArgs(this.Direction, this.Selection));
        }

        private void FilePlayer_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                this.Selection = FilePlayerSelectionEnum.Stop;
                this.OnFilePlayerChange(this, new FilePlayerEventArgs(this.Direction, this.Selection));
                e.Handled = true;
            }
        }
    }
}
