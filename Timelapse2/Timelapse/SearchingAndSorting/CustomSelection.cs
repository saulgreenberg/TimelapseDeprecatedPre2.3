using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using Timelapse.Enums;
using Timelapse.Util;

namespace Timelapse.Database
{
    /// <summary>
    /// Class CustomSelection holds a list search term particles, each reflecting criteria for a given field
    /// </summary>
    public class CustomSelection
    {
        #region Public Properties
        public List<SearchTerm> SearchTerms { get; set; }
        public CustomSelectionOperatorEnum TermCombiningOperator { get; set; }
        public bool ShowMissingDetections { get; set; }
        public Detection.DetectionSelections DetectionSelections { get; set; }
        #endregion

        #region Constructor
        /// <summary>
        /// Create a CustomSelection, where we build a list of potential search terms based on the controls found in the sorted template table
        /// The search term will be used only if its 'UseForSearching' field is true
        /// </summary>
        public CustomSelection(DataTableBackedList<ControlRow> templateTable, CustomSelectionOperatorEnum termCombiningOperator)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(templateTable, nameof(templateTable));

            this.DetectionSelections = new Detection.DetectionSelections();
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
                if (searchTerm.List.Count > 0)
                {
                    // Add the empty string to the beginning of the search list, which allows the option of searching for empty items
                    searchTerm.List.Insert(0, String.Empty);
                }
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
                    searchTerm.DatabaseValue = DateTimeHandler.ToStringDatabaseDateTime(Constant.ControlDefault.DateTimeValue);
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
        #endregion

        #region Public Methods- GetFilesWhere() creates and returns a well-formed query
        // Create and return the query composed from the search term list
        public string GetFilesWhere()
        {
            int numberOfDateTimesSearchTerms = 0;
            string where = String.Empty;

            // Construct and show the search term only if that search row is activated
            // Form after the ForEach should be:
            // "" if nothing in it
            // WHERE a=b for single terme
            // WHERE a=b AND c=d ... for multiple terms
            foreach (SearchTerm searchTerm in this.SearchTerms.Where(term => term.UseForSearching))
            {
                if (string.IsNullOrEmpty(where))
                {
                    // Because there is at least one search term, we will need the WHERE clause
                    where += Sql.Where;
                }
                // We want to see how many DateTime search terms we have. If there are two, we will be 'and-ing them nt matter what.
                if (searchTerm.ControlType == Constant.DatabaseColumn.DateTime)
                {
                    numberOfDateTimesSearchTerms++;
                }
                // check to see if the search should match an empty string
                // If so, nulls need also to be matched as NULL and empty are considered interchangeable.
                string whereForTerm;
                string dataLabel = (this.DetectionSelections.Enabled == true) ? Constant.DBTables.FileData + "." + searchTerm.DataLabel : searchTerm.DataLabel;
                // Check to see if the search and operator should match an empty value, in which case we also need to deal with NULLs 
                if (String.IsNullOrEmpty(searchTerm.DatabaseValue) && searchTerm.Operator == Constant.SearchTermOperator.Equal)
                {
                    // Form: ( dataLabel IS NULL OR  dataLabel = '' );
                    whereForTerm = SqlPhraseLabelIsNullOrDataLabelEqualsEmpty(dataLabel);
                }
                else
                {
                    // Form: dataLabel operator "value", e.g., DataLabel > "5"
                    Debug.Assert(searchTerm.DatabaseValue.Contains("\"") == false, String.Format("Search term '{0}' contains quotation marks and could be used for SQL injection.", searchTerm.DatabaseValue));
                    whereForTerm = SqlPhraseDataLabelOperatorValue(dataLabel, searchTerm.Operator, searchTerm.DatabaseValue);
                    if (searchTerm.ControlType == Constant.Control.Flag)
                    {
                        whereForTerm += Sql.CollateNocase; // so that true and false comparisons are case-insensitive
                    }
                }

                // if there is a term in the query other than ' Where 'add either and 'And' or an 'Or' to it 
                if (where != Sql.Where)
                {
                    if (numberOfDateTimesSearchTerms == 2)
                    {
                        where += Sql.And;
                        numberOfDateTimesSearchTerms = 0;
                    }
                    else
                    {
                        switch (this.TermCombiningOperator)
                        {
                            case CustomSelectionOperatorEnum.And:
                                where += Sql.And;
                                break;
                            case CustomSelectionOperatorEnum.Or:
                                where += Sql.Or;
                                break;
                            default:
                                throw new NotSupportedException(String.Format("Unhandled logical operator {0}.", this.TermCombiningOperator));
                        }
                    }
                }
                where += whereForTerm;
            }
            // If no detections, return the above
            if (GlobalReferences.DetectionsExists == false || this.DetectionSelections.Enabled == false)
            {
                return where;
            }

            // Add the Detection selection terms
            // Form prior to this point: SELECT DataTable.* INNER JOIN DataTable ON DataTable.Id = Detections.Id  
            // (and if a classification it adds: // INNER JOIN Detections ON Detections.detectionID = Classifications.detectionID 


            // There are four basic forms to come up as follows, which determines whether we should include add 'WHERE'
            // The first is a detection and uses the detection category (i.e., any category but All Detections)
            // - WHERE Detections.category = <DetectionCategory> GROUP BY ...
            // The second is a dection but does not use a detection category(i.e., All Detections chosen)
            // - GROUP BY ...
            // XXXX The third uses applies to both
            // - WHERE Detections.category = <DetectionCategory> GROUP BY ...
            // - GROUP BY...

            // Form: WHERE or AND/OR
            // Add Where if we are using the first form, otherwise AND
            bool addAndOr = false;
            if (string.IsNullOrEmpty(where) && this.DetectionSelections.AllDetections == false && this.DetectionSelections.EmptyDetections == false)
            {
                where += Sql.Where;
            }
            else
            {
                addAndOr = true;
            }
            System.Diagnostics.Debug.Print(String.Format("RecognitionType:{0} DetectCategory:{1} ClassCategory:{2}", this.DetectionSelections.RecognitionType, this.DetectionSelections.DetectionCategory, this.DetectionSelections.ClassificationCategory));
            // DETECTION, NOT ALL
            // FORM: 
            //   If its a detection:  Detections.category = <DetectionCategory>  
            //   If its a classification:  Classifications.category = <DetectionCategory>  
            // Only added if we are using a detection category (i.e., any category but All Detections)
            if (this.DetectionSelections.AllDetections == false && this.DetectionSelections.EmptyDetections == false)
            {
                if (addAndOr)
                {
                    // Choose whether to use And or Or
                    switch (this.TermCombiningOperator)
                    {
                        case CustomSelectionOperatorEnum.And:
                            where += Sql.And;
                            break;
                        case CustomSelectionOperatorEnum.Or:
                            where += Sql.Or;
                            break;
                        default:
                            throw new NotSupportedException(String.Format("Unhandled logical operator {0}.", this.TermCombiningOperator));
                    }
                }
                if (this.DetectionSelections.RecognitionType == RecognitionType.Detection)
                {
                    // a DETECTION, SO FORM IS Detections.Category = <DetectionCategory> 
                    where += Constant.DBTables.Detections + "." + Constant.DetectionColumns.Category + Sql.Equal + this.DetectionSelections.DetectionCategory;
                }
                else
                {
                    // a CLASSIFICATION, SO FORM IS Classifications.Category = <DetectionCategory> 
                    where += Constant.DBTables.Classifications + "." + Constant.DetectionColumns.Category + Sql.Equal + this.DetectionSelections.ClassificationCategory;
                }
            }

            // Form:  ...Group By Detections.Id Having Max (Detections.conf )  BETWEEN <LOWER BOUND> AND <UPPER BOUND>
            // We always use confidence. 
            // Note that a confidence of 0 captures empty items with 0 confidence i.e., images with no detections in them
            // For the All category, we really don't wan't to include those, so we just bump up the confidence to slightly above 0
            // For the Empty category, we invert the confidence
            if (this.DetectionSelections.RecognitionType == RecognitionType.Detection)
            {
                // Detection. Form: GROUP BY Detections.Id HAVING  MAX  ( Detections.conf ) BETWEEN 0.8 AND 1
                where += Sql.GroupBy + Constant.DBTables.Detections + "." + Constant.DetectionColumns.ImageID + Sql.Having +
                     Sql.Max + Sql.OpenParenthesis + Constant.DBTables.Detections + "." + Constant.DetectionColumns.Conf + Sql.CloseParenthesis;
            }
            else
            {
                // Classification. Form:GROUP BY Classifications.classificationID HAVING MAX  ( Classifications.conf )  BETWEEN 0.8 AND 1
                where += Sql.GroupBy + Constant.DBTables.Classifications + "." + Constant.ClassificationColumns.ClassificationID + Sql.Having +
                     Sql.Max + Sql.OpenParenthesis + Constant.DBTables.Classifications + "." + Constant.DetectionColumns.Conf + Sql.CloseParenthesis;
            }

            Tuple<double, double> confidenceBounds = this.DetectionSelections.ConfidenceThresholdForSelect;
            where += Sql.Between +
                     confidenceBounds.Item1.ToString() + Sql.And + confidenceBounds.Item2.ToString();
            return where;
        }

        /// <summary>
        /// Sql phrase
        /// </summary>
        /// <param name="datalabel"></param>
        /// <returns> ( label IS NULL OR  label = '' ) ;</returns>
        private string SqlPhraseLabelIsNullOrDataLabelEqualsEmpty(string datalabel)
        {
            return Sql.OpenParenthesis + datalabel + Sql.IsNull + Sql.Or + datalabel + Sql.Equal + Sql.QuotedEmptyString + Sql.CloseParenthesis;
        }

        /// <summary>
        /// Sql phrase
        /// </summary>
        /// <param name="dataLabel"></param>
        /// <param name="mathOperator"></param>
        /// <param name="value"></param>
        /// <returns>DataLabel operator "value", e.g., DataLabel > "5"</returns>
        private string SqlPhraseDataLabelOperatorValue(string dataLabel, string mathOperator, string value)
        {
            return dataLabel + TermToSqlOperator(mathOperator) + Sql.Quote(value.Trim()); ;
        }
        #endregion

        #region Public Methods - Various Gets
        public DateTimeOffset GetDateTime(int dateTimeSearchTermIndex, TimeZoneInfo imageSetTimeZone)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(imageSetTimeZone, nameof(imageSetTimeZone));

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
        #endregion

        #region Public Methods - Various Sets
        // Set the RelativePath search term to search for the provided relativePath
        public void SetRelativePathSearchTerm(string relativePath)
        {
            this.ClearCustomSearchUses();

            SearchTerm searchTerm = this.SearchTerms.First(term => term.DataLabel == Constant.DatabaseColumn.RelativePath);
            searchTerm.DatabaseValue = relativePath;
            searchTerm.Operator = Constant.SearchTermOperator.Equal;
            searchTerm.UseForSearching = true;
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
        #endregion

        #region Public Methods - Set Custom Search From Selection
        // Whenever a shortcut selection is done (other than a custom selection),
        // set the custom selection search terms to mirror that.
        public void SetCustomSearchFromSelection(FileSelectionEnum selection, string relativePath)
        {
            // Don't do anything if the selection was a custom selection
            // Note that FileSelectonENum.Folders is set elsewhere (in MenuItemSelectFOlder_Click) so we don't have to do it here.
            if (this.SearchTerms == null || selection == FileSelectionEnum.Custom)
            {
                return;
            }
            // Find the relevant search term, set its use flag to true, and set its database value to whatever we are going to select on.
            SearchTerm searchTerm;
            switch (selection)
            {
                case FileSelectionEnum.All:
                    // Clearing all use fields is the same as selecting All Files
                    this.ClearCustomSearchUses();
                    return;
                case FileSelectionEnum.Corrupted:
                case FileSelectionEnum.Missing:
                case FileSelectionEnum.Ok:
                case FileSelectionEnum.Dark:
                    this.ClearCustomSearchUses();
                    // Set the use field for Image Quality, and its value to one of the three possibilities
                    searchTerm = this.SearchTerms.First(term => term.DataLabel == Constant.DatabaseColumn.ImageQuality);
                    if (selection == FileSelectionEnum.Ok)
                    {
                        searchTerm.DatabaseValue = Constant.ImageQuality.Ok;
                    }
                    else if (selection == FileSelectionEnum.Dark)
                    {
                        searchTerm.DatabaseValue = Constant.ImageQuality.Dark;
                    }
                    else
                    {
                        // Shouldn't really get here but just in case
                        return;
                    }
                    searchTerm.UseForSearching = true;
                    break;
                case FileSelectionEnum.MarkedForDeletion:
                    this.ClearCustomSearchUses();
                    // Set the use field for DeleteFlag, and its value to true
                    searchTerm = this.SearchTerms.First(term => term.DataLabel == Constant.DatabaseColumn.DeleteFlag);
                    searchTerm.DatabaseValue = Constant.BooleanValue.True;
                    searchTerm.UseForSearching = true;
                    break;
                case FileSelectionEnum.Folders:
                    this.SetRelativePathSearchTerm(relativePath);
                    break;
                default:
                    break;
            }
        }
        #endregion

        #region Public Methods - Clear Custom Search Uses
        // Clear all the 'use' flags in the custom search term and in the detections (if any)
        public void ClearCustomSearchUses()
        {
            foreach (SearchTerm searchTerm in this.SearchTerms)
            {
                searchTerm.UseForSearching = false;
            }
            if (GlobalReferences.DetectionsExists && this.DetectionSelections != null)
            {
                this.DetectionSelections.ClearAllDetectionsUses();
            }
            this.ShowMissingDetections = false;
        }
        #endregion

        #region Private Methods - Used by above
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
        #endregion
    }
}
