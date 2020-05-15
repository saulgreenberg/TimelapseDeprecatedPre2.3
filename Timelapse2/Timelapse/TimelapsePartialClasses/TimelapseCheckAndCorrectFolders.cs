using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
        // If not, ask the user to try to locate each missing folder.
        public void CheckAndCorrectForMissingFolders(FileDatabase fileDatabase)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(fileDatabase, nameof(fileDatabase));

            // Get a list of missing folders, if any.
            List<string> missingRelativePaths = GetMissingFolders(fileDatabase);
            if (missingRelativePaths?.Count == 0)
            {
                // No folders are missing, so nothing to do.
                return;
            }

            // We want to show the normal cursor when we display dialog boxes, so save the current cursor so we can store it.
            Cursor cursor = Mouse.OverrideCursor;

            // We know that at least one or more folders are missing.
            // First attempt. See if, for each missing folder path, we can find one (and only one) folder with the same name under the root folder. 
            Dictionary<string, string> matchingFolderNames = Util.FilesFolders.TryFindMissingFolders(fileDatabase.FolderPath, missingRelativePaths);
            bool? result;
            if(matchingFolderNames != null)
            {
                Mouse.OverrideCursor = null;
                // Present a dialog box that shows the possible match for each folder.
                // The user can then confirm that they are correct, or request manual locaton of those folders, or cancel altogether.
                MissingFoldersCheckPossibleMatches dialog = new MissingFoldersCheckPossibleMatches(fileDatabase.FolderPath, matchingFolderNames);
                result = dialog.ShowDialog();
                Mouse.OverrideCursor = cursor;
                if (result == true)
                {
                    // User accepted the folder matches. Update the database
                    foreach (string key in matchingFolderNames.Keys)
                    {
                        ColumnTuple columnToUpdate = new ColumnTuple(Constant.DatabaseColumn.RelativePath, matchingFolderNames[key]);
                        ColumnTuplesWithWhere columnToUpdateWithWhere = new ColumnTuplesWithWhere(columnToUpdate, key);
                        fileDatabase.UpdateFiles(columnToUpdateWithWhere);
                    }
                    return;
                }

                if (dialog.IsCancelled)
                {
                    // User canceled all further searches for missing files, so abort.
                    Mouse.OverrideCursor = cursor;
                    return;
                }
                // If we are here, then the user requested manual search for individual folders, so we just continue on to Attept 3
            }
            else
            {
                // Attempt 2. As there were no matching folders, we raise a first dialog box telling the user what is going on,
                // and to verify that he/she wants to manually search for the missing folders...
                Mouse.OverrideCursor = null;
                result = Dialogs.ImageSetLoadingMultipleImageFoldersNotFoundDialog(this, missingRelativePaths);
                Mouse.OverrideCursor = cursor;
                if (result == false)
                {
                    return;
                }
            }

            // Attempt 3. Generate multiple dialog boxes asking for the loation of each folder. 
            Mouse.OverrideCursor = null;
            MissingFoldersLocateEachFolder missingFoldersLocateEachFolderDialog = new MissingFoldersLocateEachFolder(fileDatabase.FolderPath, missingRelativePaths);
            result = missingFoldersLocateEachFolderDialog.ShowDialog();
            return;

            string newFolderName = String.Empty;

            int attempt = 0;
            string changePrefixFrom = String.Empty;
            string changePrefixTo = String.Empty;
            bool addPrefix = false;
            bool removePrefix = false;
            bool changePrefix = false;
            // Raise a dialog box for each image asking the user to locate the missing folder
            foreach (string relativePath in missingRelativePaths)
            {
                //if (attempt == 1)
                //{
                //    string s = Path.Combine(fileDatabase.FolderPath, changePrefixTo, relativePath);
                //    System.Diagnostics.Debug.Print("1 " + s);
                //    s = Path.Combine(fileDatabase.FolderPath, relativePath.Remove(0, changePrefixFrom.Length));
                //    System.Diagnostics.Debug.Print("2 " + s);
                //    s = Path.Combine(fileDatabase.FolderPath, new Regex(Regex.Escape(changePrefixFrom)).Replace(relativePath, changePrefixTo, 1));
                //    System.Diagnostics.Debug.Print("3 " + s);
                //}
               
                Dialog.FindMissingImageFolder findMissingImageFolderDialog;
                Mouse.OverrideCursor = cursor;
                if (attempt == 1 && addPrefix && Directory.Exists(Path.Combine(fileDatabase.FolderPath, changePrefixTo, relativePath)))
                {
                    // We found the next folder by Adding a prefix to it
                    newFolderName = Path.Combine(changePrefixTo, relativePath);
                    //System.Diagnostics.Debug.Print("Found prefix " + newFolderName);
                }
                // else if (attempt == 1 && removePrefix && Directory.Exists(Path.Combine(fileDatabase.FolderPath, relativePath.Remove(0, relativePath.IndexOf(changePrefixFrom)))))
                else if (attempt == 1 && removePrefix && Directory.Exists(Path.Combine(fileDatabase.FolderPath, relativePath.Remove(0, changePrefixFrom.Length))))
                {
                    // newFolderName = relativePath.Remove(0, relativePath.IndexOf(changePrefixFrom));
                    newFolderName = relativePath.Remove(0, changePrefixFrom.Length);
                    // System.Diagnostics.Debug.Print("removed prefix " + newFolderName);
                }
                //else if (attempt == 1 && changePrefix && Directory.Exists(Path.Combine(fileDatabase.FolderPath, Regex.Replace(relativePath, "\\" + changePrefixFrom + "\\" + changePrefixTo, "$1"))))
                else if (attempt == 1 && changePrefix && Directory.Exists(Path.Combine(fileDatabase.FolderPath, new Regex(Regex.Escape(changePrefixFrom)).Replace(relativePath, changePrefixTo, 1))))
                {
                    newFolderName = new Regex(Regex.Escape(changePrefixFrom)).Replace(relativePath, changePrefixTo, 1);
                    // System.Diagnostics.Debug.Print("changed prefix " + newFolderName);
                }
                else
                {
                    // System.Diagnostics.Debug.Print("Did not find " + newFolderName);
                    findMissingImageFolderDialog = new Dialog.FindMissingImageFolder(this, fileDatabase.FolderPath, relativePath);

                    result = findMissingImageFolderDialog.ShowDialog();
                    if (result == true)
                    {
                        newFolderName = findMissingImageFolderDialog.NewFolderName;
                        if (attempt == 0)
                        {
                            // at this point, the user has specified the first new folder location.
                            // Try to find out how it differs, so we can modify and test other folder paths to see if we can find them.
                            string commonSuffix = Compare.LongestCommonSuffix(newFolderName, relativePath);
                            int suffixIndexInNewFolderName = newFolderName.LastIndexOf(commonSuffix);
                            int suffixIndexInRelativePath = relativePath.LastIndexOf(commonSuffix);
                            if (suffixIndexInNewFolderName > 0 && suffixIndexInRelativePath > 0)
                            {
                                // System.Diagnostics.Debug.Print("Change from " + relativePath.Remove(suffixIndexInRelativePath) + " to " + newFolderName.Remove(suffixIndexInNewFolderName));
                                changePrefixFrom = relativePath.Remove(suffixIndexInRelativePath);
                                changePrefixTo = newFolderName.Remove(suffixIndexInNewFolderName);
                                changePrefix = true;
                            }
                            else if (suffixIndexInNewFolderName > 0)
                            {
                                changePrefixTo = newFolderName.Remove(suffixIndexInNewFolderName);
                                addPrefix = true;
                                // System.Diagnostics.Debug.Print("Add Prefix " + newFolderName.Remove(suffixIndexInNewFolderName));
                            }
                            else // suffixIndexInRelativePath > 0
                            {
                                // System.Diagnostics.Debug.Print("Remove Prefix " + relativePath.Remove(suffixIndexInRelativePath));
                                changePrefixFrom = relativePath.Remove(suffixIndexInRelativePath);
                                removePrefix = true;
                            }
                        }
                        attempt++;
                    }
                    else if (findMissingImageFolderDialog.CancelAll)
                    {
                        // stop trying to locate missing folders
                        Mouse.OverrideCursor = cursor;
                        break;
                    }
                }
                ColumnTuple columnToUpdate = new ColumnTuple(Constant.DatabaseColumn.RelativePath, newFolderName);
                ColumnTuplesWithWhere columnToUpdateWithWhere = new ColumnTuplesWithWhere(columnToUpdate, relativePath);
                fileDatabase.UpdateFiles(columnToUpdateWithWhere);
            }
            Mouse.OverrideCursor = cursor;
        }

        // Return a list of missing folders. This is done by by getting all relative paths and seeing if each folder actually exists.
        private List<string> GetMissingFolders(FileDatabase fileDatabase)
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
