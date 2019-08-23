using System;
using System.Collections.Generic;
using System.Windows;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for ChooseDetectorFilePath.xaml
    /// </summary>
    public partial class ChooseDetectorFilePath : Window
    {
        // This will contain the file selected by the user
        public string SelectedFolder { get; set; }
        public ChooseDetectorFilePath(List<string> candidateFolders, string truncatedTerm, string imageFilePath, Window owner)
        {
            this.InitializeComponent();
            this.Owner = owner;
            this.SelectedFolder = String.Empty;
            FolderPathsListbox.ItemsSource = candidateFolders;
            this.Message.Problem = "The detection file was run on a folder called '" + truncatedTerm + "' that contained many sub-folders with images." + Environment.NewLine;
            this.Message.Problem += "The folder containing your image set is one of those sub-folders." + Environment.NewLine;
            this.Message.Problem += "The problem is that several sub-folders in the detection data match your subfolder's name." + Environment.NewLine;
            this.Message.Problem += "Timelapse doesn't know which one to use.";
            this.Message.Solution = "Select the folder that originally held the sample image file named below." + Environment.NewLine;
            this.Message.Solution += "If you are unsure, select Cancel which will ignore the detection data until you find out.";
            this.ImageMessageLabel.Content += imageFilePath;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dialogs.SetDefaultDialogPosition(this);
            Dialogs.TryFitDialogWindowInWorkingArea(this);
            // marking the OK button IsDefault to associate it with dialog completion also gives it initial focus
            // It's more helpful to put focus on the database list as this saves having to tab to the list as a first step.
            this.FolderPathsListbox.Focus();
        }

        private void FolderPathsListbox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (this.FolderPathsListbox.SelectedIndex != -1)
            {
                this.OkButton_Click(sender, e);
            }
        }

        private void FolderPathsListbox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            this.OkButton.IsEnabled = this.FolderPathsListbox.SelectedIndex != -1;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.SelectedFolder = this.FolderPathsListbox.SelectedItem.ToString(); // The selected file
            this.DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
    }
}
