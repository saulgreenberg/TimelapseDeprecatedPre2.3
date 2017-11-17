using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    public partial class AboutTimelapse : Window
    {
        public Nullable<DateTime> MostRecentCheckForUpdate { get; private set; }

        public AboutTimelapse(Window owner)
        {
            this.InitializeComponent();
            Utilities.TryFitWindowInWorkingArea(this);
            this.NavigateVersionUrl.NavigateUri = Constant.VersionChangesAddress;
            this.NavigateCreativeCommonLicense.NavigateUri = Constant.CreativeCommonsLicense;
            this.NavigateAdditionalLicenseDetails.NavigateUri = Constant.AdditionalLicenseDetails;

            this.Owner = owner;
            Version curVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            this.Version.Text = curVersion.ToString();

            this.MostRecentCheckForUpdate = null;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void CheckForUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            VersionClient updater = new VersionClient(this, Constant.ApplicationName, Constant.LatestVersionFileNameXML);
            if (updater.TryGetAndParseVersion(true))
            {
                // SAULXXX: TO CHECK. This isn't quite right, as the mostrecentcheckfor update data is set only (I think) if there is a new release
                // (as true is only returned by TryGetAndParseVersion when that happens, I think).
                // Perhaps the most recent check date should be done anytime a check is done???
                this.MostRecentCheckForUpdate = DateTime.UtcNow;
            }
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }
    }
}
