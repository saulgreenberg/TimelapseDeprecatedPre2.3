using Microsoft.Win32;
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
        public RecencyOrderedList<string> MostRecentImageSets { get; private set; }
        public Rect QuickPasteWindowPosition { get; set; }
        public bool SuppressAmbiguousDatesDialog { get; set; }
        public bool SuppressCsvExportDialog { get; set; }
        public bool SuppressCsvImportPrompt { get; set; }
        public bool SuppressMergeDatabasesPrompt { get; set; }
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
        public bool TabOrderIncludeDateTime { get; set; }
        public bool TabOrderIncludeDeleteFlag { get; set; }
        public bool TabOrderIncludeImageQuality { get; set; }

        public Size TimelapseWindowSize { get; set; }
        public Rect TimelapseWindowPosition { get; set; }
        public bool UseDetections { get; set; }
        #endregion

        public TimelapseUserRegistrySettings() :
            this(Constant.WindowRegistryKeys.RootKey)
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
                this.AudioFeedback = registryKey.ReadBoolean(Constant.WindowRegistryKeys.AudioFeedback, false);
                this.BookmarkScale = new Point(registryKey.ReadDouble(Constant.WindowRegistryKeys.BookmarkScaleX, 1.0), registryKey.ReadDouble(Constant.WindowRegistryKeys.BookmarkScaleY, 1.0));
                this.BookmarkTranslation = new Point(registryKey.ReadDouble(Constant.WindowRegistryKeys.BookmarkTranslationX, 1.0), registryKey.ReadDouble(Constant.WindowRegistryKeys.BookmarkTranslationY, 1.0));
                this.BoundingBoxDisplayThreshold = registryKey.ReadDouble(Constant.WindowRegistryKeys.BoundingBoxDisplayThreshold, Constant.MarkableCanvas.BoundingBoxDisplayThresholdDefault);
                this.CustomSelectionTermCombiningOperator = registryKey.ReadEnum<CustomSelectionOperatorEnum>(Constant.WindowRegistryKeys.CustomSelectionTermCombiningOperator, CustomSelectionOperatorEnum.And);
                this.DarkPixelRatioThreshold = registryKey.ReadDouble(Constant.WindowRegistryKeys.DarkPixelRatio, Constant.ImageValues.DarkPixelRatioThresholdDefault);
                this.DarkPixelThreshold = registryKey.ReadInteger(Constant.WindowRegistryKeys.DarkPixelThreshold, Constant.ImageValues.DarkPixelThresholdDefault);
                this.DeleteFolderManagement = (DeleteFolderManagementEnum)registryKey.ReadInteger(Constant.WindowRegistryKeys.DeleteFolderManagementValue, (int)DeleteFolderManagementEnum.ManualDelete);
                this.EpisodeTimeThreshold = registryKey.ReadTimeSpan(Constant.WindowRegistryKeys.EpisodeTimeThreshold, TimeSpan.FromMinutes(Constant.EpisodeDefaults.TimeThresholdDefault));
                this.FilePlayerSlowValue = registryKey.ReadDouble(Constant.WindowRegistryKeys.FilePlayerSlowValue, Constant.FilePlayerValues.PlaySlowDefault.TotalSeconds);
                this.FilePlayerFastValue = registryKey.ReadDouble(Constant.WindowRegistryKeys.FilePlayerFastValue, Constant.FilePlayerValues.PlayFastDefault.TotalSeconds);
                this.MostRecentCheckForUpdates = registryKey.ReadDateTime(Constant.WindowRegistryKeys.MostRecentCheckForUpdates, DateTime.UtcNow);
                this.MostRecentImageSets = registryKey.ReadMostRecentlyUsedList(Constant.WindowRegistryKeys.MostRecentlyUsedImageSets);
                this.QuickPasteWindowPosition = registryKey.ReadRect(Constant.WindowRegistryKeys.QuickPasteWindowPosition, new Rect(0.0, 0.0, 0.0, 0.0));
                this.SuppressAmbiguousDatesDialog = registryKey.ReadBoolean(Constant.WindowRegistryKeys.SuppressAmbiguousDatesDialog, false);
                this.SuppressCsvExportDialog = registryKey.ReadBoolean(Constant.WindowRegistryKeys.SuppressCsvExportDialog, false);
                this.SuppressCsvImportPrompt = registryKey.ReadBoolean(Constant.WindowRegistryKeys.SuppressCsvImportPrompt, false);
                this.SuppressMergeDatabasesPrompt = registryKey.ReadBoolean(Constant.WindowRegistryKeys.SuppressMergeDatabasesDialog, false);
                this.SuppressSelectedAmbiguousDatesPrompt = registryKey.ReadBoolean(Constant.WindowRegistryKeys.SuppressSelectedAmbiguousDatesPrompt, false);
                this.SuppressSelectedCsvExportPrompt = registryKey.ReadBoolean(Constant.WindowRegistryKeys.SuppressSelectedCsvExportPrompt, false);
                this.SuppressSelectedDarkThresholdPrompt = registryKey.ReadBoolean(Constant.WindowRegistryKeys.SuppressSelectedDarkThresholdPrompt, false);
                this.SuppressSelectedDateTimeFixedCorrectionPrompt = registryKey.ReadBoolean(Constant.WindowRegistryKeys.SuppressSelectedDateTimeFixedCorrectionPrompt, false);
                this.SuppressSelectedDateTimeLinearCorrectionPrompt = registryKey.ReadBoolean(Constant.WindowRegistryKeys.SuppressSelectedDateTimeLinearCorrectionPrompt, false);
                this.SuppressSelectedDaylightSavingsCorrectionPrompt = registryKey.ReadBoolean(Constant.WindowRegistryKeys.SuppressSelectedDaylightSavingsCorrectionPrompt, false);
                this.SuppressSelectedPopulateFieldFromMetadataPrompt = registryKey.ReadBoolean(Constant.WindowRegistryKeys.SuppressSelectedPopulateFieldFromMetadataPrompt, false);
                this.SuppressSelectedRereadDatesFromFilesPrompt = registryKey.ReadBoolean(Constant.WindowRegistryKeys.SuppressSelectedRereadDatesFromFilesPrompt, false);
                this.SuppressSelectedSetTimeZonePrompt = registryKey.ReadBoolean(Constant.WindowRegistryKeys.SuppressSelectedSetTimeZonePrompt, false);
                this.TabOrderIncludeDateTime = registryKey.ReadBoolean(Constant.WindowRegistryKeys.TabOrderIncludeDateTime, false);
                this.TabOrderIncludeDeleteFlag = registryKey.ReadBoolean(Constant.WindowRegistryKeys.TabOrderIncludeDeleteFlag, false);
                this.TabOrderIncludeImageQuality = registryKey.ReadBoolean(Constant.WindowRegistryKeys.TabOrderIncludeImageQuality, false);
                this.Throttles.SetDesiredImageRendersPerSecond(registryKey.ReadDouble(Constant.WindowRegistryKeys.DesiredImageRendersPerSecond, Constant.ThrottleValues.DesiredMaximumImageRendersPerSecondDefault));
                this.TimelapseWindowPosition = registryKey.ReadRect(Constant.WindowRegistryKeys.TimelapseWindowPosition, new Rect(0.0, 0.0, 1350.0, 900.0));
                this.UseDetections = registryKey.ReadBoolean(Constant.WindowRegistryKeys.UseDetections, false);
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
                return registryKey.ReadRect(key, new Rect(0.0, 0.0, Constant.AvalonDockValues.DefaultTimelapseWindowWidth, Constant.AvalonDockValues.DefaultTimelapseWindowHeight));
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
                registryKey.Write(Constant.WindowRegistryKeys.AudioFeedback, this.AudioFeedback);
                registryKey.Write(Constant.WindowRegistryKeys.BookmarkScaleX, this.BookmarkScale.X);
                registryKey.Write(Constant.WindowRegistryKeys.BookmarkScaleY, this.BookmarkScale.Y);
                registryKey.Write(Constant.WindowRegistryKeys.BookmarkTranslationX, this.BookmarkTranslation.X);
                registryKey.Write(Constant.WindowRegistryKeys.BookmarkTranslationY, this.BookmarkTranslation.Y);
                registryKey.Write(Constant.WindowRegistryKeys.BoundingBoxDisplayThreshold, this.BoundingBoxDisplayThreshold);
                registryKey.Write(Constant.WindowRegistryKeys.CustomSelectionTermCombiningOperator, this.CustomSelectionTermCombiningOperator.ToString());
                registryKey.Write(Constant.WindowRegistryKeys.DarkPixelRatio, this.DarkPixelRatioThreshold);
                registryKey.Write(Constant.WindowRegistryKeys.DarkPixelThreshold, this.DarkPixelThreshold);
                registryKey.Write(Constant.WindowRegistryKeys.DeleteFolderManagementValue, (int)this.DeleteFolderManagement);
                registryKey.Write(Constant.WindowRegistryKeys.EpisodeTimeThreshold, this.EpisodeTimeThreshold);
                registryKey.Write(Constant.WindowRegistryKeys.FilePlayerSlowValue, this.FilePlayerSlowValue);
                registryKey.Write(Constant.WindowRegistryKeys.FilePlayerFastValue, this.FilePlayerFastValue);
                registryKey.Write(Constant.WindowRegistryKeys.DesiredImageRendersPerSecond, this.Throttles.DesiredImageRendersPerSecond);
                registryKey.Write(Constant.WindowRegistryKeys.MostRecentCheckForUpdates, this.MostRecentCheckForUpdates);
                registryKey.Write(Constant.WindowRegistryKeys.MostRecentlyUsedImageSets, this.MostRecentImageSets);
                registryKey.Write(Constant.WindowRegistryKeys.QuickPasteWindowPosition, this.QuickPasteWindowPosition);
                registryKey.Write(Constant.WindowRegistryKeys.SuppressAmbiguousDatesDialog, this.SuppressAmbiguousDatesDialog);
                registryKey.Write(Constant.WindowRegistryKeys.SuppressCsvExportDialog, this.SuppressCsvExportDialog);
                registryKey.Write(Constant.WindowRegistryKeys.SuppressCsvImportPrompt, this.SuppressCsvImportPrompt);
                registryKey.Write(Constant.WindowRegistryKeys.SuppressMergeDatabasesDialog, this.SuppressMergeDatabasesPrompt);
                registryKey.Write(Constant.WindowRegistryKeys.SuppressSelectedAmbiguousDatesPrompt, this.SuppressSelectedAmbiguousDatesPrompt);
                registryKey.Write(Constant.WindowRegistryKeys.SuppressSelectedCsvExportPrompt, this.SuppressSelectedCsvExportPrompt);
                registryKey.Write(Constant.WindowRegistryKeys.SuppressSelectedDarkThresholdPrompt, this.SuppressSelectedDarkThresholdPrompt);
                registryKey.Write(Constant.WindowRegistryKeys.SuppressSelectedDateTimeFixedCorrectionPrompt, this.SuppressSelectedDateTimeFixedCorrectionPrompt);
                registryKey.Write(Constant.WindowRegistryKeys.SuppressSelectedDateTimeLinearCorrectionPrompt, this.SuppressSelectedDateTimeLinearCorrectionPrompt);
                registryKey.Write(Constant.WindowRegistryKeys.SuppressSelectedDaylightSavingsCorrectionPrompt, this.SuppressSelectedDaylightSavingsCorrectionPrompt);
                registryKey.Write(Constant.WindowRegistryKeys.SuppressSelectedPopulateFieldFromMetadataPrompt, this.SuppressSelectedPopulateFieldFromMetadataPrompt);
                registryKey.Write(Constant.WindowRegistryKeys.SuppressSelectedSetTimeZonePrompt, this.SuppressSelectedSetTimeZonePrompt);
                registryKey.Write(Constant.WindowRegistryKeys.SuppressSelectedRereadDatesFromFilesPrompt, this.SuppressSelectedRereadDatesFromFilesPrompt);
                registryKey.Write(Constant.WindowRegistryKeys.TabOrderIncludeDateTime, this.TabOrderIncludeDateTime);
                registryKey.Write(Constant.WindowRegistryKeys.TabOrderIncludeDeleteFlag, this.TabOrderIncludeDeleteFlag);
                registryKey.Write(Constant.WindowRegistryKeys.TabOrderIncludeImageQuality, this.TabOrderIncludeImageQuality);
                registryKey.Write(Constant.WindowRegistryKeys.TimelapseWindowPosition, this.TimelapseWindowPosition);
                registryKey.Write(Constant.WindowRegistryKeys.UseDetections, this.UseDetections);
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
