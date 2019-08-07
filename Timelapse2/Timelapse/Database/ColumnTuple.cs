using System;
using Timelapse.Common;
using Timelapse.Util;

namespace Timelapse.Database
{
    /// <summary>
    /// A column name and a value to assign (or assigned) to that column.
    /// </summary>
    public class ColumnTuple
    {
        public string Name { get; private set; }
        public string Value { get; private set; }

        public ColumnTuple(string column, bool value)
            : this(column, value ? Constant.BooleanValue.True : Constant.BooleanValue.False)
        {
        }

        public ColumnTuple(string column, DateTime value)
            : this(column, DateTimeHandler.ToDatabaseDateTimeString(value))
        {
            if (value.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentOutOfRangeException("value");
            }
        }

        public ColumnTuple(string column, int value)
            : this(column, value.ToString())
        {
        }

        public ColumnTuple(string column, long value)
            : this(column, value.ToString())
        {
        }

        public ColumnTuple(string column, string value)
        {
            this.Name = column;
            this.Value = value;
        }

        public ColumnTuple(string column, float value)
        {
            this.Name = column;
            this.Value = value.ToString();
        }

        public ColumnTuple(string column, TimeSpan utcOffset)
        {
            if ((utcOffset < TimeConstants.MinimumUtcOffset) ||
                (utcOffset > TimeConstants.MaximumUtcOffset))
            {
                throw new ArgumentOutOfRangeException("utcOffset", String.Format("UTC offset must be between {0} and {1}, inclusive.", DateTimeHandler.ToDatabaseUtcOffsetString(TimeConstants.MinimumUtcOffset), DateTimeHandler.ToDatabaseUtcOffsetString(TimeConstants.MinimumUtcOffset)));
            }
            if (utcOffset.Ticks % TimeConstants.UtcOffsetGranularity.Ticks != 0)
            {
                throw new ArgumentOutOfRangeException("utcOffset", String.Format("UTC offset must be an exact multiple of {0} ({1}).", DateTimeHandler.ToDatabaseUtcOffsetString(TimeConstants.UtcOffsetGranularity), DateTimeHandler.ToDisplayUtcOffsetString(TimeConstants.UtcOffsetGranularity)));
            }

            this.Name = column;
            this.Value = DateTimeHandler.ToDatabaseUtcOffsetString(utcOffset);
        }
    }
}
