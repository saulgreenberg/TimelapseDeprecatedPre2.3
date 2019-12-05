using System;
using Timelapse.Util;

namespace Timelapse.Database
{
    public abstract class FileTableColumn
    {
        protected FileTableColumn(ControlRow control)
        {
            // Check the arguments for null 
            if (control == null)
            {
                // this should not happen
                TraceDebug.PrintStackTrace(1);
                throw new ArgumentNullException(nameof(control));
            }

            this.ControlType = control.Type;
            this.DataLabel = control.DataLabel;
        }

        public string ControlType { get; private set; }

        public string DataLabel { get; private set; }

        public abstract bool IsContentValid(string content);

        public static FileTableColumn Create(ControlRow control)
        {
            // Check the arguments for null 
            if (control == null)
            {
                // this should not happen
                TraceDebug.PrintStackTrace(1);
                throw new ArgumentNullException(nameof(control));
            }

            switch (control.Type)
            {
                case Constant.Control.Note:
                case Constant.DatabaseColumn.Date:
                case Constant.DatabaseColumn.File:
                case Constant.DatabaseColumn.Folder:
                case Constant.DatabaseColumn.RelativePath:
                case Constant.DatabaseColumn.Time:
                    return new FileTableNoteColumn(control);
                case Constant.DatabaseColumn.ImageQuality:
                    return new FileTableChoiceColumn(control);
                case Constant.Control.Counter:
                    return new FileTableCounterColumn(control);
                case Constant.DatabaseColumn.DateTime:
                    return new FileTableDateTimeColumn(control);
                case Constant.DatabaseColumn.DeleteFlag:
                case Constant.Control.Flag:
                    return new FileTableFlagColumn(control);
                case Constant.Control.FixedChoice:
                    return new FileTableChoiceColumn(control);
                case Constant.DatabaseColumn.UtcOffset:
                    return new FileTableUtcOffsetColumn(control);
                default:
                    throw new NotSupportedException(String.Format("Unhandled control type {0}.", control.Type));
            }
        }
    }
}
