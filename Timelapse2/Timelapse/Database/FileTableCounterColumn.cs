using System;

namespace Timelapse.Database
{
    public class FileTableCounterColumn : FileTableColumn
    {
        public FileTableCounterColumn(ControlRow control)
            : base(control)
        {
        }

        public override bool IsContentValid(string value)
        {
            return Int64.TryParse(value, out long result);
        }
    }
}
