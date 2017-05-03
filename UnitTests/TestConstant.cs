using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Timelapse.Database;
using Timelapse.Util;

namespace Timelapse.UnitTests
{
    internal static class TestConstant
    {
        public const double DarkPixelFractionTolerance = 0.00000001;
        public const string DataHandlerFieldName = "dataHandler";
        public const string DateTimeWithOffsetFormat = "yyyy-MM-ddTHH:mm:ss.fffK";
        public const string FileCountsAutomationID = "FileCountsByQuality";
        public const string InitializeDataGridMethodName = "InitializeDataGrid";
        public const string MessageBoxAutomationID = "TimelapseMessageBox";
        public const string OkButtonAutomationID = "OkButton";
        public const string TimelapseAutomationID = "Timelapse";
        public const string TryShowImageWithoutSliderCallbackMethodName = "TryShowImageWithoutSliderCallback";

        public static readonly TimeSpan UIElementSearchTimeout = TimeSpan.FromSeconds(15.0);
        public static readonly Version Version2104 = new Version(2, 1, 0, 4);

        public static readonly ReadOnlyCollection<string> ControlsColumns = new List<string>()
            {
                Constant.Control.ControlOrder,
                Constant.Control.SpreadsheetOrder,
                Constant.Control.DefaultValue,
                Constant.Control.Label,
                Constant.Control.DataLabel,
                Constant.Control.Tooltip,
                Constant.Control.TextBoxWidth,
                Constant.Control.Copyable,
                Constant.Control.Visible,
                Constant.Control.List,
                Constant.DatabaseColumn.ID,
                Constant.Control.Type
            }.AsReadOnly();

        public static readonly ReadOnlyCollection<string> DefaultDataColumns = new List<string>()
        {
            Constant.DatabaseColumn.ID,
            Constant.DatabaseColumn.File,
            Constant.DatabaseColumn.RelativePath,
            Constant.DatabaseColumn.Folder,
            Constant.DatabaseColumn.DateTime,
            Constant.DatabaseColumn.Date,
            Constant.DatabaseColumn.DeleteFlag,
            Constant.DatabaseColumn.Time,
            Constant.DatabaseColumn.ImageQuality,
            Constant.DatabaseColumn.UtcOffset,
            TestConstant.DefaultDatabaseColumn.Counter0,
            TestConstant.DefaultDatabaseColumn.Choice0,
            TestConstant.DefaultDatabaseColumn.Note0,
            TestConstant.DefaultDatabaseColumn.Flag0,
            TestConstant.DefaultDatabaseColumn.CounterWithCustomDataLabel,
            TestConstant.DefaultDatabaseColumn.ChoiceWithCustomDataLabel,
            TestConstant.DefaultDatabaseColumn.NoteWithCustomDataLabel,
            TestConstant.DefaultDatabaseColumn.FlagWithCustomDataLabel,
            TestConstant.DefaultDatabaseColumn.CounterNotVisible,
            TestConstant.DefaultDatabaseColumn.ChoiceNotVisible,
            TestConstant.DefaultDatabaseColumn.NoteNotVisible,
            TestConstant.DefaultDatabaseColumn.FlagNotVisible,
            TestConstant.DefaultDatabaseColumn.Counter3,
            TestConstant.DefaultDatabaseColumn.Choice3,
            TestConstant.DefaultDatabaseColumn.Note3,
            TestConstant.DefaultDatabaseColumn.Flag3
        }.AsReadOnly();

        public static readonly ReadOnlyCollection<string> DefaultMarkerColumns = new List<string>()
        {
            Constant.DatabaseColumn.ID,
            TestConstant.DefaultDatabaseColumn.Counter0,
            TestConstant.DefaultDatabaseColumn.CounterWithCustomDataLabel,
            TestConstant.DefaultDatabaseColumn.CounterNotVisible,
            TestConstant.DefaultDatabaseColumn.Counter3
        }.AsReadOnly();

        public static readonly ReadOnlyCollection<string> ImageSetTableColumns = new List<string>()
        {
            Constant.DatabaseColumn.ID,
        }.AsReadOnly();

        public static class TimelapseDatabaseColumn
        {
            public const string Pelage = "Pelage";
        }

        public static class DefaultDatabaseColumn
        {
            public const string Counter0 = "Counter0";
            public const string Choice0 = "Choice0";
            public const string Note0 = "Note0";
            public const string Flag0 = "Flag0";
            public const string CounterWithCustomDataLabel = "CounterWithCustomDataLabel";
            public const string ChoiceWithCustomDataLabel = "ChoiceWithCustomDataLabel";
            public const string NoteWithCustomDataLabel = "NoteWithCustomDataLabel";
            public const string FlagWithCustomDataLabel = "FlagWithCustomDataLabel";
            public const string CounterNotVisible = "CounterNotVisible";
            public const string ChoiceNotVisible = "ChoiceNotVisible";
            public const string NoteNotVisible = "NoteNotVisible";
            public const string FlagNotVisible = "FlagNotVisible";
            public const string Counter3 = "Counter3";
            public const string Choice3 = "Choice3";
            public const string Note3 = "Note3";
            public const string Flag3 = "Flag3";
        }

        public static class Exif
        {
            public const string DateTime = "Exif IFD0.Date/Time";
            public const string DateTimeDigitized = "Exif SubIFD.Date/Time Digitized";
            public const string DateTimeFormat = "yyyy:MM:dd HH:mm:ss";
            public const string DateTimeOriginal = "Exif SubIFD.Date/Time Original";
            public const string ExposureTime = "Exif SubIFD.Exposure Time";
            public const string ShutterSpeed = "Exif SubIFD.Shutter Speed Value";
            public const string Software = "Exif IFD0.Software";

            public static class Reconyx
            {
                public const string AmbientTemperature = "Reconyx HyperFire Makernote.Ambient Temperature";
                public const string AmbientTemperatureFarenheit = "Reconyx HyperFire Makernote.Ambient Temperature Fahrenheit";
                public const string BatteryVoltage = "Reconyx HyperFire Makernote.Battery Voltage";
                public const string Brightness = "Reconyx HyperFire Makernote.Brightness";
                public const string Contrast = "Reconyx HyperFire Makernote.Contrast";
                public const string DateTimeOriginal = "Reconyx HyperFire Makernote.Date/Time Original";
                public const string FirmwareVersion = "Reconyx HyperFire Makernote.Firmware Version";
                public const string InfraredIlluminator = "Reconyx HyperFire Makernote.Infrared Illuminator";
                public const string MoonPhase = "Reconyx HyperFire Makernote.Moon Phase";
                public const string MotionSensitivity = "Reconyx HyperFire Makernote.Motion Sensitivity";
                public const string Saturation = "Reconyx HyperFire Makernote.Saturation";
                public const string Sequence = "Reconyx HyperFire Makernote.Sequence";
                public const string SerialNumber = "Reconyx HyperFire Makernote.Serial Number";
                public const string Sharpness = "Reconyx HyperFire Makernote.Sharpness";
                public const string TriggerMode = "Reconyx HyperFire Makernote.Trigger Mode";
                public const string UserLabel = "Reconyx HyperFire Makernote.User Label";

                // pending more information from Reconyx
                // public const string EventNumber = "Reconyx Makernote.Event Number";
                // public const string FirmwareDate = "Reconyx Makernote.Firmware Date";
                // public const string MakernoteVersion = "Reconyx Makernote.Makernote Version";
            }
        }

        public static class File
        {
            // template databases for backwards compatibility testing
            // version is the editor version used for creation
            public const string TestImagesDirectoryName = "TestImages";
            public const string TestDatabasesDirectoryName = "TestDatabases";
            public const string CarnivoreTemplateDatabaseFileName = "CarnivoreTemplate 2.0.1.5.tdb";
            public const string DefaultTemplateDatabaseFileName2015 = "TimelapseTemplate 2.0.1.5.tdb";
            public const string DefaultTemplateDatabaseFileName2104 = "TimelapseTemplate 2.1.0.4.tdb";
            public const string TestHybridVideoDirectoryName = "TestHybridVideo";

            // image databases for backwards compatibility testing
            // version is the Timelapse version used for creation
            public const string DefaultFileDatabaseFileName2023 = "TimelapseData 2.0.2.3.ddb";
            public const string DefaultFileDatabaseFileName2104 = "TimelapseData 2.1.0.4.ddb";

            // databases generated dynamically by tests
            // see also use of Constants.File.Default*DatabaseFileName
            public const string CarnivoreNewFileDatabaseFileName = "CarnivoreDatabaseTest.ddb";
            public const string CarnivoreNewFileDatabaseFileName2104 = "CarnivoreDatabaseTest2104.ddb";
            public const string DefaultFileDatabaseFileName = "DefaultUnitTest.ddb";
            public const string DefaultNewTemplateDatabaseFileName = "DefaultUnitTest.tdb";
        }

        public static class ImageExpectation
        {
            public static readonly FileExpectations BushnellTrophyHDAggressor3;
            public static readonly FileExpectations BushnellTrophyHDAggressor1;
            public static readonly FileExpectations ReconyxHC500Hyperfire;
            public static readonly FileExpectations BushnellTrophyHDAggressor2;

            static ImageExpectation()
            {
                TimeZoneInfo pacificTime = TimeZoneInfo.FindSystemTimeZoneById(TestConstant.TimeZone.Pacific);

                ImageExpectation.BushnellTrophyHDAggressor1 = new FileExpectations(pacificTime)
                {
                    DarkPixelFraction = 0.610071236552411,
                    DateTime = FileExpectations.ParseDateTimeOffsetString("2016-04-21T06:31:13.000-07:00"),
                    FileName = "BushnellTrophyHDAggressor-1.JPG",
                    IsColor = true,
                    Quality = FileSelection.Ok,
                    RelativePath = TestConstant.File.TestImagesDirectoryName
                };
                ImageExpectation.BushnellTrophyHDAggressor2 = new FileExpectations(pacificTime)
                {
                    DarkPixelFraction = 0.077128711384332,
                    DateTime = FileExpectations.ParseDateTimeOffsetString("2016-02-24T04:59:46.000-08:00"),
                    FileName = "BushnellTrophyHDAggressor-2.JPG",
                    IsColor = false,
                    Quality = FileSelection.Ok,
                    RelativePath = TestConstant.File.TestImagesDirectoryName,
                };

                ImageExpectation.BushnellTrophyHDAggressor3 = new FileExpectations(pacificTime)
                {
                    DarkPixelFraction = 0.242364344315876,
                    DateTime = FileExpectations.ParseDateTimeOffsetString("2015-08-05T08:06:23.000-07:00"),
                    FileName = "BushnellTrophyHDAggressor-3.JPG",
                    IsColor = true,
                    Quality = FileSelection.Ok,
                    RelativePath = TestConstant.File.TestImagesDirectoryName,
                };

                ImageExpectation.ReconyxHC500Hyperfire = new FileExpectations(pacificTime)
                {
                    DarkPixelFraction = 0.705627292783256,
                    DateTime = FileExpectations.ParseDateTimeOffsetString("2015-01-28T11:17:34.000-08:00"),
                    FileName = "ReconyxHC500Hyperfire-1.JPG",
                    IsColor = true,
                    Quality = FileSelection.Ok,
                    RelativePath = TestConstant.File.TestImagesDirectoryName,
                };
            }
        }
        
        public static class TimeZone
        {
            public const string Alaska = "Alaskan Standard Time"; // UTC-9
            public const string Arizona = "US Mountain Standard Time"; // UTC-7
            public const string CapeVerde = "Cape Verde Standard Time"; // UTC-1
            public const string Dateline = "Dateline Standard Time"; // UTC-12
            public const string Gmt = "GMT Standard Time"; // UTC+0
            public const string LineIslands = "Line Islands Standard Time"; // UTC+14
            public const string Mountain = "Mountain Standard Time"; // UTC-7
            public const string Pacific = "Pacific Standard Time"; // UTC-8
            public const string Utc = "UTC";
            public const string WestCentralAfrica = "W. Central Africa Standard Time"; // UTC+1
        }
    }
}
