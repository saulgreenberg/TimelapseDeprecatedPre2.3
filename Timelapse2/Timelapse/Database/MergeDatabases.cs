using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Timelapse.Controls;
using Timelapse.Detection;
using Timelapse.Enums;
using Timelapse.Util;

namespace Timelapse.Database
{
    // This static class will try to merge various .ddb database files into a single database.
    public static class MergeDatabases
    {
        // Given 
        // - a path to a .tdb file  (specifying the root folder)
        // - a list of ddbFiles (which must be located in sub-folders relative to the root folder)
        // create a .ddb File in the root folder that merges data found in the .ddbFiles into it, in particular, the tables: 
        // - DataTable
        // - Detections 
        // If fatal errors occur in the merge, abort 
        // Return the relevant error messages in the ErrorsAndWarnings object.
        // Note: if a merged .ddb File already exists in that root folder, it will be backed up and then over-written

        public async static Task<ErrorsAndWarnings> TryMergeDatabasesAsync(string tdbFile, List<string> sourceDDBFilePaths, IProgress<ProgressBarArguments> progress)
        {
            ErrorsAndWarnings errorMessages = new ErrorsAndWarnings();
            if (sourceDDBFilePaths?.Count == 0)
            {
                errorMessages.Errors.Add("No databases (.ddb files) were found in the sub-folders, so there was nothing to merge.");
                return errorMessages;
            }

            string rootFolderPath = Path.GetDirectoryName(tdbFile);
            string destinationDDBFileName = Constant.File.MergedFileName;
            string destinationDDBFilePath = Path.Combine(rootFolderPath, destinationDDBFileName);
            string rootFolderName = rootFolderPath.Split(Path.DirectorySeparatorChar).Last();

            // Check to see if we can actually open the template. 
            // As we can't have out parameters in an async method, we return the state and the desired templateDatabase as a tuple
            // Original form: if (!(await TemplateDatabase.TryCreateOrOpenAsync(templateDatabasePath, out this.templateDatabase).ConfigureAwait(true))
            Tuple<bool, TemplateDatabase> tupleResult = await TemplateDatabase.TryCreateOrOpenAsync(tdbFile).ConfigureAwait(true);
            TemplateDatabase templateDatabase = tupleResult.Item2;
            if (!tupleResult.Item1)
            {
                // notify the user the template couldn't be loaded rather than silently doing nothing
                errorMessages.Errors.Add("Could not open the template .tdb file: " + tdbFile);
                return errorMessages;
            }

            // if the merge file exists, move it to the backup folder as we will be overwriting it.
            bool backupMade = false;
            if (File.Exists(destinationDDBFilePath))
            {
                // Backup the old merge file by moving it to the backup folder 
                // Note that we do the move instead of copy as we will be overwriting the file anyways
                backupMade = FileBackup.TryCreateBackup(destinationDDBFilePath, true);
            }

            FileDatabase fd = await FileDatabase.CreateEmptyDatabase(destinationDDBFilePath, templateDatabase).ConfigureAwait(true);
            fd.Dispose();
            fd = null;

            // Open the database
            SQLiteWrapper destinationDDB = new SQLiteWrapper(destinationDDBFilePath);

            // Get the DataLabels from the DataTable in the main database.
            // We will later check to see if they match their counterparts in each database to merge in
            List<string> mergedDDBDataLabels = destinationDDB.SchemaGetColumns(Constant.DBTables.FileData);

            for (int i = 0; i < sourceDDBFilePaths.Count; i++)
            {
                // Try to merge each database into the merged database
                await Task.Run(() =>
                {
                    // Report progress, introducing a delay to allow the UI thread to update and to make the progress bar linger on the display
                    progress.Report(new ProgressBarArguments((int)((i + 1) / (double)sourceDDBFilePaths.Count * 100.0),
                        String.Format("Merging {0}/{1} databases. Please wait...", i + 1, sourceDDBFilePaths.Count),
                        "Processing detections...",
                        false, false));
                    Thread.Sleep(250);
                    if (MergeDatabases.InsertSourceDataBaseTablesintoDestinationDatabase(destinationDDB, sourceDDBFilePaths[i], rootFolderPath, mergedDDBDataLabels) == false)
                    {
                        string trimmedPath = sourceDDBFilePaths[i].Substring(rootFolderPath.Length + 1);
                        errorMessages.Warnings.Add(String.Format("'{0}' was skipped. Its template uses different data labels", trimmedPath));
                    }
                }).ConfigureAwait(true);
            }
            // After the merged database is constructed, set the Folder column to the current root folder
            if (!String.IsNullOrEmpty(rootFolderName))
            {
                destinationDDB.ExecuteNonQuery(Sql.Update + Constant.DBTables.FileData + Sql.Set + Constant.DatabaseColumn.Folder + Sql.Equal + Utilities.QuoteForSql(rootFolderName));
            }

            // After the merged database is constructed, reset fields in the ImageSetTable to the defaults i.e., first row, selection all, 
            if (!String.IsNullOrEmpty(rootFolderName))
            {
                destinationDDB.ExecuteNonQuery(Sql.Update + Constant.DBTables.ImageSet + Sql.Set + Constant.DatabaseColumn.MostRecentFileID + Sql.Equal + "1");
                destinationDDB.ExecuteNonQuery(Sql.Update + Constant.DBTables.ImageSet + Sql.Set + Constant.DatabaseColumn.Selection + Sql.Equal + ((int)FileSelectionEnum.All).ToString());
                destinationDDB.ExecuteNonQuery(Sql.Update + Constant.DBTables.ImageSet + Sql.Set + Constant.DatabaseColumn.SortTerms + Sql.Equal + Utilities.QuoteForSql(Constant.DatabaseValues.DefaultSortTerms));
            }
            if (backupMade && (errorMessages.Errors.Any() || errorMessages.Warnings.Any()))
            {
                errorMessages.Warnings.Add(String.Format("Note: A backup of your original {0} can be found in the {1} folder", destinationDDBFileName, Constant.File.BackupFolder));
            }
            return errorMessages;
        }

        #region Private internal methods
        // Merge a .ddb file specified in the sourceDDBPath path into the destinationDDB database.
        // Also update the Relative path to reflect the new location of the sourceDDB paths as defined in the rootFolderPath
        private static bool InsertSourceDataBaseTablesintoDestinationDatabase(SQLiteWrapper destinationDDB, string SourceDDBPath, string rootFolderPath, List<string> sourceDataLabels)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(destinationDDB, nameof(destinationDDB));

            // Check to see if the datalabels in the sourceDDB matches those in the destinationDataLabels.
            // If not, generate a warning and abort the merge
            SQLiteWrapper sourceDDB = new SQLiteWrapper(SourceDDBPath);
            List<string> destinationDataLabels = sourceDDB.SchemaGetColumns(Constant.DBTables.FileData);
            if (Compare.CompareLists(sourceDataLabels, destinationDataLabels) == false)
            {
                return false;
            }

            string attachedDB = "attachedDB";
            string tempDataTable = "tempDataTable";
            string tempMarkersTable = "tempMarkersTable";
            string tempDetectionsTable = "tempDetectionsTable";
            string tempClassificationsTable = "tempClassificationsTable";

            // Determine the path prefix to add to the Relative Path i.e., the difference between the .tdb root folder and the path to the ddb file
            string pathPrefixToAdd = GetDifferenceBetweenPathAndSubPath(SourceDDBPath, rootFolderPath);

            // Calculate an ID offset (the current max Id), where we will be adding that to all Ids in the ddbFile to merge. 
            // This will guarantee that there are no duplicate primary keys 
            int offsetId = destinationDDB.GetCountFromSelect(QueryGetMax(Constant.DatabaseColumn.ID, Constant.DBTables.FileData));

            // Create the first part of the query to:
            // - Attach the ddbFile
            // - Create a temporary DataTable mirroring the one in the sourceDDB (so updates to that don't affect the original ddb)
            // - Update the DataTable with the modified Ids
            // - Update the DataTable with the path prefix
            // - Insert the DataTable  into the main db's DataTable
            // Form: ATTACH DATABASE 'sourceDDB' AS attachedDB; 
            //       CREATE TEMPORARY TABLE tempDataTable AS SELECT * FROM attachedDB.DataTable;
            //       UPDATE tempDataTable SET Id = (offsetID + tempDataTable.Id);
            //       UPDATE TempDataTable SET RelativePath =  CASE WHEN RelativePath = '' THEN ("PrefixPath" || RelativePath) ELSE ("PrefixPath\\" || RelativePath) EMD
            //       INSERT INTO DataTable SELECT * FROM tempDataTable;
            string query = QueryBeginTransaction();
            query += QueryAttachDatabaseAs(SourceDDBPath, attachedDB);
            query += QueryCreateTemporaryTableFromExistingTable(tempDataTable, attachedDB, Constant.DBTables.FileData);
            query += QueryAddOffsetToIDInTable(tempDataTable, Constant.DatabaseColumn.ID, offsetId);
            query += QueryAddPrefixToRelativePathInTable(tempDataTable, pathPrefixToAdd);
            query += QueryInsertTable2DataIntoTable1(Constant.DBTables.FileData, tempDataTable);

            // Create the second part of the query to:
            // - Create a temporary Markers Table mirroring the one in the sourceDDB (so updates to that don't affect the original ddb)
            // - Update the Markers Table with the modified Ids
            // - Insert the Markers Table  into the main db's Markers Table
            // Form: CREATE TEMPORARY TABLE tempMarkers AS SELECT * FROM attachedDB.Markers;
            //       UPDATE tempMarkers SET Id = (offsetID + tempMarkers.Id);
            //       INSERT INTO Markers SELECT * FROM tempMarkers;
            query += QueryCreateTemporaryTableFromExistingTable(tempMarkersTable, attachedDB, Constant.DBTables.Markers);
            query += QueryAddOffsetToIDInTable(tempMarkersTable, Constant.DatabaseColumn.ID, offsetId);
            query += QueryInsertTable2DataIntoTable1(Constant.DBTables.Markers, tempMarkersTable);

            // Now we need to see if we have to handle detection table updates.
            // Check to see if the destinationDDB file and the sourceDDB file each have a Detections table.
            bool sourceDetectionsExists = FileDatabase.TableExists(Constant.DBTables.Detections, SourceDDBPath);
            bool destinationDetectionsExists = destinationDDB.TableExists(Constant.DBTables.Detections);

            // If the main database doesn't have detections, but the database to merge into it does,
            // then we have to create the detection tables to the main database.
            if (destinationDetectionsExists == false && sourceDetectionsExists)
            {
                DetectionDatabases.CreateOrRecreateTablesAndColumns(destinationDDB);

                // As its the first time we see a database with detections, import the Detection Categories, Classification Categories and Info 
                // This assumes (perhaps incorrectly) that all databases the merge in have the same detection/classification categories and info.
                // FORM: INSERT INTO DetectionCategories SELECT * FROM attachedDB.DetectionCategories;
                //              INSERT INTO ClassificationCategories SELECT * FROM attachedDB.ClassifciationCategories;
                //              INSERT INTO Info SELECT * FROM attachedDB.Info;
                query += QueryInsertTableDataFromAnotherDatabase(Constant.DBTables.DetectionCategories, attachedDB);
                query += QueryInsertTableDataFromAnotherDatabase(Constant.DBTables.ClassificationCategories, attachedDB);
                query += QueryInsertTableDataFromAnotherDatabase(Constant.DBTables.Info, attachedDB);
            }

            // Create the third part of the query only if the toMergeDDB contains a detections table
            // (as otherwise we don't have to update the detection table in the main ddb.
            // - Create a temporary Detections table mirroring the one in the toMergeDDB (so updates to that don't affect the original ddb)
            // - Update the Detections Table with both the modified Ids and detectionIDs
            // - Insert the Detections Table into the main db's Detections Table
            // Form: CREATE TEMPORARY TABLE tempDetectionsTable AS SELECT * FROM attachedDB.Detections;
            //       UPDATE TempDetectionsTable SET Id = (offsetId + TempDetectionsTable.Id);
            //       UPDATE TempDetectionsTable SET DetectionID = (offsetDetectionId + TempDetectionsTable.DetectionId);
            //       INSERT INTO Detections SELECT * FROM TempDetectionsTable;"
            // The Classifications form is similar, except it used the classification-specific tables, ids, offsets, etc.
            if (sourceDetectionsExists)
            {
                // The database to merge in has detections, so the SQL query also updates the Detections table.
                // Calculate an offset (the max DetectionIDs), where we will be adding that to all detectionIds in the ddbFile to merge. 
                // However, the offeset should be 0 if there are no detections in the main DB, so we can just reusue this as is.
                // as we will be creating the detection table and then just adding to it.
                int offsetDetectionId = (destinationDetectionsExists)
                    ? destinationDDB.GetCountFromSelect(QueryGetMax(Constant.DetectionColumns.DetectionID, Constant.DBTables.Detections))
                    : 0;
                query += QueryCreateTemporaryTableFromExistingTable(tempDetectionsTable, attachedDB, Constant.DBTables.Detections);
                query += QueryAddOffsetToIDInTable(tempDetectionsTable, Constant.DatabaseColumn.ID, offsetId);
                query += QueryAddOffsetToIDInTable(tempDetectionsTable, Constant.DetectionColumns.DetectionID, offsetDetectionId);
                query += QueryInsertTable2DataIntoTable1(Constant.DBTables.Detections, tempDetectionsTable);

                // Similar to the above, we also update the classifications
                int offsetClassificationId = (destinationDetectionsExists)
                    ? destinationDDB.GetCountFromSelect(QueryGetMax(Constant.ClassificationColumns.ClassificationID, Constant.DBTables.Classifications))
                    : 0;
                query += QueryCreateTemporaryTableFromExistingTable(tempClassificationsTable, attachedDB, Constant.DBTables.Classifications);
                query += QueryAddOffsetToIDInTable(tempClassificationsTable, Constant.ClassificationColumns.ClassificationID, offsetClassificationId);
                query += QueryAddOffsetToIDInTable(tempClassificationsTable, Constant.ClassificationColumns.DetectionID, offsetDetectionId);
                query += QueryInsertTable2DataIntoTable1(Constant.DBTables.Classifications, tempClassificationsTable);
            }
            query += QueryEndTransaction();
            destinationDDB.ExecuteNonQuery(query);
            return true;
        }
        #endregion

        #region Queries: return partial queries 
        // Form: BEGIN TRANSACTION;
        private static string QueryBeginTransaction()
        {
            return Sql.BeginTransaction + Sql.Semicolon;
        }

        // Form: END TRANSACTION;
        private static string QueryEndTransaction()
        {
            return Sql.EndTransaction + Sql.Semicolon; ;
        }

        // Form: "Select Max(columnName) from tableName"
        private static string QueryGetMax(string columnName, string tableName)
        {
            return Sql.Select + Sql.Max + Sql.OpenParenthesis + columnName + Sql.CloseParenthesis + Sql.From + tableName;
        }
        // Form: ATTACH DATABASE 'databasePath' AS alias;
        private static string QueryAttachDatabaseAs(string databasePath, string alias)
        {
            return Sql.AttachDatabase + Utilities.QuoteForSql(databasePath) + Sql.As + alias + Sql.Semicolon;
        }

        // Form: CREATE TEMPORARY TABLE tempDataTable AS SELECT * FROM dataBaseName.tableName;
        private static string QueryCreateTemporaryTableFromExistingTable(string tempDataTable, string dataBaseName, string tableName)
        {
            return Sql.CreateTemporaryTable + tempDataTable + Sql.As + Sql.SelectStarFrom + dataBaseName + Sql.Dot + tableName + Sql.Semicolon;
        }

        // Form: UPDATE dataTable SET IDColumn = (offset + dataTable.Id);
        private static string QueryAddOffsetToIDInTable(string tableName, string IDColumn, int offset)
        {
            return Sql.Update + tableName + Sql.Set + IDColumn + Sql.Equal + Sql.OpenParenthesis + offset.ToString() + Sql.Plus + tableName + Sql.Dot + IDColumn + Sql.CloseParenthesis + Sql.Semicolon;
        }

        // Form: UPDATE dataTable SET IDColumn = (offset + dataTable.Id);
        private static string QuerySetFolderInTable(string tableName, string folder)
        {
            return Sql.Update + tableName + Sql.Set + Constant.DatabaseColumn.Folder + Sql.Equal + Utilities.QuoteForSql(folder) + Sql.Semicolon;
        }

        //Form:  UPDATE tableName SET RelativePath = CASE WHEN RelativePath = '' THEN ("PrefixPath" || RelativePath) ELSE ("PrefixPath\\" || RelativePath) EMD
        private static string QueryAddPrefixToRelativePathInTable(string tableName, string pathPrefixToAdd)
        {
            // A longer query, so split into three lines
            // Note that tableName must be a DataTable for this to work
            string query = Sql.Update + tableName + Sql.Set + Constant.DatabaseColumn.RelativePath + Sql.Equal + Sql.CaseWhen + Constant.DatabaseColumn.RelativePath + Sql.Equal + Utilities.QuoteForSql(String.Empty);
            query += Sql.Then + Sql.OpenParenthesis + Utilities.QuoteForSql(pathPrefixToAdd) + Sql.Concatenate + Constant.DatabaseColumn.RelativePath + Sql.CloseParenthesis;
            query += Sql.Else + Sql.OpenParenthesis + Utilities.QuoteForSql(pathPrefixToAdd + "\\") + Sql.Concatenate + Constant.DatabaseColumn.RelativePath + Sql.CloseParenthesis + " END " + Sql.Semicolon;
            return query;
        }

        //  Form: INSERT INTO tabl1 SELECT * FROM table2;
        private static string QueryInsertTable2DataIntoTable1(string table1, string table2)
        {
            return Sql.InsertInto + table1 + Sql.SelectStarFrom + table2 + Sql.Semicolon;
        }

        //  Form: INSERT INTO table SELECT * FROM dataBase.table;
        private static string QueryInsertTableDataFromAnotherDatabase(string table, string fromDatabase)
        {
            return Sql.InsertInto + table + Sql.SelectStarFrom + fromDatabase + Sql.Dot + table + Sql.Semicolon;
        }
        #endregion

        #region Private methods
        // Find the difference between two paths (ignoring the file name, if any) and return it
        // For example, given:
        // path1 =    "C:\\Users\\Owner\\Desktop\\Test sets\\MergeLarge\\1\\TimelapseData.ddb"
        // path2 =    "C:\\Users\\Owner\\Desktop\\Test sets\\MergeLarge" 
        // return     "1"
        private static string GetDifferenceBetweenPathAndSubPath(string path1, string path2)
        {
            if (path1.Length > path2.Length)
            {
                return Path.GetDirectoryName(path1).Replace(path2 + "\\", "");
            }
            else
            {
                return Path.GetDirectoryName(path2).Replace(path1 + "\\", "");
            }
        }
        #endregion
    }
}