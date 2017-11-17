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
            return String.Equals(value, Constant.BooleanValue.False, StringComparison.OrdinalIgnoreCase) ||
                   String.Equals(value, Constant.BooleanValue.True, StringComparison.OrdinalIgnoreCase);
        }
    }
}
