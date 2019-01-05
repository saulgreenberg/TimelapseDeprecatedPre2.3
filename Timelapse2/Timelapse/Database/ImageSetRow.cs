using System;
using System.Collections.Generic;
using System.Data;
using Timelapse.Enums;

namespace Timelapse.Database
{
    public class ImageSetRow : DataRowBackedObject
    {
        private const int MaxSortTerms = 8;           

        public ImageSetRow(DataRow row)
            : base(row)
        {
        }

        public FileSelectionEnum FileSelection
        {
            get { return (FileSelectionEnum)this.Row.GetIntegerField(Constant.DatabaseColumn.Selection); }
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

        public string VersionCompatability
        {
            get { return this.Row.GetStringField(Constant.DatabaseColumn.VersionCompatabily); }
            set { this.Row.SetField(Constant.DatabaseColumn.VersionCompatabily, value); }
        }

        // The SortTerms comprises a comma-separated list of terms e.g., "RelativePath, File,,"
        // Helper functions unpack and pack those terms (see below)
        // The first two and second two terms act as Sorting pairs. For most cases, the first term in a pair
        // is the sorting term, where the second term is empty. 
        // However, some sorting criteria are compound. For example, if the user specifies 'Date' the pair 
        // will actually comprise Date,Time. Similarly File is 'RelativePath,File'.
        public string SortTerms
        {
            get { return this.Row.GetStringField(Constant.DatabaseColumn.SortTerms); }
            set { this.Row.SetField(Constant.DatabaseColumn.SortTerms, value); }
        }

        // QuickPasteXML is an XML description of the QuickPasteEntries. It is updated whenever those entries are changed,
        // which means its state is saved.
        public string QuickPasteXML
        {
            get { return this.Row.GetStringField(Constant.DatabaseColumn.QuickPasteXML); }
            set { this.Row.SetField(Constant.DatabaseColumn.QuickPasteXML, value); }
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
                new ColumnTuple(Constant.DatabaseColumn.WhiteSpaceTrimmed, this.WhitespaceTrimmed),
                new ColumnTuple(Constant.DatabaseColumn.VersionCompatabily, this.VersionCompatability),
                new ColumnTuple(Constant.DatabaseColumn.SortTerms, this.SortTerms),
                new ColumnTuple(Constant.DatabaseColumn.QuickPasteXML, this.QuickPasteXML)
            };
            return new ColumnTuplesWithWhere(columnTuples, this.ID);
        }

        public TimeZoneInfo GetSystemTimeZone()
        {
            return TimeZoneInfo.FindSystemTimeZoneById(this.TimeZone);
        }

        #region SortTerms helper functions: setting and getting individual terms in the sort term list
        // The sort term is stored in the database as a string (as a comma-separated list) 
        //  thathas 8 slots. The primary and secondary sort terms
        // are defined in positions 0-3 and 4-7 respectively as:
        //     DataLabel, Label, ControlType, IsAscending, DataLabel, Label, ControlType, IsAscending,

        // Return the first or second sort term structure defining the 1st or 2nd sort term
        public SortTerm GetSortTerm(int whichOne)
        {
            int index = (whichOne == 0) ? 0 : 4;
            return new SortTerm(
                GetSortTermAtPosition(index),
                GetSortTermAtPosition(index + 1), 
                GetSortTermAtPosition(index + 2), 
                GetSortTermAtPosition(index + 3));
        }

        public void SetSortTerm(SortTerm sortTerm1, SortTerm sortTerm2)
        {
            this.SortTerms = String.Join(",", sortTerm1.DataLabel, sortTerm1.DisplayLabel, sortTerm1.ControlType, sortTerm1.IsAscending, sortTerm2.DataLabel, sortTerm2.DisplayLabel, sortTerm2.ControlType, sortTerm2.IsAscending);
        }

        // Return a particular  term at the index position in the sort term
        private string GetSortTermAtPosition(int termIndex)
        {
            string[] sortcriteria = SortTerms.Split(',');
            if (termIndex < sortcriteria.Length)
            {
                return sortcriteria[termIndex].Trim();
            }
            return String.Empty;
        }
        #endregion
    }
}
