using System;
using System.Reflection;
using Timelapse.Common;
using Xceed.Wpf.Toolkit;

namespace Timelapse.Controls
{
    public class UtcOffsetUpDown : TimeSpanUpDown
    {
        private static readonly FieldInfo DateTimeInfoListInfo;
        private static readonly PropertyInfo DateTimeInfoListCount;
        private static readonly MethodInfo DateTimeInfoListRemoveRange;

        static UtcOffsetUpDown()
        {
            UtcOffsetUpDown.DateTimeInfoListInfo = typeof(TimeSpanUpDown).GetField("_dateTimeInfoList", BindingFlags.Instance | BindingFlags.NonPublic);
            Type typeofListDateTimeInfo = UtcOffsetUpDown.DateTimeInfoListInfo.FieldType;
            UtcOffsetUpDown.DateTimeInfoListCount = typeofListDateTimeInfo.GetProperty("Count");
            UtcOffsetUpDown.DateTimeInfoListRemoveRange = typeofListDateTimeInfo.GetMethod("RemoveRange");
        }

        public UtcOffsetUpDown()
        {
            this.Maximum = TimeConstants.MaximumUtcOffset;
            this.Minimum = TimeConstants.MinimumUtcOffset;
        }

        protected override void InitializeDateTimeInfoList(TimeSpan? timespan)
        {
            base.InitializeDateTimeInfoList(timespan);

            object dateTimeInfoList = UtcOffsetUpDown.DateTimeInfoListInfo.GetValue(this);
            int dateTimeInfoListCount = (int)UtcOffsetUpDown.DateTimeInfoListCount.GetValue(dateTimeInfoList, null);
            int desiredCount = dateTimeInfoListCount > 5 ? 4 : 3;
            UtcOffsetUpDown.DateTimeInfoListRemoveRange.Invoke(dateTimeInfoList, new object[] { desiredCount, dateTimeInfoListCount - desiredCount });
        }

        protected override void OnCurrentDateTimePartChanged(DateTimePart oldValue, DateTimePart newValue)
        {
            base.OnCurrentDateTimePartChanged(oldValue, newValue);

            switch (newValue)
            {
                case DateTimePart.Hour12:
                case DateTimePart.Hour24:
                    this.Step = 1;
                    break;
                case DateTimePart.Minute:
                    this.Step = TimeConstants.UtcOffsetGranularity.Minutes;
                    break;
                default:
                    this.Step = 0;
                    break;
            }
        }
    }
}
