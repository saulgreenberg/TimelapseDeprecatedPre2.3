using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Timelapse.Util;

namespace Timelapse.Controls
{
    public partial class VideoPlayer : UserControl
    {
        private bool isProgrammaticUpdate;
        private readonly DispatcherTimer positionUpdateTimer;
        private readonly DispatcherTimer autoPlayDelayTimer;

        // render transforms
        private ScaleTransform videoScale;
        private TransformGroup transformGroup;
        private TranslateTransform videoTranslation;

        #region Constructor, Loading, Unloading, SetSource
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
            this.IsEnabled = false;

            // Rendering setup
            this.videoScale = new ScaleTransform(1.0, 1.0);
            this.videoTranslation = new TranslateTransform(1.0, 1.0);
        }

        private void VideoPlayer_Loaded(object sender, RoutedEventArgs e)
        {
            //  Initial render transforms
            this.videoScale.CenterX = 0.5 * this.ActualWidth;
            this.videoScale.CenterY = 0.5 * this.ActualHeight;

            this.transformGroup = new TransformGroup();
            this.transformGroup.Children.Add(this.videoScale);
            this.transformGroup.Children.Add(this.videoTranslation);
            this.Video.RenderTransform = this.transformGroup;
        }

        private void Video_Unloaded(object sender, RoutedEventArgs e)
        {
            this.positionUpdateTimer.Stop();
            this.IsEnabled = false;
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
        #endregion

        #region Pan and Zoom

        /// <summary>
        /// True if the video is unscaled, false if it is zoomed in
        /// </summary>        
        public bool IsUnScaled
        {
            get
            {
                return this.videoScale.ScaleX == 1;
            }
        }

        // Scale the video by one increment around the screen location
        public void ScaleVideo(Point location, bool zoomIn)
        {
            double VideoZoomMaximum = 2.0;
            double VideoZoomMinimum = 0;
            double VideoZoomStep = 1.2;// Constant.MarkableCanvas.ImageZoomStep

            // Abort if we are already at our maximum or minimum scaling values 
            if ((zoomIn && this.videoScale.ScaleX >= VideoZoomMaximum) ||
                (!zoomIn && this.videoScale.ScaleX <= VideoZoomMinimum))
            {
                return;
            }

            // If we are zooming in around a point off the image, then correct the location to the edge of the image
            if (location.X > this.Video.ActualWidth)
            {
                location.X = this.Video.ActualWidth;
            }
            if (location.Y > this.Video.ActualHeight)
            {
                location.Y = this.Video.ActualHeight;
            }

            // We will scale around the current point
            Point beforeZoom = this.PointFromScreen(this.PointToScreen(location));

            // Calculate the scaling factor during zoom ins or out. Ensure that we keep within our
            // maximum and minimum scaling bounds. 
            if (zoomIn)
            {
                // We are zooming in
                // Calculate the scaling factor
                this.videoScale.ScaleX *= VideoZoomStep;   // Calculate the scaling factor
                this.videoScale.ScaleY *= VideoZoomStep;

                // Make sure we don't scale beyond the maximum scaling factor
                this.videoScale.ScaleX = Math.Min(VideoZoomMaximum, this.videoScale.ScaleX);
                this.videoScale.ScaleY = Math.Min(VideoZoomMaximum, this.videoScale.ScaleY);
            }
            else
            {
                // We are zooming out. 
                // Calculate the scaling factor
                this.videoScale.ScaleX /= VideoZoomStep;
                this.videoScale.ScaleY /= VideoZoomStep;

                // Make sure we don't scale beyond the minimum scaling factor
                this.videoScale.ScaleX = Math.Max(VideoZoomMinimum, this.videoScale.ScaleX);
                this.videoScale.ScaleY = Math.Max(VideoZoomMinimum, this.videoScale.ScaleY);

                // if there is no scaling, reset translations
                if (this.videoScale.ScaleX == 1.0 && this.videoScale.ScaleY == 1.0)
                {
                    this.videoTranslation.X = 0.0;
                    this.videoTranslation.Y = 0.0;
                }
            }

            Point afterZoom = this.PointFromScreen(this.PointToScreen(location));

            // Scale the image, and at the same time translate it so that the 
            // point in the image under the cursor stays there
            lock (this)
            {
                double videoWidth = this.Video.ActualWidth * this.videoScale.ScaleX; // was Width before, but was NaN!!!
                double videoHeight = this.Video.ActualHeight * this.videoScale.ScaleY;

                Point center = this.PointFromScreen(this.PointToScreen(
                    new Point(this.Video.ActualWidth / 2.0, this.Video.ActualHeight / 2.0))); // was Width before, but was NaN!!!

                double newX = center.X - (afterZoom.X - beforeZoom.X);
                double newY = center.Y - (afterZoom.Y - beforeZoom.Y);

                if (newX - videoWidth / 2.0 >= 0.0)
                {
                    newX = videoWidth / 2.0;
                }
                else if (newX + videoWidth / 2.0 <= this.Video.ActualWidth)
                {
                    newX = this.Video.ActualWidth - videoWidth / 2.0;
                }

                if (newY - videoHeight / 2.0 >= 0.0)
                {
                    newY = videoHeight / 2.0;
                }
                else if (newY + videoHeight / 2.0 <= this.Video.ActualHeight)
                {
                    newY = this.Video.ActualHeight - videoHeight / 2.0;
                }
                this.videoTranslation.X += newX - center.X;
                this.videoTranslation.Y += newY - center.Y;
            }
        }
        #endregion

        #region Play/Pause
        // Set the video to automatically start playing after a brief delay 
        // This helps when one is navigating across videos, as there is a brief moment before the play starts.
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
        #endregion

        #region Play position
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
        #endregion
 
        #region Video options callback handlers  (Speed, external player)

        // Set the speed, which also causes the video to play (if currently paused)
        private void SetSpeed_Checked(object sender, RoutedEventArgs e)
        {
            RadioButton rb = sender as RadioButton;
            if (rb?.Tag != null)
            {
                this.Video.SpeedRatio = Convert.ToDouble(rb.Tag);
                this.Play();
            }
        }

        // Open the currently displayed video in an external player
        private void OpenExternalPlayer_Click(object sender, RoutedEventArgs e)
        {
            // Open the currently displayed video in an external player
            if (File.Exists(Uri.UnescapeDataString(this.Video.Source.AbsolutePath)))
            {
                Uri uri = new Uri(Uri.UnescapeDataString(this.Video.Source.AbsolutePath));
                ProcessExecution.TryProcessStart(uri);
            }
        }
        #endregion

        // When the video finishes playing, pause it and automatically return it to the beginning
        private void Video_MediaEnded(object sender, RoutedEventArgs e)
        {
            this.Pause();
            this.VideoPosition.Value = this.VideoPosition.Minimum; // Go back to the beginning
            if (this.CBRepeat.IsChecked == true)
            {
                this.Play();
            }
        }

        private void Video_MediaOpened(object sender, RoutedEventArgs e)
        {
            this.ShowPosition();
            if (this.CBAutoPlay.IsChecked == true)
            {
                this.autoPlayDelayTimer.Start();
            }
        }

        // When the user starts moving the slider, we want to pause the video so the two actions don't interfere with each other
        private void VideoPosition_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            this.Pause();
        }

        private void PlayPause_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // Simulate clicking of the play/pause button
            PlayOrPause.IsChecked = !PlayOrPause.IsChecked;
            PlayOrPause_Click(null, null);
        }
    }
}