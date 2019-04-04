using System.Windows;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Classify DarkImages on FirstLoad options
    /// </summary>
    public partial class DarkImagesClassifyAutomatically : Window
    {
        private TimelapseState timelapseState;
        public DarkImagesClassifyAutomatically(TimelapseState timelapseState, Window owner)
        {
            this.InitializeComponent();
            this.Owner = owner;
            this.timelapseState = timelapseState;
        }

        private void ClassifyButton_Click(object sender, RoutedEventArgs e)
        {
            this.timelapseState.ClassifyDarkImagesWhenLoading = true;
            this.DialogResult = true;
        }

        private void DontClassifyButton_Click(object sender, RoutedEventArgs e)
        {
            this.timelapseState.ClassifyDarkImagesWhenLoading = false;
            this.DialogResult = true;
        }
    }
}
