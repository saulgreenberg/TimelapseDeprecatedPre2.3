using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Timelapse.Database;
using Timelapse.Dialog;
using Timelapse.Util;
namespace Timelapse
{
    /// <summary>
    /// Methods to check for various missing folders and to ask the user to correct them if they are missing.
    /// These checks are requested during image set loading (see TimelapseImageSetLoading)
    /// </summary>
    public partial class TimelapseWindow : Window, IDisposable
    {
        // Get the root folder name from the database, and check to see if its the same as the actual root folder.
        // If not, ask the user if he/she wants to update the database.
        public void CheckAndCorrectRootFolder(FileDatabase fileDatabase)
        {
            // Check the arguments for null 
            if (fileDatabase == null)
            {
                // this should not happen
                // System.Diagnostics.Debug.Print("The fielDatabase was null and it shouldn't be");
                TracePrint.PrintStackTrace(1);
                // No-op
                return;
            }
            List<object> allRootFolderPaths = fileDatabase.GetDistinctValuesInColumn(Constant.DBTables.FileData, Constant.DatabaseColumn.Folder);
            if (allRootFolderPaths.Count < 1)
            {
                // System.Diagnostics.Debug.Print("Checking the root folder name in the database, but no entries were found. Perhaps the database is empty?");
                return;
            }

            // retrieve and compare the db and actual root folder path names. While there really should be only one entry in the allRootFolderPaths,
            // we still do a check in case there is more than one. If even one entry doesn't match, we use that entry to ask the user if he/she
            // wants to update the root folder to match the actual location of the root folder containing the template, data and image files.
            string actualRootFolderName = fileDatabase.FolderPath.Split(Path.DirectorySeparatorChar).Last();
            foreach (string databaseRootFolderName in allRootFolderPaths)
            {
                if (databaseRootFolderName.Equals(actualRootFolderName))
                {
                    continue;
                }
                else
                {
                    // We have at least one entry where there is a mismatch between the actual root folder and the stored root folder
                    // Consequently, ask the user if he/she wants to update the db entry 
                    Dialog.UpdateRootFolder renameRootFolderDialog;
                    renameRootFolderDialog = new Dialog.UpdateRootFolder(this, databaseRootFolderName, actualRootFolderName);
                    bool? result = renameRootFolderDialog.ShowDialog();
                    if (result == true)
                    {
                        ColumnTuple columnToUpdate = new ColumnTuple(Constant.DatabaseColumn.Folder, actualRootFolderName);
                        fileDatabase.UpdateFiles(columnToUpdate);
                    }
                    return;
                }
            }
        }

        // Get all the distinct relative folder paths and check to see if the folder exists.
        // If not, try to find the best matching folder for each of them
        // Then ask the user to verify and - if needed - to try to locate each missing folder.
        public static bool? CheckAndCorrectForMissingFolders(Window owner, FileDatabase fileDatabase)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(fileDatabase, nameof(fileDatabase));

            // Get a list of missing folders, if any.
            List<string> missingRelativePaths = GetMissingFolders(fileDatabase);
            if (missingRelativePaths?.Count == 0)
            {
                // No folders are missing, so nothing to do.
                return false;
            }

            // We want to show the normal cursor when we display dialog boxes, so save the current cursor so we can store it.
            Cursor cursor = Mouse.OverrideCursor;

            // We know that at least one or more folders are missing.
            // For each missing folder path, try to find one (and only one) folder with the same name under the root folder.
            // If there are more than one, just return the first one. 
            Dictionary<string, string> matchingFolderNames = Util.FilesFolders.TryFindMissingFolders(fileDatabase.FolderPath, missingRelativePaths);
            bool? result;
            if (matchingFolderNames != null)
            {
                Mouse.OverrideCursor = null;
                // Present a dialog box that shows the possible match for each folder.
                // The user can then confirm that they are correct, or request manual locaton of those folders, or cancel altogether.
                MissingFoldersLocateAllFolders dialog = new MissingFoldersLocateAllFolders(owner, fileDatabase.FolderPath, matchingFolderNames);
                result = dialog.ShowDialog();
                Mouse.OverrideCursor = cursor;
                if (result == true)
                {
                    // Get the updated folder locations
                    matchingFolderNames = dialog.FinalFolderLocations;
                    // User accepted the folder matches. Update the database
                    foreach (string key in matchingFolderNames.Keys)
                    {
                        ColumnTuple columnToUpdate = new ColumnTuple(Constant.DatabaseColumn.RelativePath, matchingFolderNames[key]);
                        ColumnTuplesWithWhere columnToUpdateWithWhere = new ColumnTuplesWithWhere(columnToUpdate, key);
                        fileDatabase.UpdateFiles(columnToUpdateWithWhere);
                    }
                    return true;
                }
            }
            return null;
        }

        // Return a list of missing folders. This is done by by getting all relative paths and seeing if each folder actually exists.
        private static List<string> GetMissingFolders(FileDatabase fileDatabase)
        {
            List<object> allRelativePaths = fileDatabase.GetDistinctValuesInColumn(Constant.DBTables.FileData, Constant.DatabaseColumn.RelativePath);
            List<string> missingRelativePaths = new List<string>();
            foreach (string relativePath in allRelativePaths)
            {
                string path = Path.Combine(fileDatabase.FolderPath, relativePath);
                if (!Directory.Exists(path))
                {
                    missingRelativePaths.Add(relativePath);
                }
            }
            return missingRelativePaths;
        }
    }
}
