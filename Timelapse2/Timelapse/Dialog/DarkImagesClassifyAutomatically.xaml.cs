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
    /// Interaction logic for DarkImagesClassifyOnFirstLoad.xaml
    /// </summary>
    public partial class DarkImagesClassifyAutomatically : Window
    {
        private TimelapseState timelapseState;
        public DarkImagesClassifyAutomatically(TimelapseState timelapseState, Window owner)
        {
            this.InitializeComponent();
            this.Owner = owner;
            this.timelapseState = timelapseState;
            this.CheckBoxClassifyDarkImagesAutomatically.IsChecked = this.timelapseState.ClassifyDarkImagesWhenLoading ? true : false;
        }

        private void CheckBoxClassifyDarkImagesWhenLoading_Click(object sender, RoutedEventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            this.timelapseState.ClassifyDarkImagesWhenLoading = (cb.IsChecked == true) ? true : false;
        }
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }
    }
}
