﻿using System;
using System.Collections.Generic;
using Timelapse.Util;

namespace Timelapse.Database
{
    /// <summary>
    /// A SearchTerm stores the search criteria for each column
    /// </summary>
    public class SearchTerm
    {
        #region Public Properties
        public string ControlType { get; set; }
        public string DatabaseValue { get; set; }
        public string DataLabel { get; set; }
        public string Label { get; set; }
        public List<string> List { get; set; }
        public string Operator { get; set; }
        public bool UseForSearching { get; set; }
        #endregion

        #region Constructors
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
            ThrowIf.IsNullArgument(other, nameof(other));

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
        #endregion

        #region Public Methods - Get values to Convert DateTime / UTCOffset
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
        #endregion

        #region Public Methods - Set Values - to Convert DateTime / UTCOffset
        public void SetDatabaseValue(DateTimeOffset dateTime)
        {
            if (this.DataLabel != Constant.DatabaseColumn.DateTime)
            {
                throw new NotSupportedException(String.Format("Attempt to retrieve date/time from a SearchTerm with data label {0}.", this.DataLabel));
            }
            this.DatabaseValue = DateTimeHandler.ToStringDatabaseDateTime(dateTime);
        }

        public void SetDatabaseValue(Nullable<DateTime> dateTime, TimeZoneInfo imageSetTimeZone)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(imageSetTimeZone, nameof(imageSetTimeZone));

            if (dateTime.HasValue)
            {
                TimeSpan utcOffset = imageSetTimeZone.GetUtcOffset(dateTime.Value);
                DateTimeOffset imageSetDateTime = DateTimeHandler.FromDatabaseDateTimeIncorporatingOffset(dateTime.Value, utcOffset);
                this.DatabaseValue = DateTimeHandler.ToStringDatabaseDateTime(imageSetDateTime);
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
                this.DatabaseValue = DateTimeHandler.ToStringDatabaseUtcOffset(utcOffset.Value);
            }
            else
            {
                this.DatabaseValue = null;
            }
        }

        // Return a cloned copy of the provided search term
        public SearchTerm Clone()
        {
            return new SearchTerm
            {
                ControlType = this.ControlType,
                DatabaseValue = this.DatabaseValue,
                DataLabel = this.DataLabel,
                Label = this.Label,
                List = new List<string>(this.List),
                Operator = this.Operator,
                UseForSearching = this.UseForSearching
            };
        }
        #endregion


    }
}
