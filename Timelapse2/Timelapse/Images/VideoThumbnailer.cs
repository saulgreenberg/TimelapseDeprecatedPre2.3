using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Timelapse.Images
{
    public static class VideoThumbnailer
    {
        // Recursively go through the folder and subfolders. When video files are found in a folder, 
        // - create a thumbnail folder 
        // - for each video generate a thumbnail and place it in that folder
        public static void GenerateVideoThumbnailsInAllFolders(string root, string thumbnailFolderName, string[] videoSuffixes)
        {
            // Generate the thumbnails for this folder
            if (!Directory.Exists(root))
            {
                return;
            }
            GenerateVideoThumbnailsInFolder(root, thumbnailFolderName, videoSuffixes);

            // Recursively descend subfolders to generate thumbnails in those folders
            DirectoryInfo dirInfo = new DirectoryInfo(root);
            System.IO.DirectoryInfo[] subDirs = dirInfo.GetDirectories();
            foreach (DirectoryInfo subDir in subDirs)
            {
                // Skip the following folders
                if (subDir.Name == Constant.File.BackupFolder || subDir.Name == Constant.File.DeletedFilesFolder || subDir.Name == thumbnailFolderName)
                {
                    continue;
                }
                GenerateVideoThumbnailsInAllFolders(subDir.FullName, thumbnailFolderName, videoSuffixes);
            }
        }

        // Recursively go through the folder and subfolders. When video files are found in a folder, 
        // - create a thumbnail folder 
        // - for each video generate a thumbnail and place it in that folder
        public static void DeleteVideoThumbnailsInAllFolders(string root, string thumbnailFolderName)
        {
            try
            {
                string thumbnailPath = Path.Combine(root, thumbnailFolderName);
                // If there is no thumbnail folder, we are done
                if (!Directory.Exists(thumbnailPath))
                {
                    return;
                }
                else
                {
                    DeleteFilesInFolder(thumbnailPath);
                    Directory.Delete(thumbnailPath);
                }

                // Recursively descend subfolders to delete thumbnails in those folders
                DirectoryInfo dirInfo = new DirectoryInfo(root);
                System.IO.DirectoryInfo[] subDirs = dirInfo.GetDirectories();
                foreach (DirectoryInfo subDir in subDirs)
                {
                    // Skip the following folders
                    if (subDir.Name == Constant.File.BackupFolder || subDir.Name == Constant.File.DeletedFilesFolder || subDir.Name == thumbnailFolderName)
                    {
                        continue;
                    }
                    DeleteVideoThumbnailsInAllFolders(subDir.FullName, thumbnailFolderName);
                }
            }
            catch
            {
                return;
            }
        }

        #region Private methods
        // Create a folder name to hold the thumbnails, and then create a thumnail file in it
        private static void GenerateVideoThumbnailsInFolder(string root, string thumbnailFolderName, string[] videoSuffixes)
        {
            try
            {
                // Generate a list of the video files in the current folder
                DirectoryInfo dirInfo = new DirectoryInfo(root);
                List<string> files = new List<string>();
                foreach (string videoSuffix in videoSuffixes)
                {
                    files.AddRange(Directory.GetFiles(dirInfo.FullName, "*" + videoSuffix));
                }
                // No video files, so no need to continue
                if (files.Count == 0)
                {
                    return;
                }

                // Create the thumbnail folder if it doesn't already exist
                string thumbnailFolderPath = Path.Combine(root, thumbnailFolderName);
                if (!Directory.Exists(thumbnailFolderPath))
                {
                    Directory.CreateDirectory(thumbnailFolderPath);
                }

                // Create a thumbnail for each video file
                foreach (string file in files)
                {
                    string thumbnailFilePath = Path.Combine(thumbnailFolderPath, Path.GetFileNameWithoutExtension(file) + ".jpg");

                    // If we can't compose an ffmpeg command, then just bail as its because ffmpeg.exe cannot be found
                    string ffmpegCommand = FfmpegComposeThumbnailCommand(file, thumbnailFilePath);
                    if (ffmpegCommand == String.Empty)
                    {
                        return;
                    }

                    // If the thumbnail file doesn't exist, use FFMpeg to create one 
                    if (!File.Exists(thumbnailFilePath))
                    {
                        Process process = new Process
                        {
                            StartInfo = new ProcessStartInfo
                            {
                                WindowStyle = ProcessWindowStyle.Hidden,
                                FileName = "cmd.exe",
                                Arguments = "/C " + ffmpegCommand
                            }
                        };
                        process.Start();
                        // We don't really have to wait for exit, but 
                        // I am not sure if there are limits to how many process we can
                        // create and run concurrently. This gives them time to finish
                        // TODO ASAP we need to check / play with this.
                        // process.WaitForExit(500);
                    }
                }
                return;
            }
            catch
            {
                return;
            }
        }

        // Delete all files in the given folder
        private static void DeleteFilesInFolder(string root)
        {
            try
            {
                string[] filePaths = Directory.GetFiles(root);
                foreach (string filePath in filePaths)
                {
                    File.Delete(filePath);
                }
            }
            catch
            {
                return;
            }
        }

        // Compose the FFmpeg command string to be used generates a thumbnail file at thumbnailFullPath from the video at videoFullPath 
        private static string FfmpegComposeThumbnailCommand(string videoFullPath, string thumbnailFullPath)
        {
            // TODO Maybe put it in the section of code that checks dll?
            string ffmpegFullPath = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().CodeBase), "ffmpeg.exe");
            ffmpegFullPath = ffmpegFullPath.Replace("file:\\", String.Empty);
            string path = ffmpegFullPath + " -i " + '"' + videoFullPath + '"' + " -frames:v 1 " + '"' + thumbnailFullPath + '"';
            return File.Exists(ffmpegFullPath) ? path : String.Empty;
        }
        #endregion
    }
}
