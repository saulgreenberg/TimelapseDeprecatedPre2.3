using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for DeleteDeleteFolder.xaml
    /// </summary>
    public partial class DeleteDeleteFolder : Window
    {
        private int HowManyDeleteFiles = 0;

        public DeleteDeleteFolder(int howManyDeleteFiles)
        {
            InitializeComponent();

            // If there are no files, just abort
            this.HowManyDeleteFiles = howManyDeleteFiles;
        }

        // Adjust this dialog window position 
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dialogs.SetDefaultDialogPosition(this);
            Dialogs.TryFitDialogWindowInWorkingArea(this);
            Message.What = String.Format("Your 'DeletedFiles' sub-folder contains backups of {0} 'deleted' image or video files.", this.HowManyDeleteFiles);
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
