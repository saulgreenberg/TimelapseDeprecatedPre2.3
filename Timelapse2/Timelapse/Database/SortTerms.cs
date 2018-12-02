using System;
using System.Collections.Generic;

namespace Timelapse.Database
{
    public class SortTerms
    {
        /// <summary>
        /// Create a CustomSort, where we build a list of potential sort terms based on the controls found in the sorted template table
        /// We do this by using and modifying the values returned by CustomSelect. 
        /// While a bit of a hack and less efficient, its easier than re-implementing what we can already get from customSelect.
        /// </summary>
        static public List<SortTerm> GetSortTerms(FileDatabase database)
        {
            const string DateLabel = "Date and time";
            const string FileLabel = "File path (relative path + file name)";
            List <SortTerm> SortTerms = new List<SortTerm>();

            // Constraints. 
            // - Add Id and Date: the SearchTerms list excludes Id, Date, Time and Folder. It does include RelativePath, DateTime, and UTCOffset 
            // - Exclude RelativePath (only use File): sorts on 'File' will sort on RelativePath and then by File.
            // - Exclude Time (only use Date): sorts on 'Date' will  will sort  Date and then by Time.
            // - Exclude DateTime and UTCOffset, as that would involve the UTCOffset complication. While limiting, we suspect that users will not care in practice.
            // 
            // Necessary modification:
            // - Add Id and Date as they are missing
            SortTerms.Add(new SortTerm(Constant.DatabaseColumn.ID, Constant.DatabaseColumn.ID));
            SortTerms.Add(new SortTerm(Constant.DatabaseColumn.Date, DateLabel));

            foreach (SearchTerm searchTerm in database.CustomSelection.SearchTerms)
            {
                // Necessary modifications:
                // - Exclude DateTime, UtcOffset
                // - Exclude RelativePath
                // - Exclude Time, Folder (they shouldn't be in the SearchTerm list, but just in case)               
                if (searchTerm.DataLabel == Constant.DatabaseColumn.Folder ||
                    searchTerm.DataLabel == Constant.DatabaseColumn.RelativePath ||
                    searchTerm.DataLabel == Constant.DatabaseColumn.DateTime || 
                    searchTerm.DataLabel == Constant.DatabaseColumn.Time ||
                    searchTerm.DataLabel == Constant.DatabaseColumn.UtcOffset)
                {
                    continue;
                }
                if (searchTerm.DataLabel == Constant.DatabaseColumn.File)
                {
                    SortTerms.Add(new SortTerm(searchTerm.DataLabel, FileLabel));
                }
                else
                {
                    SortTerms.Add(new SortTerm(searchTerm.DataLabel, searchTerm.Label));
                }
            }
            return SortTerms;
        }
    }

    // A SortTerm comprises a DataLabel and a Label
    public class SortTerm
    {
        public string DataLabel { get; set; }
        public string Label { get; set; }

        public SortTerm()
        {
            this.DataLabel = String.Empty;
            this.Label = String.Empty;
        }
        public SortTerm(string dataLabel, string label)
        {
            this.DataLabel = dataLabel;
            this.Label = label;
        }
    }
}
