namespace Timelapse.Database
{
    public class FileTableNoteColumn : FileTableColumn
    {
        public FileTableNoteColumn(ControlRow control)
            : base(control)
        {
        }

        public override bool IsContentValid(string value)
        {
            return true;
        }
    }
}
