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

        // Try importing a CSV file, checking its headers and values against the template's DataLabels and data types.
        // Return a list of errors if needed.
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
    }
}
