using System.Collections.Generic;
using Timelapse.Util;

namespace Timelapse.Database
{
    public class SortTerms
    {
        /// <summary>
        /// Create a CustomSort, where we build a list of potential sort terms based on the controls found in the sorted template table
        /// We do this by using and modifying the values returned by CustomSelect. 
        /// While a bit of a hack and less efficient, its easier than re-implementing what we can already get from customSelect.
        /// Note that each sort term is a triplet indicating the Data Label, Label, and a string flag on whether the sort should be ascending (default) or descending.
        /// </summary>
        public static List<SortTerm> GetSortTerms(List<SearchTerm> searchTerms)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(searchTerms, nameof(searchTerms));

            List<SortTerm> sortTerms = new List<SortTerm>();

            // Constraints. 
            // - the SearchTerms list excludes Id, Date, Time and Folder. It also includes two DateTime copies of DateTime
            // - Add Id 
            // - Exclude RelativePath (as we only use File): sorts on 'File' will sort on RelativePath and then by File.
            // - Exclude Date and Time (as we only use DateTime): although the UTC Offset is not calculated in. While limiting, we suspect that users will not care in practice.
            // - Exclude UTCOffset, as that would involve UTCOffset complication. 
            // - Remove the 2nd DateTiime
            // - Add Id as it is missing
            bool firstDateTimeSeen = false;
            sortTerms.Add(new SortTerm(Constant.DatabaseColumn.ID, Constant.DatabaseColumn.ID, Sql.Integer, Constant.BooleanValue.True));

            foreach (SearchTerm searchTerm in searchTerms)
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
                    sortTerms.Add(new SortTerm(searchTerm.DataLabel, Constant.SortTermValues.FileDisplayLabel, searchTerm.ControlType, Constant.BooleanValue.True));
                }
                else if (searchTerm.DataLabel == Constant.DatabaseColumn.DateTime)
                {
                    // Skip the second DateTime
                    if (firstDateTimeSeen == true)
                    {
                        continue;
                    }
                    firstDateTimeSeen = true;
                    sortTerms.Add(new SortTerm(searchTerm.DataLabel, Constant.SortTermValues.DateDisplayLabel, searchTerm.ControlType, Constant.BooleanValue.True));
                }
                else
                {
                    sortTerms.Add(new SortTerm(searchTerm.DataLabel, searchTerm.Label, searchTerm.ControlType, Constant.BooleanValue.True));
                }
            }
            return sortTerms;
        }
    }
}
