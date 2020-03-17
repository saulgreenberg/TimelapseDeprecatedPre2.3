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
        // - a list of ddbFiles (which must be located only in sub-folders relative to the root folder)
        // create a .ddb File in the root folder that merges data found in the .ddbFiles into it, in particular, the tables: 
        // - DataTable
        // - Detections 
        // If errors occur, abort and return the relevant error messages as a list of errorMessage strings.
        // Note: if a .ddb File already exists in that root folder, it will be over-written
        // TO DO: 
        // - MAKE A NEW .DDB FILE AND RENAME IT WHEN DONE OR DELETE IT IF ERROR, MOVE OLD ONE INTO BACKUP
        // - MOVE PROGRESS TO CALLING METHOD
        // - MAYBE SEARCH FOR .ddb FILES HERE?
        // - CREATE A NEW DDB RATHER THAN COPYING ONE
        // - CHECK .DDBs ADHERE TO TEMPLATE SCHEMA
        public async static Task<List<string>> TryMergeDatabasesAsync(string tdbFile, List<string> ddbFilePaths, IProgress<ProgressBarArguments> progress)
        {
            List<string> errorMessages = new List<string>();
            if (ddbFilePaths?.Count == 0)
            {
                errorMessages.Add("No databases (.ddb files) were found in the sub-folders, so there was nothing to merge.");
                return errorMessages;
            }

            string rootFolderPath = Path.GetDirectoryName(tdbFile);
            string mergedDDBPath = Path.Combine(rootFolderPath, Constant.File.MergedFileName);
            string rootFolderName = rootFolderPath.Split(Path.DirectorySeparatorChar).Last();
            await Task.Run(() =>
            {
                // Update the progress bar
                progress.Report(new ProgressBarArguments((int)(1 / (double)ddbFilePaths.Count * 100.0), String.Format("Merging 1/{0} databases. Please wait...", ddbFilePaths.Count), "Processing detections...", false, false));
                Thread.Sleep(250); // Allows the UI thread to update plus makes the progress bar readable. While it does introduce a short delay, its negligable.

                // Create and open the main ddb file in the root folder as a copy of the first database we find. 
                // If it exists, delete it 
                // We will then merge the the ddb datatables to that file.
                if (File.Exists(mergedDDBPath))
                {
                    File.Delete(mergedDDBPath);
                }
                File.Copy(ddbFilePaths[0], mergedDDBPath);
            }).ConfigureAwait(true);

            SQLiteWrapper mergedDDB = new SQLiteWrapper(mergedDDBPath);

            // Get a sample relative path from the datatable, which we will use to figure out the prefix to add to the relative from the current root
            // Form:  SELECT RelativePath FROM DataTable LIMIT 1"))
            string pathPrefixToAdd;
            string query = Sql.Select + Constant.DatabaseColumn.RelativePath + Sql.From + Constant.DBTables.FileData + Sql.LimitOne;
            using (DataTable dt = mergedDDB.GetDataTableFromSelect(query))
            {
                if (dt.Rows.Count == 0)
                {
                    // No rows in the main database table. Abort
                    // But note that this could be possible if the first db has nothing in it, where we really should continue... But later.
                    errorMessages.Add("No rows in table!");
                    return errorMessages;
                }

                // Correct the relative path. Find the extra path to the ddb File path from the .tdb File path.
                // then append it onto the relative paths of the ddb file rows
                // SQL Form: UPDATE DataTable SET RelativePath = ("pathPrefixToAdd\" || RelativePath)
                pathPrefixToAdd = GetDifferenceBetweenPathAndSubPath(ddbFilePaths[0], rootFolderPath);
                if (!String.IsNullOrEmpty(pathPrefixToAdd))
                {
                    // DONT FORGET TO DEAL WITH ERROR MESSAGES HERE
                    mergedDDB.ExecuteNonQuery(Sql.Update + Constant.DBTables.FileData + Sql.Set + Constant.DatabaseColumn.RelativePath + Sql.Equal + Utilities.QuoteForSql(pathPrefixToAdd) + Sql.Concatenate + Constant.DatabaseColumn.RelativePath);
                }
            }

            for (int i = 1; i < ddbFilePaths.Count; i++)
            {
                await Task.Run(() =>
                {
                    string message = String.Format("Merging {0}/{1} databases. Please wait...", i + 1, ddbFilePaths.Count);
                    progress.Report(new ProgressBarArguments((int)((i + 1) / (double)ddbFilePaths.Count * 100.0), message, "Processing detections...", false, false));
                    Thread.Sleep(250); // Allows the UI thread to update plus makes the progress bar readable
                    pathPrefixToAdd = GetDifferenceBetweenPathAndSubPath(ddbFilePaths[i], rootFolderPath);
                    MergeDatabases.MergeIntoDDB(mergedDDB, ddbFilePaths[i], pathPrefixToAdd);
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
            return errorMessages;
        }

        #region Private internal methods
        // Merge a .ddb file specified in the toMergeDDB path into the mergedDDB database.
        // Also update the Relative path to reflect the location of the toMergeDDB by prefixing it with pathPrefixToAdd
        private static bool MergeIntoDDB(SQLiteWrapper mergedDDB, string toMergeDDBPath, string pathPrefixToAdd)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(mergedDDB, nameof(mergedDDB));

            string attachedDB = "attachedDB";
            string tempDataTable = "tempDataTable";
            string tempDetectionsTable = "tempDetectionsTable";

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