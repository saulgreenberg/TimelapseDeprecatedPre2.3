using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Timelapse.Controls
{
    public partial class VideoPlayer : UserControl
    {
        private bool isProgrammaticUpdate;
        private readonly DispatcherTimer positionUpdateTimer;
        private readonly DispatcherTimer autoPlayDelayTimer;
        // TODO: Setup up video so a mouse click starts and stops it, i.e., as does the space bar.
        public VideoPlayer()
        {
            this.InitializeComponent();
            this.isProgrammaticUpdate = false;
            this.positionUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250.0)
            };
            this.positionUpdateTimer.Tick += this.Timer_Tick;

            // Timer used to automatically start playing the videos after a modest interval
            this.autoPlayDelayTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300.0)
            };
            this.autoPlayDelayTimer.Tick += this.AutoPlayDelayTimer_Tick;

            // TODO Add magnifier to videos. But to do this, we should change all magnifiers to the xceed one.
            // The commented out code below will use the Xceed magnifier.
            // TranslateTransform tt = new TranslateTransform(100, -100);
            // this._magnifier.RenderTransform = tt;
            // this._magnifier.IsEnabled = true;
            // this._magnifier.Visibility = Visibility.Visible;

            this.IsEnabled = false;
        }

        private void AutoPlayDelayTimer_Tick(object sender, EventArgs e)
        {
            this.Play();
            this.autoPlayDelayTimer.Stop();
        }

        public void Pause()
        {
            this.positionUpdateTimer.Stop();
            this.Video.Pause();
            this.PlayOrPause.IsChecked = false;
            this.ShowPosition();
        }

        private void Play()
        {
            this.PlayOrPause.IsChecked = true;

            // start over from beginning if at end of video
            if (this.Video.NaturalDuration.HasTimeSpan && this.Video.Position == this.Video.NaturalDuration.TimeSpan)
            {
                this.Video.Position = TimeSpan.Zero;
                this.ShowPosition();
            }

            this.positionUpdateTimer.Start();
            this.Video.Play();
        }

        private void PlayOrPause_Click(object sender, RoutedEventArgs e)
        {
            if (this.PlayOrPause.IsChecked == true)
            {
                this.Play();
            }
            else
            {
                this.Pause();
            }
        }

        public void SetSource(Uri source)
        {
            // If the source and the thumbnail exists, show that as the background. This avoids the annoying black frames that otherwise appear for a few moments.
            string thumbnailpath = String.Empty;
            if (source != null)
            {
                thumbnailpath = Path.Combine(Path.GetDirectoryName(source.LocalPath), Constant.File.VideoThumbnailFolderName, Path.GetFileNameWithoutExtension(source.LocalPath) + Constant.File.JpgFileExtension);
            }

            if (File.Exists(thumbnailpath))
            {
                this.ThumbnailImage.Source = Images.BitmapUtilities.GetBitmapFromFileWithPlayButton(thumbnailpath);
            }

            // MediaElement seems only deterministic about displaying the first frame when LoadedBehaviour is set to Pause, which isn't helpful as calls to
            // Play() then have no effect.  This is a well known issue with various folks getting results.  The below combination of Play(), Pause() and Position
            // seems to work, though neither Pause() or Position is sufficent on its own and black frames still get rendered if Position is set to zero or
            // an especially small value.
            this.Video.Source = source;
            this.IsEnabled = true;
            double originalVolume = this.Video.Volume;
            this.Video.Volume = 0.0;
            this.Video.Play();
            this.Video.Pause();
            this.PlayOrPause.IsChecked = false;
            this.Video.Position = TimeSpan.FromMilliseconds(0.0);
            this.Video.Volume = originalVolume;
            // position updated through the media opened event
        }

        // Show the current play position in the ScrollBar and TextBox, if possible.
        private void ShowPosition()
        {
            this.isProgrammaticUpdate = true;
            if (this.Video.NaturalDuration.HasTimeSpan)
            {
                this.VideoPosition.Maximum = this.Video.NaturalDuration.TimeSpan.TotalSeconds;
                // SAULXX: The line below will show the end time as a delta rather than absolute time. I decided that is undesirable, as the start time already shows the delta
                // this.TimeDuration.Text = (this.Video.NaturalDuration.TimeSpan - this.Video.Position).ToString(Constant.Time.VideoPositionFormat);
                this.TimeDuration.Text = this.Video.NaturalDuration.TimeSpan.ToString(Constant.Time.VideoPositionFormat);
                this.VideoPosition.TickFrequency = this.VideoPosition.Maximum / 10.0;
            }
            this.TimeFromStart.Text = this.Video.Position.ToString(Constant.Time.VideoPositionFormat);
            this.VideoPosition.Value = this.Video.Position.TotalSeconds;
            this.isProgrammaticUpdate = false;
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (this.Video.Source != null)
            {
                this.ShowPosition();
            }
        }

        public bool TryPlayOrPause()
        {
            if (this.Visibility != Visibility.Visible)
            {
                return false;
            }

            // WPF doesn't offer a way to fire a toggle button's click event programatically (ToggleButtonAutomationPeer -> IToggleProvider -> Toggle()
            // changes the state of the button but fails to trigger the click event) so do the equivalent in code
            this.PlayOrPause.IsChecked = !this.PlayOrPause.IsChecked;
            this.PlayOrPause_Click(this, null);
            return true;
        }

        // When the video finishes playing, pause it and automatically return it to the beginning
        private void Video_MediaEnded(object sender, RoutedEventArgs e)
        {
            this.Pause();
            this.VideoPosition.Value = this.VideoPosition.Minimum; // Go back to the beginning
        }

        private void Video_MediaOpened(object sender, RoutedEventArgs e)
        {
            this.ShowPosition();
            // SAULXXX Uncomment this line if you want the video to automatically start playing as soon as it is opened
            // this.autoPlayDelayTimer.Start();
        }

        private void Video_Unloaded(object sender, RoutedEventArgs e)
        {
            this.positionUpdateTimer.Stop();
            this.IsEnabled = false;
        }

        // When the user starts moving the slider, we want to pause the video so the two actions don't interfere with each other
        private void VideoPosition_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            this.Pause();
        }

        // Scrub the video to the current slider position
        private void VideoPosition_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (this.isProgrammaticUpdate)
            {
                return;
            }

            TimeSpan videoPosition = TimeSpan.FromSeconds(this.VideoPosition.Value);
            this.Video.Position = videoPosition;
            this.ShowPosition();
            this.Pause(); // If a user scrubs, force the video to pause if its playing
        }

        private void PlayPause_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // Simulate clicking of the play/pause button
            PlayOrPause.IsChecked = !PlayOrPause.IsChecked;
            PlayOrPause_Click(null, null);
        }
    }
}