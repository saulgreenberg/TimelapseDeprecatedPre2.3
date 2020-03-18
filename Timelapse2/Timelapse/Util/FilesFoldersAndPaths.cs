using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Timelapse.Util
{
    public static class FilesFoldersAndPaths
    {
        // Given a list of folders, return a FileInfo list containing only image and video files found in those folders
        // Currently, those files are identified by three extensions: .jpg, .avi, and .mp4
        public static List<FileInfo> GetAllImageAndVideoFilesInFolders(IEnumerable<string> folderPaths)
        {
            List<FileInfo> fileInfoList = new List<FileInfo>();
            if (folderPaths == null)
            {
                // this should not happen
                TraceDebug.PrintStackTrace(1);
                // throw new ArgumentNullException(nameof(folderPaths));
                // Not sure if this will work, but worth a shot
                return fileInfoList;
            }
            foreach (string folderPath in folderPaths)
            {
                DirectoryInfo directoryInfo = new DirectoryInfo(folderPath);
                foreach (string extension in new List<string>() { Constant.File.JpgFileExtension, Constant.File.AviFileExtension, Constant.File.Mp4FileExtension })
                {
                    // GetFiles has a 'bug', where it can match an extension even if there are more letters after the extension. 
                    // That is, if we are looking for *.jpg, it will not only return *.jpg files, but files such as *.jpgXXX
                    fileInfoList.AddRange(directoryInfo.GetFiles("*" + extension));
                }
            }

            // Because of that bug, we need to check for, and remove, any files that don't exactly match the desired extension
            // At the same time, we also remove MacOSX hidden files, if any
            FilesRemoveAllButImagesAndVideos(fileInfoList);

            // Reorder the files
            return fileInfoList.OrderBy(file => file.FullName).ToList();
        }
        public static void GetAllImageAndVideoFilesInFolderAndSubfolders(string rootFolderPath, List<FileInfo> fileInfoList)
        {
            GetAllImageAndVideoFilesInFolderAndSubfolders(rootFolderPath, fileInfoList, 0);
        }
        private static void GetAllImageAndVideoFilesInFolderAndSubfolders(string rootFolderPath, List<FileInfo> fileInfoList, int recursionLevel)
        {
            // Check the arguments for null 
            if (fileInfoList == null)
            {
                // this should not happen
                TraceDebug.PrintStackTrace(1);
                // Not show what happens if we return with a null fileInfoList, but its worth a shot
                // throw new ArgumentNullException(nameof(control));
                return;
            }

            int nextRecursionLevel = recursionLevel + 1;
            if (!Directory.Exists(rootFolderPath))
            {
                return;
            }

            DirectoryInfo directoryInfo = new DirectoryInfo(rootFolderPath);
            foreach (string extension in new List<string>() { Constant.File.JpgFileExtension, Constant.File.AviFileExtension, Constant.File.Mp4FileExtension })
            {
                // GetFiles has a 'bug', where it can match an extension even if there are more letters after the extension. 
                // That is, if we are looking for *.jpg, it will not only return *.jpg files, but files such as *.jpgXXX
                fileInfoList.AddRange(directoryInfo.GetFiles("*" + extension));
            }

            // Recursively descend subfolders
            DirectoryInfo dirInfo = new DirectoryInfo(rootFolderPath);
            DirectoryInfo[] subDirs = dirInfo.GetDirectories();
            foreach (DirectoryInfo subDir in subDirs)
            {
                // Skip the following folders
                if (subDir.Name == Constant.File.BackupFolder || subDir.Name == Constant.File.DeletedFilesFolder || subDir.Name == Constant.File.VideoThumbnailFolderName)
                {
                    continue;
                }
                GetAllImageAndVideoFilesInFolderAndSubfolders(subDir.FullName, fileInfoList, nextRecursionLevel);
            }

            if (recursionLevel == 0)
            {
                // After all recursion is complete, do the following (but only on the initial recursion level)
                // Because of that bug, we need to check for, and remove, any files that don't exactly match the desired extension
                // At the same time, we also remove MacOSX hidden files, if any
                FilesRemoveAllButImagesAndVideos(fileInfoList);
                if (fileInfoList.Count != 0)
                {
                    fileInfoList.OrderBy(file => file.FullName).ToList();
                }
            }
        }

        // Populate folderPaths with all the folders and subfolders (from the root folder) that contains at least one video or image file
        public static void GetAllFoldersContainingAnImageOrVideo(string folderRoot, List<string> folderPaths)
        {
            // Check the arguments for null 
            if (folderPaths == null)
            {
                // this should not happen
                TraceDebug.PrintStackTrace(1);
                // throw new ArgumentNullException(nameof(folderPaths));
                // Not sure what happens if we have a null folderPaths, but we may as well try it.
                return;
            }

            if (!Directory.Exists(folderRoot))
            {
                return;
            }
            // Add a folder only if it contains one of the desired extensions
            if (CheckFolderForAtLeastOneImageOrVideoFiles(folderRoot) == true)
            {
                folderPaths.Add(folderRoot);
            }

            // Recursively descend subfolders, collecting directory info on the way
            // Note that while folders without images are also collected, these will eventually be skipped when it is later scanned for images to load
            DirectoryInfo dirInfo = new DirectoryInfo(folderRoot);
            DirectoryInfo[] subDirs = dirInfo.GetDirectories();
            foreach (DirectoryInfo subDir in subDirs)
            {
                // Skip the following folders
                if (subDir.Name == Constant.File.BackupFolder || subDir.Name == Constant.File.DeletedFilesFolder || subDir.Name == Constant.File.VideoThumbnailFolderName)
                {
                    continue;
                }
                GetAllFoldersContainingAnImageOrVideo(subDir.FullName, folderPaths);
            }
        }

        // Return true if any of the files in the fileinfo list includes at least  image or video
        public static bool CheckFolderForAtLeastOneImageOrVideoFiles(string folderPath)
        {
            DirectoryInfo directoryInfo = new DirectoryInfo(folderPath);
            foreach (string extension in new List<string>() { Constant.File.JpgFileExtension, Constant.File.AviFileExtension, Constant.File.Mp4FileExtension })
            {
                List<FileInfo> fileInfoList = new List<FileInfo>();
                fileInfoList.AddRange(directoryInfo.GetFiles("*" + extension));
                FilesRemoveAllButImagesAndVideos(fileInfoList);
                if (fileInfoList.Any(x => x.Name.EndsWith(extension, StringComparison.InvariantCultureIgnoreCase) == true))
                {
                    return true;
                }
            }
            return false;
        }

        public static List<string> RecursivelyFindFilesWithPattern(string startFolder, string pattern, bool ignoreBackupFolder, List<string> foundFiles)
        {
            if (foundFiles == null)
            {
                // This should not happen, as it should be initialized before this call
                foundFiles = new List<string>();
            }
            try
            {
                foreach (string directory in Directory.GetDirectories(startFolder))
                {
                    string foldername = directory.Split(Path.DirectorySeparatorChar).Last();
                    if (ignoreBackupFolder && foldername == Constant.File.BackupFolder)
                    {
                        continue;
                    }
                    foundFiles.AddRange(System.IO.Directory.GetFiles(directory, "*.ddb", SearchOption.TopDirectoryOnly));
                    RecursivelyFindFilesWithPattern(directory, pattern, true, foundFiles);
                }
            }
            catch (System.Exception)
            {
                return null;
            }
            return foundFiles;
        }

        #region Private methods
        // Remove, any files that 
        // - don't exactly match the desired image or video extension, 
        // - have a MacOSX hidden file prefix
        // When files from a MacOSX system are copied to Windows, it may produce 'hidden' files mirroring the valid files. 
        // These are prefixed by '._' and are not actually a valid image or video
        private static void FilesRemoveAllButImagesAndVideos(List<FileInfo> fileInfoList)
        {
            fileInfoList.RemoveAll(x => !(x.Name.EndsWith(Constant.File.JpgFileExtension, StringComparison.InvariantCultureIgnoreCase) == true
                                   || x.Name.EndsWith(Constant.File.AviFileExtension, StringComparison.InvariantCultureIgnoreCase) == true
                                   || x.Name.EndsWith(Constant.File.Mp4FileExtension, StringComparison.InvariantCultureIgnoreCase) == true)
                                   || x.Name.IndexOf(Constant.File.MacOSXHiddenFilePrefix) == 0);
        }

        #endregion

        #region Unused methods
        // Return true if any of the files in the fileInfo list could be a MacOSX Hidden file, 
        // i.e., prefixed by '._'
#pragma warning disable IDE0051 // Remove unused private members
        private static bool CheckForMacOSXHiddenFiles(List<FileInfo> fileInfoList)

        {
            return fileInfoList.Any(x => x.Name.IndexOf(Constant.File.MacOSXHiddenFilePrefix) == 0);
        }

        // Remove any of the files in the fileInfo list likely to be a MacOSX Hidden file, 
        // i.e., prefixed by '._'
        private static void RemoveMacOSXHiddenFiles(List<FileInfo> fileInfoList)
        {
            fileInfoList.RemoveAll(x => x.Name.IndexOf(Constant.File.MacOSXHiddenFilePrefix) == 0);
        }
#pragma warning restore IDE0051 // Remove unused private members
        #endregion
    }
}
