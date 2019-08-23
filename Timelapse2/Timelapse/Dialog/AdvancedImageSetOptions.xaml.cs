using System;
using System.Windows;
using Timelapse.Database;

namespace Timelapse.Dialog
{
    public partial class AdvancedImageSetOptions : Window
    {
        private TimeZoneInfo currentImageSetTimeZone;
        private FileDatabase database;

        public AdvancedImageSetOptions(FileDatabase database, Window owner)
        {
            this.InitializeComponent();
            this.Owner = owner;
            this.database = database;

            this.currentImageSetTimeZone = database.ImageSet.GetSystemTimeZone();
            this.TimeZones.SelectedItem = this.currentImageSetTimeZone.DisplayName;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.database.ImageSet.TimeZone = this.TimeZones.TimeZonesByDisplayName[(string)this.TimeZones.SelectedItem].Id;
            this.database.SyncImageSetToDatabase();

            this.DialogResult = true;
        }

        private void ResetTimeZone_Click(object sender, RoutedEventArgs e)
        {
            this.TimeZones.SelectedItem = this.currentImageSetTimeZone;
        }
    }
}