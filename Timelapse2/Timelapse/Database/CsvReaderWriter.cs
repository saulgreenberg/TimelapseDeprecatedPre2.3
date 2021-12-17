using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Timelapse.Controls;
using Timelapse.Enums;
using Timelapse.Util;

namespace Timelapse.Database
{
    /// <summary>
    /// Import and export .csv files.
    /// </summary>
    internal class CsvReaderWriter
    {
        #region Public Static Method - Export to CSV
        /// <summary>
        /// Export all the database data associated with the selected view to the .csv file indicated in the file path so that spreadsheet applications (like Excel) can display it.
        /// </summary>
        public static bool ExportToCsv(FileDatabase database, string filePath, CSVDateTimeOptionsEnum csvDateTimeOptions, bool csvInsertSpaceBeforeDates)
        {
            try
            {
                using (StreamWriter fileWriter = new StreamWriter(filePath, false))
                {
                    // Write the header as defined by the data labels in the template file
                    // If the data label is an empty string, we use the label instead.
                    // The append sequence results in a trailing comma which is retained when writing the line.
                    StringBuilder header = new StringBuilder();
                    List<string> dataLabels = database.GetDataLabelsExceptIDInSpreadsheetOrder();
                    foreach (string dataLabel in dataLabels)
                    {
                        if (dataLabel == Constant.DatabaseColumn.UtcOffset)
                        {
                            // Always skip UTC Offset, as the user has the option of including that in the DateTime column instead
                            continue;
                        }
                        // Skip the DateTime and Utc offset column headers
                        //if (excludeDateTimeAndUTCOffset == true && (dataLabel == Constant.DatabaseColumn.DateTime || dataLabel == Constant.DatabaseColumn.UtcOffset))
                        if ((dataLabel == Constant.DatabaseColumn.Date || dataLabel == Constant.DatabaseColumn.Time) && csvDateTimeOptions != CSVDateTimeOptionsEnum.DateAndTimeColumns)
                        {
                            // Skip the Date column and Time column if the CSVDateTimeOptions are set to a parameter other than the two Date / Time columns 
                            continue;
                        }
                        if (dataLabel == Constant.DatabaseColumn.DateTime && csvDateTimeOptions == CSVDateTimeOptionsEnum.DateAndTimeColumns)
                        {
                            // Skip the DateTime column if the CSVDateTimeOptions is set to show the two Date / Time columns instead
                            continue;
                        }
                        header.Append(AddColumnValue(dataLabel));
                    }
                    fileWriter.WriteLine(header.ToString());

                    // For each row in the data table, write out the columns in the same order as the 
                    // data labels in the template file
                    int countAllCurrentlySelectedFiles = database.CountAllCurrentlySelectedFiles;
                    for (int row = 0; row < countAllCurrentlySelectedFiles; row++)
                    {
                        StringBuilder csvRow = new StringBuilder();
                        ImageRow image = database.FileTable[row];
                        foreach (string dataLabel in dataLabels)
                        {
                            if (dataLabel == Constant.DatabaseColumn.UtcOffset)
                            {
                                // Always skip UTC Offset, as the user has the option of including that in the DateTime column instead
                                continue;
                            }
                            if ((dataLabel == Constant.DatabaseColumn.Date || dataLabel == Constant.DatabaseColumn.Time) && csvDateTimeOptions != CSVDateTimeOptionsEnum.DateAndTimeColumns)
                            {
                                // Skip the Date column and Time column if the CSVDateTimeOptions are set to a parameter other than the two Date / Time columns 
                                continue;
                            }
                            if (dataLabel == Constant.DatabaseColumn.DateTime)
                            {
                                if (csvDateTimeOptions == CSVDateTimeOptionsEnum.DateAndTimeColumns)
                                {
                                    // Skip the DateTime column if the CSVDateTimeOptions is set to show the two Date / Time columns instead
                                    continue;
                                }
                                else
                                {
                                    string prefix = csvInsertSpaceBeforeDates ? " " : String.Empty;
                                    if (csvDateTimeOptions == CSVDateTimeOptionsEnum.LocalDateTimeColumn)
                                    {
                                        csvRow.Append(prefix + AddColumnValue(image.GetValueCSVLocalDateTimeString()));
                                    }
                                    else if (csvDateTimeOptions == CSVDateTimeOptionsEnum.LocalDateTimeWithoutTSeparatorColumn)
                                    {
                                        csvRow.Append(prefix + AddColumnValue(image.GetValueCSVLocalDateTimeWithoutTSeparatorString()));
                                    }
                                    else if (csvDateTimeOptions == CSVDateTimeOptionsEnum.UTCWithOffsetDateTimeColumn)
                                    {
                                        csvRow.Append(prefix + AddColumnValue(image.GetValueCSVUTCWithOffsetDateTimeString()));
                                    }
                                }
                            }
                            else if (dataLabel == Constant.DatabaseColumn.Date || dataLabel == Constant.DatabaseColumn.Time)
                            {
                                string prefix = csvInsertSpaceBeforeDates ? " " : String.Empty;
                                csvRow.Append(prefix + AddColumnValue(image.GetValueDatabaseString(dataLabel)));
                            }
                            else
                            {
                                csvRow.Append(AddColumnValue(image.GetValueDatabaseString(dataLabel)));
                            }
                        }
                        fileWriter.WriteLine(csvRow.ToString());
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
        #endregion

        #region Public Static Method - Import from CSV (async)
        // Try importing a CSV file, checking its headers and values against the template's DataLabels and data types.
        // Duplicates are handled.
        // Return a list of errors if needed.
        // However, error reporting is limited to only gross mismatches.
        // Note that:
        // - rows in the CSV file that are not in the .ddb file are ignored (not reported - maybe it should be?)
        // - rows in the .ddb file that are not in the CSV file are ignored
        // - if there are more duplicate rows for an image in the .csv file than there are in the .ddb file, those extra duplicates are ignored (not reported - maybe it should be?)
        // - if there are more duplicate rows for an image in the .ddb file than there are in the .csv file, those extra duplicates are ignored (not reported - maybe it should be?)
        public static async Task<Tuple<bool, List<string>>> TryImportFromCsv(string filePath, FileDatabase fileDatabase)
        {
            // Set up a progress handler that will update the progress bar
            Progress<ProgressBarArguments> progressHandler = new Progress<ProgressBarArguments>(value =>
            {
                // Update the progress bar
                CsvReaderWriter.UpdateProgressBar(GlobalReferences.BusyCancelIndicator, value.PercentDone, value.Message, value.IsCancelEnabled, value.IsIndeterminate);
            });
            IProgress<ProgressBarArguments> progress = progressHandler;

            bool abort = false;
            List<string> importErrors = new List<string>();

            return await Task.Run(() =>
                {
                    const int bulkFilesToHandle = 500;
                    int processedFilesCount = 0;
                    progress.Report(new ProgressBarArguments(0, "Reading the CSV file. Please wait", false, true));

                    //
                    // Part 1. Abort if there is a problem in reading the CSV file or if the CSV file is empty.
                    //
                    List<List<string>> parsedFile = ReadAndParseCSVFile(filePath);
                    
                    // Abort if the CSV file could not be read 
                    if (parsedFile == null)
                    {
                        // Could not open the file
                        importErrors.Add(String.Format("The file '{0}' could not be read. This could happen if the file is currently opened by another application, or if its not a valid CSV file.", Path.GetFileName(filePath)));
                        return new Tuple<bool, List<string>>(false, importErrors);
                    }

                    // Abort if The CSV file is empty or only contains a header row
                    if (parsedFile.Count < 2)
                    {
                        importErrors.Add(String.Format("The file '{0}' does not contain any data.", Path.GetFileName(filePath)));
                        return new Tuple<bool, List<string>>(false, importErrors);
                    }

                    //
                    // Part 2. Abort if required CSV column are missing or there is a problem matching the CSV file headers against the DB headers.
                    //

                    // Get the dataLabels from the database and from the headers in the CSV files (and remove any empty trailing headers from the CSV file list)
                    List<string> dataLabelsFromDB = fileDatabase.GetDataLabelsExceptIDInSpreadsheetOrder();
                    List<string> dataLabelsFromCSV = parsedFile[0].Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
                    List<string> dataLabelsInHeaderButNotFileDatabase = dataLabelsFromCSV.Except(dataLabelsFromDB).ToList();

                    // Abort if the File and Relative Path columns are missing from the CSV file 
                    // While the CSV data labels can be a subset of the DB data labels,
                    // the File and Relative Path are a required CSV datalabel, as we can't match the DB data row without it.
                    if (dataLabelsFromCSV.Contains(Constant.DatabaseColumn.File) == false || dataLabelsFromCSV.Contains(Constant.DatabaseColumn.RelativePath) == false)
                    {
                        importErrors.Add(String.Format("Columns necessary to match each CSV row with an image or video file are missing in your CSV file.", Constant.DatabaseColumn.File));
                        if (dataLabelsFromCSV.Contains(Constant.DatabaseColumn.File) == false)
                        {
                            importErrors.Add(String.Format("-the '{0}' column containing the file names.", Constant.DatabaseColumn.File));
                        }
                        if (dataLabelsFromCSV.Contains(Constant.DatabaseColumn.RelativePath) == false)
                        {
                            importErrors.Add(String.Format("- the '{0}' column containing matching relative paths to the subfolders containing each file." , Constant.DatabaseColumn.RelativePath));
                            importErrors.Add("  (If all your files are in the root  folder, you still need the RelativePath column, albeit with empty values");
                        }
                        abort = true;
                    }

                    // Abort if a column header in the CSV file does not exist in the template
                    // Note: could do this as a warning rather than as an abort, but...
                    foreach (string dataLabel in dataLabelsInHeaderButNotFileDatabase)
                    {
                        importErrors.Add(String.Format("The column heading '{0}' in the CSV file does not match any DataLabel in the template.", dataLabel));
                        abort = true;
                    }

                    if (abort)
                    {
                        // We failed. abort.
                        return new Tuple<bool, List<string>>(false, importErrors);
                    }

                    //
                    // Part 3. Get all data rows, and validate each column's data against its type.
                    // Abort if the type does not match
                    // 

                    // Create a List of all data rows, where each row is a dictionary containing the header and that row's valued for the header
                    List<Dictionary<string, string>> rowDictionaryList = new List<Dictionary<string, string>>();
                    int rowNumber = 0;
                    int numberOfHeaders = dataLabelsFromCSV.Count;
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
                            rowDictionary.Add(dataLabelsFromCSV[i], parsedRow[i]);
                        }
                        rowDictionaryList.Add(rowDictionary);
                    }

                    // For each column in the CSV file,
                    // - get its type from the template
                    // - for particular types, validate the data in the column against that type
                    // Validation ignored for:
                    // - Note, as it can hold any data
                    // - Folder, as it is just a string (although we could check to see if it has invalid characters, the folder name is not used to do anything important)
                    // - File, RelativePath, as that data row would be ignored if it does not create a valid path
                    // Although dates are currently ignored, we could do selected DateTime formats 
                    // - Date, Time format: ignore, as Excel will change it if it is written out again, Note that UtcOffset is not exported in this formaat
                    // - DateTime, UtcOffset:
                    //   - YYYY-MM-DDTHH:MM:SS (includes T separator, incorporates UTCoffset in its time): Check, as not altered by Excel, no UTC offset
                    //   - YYYY-MM-DD HH:MM:SS (excludes T separator, incorporates UTCoffset in its time): Altered by Excel (e.g., leading 0s removed), no UTC offset
                    //   - YYYY-MM-DDTHH:MM:SSZ+HH:MM (excludes T separator): (UTC format with offset that must be added in): Check, as not altered by Excel, no UTC offset
                    //   - UtcOffset - it appears I never include that. Check this...
                    foreach (string csvHeader in dataLabelsFromCSV) 
                    {
                        ControlRow controlRow = fileDatabase.GetControlFromTemplateTable(csvHeader);

                        // We don't need to worry about File-related or Date-related controls as they are not updated
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
                                        if (!Boolean.TryParse(rowDict[csvHeader], out _))
                                        {
                                            // Flag values must be true or false, but its not. So raise an error
                                            importErrors.Add(String.Format("Error in row {1}. {0} values must be true or false, but is '{2}'", csvHeader, rowNumber, rowDict[csvHeader]));
                                            abort = true;
                                        }
                                        break;
                                    case Constant.Control.Counter:
                                        if (!String.IsNullOrWhiteSpace(rowDict[csvHeader]) && !Int32.TryParse(rowDict[csvHeader], out _))
                                        {
                                            // Counters must be integers / blanks 
                                            importErrors.Add(String.Format("Error in row {1}. {0} values must be blank or a number, but is '{2}'", csvHeader, rowNumber, rowDict[csvHeader]));
                                            abort = true;
                                        }
                                        break;
                                    case Constant.Control.FixedChoice:
                                    case Constant.DatabaseColumn.ImageQuality:
                                        if (controlRow.List.Contains(rowDict[csvHeader]) == false)
                                        {
                                            // Fixed Choices must be in the Choice List
                                            importErrors.Add(String.Format("Error in row {1}. {0} values must be in the template's choice list, but '{2}' isn't in it.", csvHeader, rowNumber, rowDict[csvHeader]));
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
                        //Abort, as some of the data values do not match the type. 
                        return new Tuple<bool, List<string>>(false, importErrors);
                    }


                    //
                    // Part 4. Check and manage duplicates
                    // 
                    // Get a list of duplicates in the database, i.e. rows with both the Same relativePath and File
                    List<string> databaseDuplicates = fileDatabase.GetDistinctRelativePathFileCombinationsDuplicates();

                    // Sort the rowDictionaryList so that duplicates in the CSV file (with the same relative path / File name) are in order, one after the other.
                    List<Dictionary<string, string>> sortedRowDictionaryList = rowDictionaryList.OrderBy(dict => dict["RelativePath"]).ThenBy(dict => dict["File"]).ToList();
                    int sortedRowDictionaryListCount = sortedRowDictionaryList.Count();
                    // Create the data structure for the query

                    List<ColumnTuplesWithWhere> imagesToUpdate = new List<ColumnTuplesWithWhere>();

                    // Begin Handle duplicates
                    int nextRowIndex = 0;
                    string currentPath = String.Empty;   // the path of the current row 
                    string examinedPath = String.Empty;  // the path of a surrounding row currently being examined to see if its a duplicate
                    string duplicatePath = String.Empty; // a duplicate was identified, and this holds the duplicate path
                    List<Dictionary<string, string>> duplicatesDictionaryList = new List<Dictionary<string, string>>();
                    foreach (Dictionary<string, string> rowDict in sortedRowDictionaryList)
                    {
                        nextRowIndex++;

                        // Duplicates are special cases, where we have to update each set of duplicates separately as a chunk.
                        // To begin, check if its a duplicate, which occurs if the path (RelativePath/File) is identical
                        currentPath = Path.Combine(rowDict[Constant.DatabaseColumn.RelativePath], rowDict[Constant.DatabaseColumn.File]);

                        if (currentPath == duplicatePath)
                        {
                            // we are in the middle of a sequence, and this record has the same path as the previously identified duplicate.
                            // Thus the current record has to be a duplicate.
                            // Add it to the list.
                            duplicatesDictionaryList.Add(rowDict);

                            // A check if we are at the end of the CSV file - this catches the condition where the very last entry in the sorted csv file is a duplicate
                            if (nextRowIndex >= sortedRowDictionaryListCount)
                            {
                                string error = UpdateDuplicatesInDatabase(fileDatabase, duplicatesDictionaryList, Path.GetDirectoryName(duplicatePath), Path.GetFileName(duplicatePath));
                                if (false == String.IsNullOrEmpty(error))
                                {
                                    importErrors.Add(error);
                                }
                                duplicatesDictionaryList.Clear();
                            }
                            continue;
                        }
                        else
                        {
                            // Check if we are at the end of a duplicate sequence
                            if (duplicatesDictionaryList.Count > 0)
                            {
                                // This entry marks the end of a sequence as the paths aren't equal but we have duplicates. Process the prior sequence
                                string error = UpdateDuplicatesInDatabase(fileDatabase, duplicatesDictionaryList, Path.GetDirectoryName(duplicatePath), Path.GetFileName(duplicatePath));
                                if (false == String.IsNullOrEmpty(error))
                                {
                                    importErrors.Add(error);
                                }
                                duplicatesDictionaryList.Clear();
                            }

                            // We are either not in a sequence, or we completed the sequence. So we need to manage the current entry.
                            if (nextRowIndex < sortedRowDictionaryListCount)
                            {
                                // We aren't currently in a sequence. Determine if the current entry is a singleton or the first duplicate in a sequence by checking its path against the next record.
                                // If it is a duplicate, add it to the list.
                                Dictionary<string, string> nextRow = sortedRowDictionaryList[nextRowIndex];
                                examinedPath = Path.Combine(nextRow[Constant.DatabaseColumn.RelativePath], nextRow[Constant.DatabaseColumn.File]);
                                if (examinedPath == currentPath)
                                {
                                    // Yup, its the beginning of a sequence.
                                    duplicatePath = currentPath;
                                    duplicatesDictionaryList.Clear();
                                    duplicatesDictionaryList.Add(rowDict);
                                    continue;
                                }
                                else
                                {
                                    // It must be singleton
                                    duplicatePath = String.Empty;
                                    if (databaseDuplicates.Contains(currentPath))
                                    {
                                        // But, if the database contains a duplicate with the same relativePath/File, then we want to update just the first database duplicate, rather than update all those
                                        // database duplicates with the same value (if we let it fall thorugh
                                        duplicatesDictionaryList.Add(rowDict);
                                        string error = UpdateDuplicatesInDatabase(fileDatabase, duplicatesDictionaryList, Path.GetDirectoryName(currentPath), Path.GetFileName(currentPath));
                                        if (false == String.IsNullOrEmpty(error))
                                        {
                                            importErrors.Add(error);
                                        }
                                        duplicatesDictionaryList.Clear();
                                        continue;
                                    }
                                }
                            }
                        }
                        // END Handle duplicates
                       
                        // Process each non-duplicate row
                        // Note that we never update:
                        // - Path-related fields (File, RelativePath, Folder)
                        // - Date and Time-related fields (DateTime, Date, Time, UtcOffset
                        ColumnTuplesWithWhere imageToUpdate = new ColumnTuplesWithWhere();
                        foreach (string header in rowDict.Keys)
                        {
                            ControlRow controlRow = fileDatabase.GetControlFromTemplateTable(header);
                            // process each column but only if its off the specific type
                            if (controlRow.Type == Constant.Control.Note || 
                                controlRow.Type == Constant.Control.Flag ||
                                controlRow.Type == Constant.DatabaseColumn.DeleteFlag ||
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

                         // Write current batch of updates to database. Note that we Update the database every number of rows as specified in bulkFilesToHandle.
                         // We should probably put in a cancellation token somewhere around here...
                        if (imagesToUpdate.Count >= bulkFilesToHandle)
                        {
                            processedFilesCount += bulkFilesToHandle;
                            progress.Report(new ProgressBarArguments(Convert.ToInt32(((double)processedFilesCount)/ sortedRowDictionaryListCount * 100.0), String.Format("Processing {0}/{1} files. Please wait...", processedFilesCount, sortedRowDictionaryListCount), false, false));
                            fileDatabase.UpdateFiles(imagesToUpdate);
                            imagesToUpdate.Clear();
                        }
                    }
                    // perform any remaining updates
                    fileDatabase.UpdateFiles(imagesToUpdate);
                    return new Tuple<bool, List<string>>(true, importErrors);
                }).ConfigureAwait(true);
        }

        // Given a list of duplicates and their common relative path, update the corresponding duplicates in the database
        // We do this by getting the IDs of duplicates in the database, where we update each database by ID to a duplicate.
        // If there is a mismatch in the number of duplicates in the database vs. in the CSV file, we just update whatever does match.
        private static string UpdateDuplicatesInDatabase(FileDatabase fileDatabase, List<Dictionary<string, string>> duplicatesDictionaryList, string relativePath, string file)
        {
            List<ColumnTuplesWithWhere> imagesToUpdate = new List<ColumnTuplesWithWhere>();
            string errorMessage = String.Empty;

            // Find THE IDs of ImageRows with those RelativePath / File values

            List<long> duplicateIDS = fileDatabase.SelectFilesByRelativePathAndFileName(relativePath, file);

            if (duplicateIDS.Count != duplicatesDictionaryList.Count)
            {
                string dbEntry = duplicateIDS.Count == 1 ? "entry" : "entries";
                string csvEntry = duplicatesDictionaryList.Count == 1 ? "entry" : "entries";
                errorMessage = String.Format("duplicate entry mismatch for {0}: {1} database {2} vs. {3} CSV {4}.", Path.Combine(relativePath, file), duplicateIDS.Count, dbEntry, duplicatesDictionaryList.Count, csvEntry);
            }

            int idIndex = 0;
            foreach (Dictionary<string, string> rowDict in duplicatesDictionaryList)
            {
                if (idIndex >= duplicateIDS.Count)
                {
                    break;
                }

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
                    imageToUpdate.SetWhere(duplicateIDS[idIndex]);
                    imagesToUpdate.Add(imageToUpdate);
                }
                idIndex++;
            }
            if (imagesToUpdate.Count > 0)
            {
                fileDatabase.UpdateFiles(imagesToUpdate);
            }
            return errorMessage;
        }
        #endregion

        #region Private Method - Update Progress Bar
        static private void UpdateProgressBar(BusyCancelIndicator busyCancelIndicator, int percent, string message, bool isCancelEnabled, bool isIndeterminate)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // Code to run on the GUI thread.
                // Check the arguments for null 
                ThrowIf.IsNullArgument(busyCancelIndicator, nameof(busyCancelIndicator));

                // Set it as a progressive or indeterminate bar
                busyCancelIndicator.IsIndeterminate = isIndeterminate;

                // Set the progress bar position (only visible if determinate)
                busyCancelIndicator.Percent = percent;

                // Update the text message
                busyCancelIndicator.Message = message;

                // Update the cancel button to reflect the cancelEnabled argument
                busyCancelIndicator.CancelButtonIsEnabled = isCancelEnabled;
                busyCancelIndicator.CancelButtonText = isCancelEnabled ? "Cancel" : "Processing CSV file...";
            });
        }
        #endregion

        #region Private Methods - Used by above
        // Given a string representing a comma-separated row of values, add a value to it.
        // If special characters are in the string,  escape the string as needed
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

        // Parse the rows in a CSV file and return it as a list   of lines, each line being a list of values
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
        #endregion
    }
}
