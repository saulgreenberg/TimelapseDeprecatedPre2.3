using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Timelapse.Enums;
using Timelapse.Util;

namespace Timelapse.Database
{
    /// <summary>
    /// Class CustomSelection holds a list search term particles, each reflecting criteria for a given field
    /// </summary>
    public class CustomSelection
    {
        public List<SearchTerm> SearchTerms { get; set; }
        public CustomSelectionOperatorEnum TermCombiningOperator { get; set; }
        public Detection.DetectionSelections DetectionSelections = new Detection.DetectionSelections();

        /// <summary>
        /// Create a CustomSelection, where we build a list of potential search terms based on the controls found in the sorted template table
        /// The search term will be used only if its 'UseForSearching' field is true
        /// </summary>
        public CustomSelection(DataTableBackedList<ControlRow> templateTable, CustomSelectionOperatorEnum termCombiningOperator)
        {
            this.SearchTerms = new List<SearchTerm>();
            this.TermCombiningOperator = termCombiningOperator;

            // skip hidden controls as they're not normally a part of the user experience
            // this is potentially problematic in corner cases; an option to show terms for all controls can be added if needed
            foreach (ControlRow control in templateTable)
            {
                // If you don't want a control to appear in the CustomSelection, add it here
                // The Folder is usually the same for all files in the image set and thus not useful for selection
                // date and time are redundant with DateTime
                string controlType = control.Type;
                if (controlType == Constant.DatabaseColumn.Date ||
                    controlType == Constant.DatabaseColumn.Folder ||
                    controlType == Constant.DatabaseColumn.Time) 
                {
                    continue;
                }

                // create search term for the control
                SearchTerm searchTerm = new SearchTerm
                {
                    ControlType = controlType,
                    DataLabel = control.DataLabel,
                    DatabaseValue = control.DefaultValue,
                    Operator = Constant.SearchTermOperator.Equal,
                    Label = control.Label,
                    List = control.GetChoices(true),
                    UseForSearching = false
                };
                this.SearchTerms.Add(searchTerm);

                // Create a new search term for each row, where each row specifies a particular control and how it can be searched
                if (controlType == Constant.Control.Counter)
                {
                    searchTerm.DatabaseValue = "0";
                    searchTerm.Operator = Constant.SearchTermOperator.GreaterThan;  // Makes more sense that people will test for > as the default rather than counters
                }
                else if (controlType == Constant.DatabaseColumn.DateTime)
                {
                    // the first time the CustomSelection dialog is popped Timelapse calls SetDateTime() to changes the default date time to the date time 
                    // of the current image
                    searchTerm.DatabaseValue = DateTimeHandler.ToDatabaseDateTimeString(Constant.ControlDefault.DateTimeValue);
                    searchTerm.Operator = Constant.SearchTermOperator.GreaterThanOrEqual;

                    // support querying on a range of datetimes by giving the user two search terms, one configured for the start of the interval and one
                    // for the end
                    SearchTerm dateTimeLessThanOrEqual = new SearchTerm(searchTerm)
                    {
                        Operator = Constant.SearchTermOperator.LessThanOrEqual
                    };
                    this.SearchTerms.Add(dateTimeLessThanOrEqual);
                }
                else if (controlType == Constant.Control.Flag)
                {
                    searchTerm.DatabaseValue = Constant.BooleanValue.False;
                }
                else if (controlType == Constant.DatabaseColumn.UtcOffset)
                {
                    // the first time it's popped CustomSelection dialog changes this default to the date time of the current image
                    searchTerm.SetDatabaseValue(Constant.ControlDefault.DateTimeValue.Offset); 
                }
                // else use default values above
            }
        }

        public DateTimeOffset GetDateTime(int dateTimeSearchTermIndex, TimeZoneInfo imageSetTimeZone)
        {
            DateTime dateTime = this.SearchTerms[dateTimeSearchTermIndex].GetDateTime();
            return DateTimeHandler.FromDatabaseDateTimeIncorporatingOffset(dateTime, imageSetTimeZone.GetUtcOffset(dateTime));
        }

        public string GetRelativePathFolder()
        {
            foreach (SearchTerm searchTerm in this.SearchTerms)
            {
                if (searchTerm.DataLabel == Constant.DatabaseColumn.RelativePath)
                {
                    return searchTerm.DatabaseValue;
                }
            }
            return String.Empty;
        }

        // Create and return the query composed from the search term list
        public string GetFilesWhere()
        {
            // this.DetectionSelections.SetCriteria("1", 0.9, "1", 0.9); //FOR TESTING ONLY - REMOVE THIS

            int numberOfDateTimesSearchTerms = 0;
            string where = String.Empty;

            // Construct and show the search term only if that search row is activated
            foreach (SearchTerm searchTerm in this.SearchTerms.Where(term => term.UseForSearching))
            {
                // We want to see how many DateTime search terms we have. If there are two, we will be 'and-ing them nt matter what.
                if (searchTerm.ControlType == Constant.DatabaseColumn.DateTime)
                {
                    numberOfDateTimesSearchTerms++;
                }
                // check to see if the search should match an empty string
                // If so, nulls need also to be matched as NULL and empty are considered interchangeable.
                string whereForTerm;
                string label = (this.DetectionSelections.Enabled == true) ? Constant.DatabaseTable.FileData + "." + searchTerm.DataLabel : searchTerm.DataLabel;
                // Check to see if the search and operator should match an empty value, in which case we also need to deal with NULLs 
                if (String.IsNullOrEmpty(searchTerm.DatabaseValue) && searchTerm.Operator == Constant.SearchTermOperator.Equal)
                {
                    // The where expression constructed should look something like: (DataLabel IS NULL OR DataLabel = '')
                    whereForTerm = " (" + label + " IS NULL OR " + label + " = '') ";
                    //whereForTerm = " (" + searchTerm.DataLabel + " IS NULL OR " + searchTerm.DataLabel + " = '') ";
                }
                else
                {
                    // The where expression constructed should look something like DataLabel > "5"
                    Debug.Assert(searchTerm.DatabaseValue.Contains("\"") == false, String.Format("Search term '{0}' contains quotation marks and could be used for SQL injection.", searchTerm.DatabaseValue));
                    whereForTerm = label + TermToSqlOperator(searchTerm.Operator) + Utilities.QuoteForSql(searchTerm.DatabaseValue.Trim());
                    //whereForTerm = searchTerm.DataLabel + TermToSqlOperator(searchTerm.Operator) + Utilities.QuoteForSql(searchTerm.DatabaseValue.Trim());
                    if (searchTerm.ControlType == Constant.Control.Flag)
                    {
                        whereForTerm += Constant.Sqlite.CollateNocase; // so that true and false comparisons are case-insensitive
                    }
                }

                // if there is already a term in the query add either and 'And' or an 'Or' to it 
                if (where.Length > 0)
                {
                    if (numberOfDateTimesSearchTerms == 2)
                    {
                        where += Constant.Sqlite.And;
                        numberOfDateTimesSearchTerms = 0;
                    }
                    else
                    { 
                        switch (this.TermCombiningOperator)
                        {
                            case CustomSelectionOperatorEnum.And:
                                where += Constant.Sqlite.And;
                                break;
                            case CustomSelectionOperatorEnum.Or:
                                where += Constant.Sqlite.Or;
                                break;
                            default:
                                throw new NotSupportedException(String.Format("Unhandled logical operator {0}.", this.TermCombiningOperator));
                        }
                    }
                }
                where += whereForTerm;
            }
            // Add the Detection selection terms
            if (DetectionSelections.Enabled)
            {
                if (where != String.Empty) where += Constant.Sqlite.And;
                bool AddAnd = false;
                if (DetectionSelections.UseDetectionCategory)
                {
                    where += Constant.DBTableNames.Detections + "." + Constant.DetectionColumns.Category + Constant.Sqlite.Equal + DetectionSelections.DetectionCategory;
                    AddAnd = true;
                }
                if (DetectionSelections.UseDetectionCategory)
                {

                    if (AddAnd)
                    {
                        where += Constant.Sqlite.And;
                    }
                    where +=  Constant.DBTableNames.Detections + "." + Constant.DetectionColumns.Conf + Constant.Sqlite.GreaterThanEqual + DetectionSelections.DetectionConfidenceThreshold.ToString();
                }
            }
            System.Diagnostics.Debug.Print(where);
            return where.Trim();
        }

        public void SetDateTime(int dateTimeSearchTermIndex, DateTimeOffset newDateTime, TimeZoneInfo imageSetTimeZone)
        {
            DateTimeOffset dateTime = this.GetDateTime(dateTimeSearchTermIndex, imageSetTimeZone);
            this.SearchTerms[dateTimeSearchTermIndex].SetDatabaseValue(new DateTimeOffset(newDateTime.DateTime, dateTime.Offset));
        }

        public void SetDateTimesAndOffset(DateTimeOffset dateTime)
        {
            foreach (SearchTerm dateTimeTerm in this.SearchTerms.Where(term => term.DataLabel == Constant.DatabaseColumn.DateTime))
            {
                dateTimeTerm.SetDatabaseValue(dateTime);
            }

            SearchTerm utcOffsetTerm = this.SearchTerms.FirstOrDefault(term => term.DataLabel == Constant.DatabaseColumn.UtcOffset);
            if (utcOffsetTerm != null)
            {
                utcOffsetTerm.SetDatabaseValue(dateTime.Offset);
            }
        }

        // return SQL expressions to database equivalents
        // this is needed as the searchterm operators are unicodes representing symbols rather than real opeators 
        // e.g., \u003d is the symbol for '='
        private static string TermToSqlOperator(string expression)
        {
            switch (expression)
            {
                case Constant.SearchTermOperator.Equal:
                    return "=";
                case Constant.SearchTermOperator.NotEqual:
                    return "<>";
                case Constant.SearchTermOperator.LessThan:
                    return "<";
                case Constant.SearchTermOperator.GreaterThan:
                    return ">";
                case Constant.SearchTermOperator.LessThanOrEqual:
                    return "<=";
                case Constant.SearchTermOperator.GreaterThanOrEqual:
                    return ">=";
                case Constant.SearchTermOperator.Glob:
                    return Constant.SearchTermOperator.Glob;
                default:
                    return String.Empty;
            }
        }
    }
}
