using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace Timelapse.Controls
{
    public class TimeZonePicker : ComboBox
    {
        // can't use ReadOnlyDictionary as it's in .NET 4.5
        public Dictionary<string, TimeZoneInfo> TimeZonesByDisplayName { get; private set; }

        public TimeZonePicker()
        {
            this.FontWeight = FontWeights.Normal;

            this.TimeZonesByDisplayName = new Dictionary<string, TimeZoneInfo>();
            foreach (TimeZoneInfo timeZone in TimeZoneInfo.GetSystemTimeZones())
            {
                this.TimeZonesByDisplayName.Add(timeZone.DisplayName, timeZone);
                this.Items.Add(timeZone.DisplayName);
            }
        }
    }
}
