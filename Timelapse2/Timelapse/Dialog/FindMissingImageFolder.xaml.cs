
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
        private string FolderPath;
        private string MissingFolderName;

        public string NewFolderName { get; set; }

        public FindMissingImageFolder(Window owner, string folderPath, string missingFolderName)
        {
            InitializeComponent();
            this.Owner = owner;
            this.FolderPath = folderPath;
            this.MissingFolderName = missingFolderName;
            this.Message.Title = "Locate the image folder: " + this.MissingFolderName ;
            this.Message.Problem = "Timelapse could not locate the image folder: " + this.MissingFolderName + ".";
            this.NewFolderName = String.Empty;
        }

        // Adjust this dialog window position 
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Utilities.SetDefaultDialogPosition(this);
            Utilities.TryFitWindowInWorkingArea(this);
        }


        private void LocateFolderButton_Click(object sender, RoutedEventArgs e)
        {
            string newFolderPath = string.Empty;
            CommonOpenFileDialog folderSelectionDialog = new CommonOpenFileDialog()
            {
                Title = "Select one or more folders ...",
                DefaultDirectory = this.FolderPath,
                IsFolderPicker = true,
                Multiselect = false
            };
            folderSelectionDialog.InitialDirectory = folderSelectionDialog.DefaultDirectory;
            folderSelectionDialog.FolderChanging += this.FolderSelectionDialog_FolderChanging;
            if (folderSelectionDialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                this.NewFolderName = (folderSelectionDialog.FileName.Length > this.FolderPath.Length) ? folderSelectionDialog.FileName.Substring(this.FolderPath.Length + 1) : String.Empty;
                this.TextBoxNewFolderName.Text = this.NewFolderName;
            }
        }
        private void FolderSelectionDialog_FolderChanging(object sender, CommonFileDialogFolderChangeEventArgs e)
        {
            // require folders to be loaded be either the same folder as the .tdb and .ddb or subfolders of it
            if (e.Folder.StartsWith(this.FolderPath, StringComparison.OrdinalIgnoreCase) == false)
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
