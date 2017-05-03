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
            double utcOffset;
            return double.TryParse(value, out utcOffset);
        }
    }
}
