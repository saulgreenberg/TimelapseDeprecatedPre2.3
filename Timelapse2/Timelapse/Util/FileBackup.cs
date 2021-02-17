﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Timelapse.Database
{
    /// <summary>
    /// Make a time-stamped backup of the given file in the backup folder.
    /// At the same time, limit the number of backup files, where we prune older files with the same extension as needed. 
    /// </summary>
    public static class FileBackup
    {
        #region Public Static Methods - Get Backup-related things
        private static IEnumerable<FileInfo> GetBackupFiles(DirectoryInfo backupFolder, string sourceFilePath)
        {
            string sourceFileNameWithoutExtension = Path.GetFileNameWithoutExtension(sourceFilePath);
            string sourceFileExtension = Path.GetExtension(sourceFilePath);
            string searchPattern = sourceFileNameWithoutExtension + "*" + sourceFileExtension;
            try
            {
                return backupFolder.GetFiles(searchPattern);
            }
            catch
            {
                return null;
            }
        }

        public static DateTime GetMostRecentBackup(string sourceFilePath)
        {
            try
            {
                DirectoryInfo backupFolder = FileBackup.GetOrCreateBackupFolder(sourceFilePath);
                FileInfo mostRecentBackupFile = null;
                if (backupFolder != null)
                {
                    mostRecentBackupFile = FileBackup.GetBackupFiles(backupFolder, sourceFilePath).OrderByDescending(file => file.LastWriteTimeUtc).FirstOrDefault();
                }
                if (backupFolder != null && mostRecentBackupFile != null)
                {
                    return mostRecentBackupFile.LastWriteTimeUtc;
                }
                return DateTime.MinValue.ToUniversalTime();
            }
            catch
            {
                return DateTime.MinValue.ToUniversalTime();
            }
        }

        public static DirectoryInfo GetOrCreateBackupFolder(string sourceFilePath)
        {
            string sourceFolderPath = Path.GetDirectoryName(sourceFilePath);
            DirectoryInfo backupFolder = new DirectoryInfo(Path.Combine(sourceFolderPath, Constant.File.BackupFolder));   // The Backup Folder 
            if (backupFolder.Exists == false)
            {
                try
                {
                    backupFolder.Create();
                }
                catch
                {
                    return null;
                }
            }
            return backupFolder;
        }

        // UNUSED - but keep just in case 
        //public static string GetLastBackupFilePath(string sourceFilePath)
        //{
        //    string sourceFolderPath = Path.GetDirectoryName(sourceFilePath);
        //    DirectoryInfo backupFolder = new DirectoryInfo(Path.Combine(sourceFolderPath, Constant.File.BackupFolder));   // The Backup Folder 
        //    if (backupFolder.Exists == false)
        //    {
        //        // If there is no backp folder, then there is no backup file
        //        return String.Empty;
        //    }

        //    // Get the backup files
        //    IEnumerable<FileInfo> backupFiles = FileBackup.GetBackupFiles(backupFolder, sourceFilePath).OrderByDescending(file => file.LastWriteTimeUtc);
        //    if (backupFiles.Any() == false)
        //    {
        //        // No backup files 
        //        return String.Empty;
        //    }
        //    return backupFiles.Last().FullName;
        //}
        #endregion

        #region Public Static Methods -TryCreateBackup, various versions

        // Copy to backup version with with full path to source file
        public static bool TryCreateBackup(string sourceFilePath)
        {
            return FileBackup.TryCreateBackup(Path.GetDirectoryName(sourceFilePath), Path.GetFileName(sourceFilePath), false);
        }

        // Copy or move file to backup version with full path to source file
        public static bool TryCreateBackup(string sourceFilePath, bool moveInsteadOfCopy)
        {
            return FileBackup.TryCreateBackup(Path.GetDirectoryName(sourceFilePath), Path.GetFileName(sourceFilePath), moveInsteadOfCopy);
        }

        // Copy or move file to backup version with separated path/source file name
        public static bool TryCreateBackup(string folderPath, string sourceFileName)
        {
            return FileBackup.TryCreateBackup(folderPath, sourceFileName, false);
        }

        // Full version: Copy or move file to backup version with separated path/source file name
        public static bool TryCreateBackup(string folderPath, string sourceFileName, bool moveInsteadOfCopy)
        {
            string sourceFilePath = Path.Combine(folderPath, sourceFileName);
            if (File.Exists(sourceFilePath) == false)
            {
                // nothing to do
                return false;
            }

            // create backup folder if needed
            DirectoryInfo backupFolder = FileBackup.GetOrCreateBackupFolder(sourceFilePath);
            if (backupFolder == null)
            {
                // Something went wrong...
                return false;
            }
            // create a timestamped copy of the file
            // file names can't contain colons so use non-standard format for timestamp with dashes for 24 hour-minute-second separation
            string sourceFileNameWithoutExtension = Path.GetFileNameWithoutExtension(sourceFileName);
            string sourceFileExtension = Path.GetExtension(sourceFileName);
            string destinationFileName = String.Concat(sourceFileNameWithoutExtension, ".", DateTime.Now.ToString("yyyy-MM-dd.HH-mm-ss"), sourceFileExtension);
            string destinationFilePath = Path.Combine(backupFolder.FullName, destinationFileName);

            try
            {
                if (File.Exists(destinationFilePath) && new System.IO.FileInfo(destinationFilePath).Attributes.HasFlag(System.IO.FileAttributes.ReadOnly))
                {
                    // Can't overwrite it...
                    return false;
                }
                if (moveInsteadOfCopy)
                {

                    File.Move(sourceFilePath, destinationFilePath);
                }
                else
                {
                    File.Copy(sourceFilePath, destinationFilePath, true);
                }
            }
            catch
            {
                // Old code: We just don't create the backup now. While we previously threw an exception, we now test and warn the user earlier on in the code that a backup can't be made 
                // System.Diagnostics.Debug.Print("Did not back up" + destinationFilePath);
                // throw new PathTooLongException("Backup failure: Could not create backups as the file path is too long", e);
                return false;
            }

            // age out older backup files
            IEnumerable<FileInfo> backupFiles = FileBackup.GetBackupFiles(backupFolder, sourceFilePath).OrderByDescending(file => file.LastWriteTimeUtc);
            if (backupFiles == null)
            {
                // We can't delete older backups, but at least we were able to create a backup.
                return true;
            }
            foreach (FileInfo file in backupFiles.Skip(Constant.File.NumberOfBackupFilesToKeep))
            {
                File.Delete(file.FullName);
            }
            return true;
        }

        #endregion
    }
}