using System.Windows;
namespace Timelapse.Dialog
{
    /// <summary>
    /// Ask the user if he/she wants to update the root folder names in the database to match the name of the actual root folder where the template, data and images currently reside
    /// </summary>
    public partial class UpdateRootFolder : Window
    {
        private string dbfoldername;
        private string actualFolderName;
        public UpdateRootFolder(Window owner, string dbfoldername, string actualFolderName)
        {
            InitializeComponent();
            this.Owner = owner;
            this.dbfoldername = dbfoldername;
            this.actualFolderName = actualFolderName;
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dialogs.SetDefaultDialogPosition(this);
            Dialogs.TryFitDialogWindowInWorkingArea(this);
            this.Message.What = "A root folder location saved in your database (.ddb)  file is '" + dbfoldername + "'. However, your template is currently located in a different root folder '" + actualFolderName + "'.";
            this.Message.Solution = "Clicking Update will update the root folder location that is saved in your database from '" + dbfoldername + "' to '" + actualFolderName + "'.";
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
