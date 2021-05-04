﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Timelapse.DataStructures;
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

        public int RandomSample { get; set; }

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
            // this is potentially problematic in corner cases; perhaps add an option to show terms for all controls can be added if needed?
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

            // The hacky mess below is simply to get the search terms in an ordered list, where:
            // - the standard visible search terms are at the beginning, in a specific order
            // - the remaining non-standard search terms follow, in the order specified in the template

            // Get the unordered standard search tersm
            IEnumerable<SearchTerm> unodrderedStandardSearchTerms = SearchTerms.Where(
                term => term.DataLabel == Constant.DatabaseColumn.File || term.DataLabel == Constant.DatabaseColumn.Folder ||
                           term.DataLabel == Constant.DatabaseColumn.RelativePath || term.DataLabel == Constant.DatabaseColumn.DateTime ||
                           term.DataLabel == Constant.DatabaseColumn.ImageQuality || term.DataLabel == Constant.DatabaseColumn.UtcOffset || term.DataLabel == Constant.DatabaseColumn.DeleteFlag);

            // Create a dictionary that will contain items in the correct order
            string secondDateTimeLabel = "2nd" + Constant.DatabaseColumn.DateTime;
            Dictionary<string, SearchTerm> dictOrderedTerms = new Dictionary<string, SearchTerm>
            {
                { Constant.DatabaseColumn.File, null },
                { Constant.DatabaseColumn.Folder, null },
                { Constant.DatabaseColumn.RelativePath, null },
                { Constant.DatabaseColumn.DateTime, null },
                { secondDateTimeLabel, null },
                { Constant.DatabaseColumn.UtcOffset, null },
                { Constant.DatabaseColumn.ImageQuality, null },
                { Constant.DatabaseColumn.DeleteFlag, null }
            };

            // Add the unordered search terms into the dictionary, which will put them in the correct order
            List<SearchTerm> orderedSearchTerms = new List<SearchTerm>();
            foreach (SearchTerm searchTerm in unodrderedStandardSearchTerms)
            {
                if (dictOrderedTerms.ContainsKey(searchTerm.DataLabel))
                {
                    if (searchTerm.DataLabel == Constant.DatabaseColumn.DateTime && dictOrderedTerms[searchTerm.DataLabel] != null)
                    {
                        // We need to use the 2nd datetime label as there may be two DateTime entries (i.e. to allow a user to select a date range)
                        dictOrderedTerms[secondDateTimeLabel] = searchTerm;
                    }
                    else
                    {
                        dictOrderedTerms[searchTerm.DataLabel] = searchTerm;
                    }
                }
            }
            // Create a new ordered list of standard search terms based on the non-null (and correctly ordered) search terms in the dictionary
            List<SearchTerm> standardSearchTerms = new List<SearchTerm>();
            foreach (KeyValuePair<string, SearchTerm> kvp in dictOrderedTerms)
            {
                if (kvp.Value != null)
                {
                    standardSearchTerms.Add(kvp.Value);
                }
            }

            // Collect all the non-standard search terms which the user currently selected as UseForSearching
            IEnumerable<SearchTerm> nonStandardSearchTerms = SearchTerms.Except(unodrderedStandardSearchTerms).ToList();
            // FInally, concat the two lists together to collect all the correctly ordered search terms into a single list
            SearchTerms = standardSearchTerms.Concat(nonStandardSearchTerms).ToList();
        }
        #endregion

        #region Public Methods - Set Custom Search From Selection
        // Whenever a shortcut selection is done (other than a custom selection),
        // set the custom selection search terms to mirror that.
        public void SetSearchTermsFromSelection(FileSelectionEnum selection, string relativePath)
        {
            // Don't do anything if the selection was a custom selection
            // Note that FileSelectonENum.Folders is set elsewhere (in MenuItemSelectFOlder_Click) so we don't have to do it here.
            if (this.SearchTerms == null)
            {
                // This shouldn't happen, but just in case treat it as a no-op
                return;
            }

            // The various selections dictate what kinds of search terms to set and use.
            switch (selection)
            {
                case FileSelectionEnum.Custom:
                    // If its a custom selection we use the existing settings
                    // so nothing should be reset
                    return;
                case FileSelectionEnum.All:
                    // Clearing all use fields is the same as selecting All Files

                    this.ClearCustomSearchUses();
                    break;
                case FileSelectionEnum.Folders:
                    // Set and only use the relative path as a search term
                    // Note that we do a return here, so we don't reset the relative path to the constrained root (if the arguments are specified)
                    this.ClearCustomSearchUses();
                    this.SetAndUseRelativePathSearchTerm(relativePath);
                    return;
                case FileSelectionEnum.Corrupted:
                case FileSelectionEnum.Missing:
                case FileSelectionEnum.Ok:
                case FileSelectionEnum.Dark:
                    // Set and only use the chosen ImageQuality as a search term
                    this.ClearCustomSearchUses();
                    this.SetAndUseImageQualitySearchTerm(selection);
                    break;
                case FileSelectionEnum.MarkedForDeletion:
                    // Set and only use the DeleteFlag as true as a search term
                    this.ClearCustomSearchUses();
                    this.SetAndUseDeleteFlagSearchTerm();
                    break;
                default:
                    // Shouldn't get here, but just in case it makes it a no-op
                    return;
            }

            Arguments arguments = Util.GlobalReferences.MainWindow.Arguments;
            // For all other special cases, we also set the relative path if we are contrained to a relative path
            if (arguments != null && arguments.ConstrainToRelativePath)
            {
                this.SetAndUseRelativePathSearchTerm(arguments.RelativePath);
            };
        }
        #endregion

        #region Public Methods- GetFilesWhere() creates and returns a well-formed query
        // Create and return the query composed from the search term list
        public string GetFilesWhere()
        {
            string where = String.Empty;

            // Collect all the standard search terms which the user currently selected as UseForSearching
            IEnumerable<SearchTerm> standardSearchTerms = this.SearchTerms.Where(term => term.UseForSearching
            && (term.DataLabel == Constant.DatabaseColumn.File || term.DataLabel == Constant.DatabaseColumn.Folder ||
                term.DataLabel == Constant.DatabaseColumn.RelativePath || term.DataLabel == Constant.DatabaseColumn.DateTime ||
                term.DataLabel == Constant.DatabaseColumn.ImageQuality || term.DataLabel == Constant.DatabaseColumn.UtcOffset || term.DataLabel == Constant.DatabaseColumn.DeleteFlag));

            // Collect all the non-standard search terms which the user currently selected as UseForSearching
            IEnumerable<SearchTerm> nonstandardSearchTerms = this.SearchTerms.Where(term => term.UseForSearching).Except(standardSearchTerms);

            // Combine the standard terms using the AND operator
            string standardWhere = CombineSearchTermsAndOperator(standardSearchTerms, CustomSelectionOperatorEnum.And);

            // Combine the non-standard terms using the operator defined by the user (either AND or OR)
            string nonStandarWhere = CombineSearchTermsAndOperator(nonstandardSearchTerms, this.TermCombiningOperator);

            // Combine the standardWhere and nonStandardWhere clauses, depending if one or both of them exists
            if (false == String.IsNullOrWhiteSpace(standardWhere) && false == String.IsNullOrWhiteSpace(nonStandarWhere))
            {
                // We have both standard and non-standard clauses, so surround them with parenthesis and combine them with an AND
                // Form: WHERE (standardWhere clauses) AND (nonStandardWhere clauses)
                where += Sql.Where + Sql.OpenParenthesis + standardWhere + Sql.CloseParenthesis
                          + Sql.And
                          + Sql.OpenParenthesis + nonStandarWhere + Sql.CloseParenthesis;
            }
            else if (false == String.IsNullOrWhiteSpace(standardWhere) && String.IsNullOrWhiteSpace(nonStandarWhere))
            {
                // We only have a standard clause
                // Form: WHERE (standardWhere clauses)
                where += Sql.Where + Sql.OpenParenthesis + standardWhere + Sql.CloseParenthesis;
            }
            else if (String.IsNullOrWhiteSpace(standardWhere) && false == String.IsNullOrWhiteSpace(nonStandarWhere))
            {
                // We only have a non-standard clause
                // Form: WHERE nonStandardWhere clauses
                where += Sql.Where + nonStandarWhere;
            }

            // If no detections, we are done. Return the current where clause
            if (GlobalReferences.DetectionsExists == false || this.DetectionSelections.Enabled == false)
            {
                return where;
            }

            // Add the Detection selection terms
            // Form prior to this point: SELECT DataTable.* INNER JOIN DataTable ON DataTable.Id = Detections.Id  
            // (and if a classification it adds: // INNER JOIN Detections ON Detections.detectionID = Classifications.detectionID 

            // There are four basic forms to come up as follows, which determines whether we should add 'WHERE'
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

            // DETECTION, NOT ALL
            // FORM: 
            //   If its a detection:  Detections.category = <DetectionCategory>  
            //   If its a classification:  Classifications.category = <DetectionCategory>  
            // Only added if we are using a detection category (i.e., any category but All Detections)
            if (this.DetectionSelections.AllDetections == false && this.DetectionSelections.EmptyDetections == false)
            {
                if (addAndOr)
                {
                    where += Sql.And;
                }
                if (this.DetectionSelections.RecognitionType == RecognitionType.Detection)
                {
                    // a DETECTION
                    // FORM Detections.Category = <DetectionCategory> 
                    where += SqlPhrase.DetectionCategoryEqualsDetectionCategory(this.DetectionSelections.DetectionCategory);
                }
                else
                {
                    // a CLASSIFICATION, 
                    // FORM Classifications.Category = <ClassificationCategory> 
                    where += SqlPhrase.ClassificationsCategoryEqualsClassificationCategory(this.DetectionSelections.ClassificationCategory);
                }
            }

            // Form:  see below to use the confidence range
            // Note that a confidence of 0 captures empty items with 0 confidence i.e., images with no detections in them
            // For the All category, we really don't wan't to include those, so the confidence has been bumped up slightly(in Item1) above 0
            // For the Empty category, we invert the confidence
            Tuple<double, double> confidenceBounds = this.DetectionSelections.ConfidenceThresholdForSelect;
            if (this.DetectionSelections.RecognitionType == RecognitionType.Detection && this.DetectionSelections.RankByConfidence == false)
            {
                // Detection. Form: Group By Detections.Id Having Max ( Detections.conf ) BETWEEN <Item1> AND <Item2>  e.g.. Between .8 and 1
                where += SqlPhrase.GroupByDetectionsIdHavingMaxDetectionsConf(confidenceBounds.Item1, confidenceBounds.Item2);
            }
            else if (this.DetectionSelections.RecognitionType == RecognitionType.Classification && this.DetectionSelections.RankByConfidence == false)
            {
                // Classification. Form: GROUP BY Classifications.classificationID HAVING MAX  ( Classifications.conf ) e.g.,  BETWEEN 0.8 AND 1
                // Note: we omit this phrase if we are ranking by confidence, as we want to return all classifications
                where += SqlPhrase.GroupByClassificationsIdHavingMaxClassificationsConf(confidenceBounds.Item1, confidenceBounds.Item2);
            }
            return where;
        }

        // Combine the search terms in searchTemrs using the termCombiningOperator (i.e. And or OR), and special cases in as needed.
        private string CombineSearchTermsAndOperator(IEnumerable<SearchTerm> searchTerms, CustomSelectionOperatorEnum termCombiningOperator)
        {
            string where = String.Empty;
            foreach (SearchTerm searchTerm in searchTerms)
            {
                // Basic Form after the ForEach iteration should be:
                // "" if nothing in it
                // a=b for the firt term
                // ... AND/OR c=d ... for subsequent terms (AND/OR defined in termCombiningOperator
                // variations are special cases for relative path and datetime
                string whereForTerm = String.Empty;

                // If we are using detections, then we have to qualify the data label e.g., DataTable.X
                string dataLabel = (this.DetectionSelections.Enabled == true) ? Constant.DBTables.FileData + "." + searchTerm.DataLabel : searchTerm.DataLabel;

                // Check to see if the search term is querying for an empty string
                if (String.IsNullOrEmpty(searchTerm.DatabaseValue) && searchTerm.Operator == Constant.SearchTermOperator.Equal)
                {
                    // It is, so we also need to expand the query to check for both nulls an empty string, as both are considered equivalent for query purposes
                    // Form: ( dataLabel IS NULL OR  dataLabel = '' );
                    whereForTerm = SqlPhrase.LabelIsNullOrDataLabelEqualsEmpty(dataLabel);
                }
                else
                {
                    // The search term is querying for a non-empty value.
                    Debug.Assert(searchTerm.DatabaseValue.Contains("\"") == false, String.Format("Search term '{0}' contains quotation marks and could be used for SQL injection.", searchTerm.DatabaseValue));
                    if (dataLabel == Constant.DatabaseColumn.RelativePath || dataLabel == Constant.DBTables.FileData + "." + Constant.DatabaseColumn.RelativePath)
                    {
                        // Special case for RelativePath and DataTable.RelativePath, 
                        // as we want to return images not only in the relative path folder, but its subfolder as well.
                        // Form: ( DataTable.RelativePath='relpathValue' OR DataTable.RelativePath GLOB 'relpathValue\*' )
                        string term1 = SqlPhrase.DataLabelOperatorValue(dataLabel, TermToSqlOperator(Constant.SearchTermOperator.Equal), searchTerm.DatabaseValue, false);
                        string term2 = SqlPhrase.DataLabelOperatorValue(dataLabel, TermToSqlOperator(Constant.SearchTermOperator.Glob), searchTerm.DatabaseValue + @"\*", false);
                        whereForTerm += Sql.OpenParenthesis + term1 + Sql.Or + term2 + Sql.CloseParenthesis;
                    }
                    else
                    {
                        // Standard search term
                        // Form: dataLabel operator "value", e.g., DataLabel > "5"
                        whereForTerm = SqlPhrase.DataLabelOperatorValue(dataLabel, TermToSqlOperator(searchTerm.Operator), searchTerm.DatabaseValue, searchTerm.ControlType== Constant.Control.Counter);
                    }


                    if (searchTerm.ControlType == Constant.Control.Flag)
                    {
                        // Because flags can have capitals or lower case, we need to make the search case insenstive
                        whereForTerm += Sql.CollateNocase; // so that true and false comparisons are case-insensitive
                    }
                }

                // We are now ready to assemble the search term
                // First, and only if there terms have already been added to  the query, we need to add the appropriate operator
                if (!String.IsNullOrEmpty(where))
                {
                    switch (termCombiningOperator)
                    {
                        case CustomSelectionOperatorEnum.And:
                            where += Sql.And;
                            break;
                        case CustomSelectionOperatorEnum.Or:
                            where += Sql.Or;
                            break;
                        default:
                            throw new NotSupportedException(String.Format("Unhandled logical operator {0}.", termCombiningOperator));
                    }
                }
                // Now we add the actual search terms
                where += whereForTerm;
            }
            // Done. Return this portion of the where clause
            return where;
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

        #region Public Methods - SetAndUse a particular search term
        // Set and use the RelativePath search term to search for the provided relativePath
        // Note that the query using the relative path will be created elsewhere to include sub-folders of the relative path via a GLOB operator
        public void SetAndUseRelativePathSearchTerm(string relativePath)
        {
            SearchTerm searchTerm = this.SearchTerms.First(term => term.DataLabel == Constant.DatabaseColumn.RelativePath);
            searchTerm.DatabaseValue = relativePath;
            searchTerm.Operator = Constant.SearchTermOperator.Equal;
            searchTerm.UseForSearching = true;
        }

        public SearchTerm GetDeleteFlagSearchTerm()
        {
            return this.SearchTerms.First(term => term.DataLabel == Constant.DatabaseColumn.DeleteFlag);
        }

        public void SetDeleteFlagSearchTermValuesTo(SearchTerm searchTermValuesToCopy)
        {
            if (searchTermValuesToCopy == null)
            {
                return;
            }
            SearchTerm currentSearchTerm = this.SearchTerms.First(term => term.DataLabel == Constant.DatabaseColumn.DeleteFlag);
            currentSearchTerm.DatabaseValue = searchTermValuesToCopy.DatabaseValue;
            currentSearchTerm.Operator = searchTermValuesToCopy.Operator;
            currentSearchTerm.UseForSearching = searchTermValuesToCopy.UseForSearching;
        }


        // Set and use the ImageQuality search term 
        public void SetAndUseImageQualitySearchTerm(FileSelectionEnum selection)
        {
            // Set the use field for Image Quality, and its value to one of the three possibilities
            SearchTerm searchTerm = this.SearchTerms.First(term => term.DataLabel == Constant.DatabaseColumn.ImageQuality);
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
                // Shouldn't really get here but just in case, this ignores the search term.
                return;
            }
            searchTerm.UseForSearching = true;
            searchTerm.Operator = Constant.SearchTermOperator.Equal;
            searchTerm.UseForSearching = true;
        }

        public void SetAndUseDeleteFlagSearchTerm()
        {
            // Set the use field for DeleteFlag, and its value to true
            SearchTerm searchTerm = this.SearchTerms.First(term => term.DataLabel == Constant.DatabaseColumn.DeleteFlag);
            searchTerm.DatabaseValue = Constant.BooleanValue.True;
            searchTerm.Operator = Constant.SearchTermOperator.Equal;
            searchTerm.UseForSearching = true;
        }
        #endregion

        #region Public Methods - Various Sets to initialize DateTimes and Offsets
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
        // Special case term for RelativePath 
        // as we want to return images not only in the relative path folder, but its subfolder as well.
        // Construct Partial Sql phrase used in Where for RelativePath that includes subfolders
        // Form: ( RelativePath='Station1\\Deployment2' OR RelativePath GLOB 'Station1\\Deployment2\\*' )  
        //   or if relativePath is empty: ""
        public static string RelativePathGlobToIncludeSubfolders(string relativePathColumnName, string relativePath)
        {
            if (String.IsNullOrEmpty(relativePath))
            {
                return String.Empty;
            }

            // Form: ( DataTable.RelativePath='relpathValue' OR DataTable.RelativePath GLOB 'relpathValue\*' )
            string term1 = SqlPhrase.DataLabelOperatorValue(relativePathColumnName, CustomSelection.TermToSqlOperator(Constant.SearchTermOperator.Equal), relativePath, false); 
            string term2 = SqlPhrase.DataLabelOperatorValue(relativePathColumnName, CustomSelection.TermToSqlOperator(Constant.SearchTermOperator.Glob), System.IO.Path.Combine(relativePath, "*"), false);
            return Sql.OpenParenthesis + term1 + Sql.Or + term2 + Sql.CloseParenthesis;
        }

        // return SQL expressions to database equivalents
        // this is needed as the searchterm operators are unicodes representing symbols rather than real opeators 
        // e.g., \u003d is the symbol for '='
        public static string TermToSqlOperator(string expression)
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
