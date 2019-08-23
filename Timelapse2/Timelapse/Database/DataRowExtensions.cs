using System;
using System.Data;
using System.Diagnostics;

namespace Timelapse.Database
{
    public static class DataRowExtensions
    {
        public static bool GetBooleanField(this DataRow row, string column)
        {
            string fieldAsString = row.GetStringField(column);
            if (fieldAsString == null)
            {
                return false;
            }
            return String.Equals(Boolean.TrueString, fieldAsString, StringComparison.OrdinalIgnoreCase) ? true : false;
        }

        public static DateTime GetDateTimeField(this DataRow row, string column)
        {
            DateTime dateTime = (DateTime)row[column];
            Debug.Assert(dateTime.Kind == DateTimeKind.Utc, String.Format("Unexpected kind {0} for date time {1}.", dateTime.Kind, dateTime));
            return dateTime;
        }

        public static TEnum GetEnumField<TEnum>(this DataRow row, string column) where TEnum : struct, IComparable, IFormattable, IConvertible
        {
            string fieldAsString = row.GetStringField(column);
            if (String.IsNullOrEmpty(fieldAsString))
            {
                return default(TEnum);
            }
            return (TEnum)Enum.Parse(typeof(TEnum), fieldAsString);
        }

        public static long GetID(this DataRow row)
        {
            return row.GetLongField(Constant.DatabaseColumn.ID);
        }

        public static int GetIntegerField(this DataRow row, string column)
        {
            string fieldAsString = row.GetStringField(column);
            if (fieldAsString == null)
            {
                return -1;
            }
            return Int32.Parse(fieldAsString);
        }
        public static long GetLongStringField(this DataRow row, string column)
        {
            string fieldAsString = row.GetStringField(column);
            if (fieldAsString == null)
            {
                return -1;
            }
            return Int64.Parse(fieldAsString);
        }

        public static long GetLongField(this DataRow row, string column)
        {
            return (long)row[column];
        }

        public static string GetStringField(this DataRow row, string columnName)
        {
            // throws ArgumentException if column is not present in table
            object field = row[columnName];

            // SQLite assigns both String.Empty and null to DBNull on input
            if (field is DBNull)
            {
                return null;
            }
            return (string)field;
        }

        public static TimeSpan GetUtcOffsetField(this DataRow row, string column)
        {
            TimeSpan utcOffset = TimeSpan.FromHours((double)row[column]);
            Debug.Assert(utcOffset.Ticks % Constant.Time.UtcOffsetGranularity.Ticks == 0, "Unexpected rounding error: UTC offset is not an exact multiple of 15 minutes.");
            return utcOffset;
        }

        public static void SetField(this DataRow row, string column, bool value)
        {
            row[column] = value.ToString().ToLowerInvariant();
        }

        public static void SetField(this DataRow row, string column, DateTime value)
        {
            if (value.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentOutOfRangeException("value");
            }
            row[column] = value;
        }

        public static void SetField(this DataRow row, string column, int value)
        {
            row[column] = value.ToString();
        }

        public static void SetField(this DataRow row, string column, long value)
        {
            row[column] = value;
        }

        public static void SetField(this DataRow row, string column, string value)
        {
            row[column] = value;
        }

        public static void SetField<TEnum>(this DataRow row, string column, TEnum value) where TEnum : struct, IComparable, IFormattable, IConvertible
        {
            row.SetField(column, value.ToString());
        }

        public static void SetUtcOffsetField(this DataRow row, string column, TimeSpan value)
        {
            Debug.Assert(value.Ticks % Constant.Time.UtcOffsetGranularity.Ticks == 0, "Unexpected rounding error: UTC offset is not an exact multiple of 15 minutes.");
            row[column] = value.TotalHours;
        }
    }
}
