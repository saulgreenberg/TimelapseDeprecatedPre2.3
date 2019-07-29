using System;
using Timelapse.Common;
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
            DateTime dateTime;
            return DateTimeHandler.TryParseDatabaseDateTime(value, out dateTime);
        }
    }
}
