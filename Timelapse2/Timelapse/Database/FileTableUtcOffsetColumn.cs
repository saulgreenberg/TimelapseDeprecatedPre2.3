namespace Timelapse.Database
{
    public class FileTableUtcOffsetColumn : FileTableColumn
    {
        public FileTableUtcOffsetColumn(ControlRow control)
            : base(control)
        {
        }

        public override bool IsContentValid(string value)
        {
            return double.TryParse(value, out double utcOffset);
        }
    }
}
