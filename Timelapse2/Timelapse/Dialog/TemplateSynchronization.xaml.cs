using System.Collections.Generic;
using System.Windows;
using System.Windows.Documents;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for the mismatched templates dialog.
    /// </summary>
    public partial class TemplateSynchronization : Window
    {
        public TemplateSynchronization(List<string> errors, Window owner)
        {
            this.InitializeComponent();
            this.Owner = owner;
            foreach (string error in errors)
            {
                this.TextBlockDetails.Inlines.Add(new Run { Text = "     " + error });
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Utilities.SetDefaultDialogPosition(this);
            Utilities.TryFitWindowInWorkingArea(this);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        private void UseOriginalTemplateButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }
    }
}
