using System;
using System.Windows;
using System.Windows.Input;
using Timelapse.Enums;
using Timelapse.EventArguments;

namespace Timelapse
{
    // FilePlayer and FilePlayerTimer
    public partial class TimelapseWindow : Window, IDisposable
    {
        // FilePlayerChange: The user has clicked on the file player. Take action on what was requested
        private void FilePlayer_FilePlayerChange(object sender, FilePlayerEventArgs args)
        {
            switch (args.Selection)
            {
                case FilePlayerSelectionEnum.First:
                    FilePlayer_Stop();
                    FileNavigatorSlider.Value = 1;
                    break;
                case FilePlayerSelectionEnum.Page:
                    this.FilePlayer_ScrollPage();
                    break;
                case FilePlayerSelectionEnum.Row:
                    this.FilePlayer_ScrollRow();
                    break;
                case FilePlayerSelectionEnum.Last:
                    FilePlayer_Stop();
                    FileNavigatorSlider.Value = this.dataHandler.FileDatabase.CurrentlySelectedFileCount;
                    break;
                case FilePlayerSelectionEnum.Step:
                    FilePlayer_Stop();
                    FilePlayerTimer_Tick(null, null);
                    break;
                case FilePlayerSelectionEnum.PlayFast:
                    FilePlayer_Play(TimeSpan.FromSeconds(this.state.FilePlayerFastValue));
                    break;
                case FilePlayerSelectionEnum.PlaySlow:
                    FilePlayer_Play(TimeSpan.FromSeconds(this.state.FilePlayerSlowValue));
                    break;
                case FilePlayerSelectionEnum.Stop:
                default:
                    FilePlayer_Stop();
                    break;
            }
        }

        // Play. Stop the timer, reset the timer interval, and then restart the timer 
        private void FilePlayer_Play(TimeSpan timespan)
        {
            this.FilePlayerTimer.Stop();
            this.FilePlayerTimer.Interval = timespan;
            this.FilePlayerTimer.Start();
        }

        // Stop: both the file player and the timer
        private void FilePlayer_Stop()
        {
            this.FilePlayerTimer.Stop();
            this.FilePlayer.Stop();
        }

        // Scroll Row - a row of images the ClickableImaesGrid
        private void FilePlayer_ScrollRow()
        {
            this.TryFileShowWithoutSliderCallback(this.FilePlayer.Direction, this.MarkableCanvas.ClickableImagesGrid.ImagesInRow);
        }

        // ScrollPage: a page of images the ClickableImaegsGrid
        private void FilePlayer_ScrollPage()
        {
            this.TryFileShowWithoutSliderCallback(this.FilePlayer.Direction, this.MarkableCanvas.ClickableImagesGrid.ImagesInRow * this.MarkableCanvas.ClickableImagesGrid.RowsInGrid);
        }

        // TimerTick: On every tick, try to show the next/previous file as indicated by the direction
        private void FilePlayerTimer_Tick(object sender, EventArgs e)
        {
            this.TryFileShowWithoutSliderCallback(this.FilePlayer.Direction, ModifierKeys.None);

            // Stop the timer if the image reaches the beginning or end of the image set
            if ((this.dataHandler.ImageCache.CurrentRow >= this.dataHandler.FileDatabase.CurrentlySelectedFileCount - 1) || (this.dataHandler.ImageCache.CurrentRow <= 0))
            {
                FilePlayer_Stop();
            }
        }
    }
}
