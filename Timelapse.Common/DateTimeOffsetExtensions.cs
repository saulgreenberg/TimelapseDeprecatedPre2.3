using System;

namespace Timelapse.Common
{
    public static class DateTimeOffsetExtensions
    {
        public static DateTimeOffset SetOffset(this DateTimeOffset dateTime, TimeSpan offset)
        {
            return new DateTimeOffset(dateTime.DateTime.AsUnspecifed(), offset);
        }
    }
}
