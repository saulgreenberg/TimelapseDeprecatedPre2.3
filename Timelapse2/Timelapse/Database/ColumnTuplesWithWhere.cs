using System;
using System.Collections.Generic;
using Timelapse.Data;
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

        public ColumnTuplesWithWhere(ColumnTuple column, string field)
        {
            this.SetWhere(column, field);
            this.Columns = new List<ColumnTuple>
            {
                column
            };
        }

        public ColumnTuplesWithWhere(ColumnTuple column, string field, bool useNotEqualCondition)
        {
            if (useNotEqualCondition)
            {
                this.SetWhereNotEquals(column, field);
            }
            else
            {
                this.SetWhere(column, field);
            }
            this.Columns = new List<ColumnTuple>
            {
                column
            };
        }

        public void SetWhere(long id)
        {
            this.Where = Constant.DatabaseColumn.ID + " = " + id.ToString();
        }

        public void SetWhere(ColumnTuple columnTuple, string field)
        {
            this.Where = String.Format("{0} = {1}", columnTuple.Name, SqlUtility.QuoteForSql(field));
        }

        public void SetWhereNotEquals(ColumnTuple columnTuple, string field)
        {
            this.Where = String.Format("{0} <> {1}", columnTuple.Name, SqlUtility.QuoteForSql(field));
        }

        public void SetWhere(string folder, string relativePath, string file)
        {
            this.Where = String.Format("{0} = {1}", Constant.DatabaseColumn.File, SqlUtility.QuoteForSql(file));
            this.Where += String.Format(" AND {0} = {1}", Constant.DatabaseColumn.RelativePath, SqlUtility.QuoteForSql(relativePath));
            this.Where += String.Format(" AND {0} = {1}", Constant.DatabaseColumn.Folder, SqlUtility.QuoteForSql(folder));
        }

        public void SetWhere(string relativePath, string file)
        {
            this.Where = String.Format("{0} = {1}", Constant.DatabaseColumn.File, SqlUtility.QuoteForSql(file));
            this.Where += String.Format(" AND {0} = {1}", Constant.DatabaseColumn.RelativePath, SqlUtility.QuoteForSql(relativePath));
        }
        public void SetWhere(string file)
        {
            this.Where = String.Format("{0} = {1}", Constant.DatabaseColumn.File, SqlUtility.QuoteForSql(file));
        }
    }
}
