using System;
using System.Data;
using System.Diagnostics;
using Timelapse.Util;

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
            // Check the arguments for null 
            ThrowIf.IsNullArgument(row, nameof(row));

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
            try
            {
                //TEnum result = (TEnum)Enum.Parse(typeof(TEnum), fieldAsString);
                if (Enum.TryParse(fieldAsString, out TEnum result))
                {
                    // The parse succeeded, where the TEnum result is in result
                    return result;
                }
                else
                {
                    // The parse dis not succeeded. The TEnum result contains the default enum value, ie, the same as returning default(TEnum)
                    return result;
                }
            }
            catch
            {
                // Shouldn't get here as we used a tryParse, but just in case
                return default(TEnum);
            }
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
            // Check the arguments for null 
            ThrowIf.IsNullArgument(row, nameof(row));

            return (long)row[column];
        }

        public static string GetStringField(this DataRow row, string columnName)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(row, nameof(row));

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
            // Check the arguments for null 
            ThrowIf.IsNullArgument(row, nameof(row));

            TimeSpan utcOffset = TimeSpan.FromHours((double)row[column]);
            Debug.Assert(utcOffset.Ticks % Constant.Time.UtcOffsetGranularity.Ticks == 0, "Unexpected rounding error: UTC offset is not an exact multiple of 15 minutes.");
            return utcOffset;
        }

        public static void SetField(this DataRow row, string column, bool value)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(row, nameof(row));

            row[column] = String.Format("{0}", value).ToLowerInvariant();
        }

        public static void SetField(this DataRow row, string column, DateTime value)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(row, nameof(row));

            if (value.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
            row[column] = value;
        }

        public static void SetField(this DataRow row, string column, int value)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(row, nameof(row));
            row[column] = value.ToString();
        }

        public static void SetField(this DataRow row, string column, long value)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(row, nameof(row));
            row[column] = value;
        }

        public static void SetField(this DataRow row, string column, string value)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(row, nameof(row));

            row[column] = value;
        }

        public static void SetField<TEnum>(this DataRow row, string column, TEnum value) where TEnum : struct, IComparable, IFormattable, IConvertible
        {
            row.SetField(column, value.ToString());
        }

        public static void SetUtcOffsetField(this DataRow row, string column, TimeSpan value)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(row, nameof(row));

            Debug.Assert(value.Ticks % Constant.Time.UtcOffsetGranularity.Ticks == 0, "Unexpected rounding error: UTC offset is not an exact multiple of 15 minutes.");
            row[column] = value.TotalHours;
        }
    }
}
