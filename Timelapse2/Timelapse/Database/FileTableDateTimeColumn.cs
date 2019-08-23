using System;
using Timelapse.Util;

namespace Timelapse.Database
{
    public class FileTableDateTimeColumn : FileTableColumn
    {
        public FileTableDateTimeColumn(ControlRow control)
            : base(control)
        {
        }

        public override bool IsContentValid(string value)
        {
            return DateTimeHandler.TryParseDatabaseDateTime(value, out DateTime dateTime);
        }
    }
}
