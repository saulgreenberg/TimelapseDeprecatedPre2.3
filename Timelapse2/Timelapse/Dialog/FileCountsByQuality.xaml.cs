using System;
using System.Collections.Generic;
using System.Windows;
using Timelapse.Database;
using Timelapse.Util;

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
        public FileCountsByQuality(Dictionary<FileSelection, int> counts, Window owner)
        {
            this.InitializeComponent();
            this.Owner = owner;

            // Fill in the counts
            int light = counts[FileSelection.Ok];
            this.Light.Text = String.Format("{0,5}", light);
            int fileNoLongerAvailable = counts[FileSelection.Missing];
            this.FileNoLongerAvailable.Text = String.Format("{0,5}", fileNoLongerAvailable);
            int dark = counts[FileSelection.Dark];
            this.Dark.Text = String.Format("{0,5}", dark);
            int corrupted = counts[FileSelection.Corrupted];
            this.Corrupted.Text = String.Format("{0,5}", corrupted);

            int total = light + dark + corrupted + fileNoLongerAvailable;
            this.Total.Text = String.Format("{0,5}", total);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Utilities.SetDefaultDialogPosition(this);
            Utilities.TryFitWindowInWorkingArea(this);
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }
    }
}