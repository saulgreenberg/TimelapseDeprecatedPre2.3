using System;
using System.Collections.Generic;
using System.Windows;
using Timelapse.Database;
using Timelapse.Enums;
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
        public FileCountsByQuality(Dictionary<FileSelectionEnum, int> counts, Window owner)
        {
            this.InitializeComponent();
            this.Owner = owner;

            // Fill in the counts
            int light = counts[FileSelectionEnum.Light];
            this.Light.Text = String.Format("{0,5}", light);
            int dark = counts[FileSelectionEnum.Dark];
            this.Dark.Text = String.Format("{0,5}", dark);
            int unknown = counts[FileSelectionEnum.Unknown];
            this.Unknown.Text = String.Format("{0,5}", unknown);
            int total = light + dark + unknown;
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