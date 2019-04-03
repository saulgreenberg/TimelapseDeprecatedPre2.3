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
using System.Windows.Shapes;
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
