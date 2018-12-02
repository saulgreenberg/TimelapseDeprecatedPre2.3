using System;
using System.Collections.Generic;
using System.Data;

namespace Timelapse.Database
{

    public class ImageSetRow : DataRowBackedObject
    {
        private const int MaxSortTerms = 4;                              // The max number of sort terms allowed in the SortTerms list

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
                new ColumnTuple(Constant.DatabaseColumn.SortTerms, this.SortTerms)
            };
            return new ColumnTuplesWithWhere(columnTuples, this.ID);
        }

        public TimeZoneInfo GetSystemTimeZone()
        {
            return TimeZoneInfo.FindSystemTimeZoneById(this.TimeZone);
        }

        #region SortTerms helper functions: setting and getting individual terms in the sort term list
        // Return a sort term at the index position in the 0-based list of sort terms
        public string GetSortTerm(int indexInSortTerms)
        {
            string[] sortcriteria = SortTerms.Split(',');
            if (indexInSortTerms < sortcriteria.Length)
            {
                return (sortcriteria[indexInSortTerms].Trim());
            }
            return (String.Empty);
        }

        // Set a sort term at the index position in the 0-based list of sort terms
        public void SetSortTerm(string sortTerm, int indexInSortTerms)
        {
            string[] current_sortterms = SortTerms.Split(',');
            string[] new_sortterms = new string[MaxSortTerms];
            for (int i = 0; i < MaxSortTerms; i++)
            {
                if (i == indexInSortTerms)
                {
                    new_sortterms[i] = sortTerm;
                }
                else
                {
                    new_sortterms[i] = (indexInSortTerms < SortTerms.Length) ? current_sortterms[i] : String.Empty;
                }
            }
            this.SortTerms = String.Join(",", new_sortterms);
        }
        public void SetSortTerm(string term1, string term2, string term3, string term4)
        {
            this.SortTerms = String.Join(",", term1, term2, term3, term4);
        }
        #endregion
    }
}
