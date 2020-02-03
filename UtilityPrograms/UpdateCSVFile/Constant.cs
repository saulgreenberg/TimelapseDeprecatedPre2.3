using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UpdateCSVFile
{
    public class Constant
    {
        public static class DatabaseColumn
        {
            public const string ID = "Id";

            // Required columns in ImageDataTable
            public const string Date = "Date";
            public const string DateTime = "DateTime";
            public const string File = "File";
            public const string Folder = "Folder";
            public const string ImageQuality = "ImageQuality";
            public const string DeleteFlag = "DeleteFlag";
            public const string RelativePath = "RelativePath";
            public const string Time = "Time";
            public const string UtcOffset = "UtcOffset";
        }
    }
}
