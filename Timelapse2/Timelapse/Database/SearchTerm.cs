using System;
using System.Collections.Generic;
using Timelapse.Util;

namespace Timelapse.Database
{
    /// <summary>
    /// A SearchTerm stores the search criteria for each column
    /// </summary>
    public class SearchTerm
    {
        public string ControlType { get; set; }
        public string DatabaseValue { get; set; }
        public string DataLabel { get; set; }
        public string Label { get; set; }
        public List<string> List { get; set; }
        public string Operator { get; set; }
        public bool UseForSearching { get; set; }

        public SearchTerm()
        {
            this.ControlType = String.Empty;
            this.DatabaseValue = String.Empty;
            this.DataLabel = String.Empty;
            this.Label = String.Empty;
            this.List = null;
            this.Operator = String.Empty;
            this.UseForSearching = false;
        }

        public SearchTerm(SearchTerm other)
        {
            // Check the arguments for null 
            if (other == null)
            {
                // this should not happen
                TraceDebug.PrintStackTrace(1);
                throw new ArgumentNullException(nameof(other));
            }

            this.ControlType = other.ControlType;
            this.DatabaseValue = other.DatabaseValue;
            this.DataLabel = other.DataLabel;
            this.Label = other.Label;
            if (other.List == null)
            {
                this.List = null;
            }
            else
            {
                this.List = new List<string>(other.List);
            }
            this.Operator = other.Operator;
            this.UseForSearching = other.UseForSearching;
        }

        public DateTime GetDateTime()
        {
            if (this.DataLabel != Constant.DatabaseColumn.DateTime)
            {
                throw new NotSupportedException(String.Format("Attempt to retrieve date/time from a SearchTerm with data label {0}.", this.DataLabel));
            }
            return DateTimeHandler.ParseDatabaseDateTimeString(this.DatabaseValue);
        }

        public TimeSpan GetUtcOffset()
        {
            if (this.DataLabel != Constant.DatabaseColumn.UtcOffset)
            {
                throw new NotSupportedException(String.Format("Attempt to retrieve UTC offset from a SearchTerm with data label {0}.", this.DataLabel));
            }
            return DateTimeHandler.ParseDatabaseUtcOffsetString(this.DatabaseValue);
        }

        public void SetDatabaseValue(DateTimeOffset dateTime)
        {
            if (this.DataLabel != Constant.DatabaseColumn.DateTime)
            {
                throw new NotSupportedException(String.Format("Attempt to retrieve date/time from a SearchTerm with data label {0}.", this.DataLabel));
            }
            this.DatabaseValue = DateTimeHandler.ToDatabaseDateTimeString(dateTime);
        }

        public void SetDatabaseValue(Nullable<DateTime> dateTime, TimeZoneInfo imageSetTimeZone)
        {
            // Check the arguments for null 
            if (imageSetTimeZone == null)
            {
                // this should not happen
                TraceDebug.PrintStackTrace(1);
                throw new ArgumentNullException(nameof(imageSetTimeZone));
            }

            if (dateTime.HasValue)
            {
                TimeSpan utcOffset = imageSetTimeZone.GetUtcOffset(dateTime.Value);
                DateTimeOffset imageSetDateTime = DateTimeHandler.FromDatabaseDateTimeIncorporatingOffset(dateTime.Value, utcOffset);
                this.DatabaseValue = DateTimeHandler.ToDatabaseDateTimeString(imageSetDateTime);
            }
            else
            {
                this.DatabaseValue = null;
            }
        }

        public void SetDatabaseValue(Nullable<TimeSpan> utcOffset)
        {
            if (utcOffset.HasValue)
            {
                this.DatabaseValue = DateTimeHandler.ToDatabaseUtcOffsetString(utcOffset.Value);
            }
            else
            {
                this.DatabaseValue = null;
            }
        }
    }
}
