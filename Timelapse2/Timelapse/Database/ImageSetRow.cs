using System;
using System.Collections.Generic;
using System.Data;

namespace Timelapse.Database
{
    public class ImageSetRow : DataRowBackedObject
    {
        public ImageSetRow(DataRow row)
            : base(row)
        {
        }

        public FileSelection FileSelection
        {
            get { return (FileSelection)this.Row.GetIntegerField(Constant.DatabaseColumn.Selection); }
            set { this.Row.SetField(Constant.DatabaseColumn.Selection, (int)value); }
        }

        public long MostRecentFileID
        {
            get { return this.Row.GetLongStringField(Constant.DatabaseColumn.MostRecentFileID); }
            set { this.Row.SetField(Constant.DatabaseColumn.MostRecentFileID, value); }
        }

        public string Log
        {
            get { return this.Row.GetStringField(Constant.DatabaseColumn.Log); }
            set { this.Row.SetField(Constant.DatabaseColumn.Log, value); }
        }

        public bool MagnifyingGlassEnabled
        {
            get { return this.Row.GetBooleanField(Constant.DatabaseColumn.MagnifyingGlass); }
            set { this.Row.SetField(Constant.DatabaseColumn.MagnifyingGlass, value); }
        }

        public string TimeZone
        {
            get { return this.Row.GetStringField(Constant.DatabaseColumn.TimeZone); }
            set { this.Row.SetField(Constant.DatabaseColumn.TimeZone, value); }
        }

        public bool WhitespaceTrimmed
        {
            get { return this.Row.GetBooleanField(Constant.DatabaseColumn.WhiteSpaceTrimmed); }
            set { this.Row.SetField(Constant.DatabaseColumn.WhiteSpaceTrimmed, value); }
        }

        public override ColumnTuplesWithWhere GetColumnTuples()
        {
            List<ColumnTuple> columnTuples = new List<ColumnTuple>
            {
                new ColumnTuple(Constant.DatabaseColumn.Selection, (int)this.FileSelection),
                new ColumnTuple(Constant.DatabaseColumn.Log, this.Log),
                new ColumnTuple(Constant.DatabaseColumn.MagnifyingGlass, this.MagnifyingGlassEnabled),
                new ColumnTuple(Constant.DatabaseColumn.MostRecentFileID, this.MostRecentFileID),
                new ColumnTuple(Constant.DatabaseColumn.TimeZone, this.TimeZone),
                new ColumnTuple(Constant.DatabaseColumn.WhiteSpaceTrimmed, this.WhitespaceTrimmed)
            };
            return new ColumnTuplesWithWhere(columnTuples, this.ID);
        }

        public TimeZoneInfo GetSystemTimeZone()
        {
            return TimeZoneInfo.FindSystemTimeZoneById(this.TimeZone);
        }
    }
}
