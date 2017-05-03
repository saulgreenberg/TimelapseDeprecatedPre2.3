using System;
using System.Data;
using System.IO;

namespace Timelapse.Database
{
    public class FileTable : DataTableBackedList<ImageRow>
    {
        public FileTable(DataTable imageDataTable)
            : base(imageDataTable, FileTable.CreateRow)
        {
        }

        private static ImageRow CreateRow(DataRow row)
        {
            string fileName = row.GetStringField(Constant.DatabaseColumn.File);
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
            throw new NotSupportedException(String.Format("Unhandled extension '{0}' for file '{1}'.", fileExtension, fileName));
        }

        public ImageRow NewRow(FileInfo file)
        {
            DataRow row = this.DataTable.NewRow();
            row[Constant.DatabaseColumn.File] = file.Name;
            return FileTable.CreateRow(row);
        }
    }
}
