using System;
using System.Data;
using System.IO;
using Timelapse.Util;

namespace Timelapse.Database
{
    public class FileTable : DataTableBackedList<ImageRow>
    {
        public FileTable(DataTable imageDataTable)
            : base(imageDataTable, FileTable.CreateRow)
        {
        }

        // Return a new image or video row
        private static ImageRow CreateRow(DataRow row)
        {
            string fileName = row.GetStringField(Constant.DatabaseColumn.File);

            // Return a video row if its a video file (as identified by its suffix)
            string fileExtension = Path.GetExtension(fileName);
            if (String.Equals(fileExtension, Constant.File.AviFileExtension, StringComparison.OrdinalIgnoreCase) ||
                String.Equals(fileExtension, Constant.File.Mp4FileExtension, StringComparison.OrdinalIgnoreCase))
            {
                return new VideoRow(row);
            }
            if (String.Equals(fileExtension, Constant.File.JpgFileExtension, StringComparison.OrdinalIgnoreCase))
            {
                return new ImageRow(row);
            }
            // This should never be reached
            throw new NotSupportedException(String.Format("Unhandled extension '{0}' for file '{1}'.", fileExtension, fileName));
        }

        public ImageRow NewRow(FileInfo file)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(file, nameof(file));

            DataRow row = this.DataTable.NewRow();
            row[Constant.DatabaseColumn.File] = file.Name;
            return FileTable.CreateRow(row);
        }
    }
}
