//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace Timelapse.DEPRACATED
//{
// This code was in FileDatabase. It may still be useful sometimes in the future... but likely not
//class DepracatedFileDatabase
//{
#region  GetDistinctValuesInFileDataBaseTableColumn
// Return all distinct values from a column in the file database
// We used to use this for autocomplete, but its now depracated as 
// we scan the file table instead. However, this is a bit of a limitation, as it means autocomplete
// only works on the Selected File row vs. every entry.
//public List<string> GetDistinctValuesInFileDataBaseTableColumn(string dataLabel)
//{
//    List<string> distinctValues = new List<string>();
//    foreach (object value in this.Database.GetDistinctValuesInColumn(Constant.DBTables.FileData, dataLabel))
//    {
//        distinctValues.Add(value.ToString());
//    }
//    return distinctValues;
//}
#endregion

#region GetControlDefaultValue
//public string GetControlDefaultValue(string dataLabel)
//{
//    long id = this.GetControlIDFromTemplateTable(dataLabel);
//    ControlRow control = this.Controls.Find(id);
//    return control.DefaultValue;
//}
#endregion

#region FindFirstDisplayableImage
// Find the next displayable image after the provided row in the current image set
// If there is no next displayable image, then find the first previous image before the provided row that is dispay
//public int FindFirstDisplayableImage(int firstRowInSearch)
//{
//    for (int row = firstRowInSearch; row < this.CountAllCurrentlySelectedFiles; row++)
//    {
//        if (this.IsFileDisplayable(row))
//        {
//            return row;
//        }
//    }
//    for (int row = firstRowInSearch - 1; row >= 0; row--)
//    {
//        if (this.IsFileDisplayable(row))
//        {
//            return row;
//        }
//    }
//    return -1;
//}
#endregion

#region GetDuplicateFiles()
// SAULXXX: TEMPORARY - TO FIX DUPLICATE BUG. TO BE REMOVED IN FUTURE VERSIONS
// Get a table containing a subset of rows that have duplicate File and RelativePaths 
//public FileTable GetDuplicateFiles()
//{
//    string query = Sql.Select + " RelativePath, File, COUNT(*) " + Sql.From + Constant.DBTables.FileData;
//    query += Sql.GroupBy + " RelativePath, File HAVING COUNT(*) > 1";
//    DataTable images = this.Database.GetDataTableFromSelect(query);
//    return new FileTable(images);
//}

#endregion

#region GetOrCreateFile
/// <summary>
/// Get the row matching the specified image or create a new image.  The caller is responsible for adding newly created images the database and data table.
/// </summary>
/// <returns>true if the image is already in the database</returns>
//public bool GetOrCreateFile(FileInfo fileInfo, out ImageRow file)
//{
//    // Check the arguments for null 
//    ThrowIf.IsNullArgument(fileInfo, nameof(fileInfo));

//    // Path.GetFileName strips the last folder of the folder path,which in this case gives us the root folder..
//    string initialRootFolderName = Path.GetFileName(this.FolderPath);

//    // GetRelativePath() includes the image's file name; remove that from the relative path as it's stored separately
//    // GetDirectoryName() returns String.Empty if there's no relative path; the SQL layer treats this inconsistently, resulting in 
//    // DataRows returning with RelativePath = String.Empty even if null is passed despite setting String.Empty as a column default
//    // resulting in RelativePath = null.  As a result, String.IsNullOrEmpty() is the appropriate test for lack of a RelativePath.
//    string relativePath = NativeMethods.GetRelativePath(this.FolderPath, fileInfo.FullName);
//    relativePath = Path.GetDirectoryName(relativePath);

//    // Check if the file already exists in the database. If so, no need to recreate it.
//    // This is necessary in cases where the user is adding a folder that has previously been added
//    // where files that exists are skipped, but new files are added.
//    ColumnTuplesWithWhere fileQuery = new ColumnTuplesWithWhere();
//    fileQuery.SetWhere(initialRootFolderName, relativePath, fileInfo.Name);
//    file = this.GetFile(fileQuery.Where);

//    if (file != null)
//    {
//        return true;
//    }
//    else
//    {
//        file = this.FileTable.NewRow(fileInfo);
//        file.Folder = initialRootFolderName;
//        file.RelativePath = relativePath;
//        file.SetDateTimeOffsetFromFileInfo(this.FolderPath);
//        return false;
//    }
//}

//private ImageRow GetFile(string where)
//{
//    if (String.IsNullOrWhiteSpace(where))
//    {
//        throw new ArgumentOutOfRangeException(nameof(where));
//    }

//    string query = Sql.SelectStarFrom + Constant.DBTables.FileData + Sql.Where + where;
//    DataTable images = this.Database.GetDataTableFromSelect(query);
//    using (FileTable temporaryTable = new FileTable(images))
//    {
//        if (temporaryTable.RowCount != 1)
//        {
//            return null;
//        }
//        return temporaryTable[0];
//    }
//}
#endregion

#region TryGetPathPrefixForTruncation
// Its possible that the folder where the .tdb file is located is either:
// - the same as the root folder where the detections were done 
// - a subfolder somewhere beneath the root folder.
// If a subfolder, then the file paths for the detections will not be valid.
// The code below examines one of the existing images and compares it to every folder path in the detection file, where
// it tries to find the portion of the path that is 'above' the current folder. Because this could be ambiguous, it creates a list
// of possible folders where the sub-folder could have originally been located in, and asks the user to disambiguate between them
// For example, consider detections done on these folders:
//  A/B/C/X/Y
//  A/B/D/Y/Z
//  A/B/S/T
// If the Y/Z folder from A/B/C was moved to (say) myFolder and the .tdb located there, 
// the two possible locations would be A/B/C/Y/Z and A/B/D/Y/Z
// The code below would present those two to the user and ask the user to choose which one is the correct one.
// Later, any detecton image not matching A/B/C will be ignored, and for the ones matching A/B/C will be trimmed off the path
//private bool TryGetPathPrefixForTruncation(Detector detector, out string pathPrefixForTruncation)
//{
//    pathPrefixForTruncation = String.Empty;
//    string folderpath;
//    if (detector.images.Count <= 0)
//    {
//        // No point continuing as there are no detector images
//        return false;
//    }

//    // Get an example file for comparing its path against the detector paths
//    int fileindex = this.GetCurrentOrNextDisplayableFile(0);
//    if (fileindex < 0)
//    {
//        // No point continuing as there are no files to process
//        return false;
//    }

//    // First step. Get all the unique folder paths from the detector images
//    string imageFilePath = this.FileTable[fileindex].RelativePath;
//    string imageFileName = this.FileTable[fileindex].File;
//    SortedSet<string> folders = new SortedSet<string>();
//    foreach (image image in detector.images)
//    {
//        folderpath = Path.GetDirectoryName(image.file);
//        if (folderpath != String.Empty)
//        {
//            folderpath += "\\";
//        }
//        if (folders.Contains(folderpath) == false)
//        {
//            folders.Add(folderpath);
//        }
//    }

//    // Add a closing slash onto the imageFilePath to terminate any matches
//    // e.g., A/B  would also match A/Buzz, which we don't want. But A/B/ won't match that.
//    if (imageFilePath != String.Empty)
//    {
//        imageFilePath += "\\";
//    }

//    // Second Step. For all folder paths in the detections, find the minimum prefix that matches the sampleimage file path 
//    // and create a 
//    int shortestIndex = int.MaxValue;
//    int currentIndex;
//    string highestFoldermatch = String.Empty;
//    foreach (string folder in folders)
//    {
//        currentIndex = folder.IndexOf(imageFilePath);
//        if ((currentIndex < shortestIndex) && (currentIndex >= 0))
//        {
//            shortestIndex = folder.IndexOf(imageFilePath);
//            highestFoldermatch = folder;
//        }
//    }
//    currentIndex = highestFoldermatch.IndexOf(imageFilePath);
//    pathPrefixForTruncation = highestFoldermatch.Substring(0, currentIndex);
//    List<string> candidateFolders = new List<string>();
//    foreach (string folder in folders)
//    {
//        if (folder.IndexOf(imageFilePath) != -1)
//        {
//            candidateFolders.Add(folder);
//        }
//    }

//    // Third step. If there is more than one candidate folder that may have contained the image set, 
//    // ask the user to disambiguate which one it is.
//    if (candidateFolders.Count > 1)
//    {
//        ChooseDetectorFilePath chooseDetectorFilePath = new ChooseDetectorFilePath(candidateFolders, pathPrefixForTruncation, Path.Combine(imageFilePath, imageFileName), GlobalReferences.MainWindow);
//        if (chooseDetectorFilePath.ShowDialog() == true)
//        {
//            currentIndex = chooseDetectorFilePath.SelectedFolder.IndexOf(imageFilePath);
//            pathPrefixForTruncation = chooseDetectorFilePath.SelectedFolder.Substring(0, currentIndex).Replace('\\', '/');
//        }
//        else
//        {
//            // The user has aborted detections, likely because they didn't know which folder to choose
//            return false;
//        }
//    }
//    return true;
//}
#endregion

#region AddFilesFromListDictonary
// Unused for now. However, modify this to allow file data to be added to the database (e.g., from a CSV file)
//public void AddFilesFromListDictonary(List<Dictionary<string,string>> rowList)
//{
//    int rowNumber = 0;
//    StringBuilder queryColumns = new StringBuilder(Sql.InsertInto + Constant.DBTables.FileData + Sql.OpenParenthesis); // INSERT INTO DataTable (

//    if (rowList.Count == 0)
//    {
//        return;
//    }
//    List<string> columnNames = new List<string>(rowList[0].Keys);

//    // Create a comma-separated lists of column names
//    // e.g., ... File, RelativePath, Folder, DateTime, ..., 
//    foreach (string columnName in columnNames)
//    {
//        queryColumns.Append(columnName);
//        queryColumns.Append(Sql.Comma);
//    }

//    queryColumns.Remove(queryColumns.Length - 2, 2); // Remove trailing ", "
//    queryColumns.Append(Sql.CloseParenthesis + Sql.Values);

//    // We should now have a partial SQL expression in the form of: INSERT INTO DataTable ( File, RelativePath, Folder, DateTime, ... )  VALUES 
//    // Create a dataline from each of the image properties, add it to a list of data lines, then do a multiple insert of the list of datalines to the database
//    // We limit the datalines to RowsPerInsert
//    for (int imageIndex = 0; imageIndex < rowList.Count; imageIndex += Constant.DatabaseValues.RowsPerInsert)
//    {
//        // PERFORMANCE: Reimplement Markers as a foreign key, as many rows will be empty. However, this will break backwards/forwards compatability
//        List<List<ColumnTuple>> markerRows = new List<List<ColumnTuple>>();

//        string command;
//        StringBuilder queryValues = new StringBuilder();

//        // This loop creates a dataline containing this image's property values, e.g., ( 'IMG_1.JPG', 'relpath', 'folderfoo', ...) ,  
//        for (int insertIndex = imageIndex; (insertIndex < (imageIndex + Constant.DatabaseValues.RowsPerInsert)) && (insertIndex < rowList.Count); insertIndex++)
//        {
//            queryValues.Append(Sql.OpenParenthesis);


//            // Get this row;s datalabels and values
//            Dictionary<string, string> fileProperties = rowList[insertIndex];
//            List<ColumnTuple> markerRow = new List<ColumnTuple>();

//            foreach (string columnName in columnNames)
//            {
//                // Should test the column name to make sure it exists in the template
//                if (this.FileTable.ColumnNames.Contains(columnName) == false)
//                {
//                    System.Diagnostics.Debug.Print(String.Format("CSV column header {0} not found in the template's datalabels", columnName));
//                }

//                // Fill up each column in order
//                queryValues.Append($"{Sql.Quote(fileProperties[columnName])}{Sql.Comma}");

//                string controlType = this.FileTableColumnsByDataLabel[columnName].ControlType;

//                switch (controlType)
//                {
//                    //case Constant.DatabaseColumn.File:
//                    //    queryValues.Append($"{Sql.Quote(fileProperties[Constant.DatabaseColumn.File])}{Sql.Comma}");
//                    //    break;

//                    //case Constant.DatabaseColumn.RelativePath:
//                    //    queryValues.Append($"{Sql.Quote(fileProperties[Constant.DatabaseColumn.RelativePath])}{Sql.Comma}");
//                    //    break;

//                    //case Constant.DatabaseColumn.Folder:
//                    //    queryValues.Append($"{Sql.Quote(fileProperties[Constant.DatabaseColumn.Folder])}{Sql.Comma}");
//                    //    break;

//                    //case Constant.DatabaseColumn.Date:
//                    //    queryValues.Append($"{Sql.Quote(fileProperties[Constant.DatabaseColumn.Date])}{Sql.Comma}");
//                    //    break;

//                    //case Constant.DatabaseColumn.DateTime:
//                    //    queryValues.Append($"{Sql.Quote(fileProperties[Constant.DatabaseColumn.DateTime])}{Sql.Comma}");
//                    //    break;

//                    //case Constant.DatabaseColumn.UtcOffset:
//                    //    queryValues.Append($"{Sql.Quote(fileProperties[Constant.DatabaseColumn.UtcOffset])}{Sql.Comma}");
//                    //    break;

//                    //case Constant.DatabaseColumn.Time:
//                    //    queryValues.Append($"{Sql.Quote(fileProperties[Constant.DatabaseColumn.Time])}{Sql.Comma}");
//                    //    break;

//                    //case Constant.DatabaseColumn.ImageQuality:
//                    //    queryValues.Append($"{Sql.Quote(fileProperties[Constant.DatabaseColumn.ImageQuality])}{Sql.Comma}");
//                    //    break;

//                    //case Constant.DatabaseColumn.DeleteFlag:
//                    //    // Default as specified in the template file, which should be "false"
//                    //    queryValues.Append($"{Sql.Quote(fileProperties[Constant.DatabaseColumn.DeleteFlag])}{Sql.Comma}");
//                    //    break;

//                    //// Find and then add the customizable types, populating it with their default values.
//                    //case Constant.Control.Note:
//                    //case Constant.Control.FixedChoice:
//                    //case Constant.Control.Flag:
//                    //    // Now initialize notes, flags, and fixed choices to the defaults
//                    //    queryValues.Append($"{Sql.Quote(fileProperties[columnName])}{Sql.Comma}");
//                    //    break;

//                    case Constant.Control.Counter:
//                        // queryValues.Append($"{Sql.Quote(fileProperties[columnName])}{Sql.Comma}");
//                        markerRow.Add(new ColumnTuple(columnName, String.Empty));
//                        break;

//                    default:
//                        // System.Diagnostics.Debug.Print(String.Format("Unhandled control type '{0}' in AddImages.", controlType));
//                        break;
//                }
//            }

//            // Remove trailing commam then add " ) ,"
//            queryValues.Remove(queryValues.Length - 2, 2); // Remove ", "
//            queryValues.Append(Sql.CloseParenthesis + Sql.Comma);

//            // The dataline should now be added to the string list of data lines, so go to the next image
//            ++rowNumber;

//            if (markerRow.Count > 0)
//            {
//                markerRows.Add(markerRow);
//            }
//        }

//        // Remove trailing comma.
//        queryValues.Remove(queryValues.Length - 2, 2); // Remove ", "

//        // Create the entire SQL command (limited to RowsPerInsert datalines)
//        command = queryColumns.ToString() + queryValues.ToString();

//        this.CreateBackupIfNeeded();
//        this.Database.ExecuteOneNonQueryCommand(command);
//        this.InsertRows(Constant.DBTables.Markers, markerRows);
//    }

//    // Load / refresh the marker table from the database to keep it in sync - Doing so here will make sure that there is one row for each image.
//    this.MarkersLoadRowsFromDatabase();
//}
 #endregion
//}
//}
