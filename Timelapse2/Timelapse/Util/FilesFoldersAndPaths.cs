using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Timelapse.Util
{
    public static class FilesFoldersAndPaths
    {
        // Given a list of folders, return a FileInfo list containing only image and video files found in those folders
        // Currently, those files are identified by three extensions: .jpg, .avi, and .mp4
        public static List<FileInfo> GetAllImageAndVideoFilesInFolders(IEnumerable<string> folderPaths)
        {
            List<FileInfo> fileInfoList = new List<FileInfo>();
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

        public static List<FileInfo> GetAllImageAndVideoFilesInFolderAndSubfolders(string rootFolderPath)
        {
            List<string> folderPaths = new List<string>();
            GetAllFoldersContainingAnImageOrVideo(rootFolderPath, folderPaths);
                        
            List<FileInfo> fileInfoList = new List<FileInfo>();
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
            if (fileInfoList.Count() == 0)
            {
                // There are no files, so just return the empty list
                return fileInfoList;
            }

            // Reorder the files
            return fileInfoList.OrderBy(file => file.FullName).ToList();
        }

        // Populate folderPaths with all the folders and subfolders (from the root folder) that contains at least one video or image file
        public static void GetAllFoldersContainingAnImageOrVideo(string folderRoot, List<string> folderPaths)
        {
            if (!Directory.Exists(folderRoot))
            {
                return;
            }
            // Add a folder only if it contains one of the desired extensions
            
            DirectoryInfo directoryInfo = new DirectoryInfo(folderRoot);

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
        #endregion
    }
}
