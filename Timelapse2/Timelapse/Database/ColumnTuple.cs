using System;
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
            : this(column, DateTimeHandler.ToStringDatabaseDateTime(value))
        {
            if (value.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
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
            if ((utcOffset < Constant.Time.MinimumUtcOffset) ||
                (utcOffset > Constant.Time.MaximumUtcOffset))
            {
                throw new ArgumentOutOfRangeException(nameof(utcOffset), String.Format("UTC offset must be between {0} and {1}, inclusive.", DateTimeHandler.ToStringDatabaseUtcOffset(Constant.Time.MinimumUtcOffset), DateTimeHandler.ToStringDatabaseUtcOffset(Constant.Time.MinimumUtcOffset)));
            }
            if (utcOffset.Ticks % Constant.Time.UtcOffsetGranularity.Ticks != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(utcOffset), String.Format("UTC offset must be an exact multiple of {0} ({1}).", DateTimeHandler.ToStringDatabaseUtcOffset(Constant.Time.UtcOffsetGranularity), DateTimeHandler.ToStringDisplayUtcOffset(Constant.Time.UtcOffsetGranularity)));
            }

            this.Name = column;
            this.Value = DateTimeHandler.ToStringDatabaseUtcOffset(utcOffset);
        }
    }
}
