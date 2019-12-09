using System;
using System.Diagnostics;
using System.Windows;
using Timelapse.Dialog;

namespace Timelapse
{
    // Help Menu Callbacks
    public partial class TimelapseWindow : Window, IDisposable
    {
        // Help sub-menu opening
        private void Help_SubmenuOpening(object sender, RoutedEventArgs e)
        {
            this.FilePlayer_Stop(); // In case the FilePlayer is going
        }

        // Timelapse web page (via your browser): Timelapse home page
        private void MenuTimelapseWebPage_Click(object sender, RoutedEventArgs e)
        {
            Uri uri = new Uri("http://saul.cpsc.ucalgary.ca/timelapse/pmwiki.php?n=Main.Version2HomePage");
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri));
        }

        // Tutorial manual (via your browser) 
        private void MenuTutorialManual_Click(object sender, RoutedEventArgs e)
        {
            Uri uri = new Uri("http://saul.cpsc.ucalgary.ca/timelapse/uploads/Installs/Timelapse2/Timelapse2Manual.pdf");
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri));
        }

        // Download tutorial sample images (via your web browser) 
        private void MenuDownloadSampleImages_Click(object sender, RoutedEventArgs e)
        {
            Uri uri = new Uri("http://saul.cpsc.ucalgary.ca/timelapse/uploads/Main/TutorialImageSet2.zip");
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri));
        }

        // Timelapse mailing list - Join it (via your web browser)
        private void MenuJoinTimelapseMailingList_Click(object sender, RoutedEventArgs e)
        {
            Uri uri = new Uri("http://mailman.ucalgary.ca/mailman/listinfo/timelapse-l");
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri));
        }

        // Timelapse mailing list - Send email
        private void MenuMailToTimelapseMailingList_Click(object sender, RoutedEventArgs e)
        {
            Uri uri = new Uri("mailto:timelapse-l@mailman.ucalgary.ca");
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri));
        }

        // About: Display a message describing the version, etc.
        private void MenuItemAbout_Click(object sender, RoutedEventArgs e)
        {
            AboutTimelapse about = new AboutTimelapse(this);
            if ((about.ShowDialog() == true) && about.MostRecentCheckForUpdate.HasValue)
            {
                this.State.MostRecentCheckForUpdates = about.MostRecentCheckForUpdate.Value;
            }
        }
    }
}
