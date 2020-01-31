using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Timelapse.Util;

namespace Timelapse.Database
{
    /// <summary>
    /// Import and export .csv files.
    /// </summary>
    internal class CsvReaderWriter
    {
        /// <summary>
        /// Export all the database data associated with the selected view to the .csv file indicated in the file path so that spreadsheet applications (like Excel) can display it.
        /// </summary>
        public static void ExportToCsv(FileDatabase database, string filePath, bool excludeDateTimeAndUTCOffset)
        {
            using (TextWriter fileWriter = new StreamWriter(filePath, false))
            {
                // Write the header as defined by the data labels in the template file
                // If the data label is an empty string, we use the label instead.
                // The append sequence results in a trailing comma which is retained when writing the line.
                StringBuilder header = new StringBuilder();
                List<string> dataLabels = database.GetDataLabelsExceptIDInSpreadsheetOrder();
                foreach (string dataLabel in dataLabels)
                {
                    // Skip the DateTime and Utc offset column headers
                    if (excludeDateTimeAndUTCOffset == true && (dataLabel == Constant.DatabaseColumn.DateTime || dataLabel == Constant.DatabaseColumn.UtcOffset))
                    {
                        continue;
                    }
                    header.Append(AddColumnValue(dataLabel));
                }
                fileWriter.WriteLine(header.ToString());

                // For each row in the data table, write out the columns in the same order as the 
                // data labels in the template file
                for (int row = 0; row < database.CountAllCurrentlySelectedFiles; row++)
                {
                    StringBuilder csvRow = new StringBuilder();
                    ImageRow image = database.FileTable[row];
                    foreach (string dataLabel in dataLabels)
                    {
                        // Skip the DateTime and Utc offset data
                        if (excludeDateTimeAndUTCOffset == true && (dataLabel == Constant.DatabaseColumn.DateTime || dataLabel == Constant.DatabaseColumn.UtcOffset))
                        {
                            continue;
                        }
                        csvRow.Append(AddColumnValue(image.GetValueDatabaseString(dataLabel)));
                    }
                    fileWriter.WriteLine(csvRow.ToString());
                }
            }
        }

        public static bool TryImportFromCsv(string filePath, FileDatabase fileDatabase, out List<string> importErrors)
        {
            bool abort = false;
            importErrors = new List<string>();

            List<List<string>> parsedFile = ReadAndParseCSVFile(filePath);
            if (parsedFile == null)
            {
                // Could not open the file
                importErrors.Add(String.Format("The file '{0}' could not be read. To check: Is opened by another application? Is it a valid CSV file?", Path.GetFileName(filePath)));
                return false;
            }

            if (parsedFile.Count < 2)
            {
                // The CSV file is empty or only contains a header row
                importErrors.Add(String.Format("The file '{0}' does not contain any data.", Path.GetFileName(filePath)));
                return false;
            }

            List<string> dataLabels = fileDatabase.GetDataLabelsExceptIDInSpreadsheetOrder();

            // Get the header (and remove any empty trailing headers from the list)
            List<string> dataLabelsFromHeader = parsedFile[0].Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();

            // validate .csv file headers against the database
            List<string> dataLabelsInHeaderButNotFileDatabase = dataLabelsFromHeader.Except(dataLabels).ToList();

            // File - Required datalabel and contents as we can't update the file's data row without it.
            if (dataLabelsFromHeader.Contains(Constant.DatabaseColumn.File) == false)
            {
                importErrors.Add(String.Format("A '{0}' column containing matching file names to your images is required to do the update.", Constant.DatabaseColumn.File));
                abort = true;
            }

            // Required: the column headers must exist in the template as valid DataLabels
            // Note: could do this as a warning rather than as an abort, but...
            foreach (string dataLabel in dataLabelsInHeaderButNotFileDatabase)
            {
                importErrors.Add(String.Format("The column heading '{0}' in the CSV file does not match any DataLabel in the template.", dataLabel));
                abort = true;
            }

            if (abort)
            {
                // We failed. abort.
                return false;
            }

            // Create a List of all data rows, where each row is a dictionary containing the header and that row's valued for the header
            List<Dictionary<string, string>> rowDictionaryList = new List<Dictionary<string, string>>();
            int rowNumber = 0;
            int numberOfHeaders = dataLabelsFromHeader.Count;
            foreach (List<string> parsedRow in parsedFile)
            {
                // For each data row
                rowNumber++;
                if (rowNumber == 1)
                {
                    // Skip the 1st header row
                    continue;
                }

                // for this row, create a dictionary of matching the CSV column Header and that column's value 
                Dictionary<string, string> rowDictionary = new Dictionary<string, string>();
                for (int i = 0; i < numberOfHeaders; i++)
                {
                    string valueToAdd = (i < parsedRow.Count) ? parsedRow[i] : String.Empty;
                    rowDictionary.Add(dataLabelsFromHeader[i], parsedRow[i]);
                }
                rowDictionaryList.Add(rowDictionary);
            }

            // Validate each value in the dictionary against the Header type and expected
            foreach (string header in dataLabelsFromHeader)
            {
                ControlRow controlRow = fileDatabase.GetControlFromTemplateTable(header);

                // We don't need to worry about File-related or Date-related controls as they are mot updated
                if (controlRow.Type == Constant.Control.Flag ||
                    controlRow.Type == Constant.DatabaseColumn.DeleteFlag ||
                    controlRow.Type == Constant.Control.Counter ||
                    controlRow.Type == Constant.Control.FixedChoice ||
                    controlRow.Type == Constant.DatabaseColumn.ImageQuality
                   )
                {
                    rowNumber = 0;
                    foreach (Dictionary<string, string> rowDict in rowDictionaryList)
                    {
                        rowNumber++;
                        switch (controlRow.Type)
                        {
                            case Constant.Control.Flag:
                            case Constant.DatabaseColumn.DeleteFlag:
                                if (!Boolean.TryParse(rowDict[header], out _))
                                {
                                    // Flag values must be true or false, but its not. So raise an error
                                    importErrors.Add(String.Format("Error in row {1}. {0} values must be true or false, but is '{2}'", header, rowNumber, rowDict[header]));
                                    abort = true;
                                }
                                break;
                            case Constant.Control.Counter:
                                if (!String.IsNullOrWhiteSpace(rowDict[header]) && !Int32.TryParse(rowDict[header], out _))
                                {
                                    // Counters must be integers / blanks 
                                    importErrors.Add(String.Format("Error in row {1}. {0} values must be blank or a number, but is '{2}'", header, rowNumber, rowDict[header]));
                                    abort = true;
                                }
                                break;
                            case Constant.Control.FixedChoice:
                            case Constant.DatabaseColumn.ImageQuality:
                                if (controlRow.List.Contains(rowDict[header]) == false)
                                {
                                    // Fixed Choices must be in the Choice List
                                    importErrors.Add(String.Format("Error in row {1}. {0} values must be in the template's choice list, but '{2}' isn't in it.", header, rowNumber, rowDict[header]));
                                    abort = true;
                                }
                                break;
                            default:
                                break;
                        }
                    }
                }
            }
            if (abort)
            {
                // We failed. abort.
                return false;
            }

            // Create the data structure for the query
            // Update the database 100 rows at a time.
            List<ColumnTuplesWithWhere> imagesToUpdate = new List<ColumnTuplesWithWhere>();
            foreach (Dictionary<string, string> rowDict in rowDictionaryList)
            {
                // Process each row
                ColumnTuplesWithWhere imageToUpdate = new ColumnTuplesWithWhere();
                foreach (string header in rowDict.Keys)
                {
                    ControlRow controlRow = fileDatabase.GetControlFromTemplateTable(header);
                    // process each column but only if its off the specific type
                    if (controlRow.Type == Constant.Control.Flag ||
                        controlRow.Type != Constant.DatabaseColumn.DeleteFlag ||
                        controlRow.Type == Constant.Control.Counter ||
                        controlRow.Type == Constant.Control.FixedChoice ||
                        controlRow.Type == Constant.DatabaseColumn.ImageQuality
                        )
                    {
                        imageToUpdate.Columns.Add(new ColumnTuple(header, rowDict[header]));
                    }
                }

                // Add to the query only if there are columns to add!
                if (imageToUpdate.Columns.Count > 0)
                {
                    if (rowDict.ContainsKey(Constant.DatabaseColumn.RelativePath) && !String.IsNullOrWhiteSpace(rowDict[Constant.DatabaseColumn.RelativePath]))
                    {
                        imageToUpdate.SetWhere(rowDict[Constant.DatabaseColumn.RelativePath], rowDict[Constant.DatabaseColumn.File]);
                    }
                    else
                    {
                        imageToUpdate.SetWhere(rowDict[Constant.DatabaseColumn.File]);
                    }
                    imagesToUpdate.Add(imageToUpdate);
                }

                // write current batch of updates to database
                if (imagesToUpdate.Count >= 100)
                {
                    fileDatabase.UpdateFiles(imagesToUpdate);
                    imagesToUpdate.Clear();
                }
            }
            // perform any remaining updates
            fileDatabase.UpdateFiles(imagesToUpdate);
            return true;
        }

        private static string AddColumnValue(string value)
        {
            if (value == null)
            {
                return ",";
            }
            if (value.IndexOfAny("\",\x0A\x0D".ToCharArray()) > -1)
            {
                // commas, double quotation marks, line feeds (\x0A), and carriage returns (\x0D) require leading and ending double quotation marks be added
                // double quotation marks within the field also have to be escaped as double quotes
                return "\"" + value.Replace("\"", "\"\"") + "\"" + ",";
            }

            return value + ",";
        }

        private static List<List<string>> ReadAndParseCSVFile(string path)
        {
            try
            {
                List<List<string>> parsedRows = new List<List<string>>();
                using (TextFieldParser parser = new TextFieldParser(path))
                {
                    parser.Delimiters = new string[] { "," };
                    while (true)
                    {
                        string[] parts = parser.ReadFields();
                        if (parts == null)
                        {
                            break;
                        }
                        List<string> rowFields = parts.ToList<string>();
                        parsedRows.Add(rowFields);
                    }
                }
                return parsedRows;
            }
            catch
            {
                return null;
            }
        }

        #region Defunct
        // Old version for reading and parsing csv before using the built in csv reader
        //private static List<string> ReadAndParseLine(StreamReader csvReader)
        //{
        //    string unparsedLine = csvReader.ReadLine();
        //    if (unparsedLine == null)
        //    {
        //        return null;
        //    }

        //    List<string> parsedLine = new List<string>();
        //    bool isFieldEscaped = false;
        //    int fieldStart = 0;
        //    bool inField = false;
        //    for (int index = 0; index < unparsedLine.Length; ++index)
        //    {
        //        char currentCharacter = unparsedLine[index];
        //        if (inField == false)
        //        {
        //            // We are at the beginning of a field
        //            if (currentCharacter == '\"')
        //            {
        //                // start of escaped field
        //                isFieldEscaped = true;
        //                fieldStart = index + 1;
        //            }
        //            else if (currentCharacter == ',')
        //            {
        //                // empty field
        //                // promote null values to empty values to prevent the presence of SQNull objects in data tables
        //                // much Timelapse code assumes data table fields can be blindly cast to string and breaks once the data table has been
        //                // refreshed after null values are inserted
        //                parsedLine.Add(String.Empty);
        //                continue;
        //            }
        //            else
        //            {
        //                // start of unescaped field
        //                fieldStart = index;
        //            }

        //            inField = true;
        //        }
        //        else
        //        {
        //            // We are in the midst of processing a field
        //            if (currentCharacter == ',' && isFieldEscaped == false)
        //            {
        //                // end of unescaped field
        //                inField = false;
        //                string field = unparsedLine.Substring(fieldStart, index - fieldStart);
        //                parsedLine.Add(field);
        //            }
        //            else if (currentCharacter == '\"' && isFieldEscaped)
        //            {
        //                // escaped character encountered; check for end of escaped field
        //                int nextIndex = index + 1;
        //                if (nextIndex < unparsedLine.Length)
        //                {
        //                    if (unparsedLine[nextIndex] == ',')
        //                    {
        //                        // end of escaped field
        //                        // note: Whilst this implementation supports escaping of carriage returns and line feeds on export it does not support them on
        //                        // import.  This is common in .csv parsers and can be addressed if needed by appending the next line to unparsedLine and 
        //                        // continuing parsing rather than terminating the field.
        //                        inField = false;
        //                        isFieldEscaped = false;
        //                        string field = unparsedLine.Substring(fieldStart, index - fieldStart);
        //                        field = field.Replace("\"\"", "\"");
        //                        parsedLine.Add(field);
        //                        ++index;
        //                    }
        //                    else if (unparsedLine[nextIndex] == '"')
        //                    {
        //                        // escaped double quotation mark
        //                        // just move next to skip over the second quotation mark as replacement back to one quotation mark is done in field extraction
        //                        ++index;
        //                    }
        //                }
        //                else
        //                {
        //                    // We are at the end, still escaped, with no comma delimiting the last field
        //                    // We have to get rid of the last escaped quote
        //                    string field = unparsedLine.Substring(fieldStart, unparsedLine.Length - fieldStart);
        //                    field = field.TrimEnd(new Char[] { '"' }); ;
        //                    parsedLine.Add(field);
        //                    ++index;
        //                }
        //            }
        //        }
        //    }
        //    // This code had a bug, which I fixed above (where it says 'We are at the end". But just leave it here for now until I am sure.
        //    // if the last character is a non-comma add the final (non-empty) field
        //    // final empty fields are ambiguous at this level and therefore handled by the caller
        //    // if (inField)
        //    // {
        //    //    string field = unparsedLine.Substring(fieldStart, unparsedLine.Length - fieldStart);
        //    //    if (isFieldEscaped)
        //    //    {
        //    //        field = field.Replace("\"\"", "\"");
        //    //    }
        //    //    parsedLine.Add(field);
        //    // }
        //    // return parsedLine;
        //}


        // Orignal version for importing CSV files. 
        // Throw away if new version stands the test of time.
        //public static bool TryImportFromCsv(string filePath, FileDatabase fileDatabase, out List<string> importErrors)
        //{
        //    importErrors = new List<string>();

        //    List<string> dataLabels = fileDatabase.GetDataLabelsExceptIDInSpreadsheetOrder();
        //    using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        //    {
        //        using (StreamReader csvReader = new StreamReader(stream))
        //        {
        //            // validate .csv file headers against the database
        //            List<string> dataLabelsFromHeader = ReadAndParseLine(csvReader);
        //            List<string> dataLabelsInFileDatabaseButNotInHeader = dataLabels.Except(dataLabelsFromHeader).ToList();
        //            foreach (string dataLabel in dataLabelsInFileDatabaseButNotInHeader)
        //            {
        //                if (dataLabel == Constant.DatabaseColumn.DateTime || dataLabel == Constant.DatabaseColumn.UtcOffset)
        //                {
        //                    continue;
        //                }
        //                importErrors.Add("- A column with the DataLabel '" + dataLabel + "' is present in the database but nothing matches that in the .csv file." + Environment.NewLine);
        //            }
        //            List<string> dataLabelsInHeaderButNotFileDatabase = dataLabelsFromHeader.Except(dataLabels).ToList();
        //            if (importErrors.Count > 0 && dataLabelsInHeaderButNotFileDatabase.Count > 0)
        //            {
        //                // Insert a separator if needed
        //                importErrors.Add(String.Empty);
        //            }

        //            foreach (string dataLabel in dataLabelsInHeaderButNotFileDatabase)
        //            {
        //                importErrors.Add("- A column with the DataLabel '" + dataLabel + "' is present in the .csv file but nothing matches that in the database." + Environment.NewLine);
        //            }

        //            if (importErrors.Count > 0)
        //            {
        //                return false;
        //            }

        //            // read image updates from the .csv file
        //            List<ColumnTuplesWithWhere> imagesToUpdate = new List<ColumnTuplesWithWhere>();
        //            for (List<string> row = ReadAndParseLine(csvReader); row != null; row = ReadAndParseLine(csvReader))
        //            {
        //                if (row.Count == dataLabels.Count - 1)
        //                {
        //                    // .csv files are ambiguous in the sense a trailing comma may or may not be present at the end of the line
        //                    // if the final field has a value this case isn't a concern, but if the final field has no value then there's
        //                    // no way for the parser to know the exact number of fields in the line
        //                    row.Add(String.Empty);
        //                }
        //                else if (row.Count != dataLabels.Count)
        //                {
        //                    TraceDebug.PrintMessage(String.Format("Expected {0} fields in line {1} but found {2}.", dataLabels.Count, String.Join(",", row), row.Count));
        //                }

        //                // assemble set of column values to update
        //                string imageFileName = null;
        //                // string folder = null; // Folder isn't used - but I just kept it in just in case as I haven't tested what happens on import CSV
        //                string relativePath = null;
        //                ColumnTuplesWithWhere imageToUpdate = new ColumnTuplesWithWhere();
        //                for (int field = 0; field < row.Count; ++field)
        //                {
        //                    string dataLabel = dataLabelsFromHeader[field];
        //                    string value = row[field];

        //                    // capture components of image's unique identifier for constructing where clause
        //                    // at least for now, it's assumed all renames or moves are done through Timelapse and hence file name + folder + relative path form 
        //                    // an immutable (and unique) ID
        //                    if (dataLabel == Constant.DatabaseColumn.File)
        //                    {
        //                        imageFileName = value;
        //                    }
        //                    else if (dataLabel == Constant.DatabaseColumn.Folder)
        //                    {
        //                        // folder = value;
        //                        continue;
        //                    }
        //                    else if (dataLabel == Constant.DatabaseColumn.RelativePath)
        //                    {
        //                        relativePath = value;
        //                    }
        //                    else if (dataLabel == Constant.DatabaseColumn.Date ||
        //                             dataLabel == Constant.DatabaseColumn.Time)
        //                    {
        //                        // ignore date and time for now as they're redundant with the DateTime column
        //                        // if needed these two fields can be combined, put to DateTime.ParseExact(), and conflict checking done with DateTime
        //                        // Excel tends to change 
        //                        // - dates from dd-MMM-yyyy to dd-MMM-yy 
        //                        // - times from HH:mm:ss to H:mm:ss
        //                        // when saving csv files
        //                        continue;
        //                    }
        //                    else if (dataLabel == Constant.DatabaseColumn.DateTime && DateTimeHandler.TryParseDatabaseDateTime(value, out DateTime dateTime))
        //                    {
        //                        // pass DateTime to ColumnTuple rather than the string as ColumnTuple owns validation and formatting
        //                        imageToUpdate.Columns.Add(new ColumnTuple(dataLabel, dateTime));
        //                    }
        //                    else if (dataLabel == Constant.DatabaseColumn.UtcOffset && DateTimeHandler.TryParseDatabaseUtcOffsetString(value, out TimeSpan utcOffset))
        //                    {
        //                        // as with DateTime, pass parsed UTC offset to ColumnTuple rather than the string as ColumnTuple owns validation and formatting
        //                        imageToUpdate.Columns.Add(new ColumnTuple(dataLabel, utcOffset));
        //                    }
        //                    else if (fileDatabase.FileTableColumnsByDataLabel[dataLabel].IsContentValid(value))
        //                    {
        //                        // include column in update query if value is valid
        //                        imageToUpdate.Columns.Add(new ColumnTuple(dataLabel, value));
        //                    }
        //                    else
        //                    {
        //                        // if value wasn't processed by a previous clause it's invalid (or there's a parsing bug)
        //                        importErrors.Add(String.Format("Value '{0}' is not valid for the column {1}.", value, dataLabel));
        //                    }
        //                }

        //                // accumulate image
        //                Debug.Assert(String.IsNullOrWhiteSpace(imageFileName) == false, "File name was not loaded.");
        //                imageToUpdate.SetWhere(relativePath, imageFileName);
        //                imagesToUpdate.Add(imageToUpdate);

        //                // write current batch of updates to database
        //                if (imagesToUpdate.Count >= 100)
        //                {
        //                    fileDatabase.UpdateFiles(imagesToUpdate);
        //                    imagesToUpdate.Clear();
        //                }
        //            }
        //            // perform any remaining updates
        //            fileDatabase.UpdateFiles(imagesToUpdate);
        //            return true;
        //        }
        //    }
        //}


        //public static bool TryImportFromCsvOlderVersionWithoutUsingBuiltInCSVReader(string filePath, FileDatabase fileDatabase, out List<string> importErrors)
        //{
        //    bool abort = false;
        //    importErrors = new List<string>();

        //    List<string> dataLabels = fileDatabase.GetDataLabelsExceptIDInSpreadsheetOrder();
        //    using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        //    {
        //        using (StreamReader csvReader = new StreamReader(stream))
        //        {
        //            // validate .csv file headers against the database
        //            List<string> dataLabelsFromHeader = ReadAndParseLine(csvReader);
        //            List<string> dataLabelsInHeaderButNotFileDatabase = dataLabelsFromHeader.Except(dataLabels).ToList();

        //            // File - Required datalabel and contents as we can't update the file's data row without it.
        //            if (dataLabelsFromHeader.Contains(Constant.DatabaseColumn.File) == false)
        //            {
        //                importErrors.Add(String.Format("A '{0}' column containing matching file names to your images is required to do the update.", Constant.DatabaseColumn.File));
        //                abort = true;
        //            }

        //            // Required: the column headers must exist in the template as valid DataLabels
        //            // Note: could do this as a warning rather than as an abort, but...
        //            foreach (string dataLabel in dataLabelsInHeaderButNotFileDatabase)
        //            {
        //                importErrors.Add(String.Format("The column heading '{0}' in the CSV file does not match any DataLabel in the template.", dataLabel));
        //                abort = true;
        //            }

        //            if (abort)
        //            {
        //                // We failed. abort.
        //                return false;
        //            }

        //            List<Dictionary<string, string>> rowDictionaryList = new List<Dictionary<string, string>>();
        //            int rowNumber = 0;
        //            for (List<string> row = ReadAndParseLine(csvReader); row != null; row = ReadAndParseLine(csvReader))
        //            {
        //                rowNumber++;
        //                if (row.Count == dataLabelsFromHeader.Count - 1)
        //                {
        //                    // .csv files are ambiguous in the sense a trailing comma may or may not be present at the end of the line
        //                    // if the final field has a value this case isn't a concern, but if the final field has no value then there's
        //                    // no way for the parser to know the exact number of fields in the line
        //                    row.Add(String.Empty);
        //                }
        //                else if (row.Count != dataLabelsFromHeader.Count)
        //                {
        //                    importErrors.Add(String.Format("Expected {0} fields in line {1} but found {2}.", dataLabelsFromHeader.Count, rowNumber, row.Count));
        //                    abort = true;
        //                }

        //                // For a single row, create a dictionary matching the CSV column Header and that row's recorded value for that column
        //                // Project-specific DataLabel fields and values
        //                string[] rowArray = row.ToArray();
        //                string[] headerArray = dataLabelsFromHeader.ToArray();
        //                Dictionary<string, string> rowDictionary = new Dictionary<string, string>();

        //                for (int i = 0; i < headerArray.Length; i++)
        //                {
        //                    if (i < rowArray.Length)
        //                    {
        //                        // Check values for Counters and Flags and report and error if the type does not match ie., true/false for Flags, 
        //                        // Note that Date - related columns are ignored, so checking their type doesn't matter
        //                        ControlRow controlRow = fileDatabase.GetControlFromTemplateTable(headerArray[i]);
        //                        if ((controlRow.Type == Constant.Control.Flag || controlRow.Type == Constant.DatabaseColumn.DeleteFlag) && !Boolean.TryParse(rowArray[i], out _))
        //                        {
        //                            // Flag values must be true or false, but its not. So raise an error
        //                            importErrors.Add(String.Format("Error in row {1}. {0} values must be true or false, but is '{2}'", headerArray[i], rowNumber, rowArray[i]));
        //                            abort = true;
        //                        }
        //                        else if (controlRow.Type == Constant.Control.Counter && !String.IsNullOrEmpty(rowArray[i]) && !Int32.TryParse(rowArray[i], out _))
        //                        {
        //                            // Counters must be integers / blanks 
        //                            importErrors.Add(String.Format("Error in row {1}. {0} values must be blank or a number, but is '{2}'", headerArray[i], rowNumber, rowArray[i]));
        //                            abort = true;
        //                        }
        //                        // Even if its an error, we can add it as we will abort before its used
        //                        rowDictionary.Add(headerArray[i], rowArray[i].Trim());
        //                    }
        //                }

        //                // We now have the dictionary containing the key/value pairs of the header/data information for the row.
        //                // Add it to the List of row dictionaries
        //                rowDictionaryList.Add(rowDictionary);
        //            }
        //            if (abort)
        //            {
        //                // We failed. abort.
        //                return false;
        //            }

        //            // Create the data structure for the query
        //            // Update the database 100 rows at a time.
        //            List<ColumnTuplesWithWhere> imagesToUpdate = new List<ColumnTuplesWithWhere>();
        //            foreach (Dictionary<string, string> rowDict in rowDictionaryList)
        //            {
        //                // Process each row
        //                ColumnTuplesWithWhere imageToUpdate = new ColumnTuplesWithWhere();
        //                foreach (string key in rowDict.Keys)
        //                {
        //                    // process each columne
        //                    if (key != Constant.DatabaseColumn.File)
        //                    {
        //                        imageToUpdate.Columns.Add(new ColumnTuple(key, rowDict[key]));
        //                    }
        //                }
        //                if (rowDict.ContainsKey(Constant.DatabaseColumn.RelativePath) && !String.IsNullOrWhiteSpace(rowDict[Constant.DatabaseColumn.RelativePath]))
        //                {
        //                    imageToUpdate.SetWhere(rowDict[Constant.DatabaseColumn.RelativePath], rowDict[Constant.DatabaseColumn.File]);
        //                }
        //                else
        //                {
        //                    imageToUpdate.SetWhere(String.Empty, rowDict[Constant.DatabaseColumn.File]);
        //                }
        //                imagesToUpdate.Add(imageToUpdate);

        //                // write current batch of updates to database
        //                if (imagesToUpdate.Count >= 100)
        //                {
        //                    fileDatabase.UpdateFiles(imagesToUpdate);
        //                    imagesToUpdate.Clear();
        //                }
        //            }
        //            // perform any remaining updates
        //            fileDatabase.UpdateFiles(imagesToUpdate);
        //            return true;
        //        }
        //    }
        //}

        #endregion
    }
}
