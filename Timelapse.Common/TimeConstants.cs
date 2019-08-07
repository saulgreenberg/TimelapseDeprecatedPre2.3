using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Timelapse.Common
{
    /// <summary>
    /// Defines common values for dealing with date/time data.
    /// </summary>
    public static class TimeConstants
    {
        // The standard date format, e.g., 05-Apr-2011
        public const string DateFormat = "dd-MMM-yyyy";
        public const string DateTimeDatabaseFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
        public const string DateTimeDisplayFormat = "dd-MMM-yyyy HH:mm:ss";

        public const int MonthsInYear = 12;
        public const string TimeFormat = "HH:mm:ss";
        public const string TimeSpanDisplayFormat = @"hh\:mm\:ss";
        public const string UtcOffsetDatabaseFormat = "0.00";
        public const string UtcOffsetDisplayFormat = @"hh\:mm";
        public const string VideoPositionFormat = @"mm\:ss";

        public static readonly TimeSpan DateTimeDatabaseResolution = TimeSpan.FromMilliseconds(1.0);
        public static readonly TimeSpan MaximumUtcOffset = TimeSpan.FromHours(14.0);
        public static readonly TimeSpan MinimumUtcOffset = TimeSpan.FromHours(-12.0);
        public static readonly TimeSpan UtcOffsetGranularity = TimeSpan.FromTicks(9000000000); // 15 minutes

        public static readonly string[] DateTimeMetadataFormats =
        {
                // known formats supported by Metadata Extractor
                "yyyy:MM:dd HH:mm:ss.fff",
                "yyyy:MM:dd HH:mm:ss",
                "yyyy:MM:dd HH:mm",
                "yyyy-MM-dd HH:mm:ss",
                "yyyy-MM-dd HH:mm",
                "yyyy.MM.dd HH:mm:ss",
                "yyyy.MM.dd HH:mm",
                "yyyy-MM-ddTHH:mm:ss.fff",
                "yyyy-MM-ddTHH:mm:ss.ff",
                "yyyy-MM-ddTHH:mm:ss.f",
                "yyyy-MM-ddTHH:mm:ss",
                "yyyy-MM-ddTHH:mm.fff",
                "yyyy-MM-ddTHH:mm.ff",
                "yyyy-MM-ddTHH:mm.f",
                "yyyy-MM-ddTHH:mm",
                "yyyy:MM:dd",
                "yyyy-MM-dd",
                "yyyy-MM",
                "yyyy",
                // File.File Modified Date
                "ddd MMM dd HH:mm:ss K yyyy"
            };
    }
}
