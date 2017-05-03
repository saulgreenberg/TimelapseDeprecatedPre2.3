using System;

namespace Timelapse.Database
{
    public class FileTableFlagColumn : FileTableColumn
    {
        public FileTableFlagColumn(ControlRow control)
            : base(control)
        {
        }

        public override bool IsContentValid(string value)
        {
            return String.Equals(value, Constant.Boolean.False, StringComparison.OrdinalIgnoreCase) ||
                   String.Equals(value, Constant.Boolean.True, StringComparison.OrdinalIgnoreCase);
        }
    }
}
