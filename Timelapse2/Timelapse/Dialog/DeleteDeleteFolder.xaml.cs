using System;
using System.Windows;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for DeleteDeleteFolder.xaml
    /// </summary>
    public partial class DeleteDeleteFolder : Window
    {
        private readonly int howManyDeleteFiles = 0;

        public DeleteDeleteFolder(int howManyDeleteFiles)
        {
            this.InitializeComponent();

            // If there are no files, just abort
            this.howManyDeleteFiles = howManyDeleteFiles;
        }

        // Adjust this dialog window position 
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dialogs.SetDefaultDialogPosition(this);
            Dialogs.TryFitDialogWindowInWorkingArea(this);
            this.Message.What = String.Format("Your 'DeletedFiles' sub-folder contains backups of {0} 'deleted' image or video files.", this.howManyDeleteFiles);
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
    }
}
