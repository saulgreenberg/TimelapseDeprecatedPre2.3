using System;
using System.Windows.Controls.Primitives;

namespace Timelapse.Controls
{
    /// <summary>
    /// The Status Bar convenience class that collects methods to update different parts of the status bar
    /// </summary>
    internal static class StatusBarExtensions
    {
        // Clear the message portion of the status bar
        public static void ClearMessage(this StatusBar statusBar)
        {
            statusBar.SetMessage(String.Empty);
        }

        // Set the total counts in the total counts portion of the status bar
        public static void SetCount(this StatusBar statusBar, int selectedImageCount)
        {
            StatusBarItem item = (StatusBarItem)statusBar.Items[3];
            item.Content = selectedImageCount.ToString();
        }

        // Set the total counts in the total coutns portion of the status bar
        public static void SetCurrentFile(this StatusBar statusBar, int currentImage)
        {
            StatusBarItem item = (StatusBarItem)statusBar.Items[1];
            item.Content = currentImage.ToString();
        }

        // Display a message in the message portion of the status bar
        public static void SetMessage(this StatusBar statusBar, string message)
        {
            StatusBarItem item = (StatusBarItem)statusBar.Items[5];
            item.Content = message;
        }

        // Display a view in the View portion of the status bar
        public static void SetView(this StatusBar statusBar, string view)
        {
            StatusBarItem item = (StatusBarItem)statusBar.Items[4];
            item.Content = view;
        }
    }
}
