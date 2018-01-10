using Microsoft.Win32;
using Timelapse.Database;
using System;
using System.Windows;

namespace Timelapse.Util
{
    // Save the state of various things in the Registry.
    public class TimelapseUserRegistrySettings : UserRegistrySettings
    {
        #region Settings
        public bool AudioFeedback { get; set; }
        public CustomSelectionOperator CustomSelectionTermCombiningOperator { get; set; }
        public int DarkPixelThreshold { get; set; }
        public double DarkPixelRatioThreshold { get; set; }
        public DateTime MostRecentCheckForUpdates { get; set; }
        public MostRecentlyUsedList<string> MostRecentImageSets { get; private set; }
        public bool OrderFilesByDateTime { get; set; }
        public bool SuppressAmbiguousDatesDialog { get; set; }
        public bool SuppressCsvExportDialog { get; set; }
        public bool SuppressCsvImportPrompt { get; set; }
        public bool SuppressFileCountOnImportDialog { get; set; }
        public bool SuppressSelectedAmbiguousDatesPrompt { get; set; }
        public bool SuppressSelectedCsvExportPrompt { get; set; }
        public bool SuppressSelectedDarkThresholdPrompt { get; set; }
        public bool SuppressSelectedDateTimeFixedCorrectionPrompt { get; set; }
        public bool SuppressSelectedDateTimeLinearCorrectionPrompt { get; set; }
        public bool SuppressSelectedDaylightSavingsCorrectionPrompt { get; set; }
        public bool SuppressSelectedPopulateFieldFromMetadataPrompt { get; set; }
        public bool SuppressSelectedRereadDatesFromFilesPrompt { get; set; }
        public bool SuppressSelectedSetTimeZonePrompt { get; set; }
        public Throttles Throttles { get; private set; }
        public bool SuppressThrottleWhenLoading { get; set; }
        public Size TimelapseWindowSize { get; set; }
        public Rect TimelapseWindowPosition { get; set; }
        public bool ClassifyDarkImagesWhenLoading { get; set; }
        #endregion

        public TimelapseUserRegistrySettings() :
            this(Constant.Registry.RootKey)
        {
        }

        internal TimelapseUserRegistrySettings(string registryKey)
            : base(registryKey)
        {
            this.Throttles = new Throttles();
            this.ReadSettingsFromRegistry();
        }

        #region Read from registry
        // Read standard settings from registry
        public void ReadSettingsFromRegistry()
        {
            using (RegistryKey registryKey = this.OpenRegistryKey())
            {
                this.AudioFeedback = registryKey.ReadBoolean(Constant.Registry.TimelapseKey.AudioFeedback, false);
                this.ClassifyDarkImagesWhenLoading = registryKey.ReadBoolean(Constant.Registry.TimelapseKey.ClassifyDarkImagesWhenLoading, false);
                this.CustomSelectionTermCombiningOperator = registryKey.ReadEnum<CustomSelectionOperator>(Constant.Registry.TimelapseKey.CustomSelectionTermCombiningOperator, CustomSelectionOperator.And);
                this.DarkPixelRatioThreshold = registryKey.ReadDouble(Constant.Registry.TimelapseKey.DarkPixelRatio, Constant.Images.DarkPixelRatioThresholdDefault);
                this.DarkPixelThreshold = registryKey.ReadInteger(Constant.Registry.TimelapseKey.DarkPixelThreshold, Constant.Images.DarkPixelThresholdDefault);
                this.MostRecentCheckForUpdates = registryKey.ReadDateTime(Constant.Registry.TimelapseKey.MostRecentCheckForUpdates, DateTime.UtcNow);
                this.MostRecentImageSets = registryKey.ReadMostRecentlyUsedList(Constant.Registry.TimelapseKey.MostRecentlyUsedImageSets);
                this.OrderFilesByDateTime = registryKey.ReadBoolean(Constant.Registry.TimelapseKey.OrderFilesByDateTime, false);
                this.SuppressAmbiguousDatesDialog = registryKey.ReadBoolean(Constant.Registry.TimelapseKey.SuppressAmbiguousDatesDialog, false);
                this.SuppressCsvExportDialog = registryKey.ReadBoolean(Constant.Registry.TimelapseKey.SuppressCsvExportDialog, false);
                this.SuppressCsvImportPrompt = registryKey.ReadBoolean(Constant.Registry.TimelapseKey.SuppressCsvImportPrompt, false);
                this.SuppressFileCountOnImportDialog = registryKey.ReadBoolean(Constant.Registry.TimelapseKey.SuppressFileCountOnImportDialog, false);
                this.SuppressSelectedAmbiguousDatesPrompt = registryKey.ReadBoolean(Constant.Registry.TimelapseKey.SuppressSelectedAmbiguousDatesPrompt, false);
                this.SuppressSelectedCsvExportPrompt = registryKey.ReadBoolean(Constant.Registry.TimelapseKey.SuppressSelectedCsvExportPrompt, false);
                this.SuppressSelectedDarkThresholdPrompt = registryKey.ReadBoolean(Constant.Registry.TimelapseKey.SuppressSelectedDarkThresholdPrompt, false);
                this.SuppressSelectedDateTimeFixedCorrectionPrompt = registryKey.ReadBoolean(Constant.Registry.TimelapseKey.SuppressSelectedDateTimeFixedCorrectionPrompt, false);
                this.SuppressSelectedDateTimeLinearCorrectionPrompt = registryKey.ReadBoolean(Constant.Registry.TimelapseKey.SuppressSelectedDateTimeLinearCorrectionPrompt, false);
                this.SuppressSelectedDaylightSavingsCorrectionPrompt = registryKey.ReadBoolean(Constant.Registry.TimelapseKey.SuppressSelectedDaylightSavingsCorrectionPrompt, false);
                this.SuppressSelectedPopulateFieldFromMetadataPrompt = registryKey.ReadBoolean(Constant.Registry.TimelapseKey.SuppressSelectedPopulateFieldFromMetadataPrompt, false);
                this.SuppressSelectedRereadDatesFromFilesPrompt = registryKey.ReadBoolean(Constant.Registry.TimelapseKey.SuppressSelectedRereadDatesFromFilesPrompt, false);
                this.SuppressSelectedSetTimeZonePrompt = registryKey.ReadBoolean(Constant.Registry.TimelapseKey.SuppressSelectedSetTimeZonePrompt, false);
                this.SuppressThrottleWhenLoading = registryKey.ReadBoolean(Constant.Registry.TimelapseKey.SuppressThrottleWhenLoading, false);
                this.Throttles.SetDesiredImageRendersPerSecond(registryKey.ReadDouble(Constant.Registry.TimelapseKey.DesiredImageRendersPerSecond, Constant.ThrottleValues.DesiredMaximumImageRendersPerSecondDefault));
                this.TimelapseWindowPosition = registryKey.ReadRect(Constant.Registry.TimelapseKey.TimelapseWindowPosition, new Rect(0.0, 0.0, 1350.0, 900.0));
            }
        }

        public bool IsRegistryKeyExists(string key)
        {
            using (RegistryKey registryKey = this.OpenRegistryKey())
            {
                return registryKey.ReadString(key, String.Empty) != String.Empty;
            }
        }
        // Read a single registry entry
        public string ReadFromRegistryString(string key)
        {
            using (RegistryKey registryKey = this.OpenRegistryKey())
            {
                return registryKey.ReadString(key, String.Empty);
            }
        }
        public Rect ReadFromRegistryRect(string key)
        {
            using (RegistryKey registryKey = this.OpenRegistryKey())
            {
                return registryKey.ReadRect(key, new Rect(0.0, 0.0, 1350.0, 900.0)); 
            }
        }
        #endregion

        #region Write to registry
        // Write standard settings to registry
        public void WriteSettingsToRegistry()
        {
            using (RegistryKey registryKey = this.OpenRegistryKey())
            {
                registryKey.Write(Constant.Registry.TimelapseKey.AudioFeedback, this.AudioFeedback);
                registryKey.Write(Constant.Registry.TimelapseKey.TimelapseWindowPosition, this.TimelapseWindowPosition);
                registryKey.Write(Constant.Registry.TimelapseKey.CustomSelectionTermCombiningOperator, this.CustomSelectionTermCombiningOperator.ToString());
                registryKey.Write(Constant.Registry.TimelapseKey.DarkPixelRatio, this.DarkPixelRatioThreshold);
                registryKey.Write(Constant.Registry.TimelapseKey.DarkPixelThreshold, this.DarkPixelThreshold);
                registryKey.Write(Constant.Registry.TimelapseKey.DesiredImageRendersPerSecond, this.Throttles.DesiredImageRendersPerSecond);
                registryKey.Write(Constant.Registry.TimelapseKey.MostRecentCheckForUpdates, this.MostRecentCheckForUpdates);
                registryKey.Write(Constant.Registry.TimelapseKey.MostRecentlyUsedImageSets, this.MostRecentImageSets);
                registryKey.Write(Constant.Registry.TimelapseKey.OrderFilesByDateTime, this.OrderFilesByDateTime);
                registryKey.Write(Constant.Registry.TimelapseKey.SuppressAmbiguousDatesDialog, this.SuppressAmbiguousDatesDialog);
                registryKey.Write(Constant.Registry.TimelapseKey.SuppressCsvExportDialog, this.SuppressCsvExportDialog);
                registryKey.Write(Constant.Registry.TimelapseKey.SuppressCsvImportPrompt, this.SuppressCsvImportPrompt);
                registryKey.Write(Constant.Registry.TimelapseKey.SuppressFileCountOnImportDialog, this.SuppressFileCountOnImportDialog);
                registryKey.Write(Constant.Registry.TimelapseKey.SuppressSelectedAmbiguousDatesPrompt, this.SuppressSelectedAmbiguousDatesPrompt);
                registryKey.Write(Constant.Registry.TimelapseKey.SuppressSelectedCsvExportPrompt, this.SuppressSelectedCsvExportPrompt);
                registryKey.Write(Constant.Registry.TimelapseKey.SuppressSelectedDarkThresholdPrompt, this.SuppressSelectedDarkThresholdPrompt);
                registryKey.Write(Constant.Registry.TimelapseKey.SuppressSelectedDateTimeFixedCorrectionPrompt, this.SuppressSelectedDateTimeFixedCorrectionPrompt);
                registryKey.Write(Constant.Registry.TimelapseKey.SuppressSelectedDateTimeLinearCorrectionPrompt, this.SuppressSelectedDateTimeLinearCorrectionPrompt);
                registryKey.Write(Constant.Registry.TimelapseKey.SuppressSelectedDaylightSavingsCorrectionPrompt, this.SuppressSelectedDaylightSavingsCorrectionPrompt);
                registryKey.Write(Constant.Registry.TimelapseKey.SuppressSelectedPopulateFieldFromMetadataPrompt, this.SuppressSelectedPopulateFieldFromMetadataPrompt);
                registryKey.Write(Constant.Registry.TimelapseKey.SuppressSelectedSetTimeZonePrompt, this.SuppressSelectedSetTimeZonePrompt);
                registryKey.Write(Constant.Registry.TimelapseKey.SuppressSelectedRereadDatesFromFilesPrompt, this.SuppressSelectedRereadDatesFromFilesPrompt);
                registryKey.Write(Constant.Registry.TimelapseKey.SuppressThrottleWhenLoading, this.SuppressThrottleWhenLoading);
                registryKey.Write(Constant.Registry.TimelapseKey.ClassifyDarkImagesWhenLoading, this.ClassifyDarkImagesWhenLoading);
            }
        }

        // Write a single registry entry 
        public void WriteToRegistry(string key, string value)
        {
            using (RegistryKey registryKey = this.OpenRegistryKey())
            {
                registryKey.Write(key, value);
            }
        }

        public void WriteToRegistry(string key, Rect value)
        {
            using (RegistryKey registryKey = this.OpenRegistryKey())
            {
                registryKey.Write(key, value);
            }
        }
        #endregion
    }
}
