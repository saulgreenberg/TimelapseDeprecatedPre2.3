using System;
using System.Collections.Generic;
using Timelapse.Database;

namespace Timelapse.UnitTests
{
    internal static class FileDatabaseExtensions
    {
        public static IEnumerable<DateTimeOffset> GetImageTimes(this FileDatabase fileDatabase)
        {
            TimeZoneInfo imageSetTimeZone = fileDatabase.ImageSet.GetTimeZone();
            foreach (ImageRow image in fileDatabase.Files)
            {
                yield return image.GetDateTime(imageSetTimeZone);
            }
        }
    }
}
