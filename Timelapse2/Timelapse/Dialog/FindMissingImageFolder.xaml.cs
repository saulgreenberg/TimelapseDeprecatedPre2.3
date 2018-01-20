using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Windows;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for FindMissingImageFolder.xaml
    /// </summary>
    public partial class FindMissingImageFolder : Window
    {
        private string folderPath;
        private string missingFolderName;

        public string NewFolderName { get; set; }

        public FindMissingImageFolder(Window owner, string folderPath, string missingFolderName)
        {
            InitializeComponent();
            this.Owner = owner;
            this.folderPath = folderPath;
            this.missingFolderName = missingFolderName;
            this.Message.Title = "Locate the image folder: " + this.missingFolderName;
            this.Message.Problem = "Timelapse could not locate the image folder: " + this.missingFolderName + ".";
            this.NewFolderName = String.Empty;
            this.MissingFolderNameRun.Text = this.missingFolderName;
        }

        // Adjust this dialog window position 
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Utilities.SetDefaultDialogPosition(this);
            Utilities.TryFitDialogWindowInWorkingArea(this);
        }

        // Folder dialog where the user can only select a sub-folder of the root folder path
        private void LocateFolderButton_Click(object sender, RoutedEventArgs e)
        {
            string newFolderPath = string.Empty;
            CommonOpenFileDialog folderSelectionDialog = new CommonOpenFileDialog()
            {
                Title = "Select one or more folders ...",
                DefaultDirectory = this.folderPath,
                IsFolderPicker = true,
                Multiselect = false
            };
            folderSelectionDialog.InitialDirectory = folderSelectionDialog.DefaultDirectory;
            folderSelectionDialog.FolderChanging += this.FolderSelectionDialog_FolderChanging;
            if (folderSelectionDialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                // Trim the root folder path from the folder name to produce a relative path. Insert it into the textbox for feedback
                this.NewFolderName = (folderSelectionDialog.FileName.Length > this.folderPath.Length) ? folderSelectionDialog.FileName.Substring(this.folderPath.Length + 1) : String.Empty;
                this.TextBoxNewFolderName.Text = this.NewFolderName;
                this.OkButton.IsEnabled = !String.IsNullOrWhiteSpace(this.NewFolderName);
            }
            else
            {
                this.NewFolderName = String.Empty;
                this.OkButton.IsEnabled = false;
            }
        }

        // Limit the folder selection to only those that are sub-folders of the folder path
        private void FolderSelectionDialog_FolderChanging(object sender, CommonFileDialogFolderChangeEventArgs e)
        {
            // require folders to be loaded be either the same folder as the .tdb and .ddb or subfolders of it
            if (e.Folder.StartsWith(this.folderPath, StringComparison.OrdinalIgnoreCase) == false)
            {
                e.Cancel = true;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }
    }
}
