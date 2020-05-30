using System;
using System.Windows;
using Timelapse.Dialog;
using Timelapse.Util;

namespace Timelapse
{
    // Help Menu Callbacks
    public partial class TimelapseWindow : Window, IDisposable
    {
        #region Help sub-menu opening
        private void Help_SubmenuOpening(object sender, RoutedEventArgs e)
        {
            this.FilePlayer_Stop(); // In case the FilePlayer is going
        }
        #endregion

        #region Timelapse web site: home, tutorial manual, sample images
        // Timelapse web page (via your browser): Timelapse home page
        private void MenuTimelapseWebPage_Click(object sender, RoutedEventArgs e)
        {
            ProcessExecution.TryProcessStart(new Uri("http://saul.cpsc.ucalgary.ca/timelapse"));
        }

        // Tutorial manual (via your browser) 
        private void MenuTutorialManual_Click(object sender, RoutedEventArgs e)
        {
            ProcessExecution.TryProcessStart(new Uri("http://saul.cpsc.ucalgary.ca/timelapse/uploads/Installs/Timelapse2/Timelapse2Manual.pdf"));
        }

        // Download tutorial sample images (via your web browser) 
        private void MenuDownloadSampleImages_Click(object sender, RoutedEventArgs e)
        {
            ProcessExecution.TryProcessStart(new Uri("http://saul.cpsc.ucalgary.ca/timelapse/pmwiki.php?n=Main.UserGuide"));
        }
        #endregion

        #region Timelapse mailing list - Join and/or send email
        // Timelapse mailing list - Join it(via your web browser)
        private void MenuJoinTimelapseMailingList_Click(object sender, RoutedEventArgs e)
        {
            ProcessExecution.TryProcessStart(new Uri("http://mailman.ucalgary.ca/mailman/listinfo/timelapse-l"));
        }

        // Timelapse mailing list - Send email
        private void MenuMailToTimelapseMailingList_Click(object sender, RoutedEventArgs e)
        {
            ProcessExecution.TryProcessStart(new Uri("mailto:timelapse-l@mailman.ucalgary.ca"));
        }
        #endregion

        #region Mail the timelapse developers
        private void MenuMailToTimelapseDevelopers_Click(object sender, RoutedEventArgs e)
        {
            ProcessExecution.TryProcessStart(new Uri("mailto:saul@ucalgary.ca"));
        }
        #endregion

        #region About: Display a message describing the version,check for updates etc.
        private void MenuItemAbout_Click(object sender, RoutedEventArgs e)
        {
            AboutTimelapse about = new AboutTimelapse(this);
            if ((about.ShowDialog() == true) && about.MostRecentCheckForUpdate.HasValue)
            {
                this.State.MostRecentCheckForUpdates = about.MostRecentCheckForUpdate.Value;
            }
        }
        #endregion
    }
}
