using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Timelapse.Controls;
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
        public static bool ExportToCsv(FileDatabase database, string filePath, bool excludeDateTimeAndUTCOffset)
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
                    int countAllCurrentlySelectedFiles = database.CountAllCurrentlySelectedFiles;
                    for (int row = 0; row < countAllCurrentlySelectedFiles; row++)
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
                    progress.Report(new ProgressBarArguments(0, "Reading the CSV file. Please wait", false, true));
                    List<List<string>> parsedFile = ReadAndParseCSVFile(filePath);
                    if (parsedFile == null)
                    {
                        // Could not open the file
                        importErrors.Add(String.Format("The file '{0}' could not be read. To check: Is opened by another application? Is it a valid CSV file?", Path.GetFileName(filePath)));
                        return new Tuple<bool, List<string>>(false, importErrors);
                    }

                    if (parsedFile.Count < 2)
                    {
                        // The CSV file is empty or only contains a header row
                        importErrors.Add(String.Format("The file '{0}' does not contain any data.", Path.GetFileName(filePath)));
                        return new Tuple<bool, List<string>>(false, importErrors);
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
                        return new Tuple<bool, List<string>>(false, importErrors);
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
                        return new Tuple<bool, List<string>>(false, importErrors);
                    }

                    // Get a list of duplicates in the database, i.e. rows with both the Same relativePath and File
                    List<string> databaseDuplicates = fileDatabase.GetDistinctRelativePathFileCombinationsDuplicates();

                    // Sort the rowDictionaryList so that duplicates in the CSV file (with the same relative path / File name) are in order, one after the other.
                    IEnumerable<Dictionary<string, string>> sortedRowDictionaryList = rowDictionaryList.OrderBy(dict => dict["RelativePath"]).ThenBy(dict => dict["File"]);

                    // Create the data structure for the query

                    List<ColumnTuplesWithWhere> imagesToUpdate = new List<ColumnTuplesWithWhere>();

                    int nextRowIndex = 0;
                    string currentPath = String.Empty;
                    string examinedPath = String.Empty;
                    string duplicatePath = String.Empty;
                    List<Dictionary<string, string>> duplicatesDictionaryList = new List<Dictionary<string, string>>();
                    foreach (Dictionary<string, string> rowDict in sortedRowDictionaryList)
                    {
                        nextRowIndex++;
                        // BEGIN CHECK DUPLICATE

                        // Check if its a duplicate by looking at the paths
                        currentPath = Path.Combine(rowDict[Constant.DatabaseColumn.RelativePath], rowDict[Constant.DatabaseColumn.File]);
                        System.Diagnostics.Debug.Print(currentPath);
                        if (currentPath == duplicatePath)
                        {
                            // we are in the middle of a sequence, so the current record has to be a duplicate.
                            // Add it to the list.
                            duplicatesDictionaryList.Add(rowDict);

                            // Check if we are at the end of a sequence - this catches the condition where the very last entry in the sorted csv file is a duplicate
                            if (nextRowIndex >= sortedRowDictionaryList.Count())
                            {
                                // This entry marks the end of a sequence as the paths aren't equal but we have duplicates. Process the prior sequence
                                InsertDuplicates(fileDatabase, duplicatesDictionaryList, Path.GetDirectoryName(duplicatePath), Path.GetFileName(duplicatePath));
                                duplicatesDictionaryList.Clear();
                            }
                            continue;
                        }
                        else
                        {
                            // Check if we are at the end of a sequence
                            if (duplicatesDictionaryList.Count > 0)
                            {
                                // This entry marks the end of a sequence as the paths aren't equal but we have duplicates. Process the prior sequence
                                InsertDuplicates(fileDatabase, duplicatesDictionaryList, Path.GetDirectoryName(duplicatePath), Path.GetFileName(duplicatePath));
                                duplicatesDictionaryList.Clear();
                            }

                            // We are either not in a sequence, or we completed the sequence. So we need to manage the current entry.
                            if (nextRowIndex < sortedRowDictionaryList.Count())
                            {
                                // We aren't currently in a sequence. Determine if the current entry is a singleton or the first duplicate in a sequence by checking its path against the next record.
                                // If it is a duplicate, add it to the list.
                                Dictionary<string, string> nextRow = sortedRowDictionaryList.ElementAt(nextRowIndex);
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
                                        InsertDuplicates(fileDatabase, duplicatesDictionaryList, Path.GetDirectoryName(currentPath), Path.GetFileName(currentPath));
                                        duplicatesDictionaryList.Clear();
                                        continue;
                                    }
                                }
                            }
                        }

                        //System.Diagnostics.Debug.Print("Singleton: " + currentPath);
                        // END CHECK DUPLICATE

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

                        // Write current batch of updates to database. Note that we Update the database 100 rows at a time.
                        if (imagesToUpdate.Count >= 100)
                        {
                            fileDatabase.UpdateFiles(imagesToUpdate);
                            imagesToUpdate.Clear();
                        }
                    }
                    // perform any remaining updates
                    fileDatabase.UpdateFiles(imagesToUpdate);
                    return new Tuple<bool, List<string>>(true, importErrors);
                }).ConfigureAwait(true);
        }

        private static void InsertDuplicates(FileDatabase fileDatabase, List<Dictionary<string, string>> duplicatesDictionaryList, string relativePath, string file)
        {
            List<ColumnTuplesWithWhere> imagesToUpdate = new List<ColumnTuplesWithWhere>();

            // Find THE IDs of ImageRows with those RelativePath / File values

            List<long> duplicateIDS = fileDatabase.SelectFilesByRelativePathAndFileName(relativePath, file);

            if (duplicateIDS.Count != duplicatesDictionaryList.Count)
            {
                System.Diagnostics.Debug.Print(String.Format("Mismatch {0} Database records and {1} CSV records with same {2}", duplicateIDS.Count, duplicatesDictionaryList.Count, Path.Combine(relativePath, file)));
            }

            int idIndex = 0;
            foreach (Dictionary<string, string> rowDict in duplicatesDictionaryList)
            {
                if (idIndex >= duplicateIDS.Count)
                {
                    System.Diagnostics.Debug.Print("More CSV rows than IDs:" + Path.Combine(rowDict[Constant.DatabaseColumn.RelativePath], rowDict[Constant.DatabaseColumn.File]));
                    break;
                }
                //System.Diagnostics.Debug.Print(Path.Combine(rowDict[Constant.DatabaseColumn.RelativePath], rowDict[Constant.DatabaseColumn.File]) + ":" + rowDict["Species"] + "|" + rowDict["Count"]);

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
