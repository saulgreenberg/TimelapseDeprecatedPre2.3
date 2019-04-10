using Microsoft.Win32;
using Timelapse.Database;
using System;
using System.Windows;
using Timelapse.Enums;

namespace Timelapse.Util
{
    // Save the state of various things in the Registry.
    public class TimelapseUserRegistrySettings : UserRegistrySettings
    {
        #region Settings
        public bool AudioFeedback { get; set; }
        public Point BookmarkScale { get; set; }
        public Point BookmarkTranslation { get; set; }
        public Double BoundingBoxDisplayThreshold { get; set; }
        public CustomSelectionOperatorEnum CustomSelectionTermCombiningOperator { get; set; }
        public int DarkPixelThreshold { get; set; }
        public double DarkPixelRatioThreshold { get; set; }
        public TimeSpan EpisodeTimeThreshold { get; set; }
        public DeleteFolderManagementEnum DeleteFolderManagement { get; set; }
        public double FilePlayerSlowValue { get; set; }
        public double FilePlayerFastValue { get; set; }
        public DateTime MostRecentCheckForUpdates { get; set; }
        public MostRecentlyUsedCollection<string> MostRecentImageSets { get; private set; }
        public Rect QuickPasteWindowPosition { get; set; }
        public double SpeciesDetectedThreshold { get; set; }
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
        public bool ShowAllImagesWhenLoading { get; set; }
        public bool TabOrderIncludeDateTime { get; set; }
        public bool TabOrderIncludeDeleteFlag { get; set; }
        public bool TabOrderIncludeImageQuality { get; set; }

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
                this.BookmarkScale = new Point(registryKey.ReadDouble(Constant.Registry.TimelapseKey.BookmarkScaleX, 1.0), registryKey.ReadDouble(Constant.Registry.TimelapseKey.BookmarkScaleY, 1.0));
                this.BookmarkTranslation = new Point(registryKey.ReadDouble(Constant.Registry.TimelapseKey.BookmarkTranslationX, 1.0), registryKey.ReadDouble(Constant.Registry.TimelapseKey.BookmarkTranslationY, 1.0));
                this.BoundingBoxDisplayThreshold = registryKey.ReadDouble(Constant.Registry.TimelapseKey.BoundingBoxDisplayThreshold, Constant.Recognition.BoundingBoxDisplayThresholdDefault);
                this.ClassifyDarkImagesWhenLoading = registryKey.ReadBoolean(Constant.Registry.TimelapseKey.ClassifyDarkImagesWhenLoading, false);
                this.CustomSelectionTermCombiningOperator = registryKey.ReadEnum<CustomSelectionOperatorEnum>(Constant.Registry.TimelapseKey.CustomSelectionTermCombiningOperator, CustomSelectionOperatorEnum.And);
                this.DarkPixelRatioThreshold = registryKey.ReadDouble(Constant.Registry.TimelapseKey.DarkPixelRatio, Constant.ImageValues.DarkPixelRatioThresholdDefault);
                this.DarkPixelThreshold = registryKey.ReadInteger(Constant.Registry.TimelapseKey.DarkPixelThreshold, Constant.ImageValues.DarkPixelThresholdDefault);
                this.DeleteFolderManagement = (DeleteFolderManagementEnum)registryKey.ReadInteger(Constant.Registry.TimelapseKey.DeleteFolderManagementValue, (int)DeleteFolderManagementEnum.ManualDelete);
                this.EpisodeTimeThreshold = registryKey.ReadTimeSpan(Constant.Registry.TimelapseKey.EpisodeTimeThreshold, TimeSpan.FromMinutes(Constant.EpisodeDefaults.TimeThresholdDefault));
                this.FilePlayerSlowValue = registryKey.ReadDouble(Constant.Registry.TimelapseKey.FilePlayerSlowValue, Constant.FilePlayerValues.PlaySlowDefault.TotalSeconds);
                this.FilePlayerFastValue = registryKey.ReadDouble(Constant.Registry.TimelapseKey.FilePlayerFastValue, Constant.FilePlayerValues.PlayFastDefault.TotalSeconds);
                this.MostRecentCheckForUpdates = registryKey.ReadDateTime(Constant.Registry.TimelapseKey.MostRecentCheckForUpdates, DateTime.UtcNow);
                this.MostRecentImageSets = registryKey.ReadMostRecentlyUsedList(Constant.Registry.TimelapseKey.MostRecentlyUsedImageSets);
                this.QuickPasteWindowPosition = registryKey.ReadRect(Constant.Registry.TimelapseKey.QuickPasteWindowPosition, new Rect(0.0, 0.0, 0.0, 0.0));
                this.SpeciesDetectedThreshold = registryKey.ReadDouble(Constant.Registry.TimelapseKey.SpeciesDetectedThreshold, Constant.Recognition.SpeciesDetectedThresholdDefault);
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
                this.ShowAllImagesWhenLoading = registryKey.ReadBoolean(Constant.Registry.TimelapseKey.SuppressThrottleWhenLoading, false);
                this.TabOrderIncludeDateTime = registryKey.ReadBoolean(Constant.Registry.TimelapseKey.TabOrderIncludeDateTime, false);
                this.TabOrderIncludeDeleteFlag = registryKey.ReadBoolean(Constant.Registry.TimelapseKey.TabOrderIncludeDeleteFlag, false);
                this.TabOrderIncludeImageQuality = registryKey.ReadBoolean(Constant.Registry.TimelapseKey.TabOrderIncludeImageQuality, false);
                this.Throttles.SetDesiredImageRendersPerSecond(registryKey.ReadDouble(Constant.Registry.TimelapseKey.DesiredImageRendersPerSecond, Constant.ThrottleValues.DesiredMaximumImageRendersPerSecondDefault));
                this.TimelapseWindowPosition = registryKey.ReadRect(Constant.Registry.TimelapseKey.TimelapseWindowPosition, new Rect(0.0, 0.0, 1350.0, 900.0));
            }
        }

        public bool IsRegistryKeyExists(string key)
        {
            using (RegistryKey registryKey = this.OpenRegistryKey())
            {
                return String.IsNullOrEmpty(registryKey.ReadString(key, String.Empty)) ? false : true;
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

        public Rect ReadTimelapseWindowPositionAndSizeFromRegistryRect(string key)
        {
            using (RegistryKey registryKey = this.OpenRegistryKey())
            {
                return registryKey.ReadRect(key, new Rect(0.0, 0.0, Constant.AvalonDock.DefaultTimelapseWindowWidth, Constant.AvalonDock.DefaultTimelapseWindowHeight)); 
            }
        }

        public bool ReadTimelapseWindowMaximizeStateFromRegistryBool(string key)
        {
            using (RegistryKey registryKey = this.OpenRegistryKey())
            {
                return registryKey.ReadBoolean(key, false);
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
                registryKey.Write(Constant.Registry.TimelapseKey.BookmarkScaleX, this.BookmarkScale.X);
                registryKey.Write(Constant.Registry.TimelapseKey.BookmarkScaleY, this.BookmarkScale.Y);
                registryKey.Write(Constant.Registry.TimelapseKey.BookmarkTranslationX, this.BookmarkTranslation.X);
                registryKey.Write(Constant.Registry.TimelapseKey.BookmarkTranslationY, this.BookmarkTranslation.Y);
                registryKey.Write(Constant.Registry.TimelapseKey.BoundingBoxDisplayThreshold, this.BoundingBoxDisplayThreshold);
                registryKey.Write(Constant.Registry.TimelapseKey.ClassifyDarkImagesWhenLoading, this.ClassifyDarkImagesWhenLoading);
                registryKey.Write(Constant.Registry.TimelapseKey.CustomSelectionTermCombiningOperator, this.CustomSelectionTermCombiningOperator.ToString());
                registryKey.Write(Constant.Registry.TimelapseKey.DarkPixelRatio, this.DarkPixelRatioThreshold);
                registryKey.Write(Constant.Registry.TimelapseKey.DarkPixelThreshold, this.DarkPixelThreshold);
                registryKey.Write(Constant.Registry.TimelapseKey.DeleteFolderManagementValue, (int)this.DeleteFolderManagement);
                registryKey.Write(Constant.Registry.TimelapseKey.EpisodeTimeThreshold, this.EpisodeTimeThreshold);
                registryKey.Write(Constant.Registry.TimelapseKey.FilePlayerSlowValue, this.FilePlayerSlowValue);
                registryKey.Write(Constant.Registry.TimelapseKey.FilePlayerFastValue, this.FilePlayerFastValue);
                registryKey.Write(Constant.Registry.TimelapseKey.DesiredImageRendersPerSecond, this.Throttles.DesiredImageRendersPerSecond);
                registryKey.Write(Constant.Registry.TimelapseKey.MostRecentCheckForUpdates, this.MostRecentCheckForUpdates);
                registryKey.Write(Constant.Registry.TimelapseKey.MostRecentlyUsedImageSets, this.MostRecentImageSets);
                registryKey.Write(Constant.Registry.TimelapseKey.QuickPasteWindowPosition, this.QuickPasteWindowPosition);
                registryKey.Write(Constant.Registry.TimelapseKey.SpeciesDetectedThreshold, this.SpeciesDetectedThreshold);
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
                registryKey.Write(Constant.Registry.TimelapseKey.SuppressThrottleWhenLoading, this.ShowAllImagesWhenLoading);
                registryKey.Write(Constant.Registry.TimelapseKey.TabOrderIncludeDateTime, this.TabOrderIncludeDateTime);
                registryKey.Write(Constant.Registry.TimelapseKey.TabOrderIncludeDeleteFlag, this.TabOrderIncludeDeleteFlag);
                registryKey.Write(Constant.Registry.TimelapseKey.TabOrderIncludeImageQuality, this.TabOrderIncludeImageQuality);
                registryKey.Write(Constant.Registry.TimelapseKey.TimelapseWindowPosition, this.TimelapseWindowPosition);
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

        public void WriteToRegistry(string key, double value)
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

        public void WriteToRegistry(string key, bool value)
        {
            using (RegistryKey registryKey = this.OpenRegistryKey())
            {
                registryKey.Write(key, value);
            }
        }
        #endregion
    }
}
