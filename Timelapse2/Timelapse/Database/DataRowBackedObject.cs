using System;
using System.Data;
using Timelapse.Util;

namespace Timelapse.Database
{
    public abstract class DataRowBackedObject
    {
        protected DataRow Row { get; private set; }

        protected DataRowBackedObject(DataRow row)
        {
            this.Row = row;
        }

        public long ID
        {
            get { return this.Row.GetID(); }
        }

        public abstract ColumnTuplesWithWhere GetColumnTuples();

        public int GetIndex(DataTable dataTable)
        {
            //// Check the arguments for null 
            if (dataTable == null)
            {
                // this should not happen
                TraceDebug.PrintStackTrace(1);
                throw new ArgumentNullException(nameof(dataTable));
            }

            return dataTable.Rows.IndexOf(this.Row);
        }
    }
}
