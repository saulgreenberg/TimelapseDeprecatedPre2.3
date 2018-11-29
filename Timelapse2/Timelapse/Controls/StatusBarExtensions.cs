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

        // Set the sequence number of the current file in the number portion of the status bar
        public static void SetCurrentFile(this StatusBar statusBar, int currentImage)
        {
            StatusBarItem item = (StatusBarItem)statusBar.Items[1];
            item.Content = currentImage.ToString();
        }
        
        // Set the total counts in the total counts portion of the status bar
        public static void SetCount(this StatusBar statusBar, int selectedImageCount)
        {
            StatusBarItem item = (StatusBarItem)statusBar.Items[3];
            item.Content = selectedImageCount.ToString();
        }

        // Display a view in the View portion of the status bar
        public static void SetView(this StatusBar statusBar, string view)
        {
            StatusBarItem item = (StatusBarItem)statusBar.Items[6];
            item.Content = view;
        }

        // Display a message in the sort portion of the status bar
        // Note that we massage the message in a few cases (e.g., for File and for Id types
        public static void SetSort(this StatusBar statusBar, string primarySortTerm1, string primarySortTerm2, string secondarySortTerm1, string secondarySortTerm2)
        {
            StatusBarItem item = (StatusBarItem)statusBar.Items[9];
            string message = String.Empty;

            // If there is no primary sort string, then we don't know what the sorting criteria is.
            // Note that this should not happen
            if (String.IsNullOrEmpty (primarySortTerm1))
            {
                item.Content = "Unknown";
                return;
            }

            // Add the primary first key
            message += SetSortAlterTextIfNeeded(primarySortTerm1);

            // Add the primary second key if it exists
            if (!String.IsNullOrEmpty(primarySortTerm2))
            {
                message += "+" + SetSortAlterTextIfNeeded(primarySortTerm2);
            }

            // Add the secomdary first key if it exists
            if (!String.IsNullOrEmpty(secondarySortTerm1))
            {
                message += " then by " + SetSortAlterTextIfNeeded(secondarySortTerm1);
            }
            if (!String.IsNullOrEmpty(secondarySortTerm2))
            {
                message += "+" + SetSortAlterTextIfNeeded(secondarySortTerm2);
            }
            item.Content = message;
        }

        private static string SetSortAlterTextIfNeeded(string sortTerm)
        {
            switch (sortTerm)
            {
                case Constant.DatabaseColumn.File:
                    return "File name";
                case Constant.DatabaseColumn.ID:
                    return "Id (the order files were added to Timelapse)";
                case Constant.DatabaseColumn.DateTime:
                    return "Date+Time";
                default:
                    return sortTerm;
            }
        }

        // Display a message in the message portion of the status bar
        public static void SetMessage(this StatusBar statusBar, string message)
        {
            StatusBarItem item = (StatusBarItem)statusBar.Items[11];
            item.Content = message;
        }
    }
}
