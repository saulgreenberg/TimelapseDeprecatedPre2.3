using System;
using System.Globalization;

namespace Timelapse.Util
{
    public static class DateTimeHandler
    {

        #region Public Static Create / Parse to get DateTimeOffset
        /// <summary>
        /// Create a DateTimeOffset given a dateTime and timezoneinfo
        /// </summary>
        public static DateTimeOffset CreateDateTimeOffset(DateTime dateTime, TimeZoneInfo imageSetTimeZone)
        {
            if (imageSetTimeZone != null && dateTime.Kind == DateTimeKind.Unspecified)
            {
                TimeSpan utcOffset = imageSetTimeZone.GetUtcOffset(dateTime);
                return new DateTimeOffset(dateTime, utcOffset);
            }
            return new DateTimeOffset(dateTime);
        }

        /// <summary>
        /// Return a DateTimeOffset from its database string representation
        /// </summary>
        public static DateTimeOffset FromDatabaseDateTimeIncorporatingOffset(DateTime dateTime, TimeSpan utcOffset)
        {
            return new DateTimeOffset((dateTime + utcOffset).AsUnspecifed(), utcOffset);
        }

        /// <summary>
        /// Parse a utcOffsetAsString from its double representation. Return false on failure or on reasonableness checks
        /// </summary>
        public static bool TryParseDatabaseUtcOffsetString(string utcOffsetAsString, out TimeSpan utcOffset)
        {
            if (double.TryParse(utcOffsetAsString, out double utcOffsetAsDouble))
            {
                utcOffset = TimeSpan.FromHours(utcOffsetAsDouble);
                return (utcOffset >= Constant.Time.MinimumUtcOffset) &&
                       (utcOffset <= Constant.Time.MaximumUtcOffset) &&
                       (utcOffset.Ticks % Constant.Time.UtcOffsetGranularity.Ticks == 0);
            }

            utcOffset = TimeSpan.Zero;
            return false;
        }

        /// <summary>
        /// Parse a legacy date and time string into a DateTimeOffset. Return false on failure
        /// </summary>
        public static bool TryParseLegacyDateTime(string date, string time, TimeZoneInfo imageSetTimeZone, out DateTimeOffset dateTimeOffset)
        {
            return DateTimeHandler.TryParseDateTaken(date + " " + time, imageSetTimeZone, out dateTimeOffset);
        }

        /// <summary>
        /// Parse a metadata-formatted date/time string into a DateTimeOffset. Return false on failure
        /// </summary>
        public static bool TryParseMetadataDateTaken(string dateTimeAsString, TimeZoneInfo imageSetTimeZone, out DateTimeOffset dateTimeOffset)
        {
            if (DateTime.TryParseExact(dateTimeAsString, Constant.Time.DateTimeMetadataFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dateTime) == false)
            {
                dateTimeOffset = DateTimeOffset.MinValue;
                return false;
            }

            dateTimeOffset = DateTimeHandler.CreateDateTimeOffset(dateTime, imageSetTimeZone);
            return true;
        }

        /// <summary>
        /// Swap the day and month of a DateTimeOffset if possible. Otherwise use the same DateTimeOffset. Return false if we cannot do the swap.
        /// </summary>
        public static bool TrySwapDayMonth(DateTimeOffset imageDate, out DateTimeOffset swappedDate)
        {
            swappedDate = DateTimeOffset.MinValue;
            if (imageDate.Day > Constant.Time.MonthsInYear)
            {
                return false;
            }
            swappedDate = new DateTimeOffset(imageDate.Year, imageDate.Day, imageDate.Month, imageDate.Hour, imageDate.Minute, imageDate.Second, imageDate.Millisecond, imageDate.Offset);
            return true;
        }
        #endregion

        #region Public Static Parse / Convert Forms to get DateTime
        /// <summary>
        /// Return a DateTime from its DataTime database string representation in the form of "yyyy-MM-ddTHH:mm:ss.fffZ"
        /// </summary>
        public static DateTime ParseDatabaseDateTimeString(string dateTimeAsString)
        {
            return DateTime.ParseExact(dateTimeAsString, Constant.Time.DateTimeDatabaseFormat, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
        }

        /// <summary>
        /// Parse a DateTime from its database format string representation "yyyy-MM-ddTHH:mm:ss.fffZ". Return false on failure
        /// </summary>
        public static bool TryParseDatabaseDateTime(string dateTimeAsString, out DateTime dateTime)
        {
            return DateTime.TryParseExact(dateTimeAsString, Constant.Time.DateTimeDatabaseFormat, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out dateTime);
        }

        /// <summary>
        /// Converts a display string to a DateTime of DateTimeKind.Unspecified.
        /// </summary>
        /// <param name="dateTimeAsString">string potentially containing a date time in display format</param>
        /// <param name="dateTime">the date time in the string, if any</param>
        /// <returns>true if string was in the date time display format, false otherwise</returns>
        public static bool TryParseDisplayDateTime(string dateTimeAsString, out DateTime dateTime)
        {
            return DateTime.TryParseExact(dateTimeAsString, Constant.Time.DateTimeDisplayFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out dateTime);
        }

        /// <summary>
        /// Parse a DateTime from its display format string representation "dd-MMM-yyyy HH:mm:ss". Return false on failure
        /// </summary>
        public static bool TryParseDisplayDateTimeString(string dateTimeAsString, out DateTime dateTime)
        {
            if (DateTime.TryParseExact(dateTimeAsString, Constant.Time.DateTimeDisplayFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out dateTime) == true)
            {
                return true;
            }
            else
            {
                dateTime = DateTime.MinValue;
                return false;
            };
        }
        #endregion

        #region Public Static Parse to get TimeSpan
        /// <summary>
        /// Return a TimeSpan from its utcOfset string representation (hours) e.g., "-7.0"
        /// </summary>
        public static TimeSpan ParseDatabaseUtcOffsetString(string utcOffsetAsString)
        {
            TimeSpan utcOffset = TimeSpan.FromHours(double.Parse(utcOffsetAsString));
            if ((utcOffset < Constant.Time.MinimumUtcOffset) ||
                (utcOffset > Constant.Time.MaximumUtcOffset))
            {
                throw new ArgumentOutOfRangeException(nameof(utcOffsetAsString), String.Format("UTC offset must be between {0} and {1}, inclusive.", DateTimeHandler.ToStringDatabaseUtcOffset(Constant.Time.MinimumUtcOffset), DateTimeHandler.ToStringDatabaseUtcOffset(Constant.Time.MinimumUtcOffset)));
            }
            if (utcOffset.Ticks % Constant.Time.UtcOffsetGranularity.Ticks != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(utcOffsetAsString), String.Format("UTC offset must be an exact multiple of {0} ({1}).", DateTimeHandler.ToStringDatabaseUtcOffset(Constant.Time.UtcOffsetGranularity), DateTimeHandler.ToStringDisplayUtcOffset(Constant.Time.UtcOffsetGranularity)));
            }
            return utcOffset;
        }
        #endregion

        #region Private Static Convert To String
        /// <summary>
        /// Return "yyyy-MM-ddTHH:mm:ss.fffZ" database string format of DateTimeOffset
        /// </summary>
        public static string ToStringDatabaseDateTime(DateTimeOffset dateTime)
        {
            return dateTime.UtcDateTime.ToString(Constant.Time.DateTimeDatabaseFormat, CultureInfo.CreateSpecificCulture("en-US"));
        }

        /// <summary>
        /// Return "0.00" hours format of a timeSpan e.g. "11:30"
        /// </summary>
        public static string ToStringDatabaseUtcOffset(TimeSpan timeSpan)
        {
            return timeSpan.TotalHours.ToString(Constant.Time.UtcOffsetDatabaseFormat);
        }

        /// <summary>
        /// Return "dd-MMM-yyyy" format of a DateTimeOffset, e.g., 05-Apr-2016 
        /// </summary>
        public static string ToStringDisplayDate(DateTimeOffset date)
        {
            return date.DateTime.ToString(Constant.Time.DateFormat, CultureInfo.CreateSpecificCulture("en-US"));
        }

        /// <summary>
        /// Return "dd-MMM-yyyy HH:mm:ss" format of a DateTimeOffset  e.g. 05-Apr-2016 12:05:01
        /// </summary>
        public static string ToStringDisplayDateTime(DateTimeOffset dateTime)
        {
            return dateTime.DateTime.ToString(Constant.Time.DateTimeDisplayFormat, CultureInfo.CreateSpecificCulture("en-US"));
        }

        /// <summary>
        /// Return "dd-MMM-yyyy HH:mm:ss+hh:mm" format of a DateTimeOffset  e.g. 05-Apr-2016 12:05:01+5:00
        /// </summary>
        public static string ToStringDisplayDateTimeUtcOffset(DateTimeOffset dateTime)
        {
            return dateTime.DateTime.ToString(Constant.Time.DateTimeDisplayFormat, CultureInfo.CreateSpecificCulture("en-US")) + " " + DateTimeHandler.ToStringDisplayUtcOffset(dateTime.Offset);
        }

        /// <summary>
        /// Return display format of a TimeSpan
        /// </summary>
        public static string ToStringDisplayTimeSpan(TimeSpan timeSpan)
        {
            // Pretty print the adjustment time, depending upon how many day(s) were included 
            string sign = (timeSpan < TimeSpan.Zero) ? "-" : null;
            string timeSpanAsString = sign + timeSpan.ToString(Constant.Time.TimeSpanDisplayFormat);

            TimeSpan duration = timeSpan.Duration();
            if (duration.Days == 0)
            {
                return timeSpanAsString;
            }
            if (duration.Days == 1)
            {
                return sign + "1 day " + timeSpanAsString;
            }

            return sign + duration.Days.ToString("D") + " days " + timeSpanAsString;
        }

        /// <summary>
        /// Return "HH:mm:ss" in 24 hour format given a DateTimeOffset
        /// </summary>
        public static string ToStringDisplayTime(DateTimeOffset time)
        {
            return time.DateTime.ToString(Constant.Time.TimeFormat, CultureInfo.CreateSpecificCulture("en-US"));
        }

        /// <summary>
        /// Return "+hh\:mm" given a TimeSpan
        /// </summary>
        public static string ToStringDisplayUtcOffset(TimeSpan utcOffset)
        {
            string displayString = utcOffset.ToString(Constant.Time.UtcOffsetDisplayFormat);
            if (utcOffset < TimeSpan.Zero)
            {
                displayString = "-" + displayString;
            }
            return displayString;
        }
        #endregion

        #region Private Methods
        private static bool TryParseDateTaken(string dateTimeAsString, TimeZoneInfo imageSetTimeZone, out DateTimeOffset dateTimeOffset)
        {
            // use current culture as BitmapMetadata.DateTaken is not invariant
            if (DateTime.TryParse(dateTimeAsString, CultureInfo.CurrentCulture, DateTimeStyles.None, out DateTime dateTime) == false)
            {
                dateTimeOffset = DateTimeOffset.MinValue;
                return false;
            }

            dateTimeOffset = DateTimeHandler.CreateDateTimeOffset(dateTime, imageSetTimeZone);
            return true;
        }
        #endregion
    }
}
