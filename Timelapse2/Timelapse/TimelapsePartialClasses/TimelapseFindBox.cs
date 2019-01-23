using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Timelapse.Database;

namespace Timelapse
{
    // Find Box event handlers and helpers
    public partial class TimelapseWindow : Window, IDisposable
    {
        // KeyDown: find forward on enter
        private void FindBoxTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                FindBox_FindImage(true);
            }
        }

        // Click: Depending upon which button was pressed, invoke a forwards or backwards find operation 
        private void FindBoxButton_Click(object sender, RoutedEventArgs e)
        {
            Button findButton = sender as Button;
            bool isForward = (findButton == this.FindForwardButton) ? true : false;
            this.FindBox_FindImage(isForward);
        }

        // Close
        private void FindBoxClose_Click(object sender, RoutedEventArgs e)
        {
            FindBoxSetVisibility(false);
        }

        // Helper methods
        // Search either forwards or backwards for the image file name specified in the text box
        // Only invoked by the above
        private void FindBox_FindImage(bool isForward)
        {
            string searchTerm = this.FindBoxTextBox.Text;
            ImageRow row = this.dataHandler.ImageCache.Current;

            int currentIndex = this.dataHandler.FileDatabase.FileTable.IndexOf(row);
            int foundIndex = this.dataHandler.FileDatabase.FindByFileName(currentIndex, isForward, searchTerm);
            if (foundIndex != -1)
            {
                this.FileShow(foundIndex);
            }
            else
            {
                // Flash the text field to indicate no result
                if (this.FindResource("ColorAnimationBriefFlash") is Storyboard sb)
                {
                    Storyboard.SetTarget(sb, this.FindBoxTextBox);
                    sb.Begin();
                }
            }
        }

        // Adjust Find Box visibility
        // Note: Invoked from above and from other files
        private void FindBoxSetVisibility(bool isVisible)
        {
            // Only make the find box visible if there are files to view
            if (this.FindBox != null && this.IsFileDatabaseAvailable() && this.dataHandler.FileDatabase.CurrentlySelectedFileCount > 0)
            {
                this.FindBox.IsOpen = isVisible;
                this.FindBoxTextBox.Focus();
            }
        }
    }
}
