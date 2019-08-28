using System;
using System.Collections.Generic;
using System.Windows;
using Timelapse.Common;
using Timelapse.Database;
using Timelapse.Enums;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Dialog to show the user some statistics about the images
    /// </summary>
    public partial class FileCountsByQuality : Window
    {
        /// <summary>
        /// Show the user some statistics about the images in a dialog box
        /// </summary>
        public FileCountsByQuality(Dictionary<FileSelectionType, int> counts, Window owner)
        {
            this.InitializeComponent();
            this.Owner = owner;

            // Fill in the counts
            int ok = counts[FileSelectionType.Ok];
            this.Light.Text = String.Format("{0,5}", ok);
            int dark = counts[FileSelectionType.Dark];
            this.Dark.Text = String.Format("{0,5}", dark);
            int total = ok + dark;
            this.Total.Text = String.Format("{0,5}", total);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dialogs.SetDefaultDialogPosition(this);
            Dialogs.TryFitDialogWindowInWorkingArea(this);
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }
    }
}