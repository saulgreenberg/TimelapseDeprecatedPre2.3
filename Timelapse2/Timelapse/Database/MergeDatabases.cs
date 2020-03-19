using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
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

        public async static Task<ErrorsAndWarnings> TryMergeDatabasesAsync(string tdbFile, List<string> ddbFilePaths, IProgress<ProgressBarArguments> progress)
        {
            ErrorsAndWarnings errorMessages = new ErrorsAndWarnings();
            if (ddbFilePaths?.Count == 0)
            {
                errorMessages.Errors.Add("No databases (.ddb files) were found in the sub-folders, so there was nothing to merge.");
                return errorMessages;
            }

            string rootFolderPath = Path.GetDirectoryName(tdbFile);
            string mergeFileName = Constant.File.MergedFileName;
            string mergedDDBPath = Path.Combine(rootFolderPath, mergeFileName);
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
            if (File.Exists(mergedDDBPath))
            {
                // Backup the old merge file by moving it to the backup folder 
                // Note that we do the move instead of copy as we will be overwriting the file anyways
                backupMade = FileBackup.TryCreateBackup(mergedDDBPath, true);
            }

            FileDatabase fd = await FileDatabase.CreateEmptyDatabase(mergedDDBPath, templateDatabase).ConfigureAwait(true);
            fd.Dispose();
            fd = null;

            // Open the database
            SQLiteWrapper mergedDDB = new SQLiteWrapper(mergedDDBPath);

            // Get the DataLabels from the DataTable in the main database.
            // We will later check to see if they match their counterparts in each database to merge in
            List<string> mergedDDBDataLabels = mergedDDB.SchemaGetColumns(Constant.DBTables.FileData);

            for (int i = 0; i < ddbFilePaths.Count; i++)
            {
                // Try to merge each database into the merged database
                await Task.Run(() =>
                {
                    // Report progress, introducing a delay to allow the UI thread to update and to make the progress bar linger on the display
                    progress.Report(new ProgressBarArguments((int)((i + 1) / (double)ddbFilePaths.Count * 100.0),
                        String.Format("Merging {0}/{1} databases. Please wait...", i + 1, ddbFilePaths.Count), 
                        "Processing detections...", 
                        false, false));
                    Thread.Sleep(250); 
                    if (MergeDatabases.MergeIntoDDB(mergedDDB, ddbFilePaths[i], rootFolderPath, mergedDDBDataLabels) == false)
                    {
                        string trimmedPath = ddbFilePaths[i].Substring(rootFolderPath.Length + 1);
                        errorMessages.Warnings.Add(String.Format("'{0}' was skipped. Its template uses different data labels", trimmedPath));
                    }
                }).ConfigureAwait(true);
            }
            // After the merged database is constructed, set the Folder column to the current root folder
            if (!String.IsNullOrEmpty(rootFolderName))
            {
                mergedDDB.ExecuteNonQuery(Sql.Update + Constant.DBTables.FileData + Sql.Set + Constant.DatabaseColumn.Folder + Sql.Equal + Utilities.QuoteForSql(rootFolderName));
            }

            // After the merged database is constructed, reset fields in the ImageSetTable to the defaults i.e., first row, selection all, 
            if (!String.IsNullOrEmpty(rootFolderName))
            {
                mergedDDB.ExecuteNonQuery(Sql.Update + Constant.DBTables.ImageSet + Sql.Set + Constant.DatabaseColumn.MostRecentFileID + Sql.Equal + "1");
                mergedDDB.ExecuteNonQuery(Sql.Update + Constant.DBTables.ImageSet + Sql.Set + Constant.DatabaseColumn.Selection + Sql.Equal + ((int)FileSelectionEnum.All).ToString());
                mergedDDB.ExecuteNonQuery(Sql.Update + Constant.DBTables.ImageSet + Sql.Set + Constant.DatabaseColumn.SortTerms + Sql.Equal + Utilities.QuoteForSql(Constant.DatabaseValues.DefaultSortTerms));
            }
            if (backupMade && (errorMessages.Errors.Any() || errorMessages.Warnings.Any()))
            {
                errorMessages.Warnings.Add(String.Format("Note: A backup of your original {0} can be found in the {1} folder", mergeFileName, Constant.File.BackupFolder));
            }
            return errorMessages;
        }

         #region Private internal methods
        // Merge a .ddb file specified in the toMergeDDB path into the mergedDDB database.
        // Also update the Relative path to reflect the new location of the toMergeDDB as defined in the rootFolderPath
        private static bool MergeIntoDDB(SQLiteWrapper mergedDDB, string toMergeDDBPath, string rootFolderPath, List<string> mergedDDBDataLabels)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(mergedDDB, nameof(mergedDDB));

            string attachedDB = "attachedDB";
            string tempDataTable = "tempDataTable";
            string tempDetectionsTable = "tempDetectionsTable";

            // Check to see if the datalabels in the toMergeDDB matches those in the mergedDBDataLabels.
            // If not, generate n warning and abort the merge
            SQLiteWrapper toMergeDDB = new SQLiteWrapper(toMergeDDBPath);
            List<string> toMergeDBDDataLabels = toMergeDDB.SchemaGetColumns(Constant.DBTables.FileData);
            if (Compare.CompareLists(mergedDDBDataLabels, toMergeDBDDataLabels) == false)
            {
                return false;
            }

            // Determine the path prefix to add to the Relative Path i.e., the difference between the .tdb root folder and the path to the ddb file
            string pathPrefixToAdd = GetDifferenceBetweenPathAndSubPath(toMergeDDBPath, rootFolderPath);

            // Calculate an ID offset (the current max Id), where we will be adding that to all Ids in the ddbFile to merge. 
            // This will guarantee that there are no duplicate primary keys 
            int offsetId = mergedDDB.GetCountFromSelect("Select Max(Id) from DataTable");

            // Create the first part of the query to:
            // - Attach the ddbFile
            // - Create a temporary DataTable mirroring the one in the toMergeDDB (so updates to that don't affect the original ddb)
            // - Update the DataTable with the modified Ids
            // - Update the DataTable with the path prefix
            // - Insert the DataTable  into the main db's DataTable
            // Form: ATTACH DATABASE 'toMergeDDB' AS attachedDB; 
            //       CREATE TEMPORARY TABLE tempDataTable AS SELECT * FROM attachedDB.DataTable;
            //       UPDATE tempDataTable SET Id = (offsetID + tempDataTable.Id);
            //       UPDATE TempDataTable SET RelativePath = ("PrefixPath\" || RelativePath)
            //       INSERT INTO DataTable SELECT * FROM tempDataTable;
            string query = Sql.BeginTransaction + Sql.Semicolon;
            query += Sql.AttachDatabase + Utilities.QuoteForSql(toMergeDDBPath) + Sql.As + attachedDB + Sql.Semicolon;
            query += Sql.CreateTemporaryTable + tempDataTable + Sql.As + Sql.SelectStarFrom + attachedDB + Sql.Dot + Constant.DBTables.FileData + Sql.Semicolon;
            query += Sql.Update + tempDataTable + Sql.Set + Constant.DatabaseColumn.ID + Sql.Equal + Sql.OpenParenthesis + offsetId + Sql.Plus + tempDataTable + Sql.Dot + Constant.DatabaseColumn.ID + Sql.CloseParenthesis + Sql.Semicolon;
            query += Sql.Update + tempDataTable + Sql.Set + Constant.DatabaseColumn.RelativePath + Sql.Equal + Utilities.QuoteForSql(pathPrefixToAdd) + Sql.Concatenate + Constant.DatabaseColumn.RelativePath + ";";
            query += Sql.InsertInto + Constant.DBTables.FileData + Sql.SelectStarFrom + tempDataTable + Sql.Semicolon;

            // Now we need to see if we have to handle detection table updates.
            // Check to see if the main DB file and the toMerge DB file each have a Detections table.
            bool dbToMergeDetectionsExists = MergeDatabases.ExistsDetectionsTableInDB(toMergeDDBPath);
            bool mergedDDBDetectionsExists = mergedDDB.TableExists(Constant.DBTables.Detections);

            // Create the second part of the query only if the toMergeDDB contains a detections table
            // (as otherwise we don't have to update the detection table in the main ddb.
            // - Create a temporary Detections table mirroring the one in the toMergeDDB (so updates to that don't affect the original ddb)
            // - Update the Detections Table with both the modified Ids and detectionIDs
            // - Insert the Detections Table into the main db's Detections Table
            // Form: CREATE TEMPORARY TABLE tempDetectionsTable AS SELECT * FROM attachedDB.Detections;
            //       UPDATE TempDetectionsTable SET Id = (offsetId + TempDetectionsTable.Id);
            //       UPDATE TempDetectionsTable SET DetectionID = (offsetDetectionId + TempDetectionsTable.DetectionId);
            //       INSERT INTO Detections SELECT * FROM TempDetectionsTable;"
            if (dbToMergeDetectionsExists)
            {
                // The database to merge in has detections, so the SQL query also updates the Detections table.
                // Calculate an offset (the max DetectionIDs), where we will be adding that to all detectionIds in the ddbFile to merge. 
                // However, the offeset should be 0 if there are no detections in the main DB, 
                // as we will be creating the detection table and then just adding to it.
                int offsetDetectionId = (mergedDDBDetectionsExists) ? mergedDDB.GetCountFromSelect("Select Max(detectionId) from Detections") : 0;
                query += Sql.CreateTemporaryTable + tempDetectionsTable + Sql.As + Sql.SelectStarFrom + attachedDB + Sql.Dot + Constant.DBTables.Detections + Sql.Semicolon;
                query += Sql.Update + tempDetectionsTable + Sql.Set + Constant.DatabaseColumn.ID + Sql.Equal + Sql.OpenParenthesis + offsetId + Sql.Plus + tempDetectionsTable + Sql.Dot + Constant.DatabaseColumn.ID + Sql.CloseParenthesis + Sql.Semicolon;
                query += Sql.Update + tempDetectionsTable + Sql.Set + Constant.DetectionColumns.DetectionID + Sql.Equal + Sql.OpenParenthesis + offsetDetectionId + Sql.Plus + tempDetectionsTable + Sql.Dot + Constant.DetectionColumns.DetectionID + Sql.CloseParenthesis + Sql.Semicolon;
                query += Sql.InsertInto + Constant.DBTables.Detections + Sql.SelectStarFrom + tempDetectionsTable + Sql.Semicolon;
            }
            query += Sql.EndTransaction + Sql.Semicolon;

            // If the main database doesn't have detections, but the database to merge into it does,
            // then we have to create the detection tables to the main database.
            if (mergedDDBDetectionsExists == false && dbToMergeDetectionsExists)
            {
                DetectionDatabases.CreateOrRecreateTablesAndColumns(mergedDDB);
            }
            mergedDDB.ExecuteNonQuery(query);
            return true;
        }

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

        // Check if the database specified in the path has a detections table
        private static bool ExistsDetectionsTableInDB(string dbPath)
        {   
            // Note that no error checking is done - I assume, perhaps unwisely, that the file is a valid database
            // On tedting, it does return 'false' on an invalid ddb file, so I suppose that's ok.
            SQLiteWrapper db = new SQLiteWrapper(dbPath);
            return db.TableExists("Detections");
        }
        #endregion
    }
}