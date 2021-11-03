using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using Timelapse.ExifTool;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for TestingDialog.xaml
    /// </summary>
    public partial class TestSomeCodeDialog : Window
    {
        private readonly ExifToolManager  exifManager = new ExifToolManager();
        private readonly string Filepath = @"C:\Users\Owner\Desktop\Test sets\TutorialImageSet - Flat\IMG_0001.JPG";

        public TestSomeCodeDialog(Window owner)
        {
            InitializeComponent();
            this.Owner = owner;
        }

        private void ButtonStartExif_Click(object sender, RoutedEventArgs e)
        {
            this.exifManager.StartIfNotAlreadyStarted();
            this.StatusFeedback();
        }

        private void StatusFeedback()
        {
            if (this.exifManager.IsStarted)
            {
                this.ListFeedback.Items.Insert(0, "Started");
            }
            else
            {
                this.ListFeedback.Items.Insert(0, "Not Started");
            }
        }

        private void ShowExifDataInList(Dictionary<string, string> exifData)
        {
            this.ListExifData.Items.Clear();
            this.ListExifData.Items.Add("Count is " + exifData.Count);
            foreach (KeyValuePair<string, string> kvp in exifData)
            {
                this.ListExifData.Items.Add(kvp.Key + " | " + kvp.Value);
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

        private void ButtonGetMetadataByTags_Click(object sender, RoutedEventArgs e)
        {
            string[] tags = new string[] { "Ambient Temperature", "Ambient Temperature Fahrenheit", "FOOBAR"};
            Dictionary<string, string> exifData = this.exifManager.FetchExifFrom(this.Filepath, tags);
            this.ShowExifDataInList(exifData);
        }

        private void ButtonGetAllMetadata_Click(object sender, RoutedEventArgs e)
        {
            Dictionary<string, string> exifData = this.exifManager.FetchExifFrom(this.Filepath);
            this.ShowExifDataInList(exifData);
        }
    }
}
