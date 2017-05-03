using System;
using System.Collections.Generic;
using Timelapse.Util;

namespace Timelapse.Database
{
    // A tuple with a list of ColumnTuples and a string indicating where to apply the updates contained in the tuples
    public class ColumnTuplesWithWhere
    {
        public List<ColumnTuple> Columns { get; private set; }
        public string Where { get; private set; }

        public ColumnTuplesWithWhere()
        {
            this.Columns = new List<ColumnTuple>();
        }

        public ColumnTuplesWithWhere(List<ColumnTuple> columns)
        {
            this.Columns = columns;
        }

        public ColumnTuplesWithWhere(List<ColumnTuple> columns, long id)
            : this(columns)
        {
            this.SetWhere(id);
        }

        public void SetWhere(long id)
        {
            this.Where = Constant.DatabaseColumn.ID + " = " + id.ToString();
        }

        public void SetWhere(string folder, string relativePath, string file)
        {
            this.Where = String.Format("{0} = {1}", Constant.DatabaseColumn.File, Utilities.QuoteForSql(file));
            this.Where += String.Format(" AND {0} = {1}", Constant.DatabaseColumn.RelativePath, Utilities.QuoteForSql(relativePath));
            this.Where += String.Format(" AND {0} = {1}", Constant.DatabaseColumn.Folder, Utilities.QuoteForSql(folder));
        }
    }
}
