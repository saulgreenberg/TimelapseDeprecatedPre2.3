using System.IO;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    public partial class RenameFileDatabaseFile : Window
    {
        private string currentFileName;
        public string NewFilename { get; private set; }

        public RenameFileDatabaseFile(string fileName, Window owner)
        {
            this.InitializeComponent();

            this.currentFileName = fileName;
            this.Owner = owner;
            this.NewFilename = Path.GetFileNameWithoutExtension(fileName);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Utilities.SetDefaultDialogPosition(this);
            Utilities.TryFitDialogWindowInWorkingArea(this);

            this.runOriginalFileName.Text = this.currentFileName;
            this.txtboxNewFileName.Text = this.NewFilename;
            this.OkButton.IsEnabled = false;
            this.txtboxNewFileName.TextChanged += this.TxtboxNewFileName_TextChanged;
        }

        private void TxtboxNewFileName_TextChanged(object sender, TextChangedEventArgs e)
        {
            this.NewFilename = this.txtboxNewFileName.Text + ".ddb";
            this.OkButton.IsEnabled = !this.NewFilename.Equals(this.currentFileName); // Enable the button only if the two names differ
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
