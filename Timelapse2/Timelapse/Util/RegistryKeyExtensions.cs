using Microsoft.Win32;
using System;
using System.Globalization;
using System.Windows;

namespace Timelapse.Util
{
    // Read and Write particular data types into the registry. 
    public static class RegistryKeyExtensions
    {
        public static bool ReadBoolean(this RegistryKey registryKey, string subKeyPath, bool defaultValue)
        {
            string valueAsString = registryKey.ReadString(subKeyPath);
            if (valueAsString != null)
            {
                if (Boolean.TryParse(valueAsString, out bool value))
                {
                    return value;
                }
            }

            return defaultValue;
        }

        public static DateTime ReadDateTime(this RegistryKey registryKey, string subKeyPath, DateTime defaultValue)
        {
            string value = registryKey.ReadString(subKeyPath);
            if (value == null)
            {
                return defaultValue;
            }

            return DateTime.ParseExact(value, Constant.Time.DateTimeDatabaseFormat, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
        }

        // Read TimeSpan as seconds
        public static TimeSpan ReadTimeSpan(this RegistryKey registryKey, string subKeyPath, TimeSpan defaultValue)
        {
            string value = registryKey.ReadString(subKeyPath);
            if (value == null)
            {
                return defaultValue;
            }
            return int.TryParse(value, out int seconds) ? TimeSpan.FromSeconds(seconds) : defaultValue;
        }

        public static double ReadDouble(this RegistryKey registryKey, string subKeyPath, double defaultValue)
        {
            string valueAsString = registryKey.ReadString(subKeyPath);
            if (valueAsString != null)
            {
                if (Double.TryParse(valueAsString, out double value))
                {
                    return value;
                }
            }

            return defaultValue;
        }

        public static TEnum ReadEnum<TEnum>(this RegistryKey registryKey, string subKeyPath, TEnum defaultValue) where TEnum : struct, IComparable, IConvertible, IFormattable
        {
            string valueAsString = registryKey.ReadString(subKeyPath);
            if (valueAsString != null)
            {
                return (TEnum)Enum.Parse(typeof(TEnum), valueAsString);
            }

            return defaultValue;
        }

        public static int ReadInteger(this RegistryKey registryKey, string subKeyPath, int defaultValue)
        {
            // Check the arguments for null 
            if (registryKey == null)
            {
                // this should not happen
                TraceDebug.PrintStackTrace(1);
                // throw new ArgumentNullException(nameof(registryKey));
                return defaultValue;
            }

            object value = registryKey.GetValue(subKeyPath);
            if (value == null)
            {
                return defaultValue;
            }

            if (value is Int32)
            {
                return (int)value;
            }

            if (value is string)
            {
                return Int32.Parse((string)value);
            }

            throw new NotSupportedException(String.Format("Registry key {0}\\{1} has unhandled type {2}.", registryKey.Name, subKeyPath, value.GetType().FullName));
        }

        // Read a rect frin the registry. If there are issues, just return the default value.
        public static Rect ReadRect(this RegistryKey registryKey, string subKeyPath, Rect defaultValue)
        {
            string rectAsString = registryKey.ReadString(subKeyPath);

            if (rectAsString == null)
            {
                return defaultValue;
            }
            try
            {
                Rect rectangle = Rect.Parse(rectAsString);
            }
            catch
            {
                // The parse can fail if the number format was saved as a non-American number, eg, Portugese uses , vs. as the decimal place.
                // This shouldn't happen as I have used an invarient to save numbers, but just in case...
                return defaultValue;
            }
            return Rect.Parse(rectAsString);
        }

        public static Size ReadSize(this RegistryKey registryKey, string subKeyPath, Size defaultValue)
        {
            string sizeAsString = registryKey.ReadString(subKeyPath);
            if (sizeAsString == null)
            {
                return defaultValue;
            }
            return Size.Parse(sizeAsString);
        }

        public static string ReadString(this RegistryKey registryKey, string subKeyPath, string defaultValue)
        {
            string valueAsString = registryKey.ReadString(subKeyPath);
            if (valueAsString == null)
            {
                return defaultValue;
            }
            return valueAsString;
        }

        // read a REG_SZ key's value from the registry
        public static string ReadString(this RegistryKey registryKey, string subKeyPath)
        {
            // Check the arguments for null 
            if (registryKey == null)
            {
                // this should not happen
                TraceDebug.PrintStackTrace(1);
                throw new ArgumentNullException(nameof(registryKey));
            }
            return (string)registryKey.GetValue(subKeyPath);
        }

        // read a series of REG_SZ keys' values from the registry
        public static MostRecentlyUsedCollection<string> ReadMostRecentlyUsedList(this RegistryKey registryKey, string subKeyPath)
        {
            // Check the arguments for null 
            if (registryKey == null)
            {
                // this should not happen
                TraceDebug.PrintStackTrace(1);
                throw new ArgumentNullException(nameof(registryKey));
            }

            RegistryKey subKey = registryKey.OpenSubKey(subKeyPath);
            MostRecentlyUsedCollection<string> values = new MostRecentlyUsedCollection<string>(Constant.NumberOfMostRecentDatabasesToTrack);

            if (subKey != null)
            {
                for (int index = subKey.ValueCount - 1; index >= 0; --index)
                {
                    string listItem = (string)subKey.GetValue(index.ToString());
                    if (listItem != null)
                    {
                        values.SetMostRecent(listItem);
                    }
                }
            }

            return values;
        }

        public static void Write(this RegistryKey registryKey, string subKeyPath, bool value)
        {
            registryKey.Write(subKeyPath, value.ToString().ToLowerInvariant());
        }

        public static void Write(this RegistryKey registryKey, string subKeyPath, DateTime value)
        {
            registryKey.Write(subKeyPath, value.ToString(Constant.Time.DateTimeDatabaseFormat));
        }

        public static void Write(this RegistryKey registryKey, string subKeyPath, double value)
        {
            registryKey.Write(subKeyPath, value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        public static void Write(this RegistryKey registryKey, string subKeyPath, MostRecentlyUsedCollection<string> values)
        {
            // Check the arguments for null 
            if (registryKey == null)
            {
                // this should not happen
                TraceDebug.PrintStackTrace(1);
                throw new ArgumentNullException(nameof(registryKey));
            }
            if (values != null)
            {
                // create the key whose values represent elements of the list
                RegistryKey subKey = registryKey.OpenSubKey(subKeyPath, true);
                if (subKey == null)
                {
                    subKey = registryKey.CreateSubKey(subKeyPath);
                }

                // write the values
                int index = 0;
                foreach (string value in values)
                {
                    subKey.SetValue(index.ToString(), value);
                    ++index;
                }

                // remove any additional values when the new list is shorter than the old one
                int maximumValueName = subKey.ValueCount;
                for (; index < maximumValueName; ++index)
                {
                    subKey.DeleteValue(index.ToString());
                }
            }
        }

        public static void Write(this RegistryKey registryKey, string subKeyPath, int value)
        {
            // Check the arguments for null 
            if (registryKey == null)
            {
                // this should not happen
                TraceDebug.PrintStackTrace(1);
                throw new ArgumentNullException(nameof(registryKey));
            }
            registryKey.SetValue(subKeyPath, value, RegistryValueKind.DWord);
        }

        public static void Write(this RegistryKey registryKey, string subKeyPath, Rect value)
        {
            registryKey.Write(subKeyPath, value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        public static void Write(this RegistryKey registryKey, string subKeyPath, Size value)
        {
            registryKey.Write(subKeyPath, value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        public static void Write(this RegistryKey registryKey, string subKeyPath, string value)
        {
            // Check the arguments for null 
            if (registryKey == null)
            {
                // this should not happen
                TraceDebug.PrintStackTrace(1);
                throw new ArgumentNullException(nameof(registryKey));
            }
            registryKey.SetValue(subKeyPath, value, RegistryValueKind.String);
        }

        // Save Timespan as seconds
        public static void Write(this RegistryKey registryKey, string subKeyPath, TimeSpan value)
        {
            // Check the arguments for null 
            if (registryKey == null)
            {
                // this should not happen
                TraceDebug.PrintStackTrace(1);
                throw new ArgumentNullException(nameof(registryKey));
            }
            registryKey.SetValue(subKeyPath, value.TotalSeconds.ToString(), RegistryValueKind.String);
        }
    }
}
