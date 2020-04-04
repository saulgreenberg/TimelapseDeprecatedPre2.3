using System;
using System.Windows;
using System.Windows.Input;
using Timelapse.Dialog;
using Timelapse.Enums;

namespace Timelapse
{
    // View Menu Callbacks
    public partial class TimelapseWindow : Window, IDisposable
    {
        ImageAdjuster ImageAdjuster;

        // View sub-menu opening
        private void View_SubmenuOpening(object sender, RoutedEventArgs e)
        {
            this.FilePlayer_Stop(); // In case the FilePlayer is going

            bool state = this.IsDisplayingActiveSingleImage();
            this.MenuItemViewDifferencesCycleThrough.IsEnabled = state;
            this.MenuItemViewDifferencesCombined.IsEnabled = state;
            this.MenuItemZoomIn.IsEnabled = state;
            this.MenuItemZoomOut.IsEnabled = state;
            this.MenuItemBookmarkDefaultPanZoom.IsEnabled = state;
            this.MenuItemBookmarkSavePanZoom.IsEnabled = state;
            this.MenuItemBookmarkSetPanZoom.IsEnabled = state;
        }

        // View next filein this image set
        private void MenuItemShowNextFile_Click(object sender, RoutedEventArgs e)
        {
            this.TryFileShowWithoutSliderCallback(DirectionEnum.Next);
        }

        // View previous file in this image set
        private void MenuItemShowPreviousFile_Click(object sender, RoutedEventArgs e)
        {
            this.TryFileShowWithoutSliderCallback(DirectionEnum.Previous);
        }

        // Zoom in
        private void MenuItemZoomIn_Click(object sender, RoutedEventArgs e)
        {
            Point mousePosition = Mouse.GetPosition(this.MarkableCanvas.ImageToDisplay);
            this.MarkableCanvas.TryZoomInOrOut(true, mousePosition);
        }

        // Zoom out
        private void MenuItemZoomOut_Click(object sender, RoutedEventArgs e)
        {
            Point mousePosition = Mouse.GetPosition(this.MarkableCanvas.ImageToDisplay);
            this.MarkableCanvas.TryZoomInOrOut(false, mousePosition);
        }

        // Save a Bookmark of the current pan / zoom region 
        private void MenuItem_BookmarkSavePanZoom(object sender, RoutedEventArgs e)
        {
            this.MarkableCanvas.SetBookmark();
        }

        // Zoom to bookmarked _region: restores the zoom level / pan coordinates of the bookmark
        private void MenuItem_BookmarkSetPanZoom(object sender, RoutedEventArgs e)
        {
            this.MarkableCanvas.ApplyBookmark();
        }

        // Zoom out all the way: restores the level to an image that fills the space
        private void MenuItem_BookmarkDefaultPanZoom(object sender, RoutedEventArgs e)
        {
            this.MarkableCanvas.ZoomOutAllTheWay();
        }

        // Cycle through the image differences
        private void MenuItemViewDifferencesCycleThrough_Click(object sender, RoutedEventArgs e)
        {
            this.TryViewPreviousOrNextDifference();
        }

        // View  combined image differences
        private void MenuItemViewDifferencesCombined_Click(object sender, RoutedEventArgs e)
        {
            this.TryViewCombinedDifference();
        }
    }
}
