using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Timelapse.ExifTool;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for TestingDialog.xaml
    /// </summary>
    public partial class TestSomeCodeDialog : Window
    {
        ExifToolManager exifManager = new ExifToolManager();
        public TestSomeCodeDialog(Window owner)
        {
            InitializeComponent();
            this.Owner = owner;
        }

        private void ButtonStartExif_Click(object sender, RoutedEventArgs e)
        {
            this.exifManager.Start();
            this.StatusFeedback();
        }

        private void StatusFeedback()
        {
            if (this.exifManager.IsStarted())
            {
                this.Feedback.Items.Add("Started");
            }
            else
            {
                this.Feedback.Items.Add("Not Started");
            }
        }

        private void ButtonShowStatus_Click(object sender, RoutedEventArgs e)
        {
            StatusFeedback();
        }

        private void ButtonStopExif_Click(object sender, RoutedEventArgs e)
        {
            if (this.exifManager != null)
            {
                //this.exifManager.Stop();
                //Task.Run(() => this.exifManager.Stop());
                this.exifManager.Stop();
                StatusFeedback();
            }
        }

        private void ButtonKillProcesses_Click(object sender, RoutedEventArgs e)
        {
            foreach (var process in Process.GetProcessesByName("exiftool(-k)"))
            {
                System.Diagnostics.Debug.Print(process.ProcessName);
                process.Kill();
            }
        }

        private void ButtonShowMetadata_Click(object sender, RoutedEventArgs e)
        {
            this.ShowMetadata();
        }

        private void ShowMetadata()
        {
            if (this.exifManager != null && this.exifManager.IsStarted())
            {
                string file = @"C:\Users\Owner\Desktop\Test sets\TutorialImageSet - Flat\IMG_0001.JPG";
                DateTime? datetime = this.exifManager.ExifTool.GetCreationTime(file);
                this.Feedback.Items.Add("creation time: "+ datetime.ToString());
            }
            else
            {
                this.Feedback.Items.Add("Can't get creation time");
            }
        }

    }
}
