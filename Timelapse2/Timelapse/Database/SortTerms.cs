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
            // - the SearchTerms list excludes Id, Date, Time and Folder. It also includes two DateTime copies of DateTime
            // - Add Id 
            // - Exclude RelativePath (as we only use File): sorts on 'File' will sort on RelativePath and then by File.
            // - Exclude Date and Time (as we only use DateTime): although the UTC Offset is not calculated in. While limiting, we suspect that users will not care in practice.
            // - Exclude UTCOffset, as that would involve UTCOffset complication. 
            // - Remove the 2nd DateTiime
            // - Add Id as it is missing
            bool firstDateTimeSeen = false;
            SortTerms.Add(new SortTerm(Constant.DatabaseColumn.ID, Constant.DatabaseColumn.ID));

            foreach (SearchTerm searchTerm in database.CustomSelection.SearchTerms)
            {
                // Necessary modifications:
                // - Exclude UtcOffset, RelativePath
                // - Exclude Date, Time, Folder (they shouldn't be in the SearchTerm list, but just in case)               
                if (searchTerm.DataLabel == Constant.DatabaseColumn.Folder ||
                    searchTerm.DataLabel == Constant.DatabaseColumn.RelativePath ||
                    searchTerm.DataLabel == Constant.DatabaseColumn.Date || 
                    searchTerm.DataLabel == Constant.DatabaseColumn.Time ||
                    searchTerm.DataLabel == Constant.DatabaseColumn.UtcOffset)
                {
                    continue;
                }
                if (searchTerm.DataLabel == Constant.DatabaseColumn.File)
                {
                    SortTerms.Add(new SortTerm(searchTerm.DataLabel, FileLabel));
                }
                else if (searchTerm.DataLabel == Constant.DatabaseColumn.DateTime)
                {
                    // Skip the second DateTime
                    if (firstDateTimeSeen == true)
                    {
                        continue;
                    }
                    firstDateTimeSeen = true;
                    SortTerms.Add(new SortTerm(searchTerm.DataLabel, DateLabel));
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
