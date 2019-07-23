using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Documents;
using Timelapse.Controls;
using Timelapse.Database;
using Timelapse.Images;
using Timelapse.Util;

namespace Timelapse.ImageSetLoadingPipeline
{
    /// <summary>
    /// Encapsulates logic to load a set of images into the system.
    /// </summary>
    public class ImageSetLoader
    {
        private int imagesLoaded = 0;
        
        private Task pass1 = null;

        private List<ImageLoader> imagesToInsert = new List<ImageLoader>();

        private Task pass2 = null;

        public ImageLoader LastLoadComplete
        {
            get;
            private set;
        }

        public ImageRow LastInsertComplete
        {
            get;
            private set;
        }

        public int LastIndexInsertComplete
        {
            get;
            private set;
        }

        public int ImagesLoaded
        {
            get
            {
                return this.imagesLoaded;
            }
        }

        public int ImagesToLoad
        {
            get;
            private set;
        }

        public ImageSetLoader(IEnumerable<FileInfo> fileInfos, DataEntryHandler dataHandler, TimelapseState state)
        {
            // Avoid enumerating to count and enumerating to set up tasks
            FileInfo[] fileInfoArray = fileInfos.ToArray();
            this.ImagesToLoad = fileInfoArray.Length;

            this.pass1 = new Task(() => 
            {
                List<Task<ImageLoader>> loadTasks = new List<Task<ImageLoader>>();

                // Fan out the loader tasks
                foreach (FileInfo fileInfo in fileInfoArray)
                {
                    ImageLoader loader = new ImageLoader(fileInfo, dataHandler, state);

                    Task<ImageLoader> loaderTask = loader.LoadImageAsync();

                    loadTasks.Add(loaderTask);
                }

                Task<ImageLoader>[] taskArray = loadTasks.ToArray();

                // Allow all the tasks to complete.
                do
                {
                    // Wait for any of the tasks to complete
                    int completedIndex = Task.WaitAny(taskArray);

                    // Set the current progress
                    ImageLoader completedLoader = taskArray[completedIndex].Result;

                    this.LastLoadComplete = completedLoader;

                    // Set up the next iteration, minus the just-completed task. This prevents double counting completions.
                    loadTasks.RemoveAt(completedIndex);
                    taskArray = loadTasks.ToArray();

                    if (completedLoader.RequiresDatabaseInsert)
                    {
                        this.imagesToInsert.Add(completedLoader);
                    }

                    #region old
                    /*Task<ImageLoader>[] nextTaskArray = new Task<ImageLoader>[taskArray.Length - 1];

                    if (completedIndex > 0 && completedIndex < taskArray.Length - 1)
                    {
                        Array.Copy(taskArray, 0, nextTaskArray, 0, completedIndex);
                        Array.Copy(taskArray, completedIndex + 1, nextTaskArray, completedIndex, taskArray.Length - completedIndex);
                    }
                    else if (nextTaskArray.Length == 0)
                    {
                        // This was the last task
                    }
                    else if (completedIndex == 0)
                    {
                        // Only need the tasks after index 0
                        Array.Copy(taskArray, 1, nextTaskArray, 0, taskArray.Length - 1);
                    }
                    else if (completedIndex == taskArray.Length - 1)
                    {
                        // Only need the tasks before the last index
                        Array.Copy(taskArray, 0, nextTaskArray, 0, taskArray.Length - 1);
                    }

                    taskArray = nextTaskArray;*/
                    #endregion
                } while (Interlocked.Increment(ref imagesLoaded) < this.ImagesToLoad);
            });

            this.pass2 = new Task(() =>
            {
                // Load the necessary items into the database.
                List<ImageRow> filesToInsert = (from imageLoader in this.imagesToInsert
                                                select imageLoader.File)
                                               .OrderBy(file => Path.Combine(file.RelativePath, file.File))
                                               .ToList();

                dataHandler.FileDatabase.AddFiles(filesToInsert, (ImageRow file, int fileIndex) => 
                {
                    this.LastInsertComplete = file;
                    this.LastIndexInsertComplete = fileIndex;
                });

            });
        }

        internal async Task LoadAsync(Action<int, FolderLoadProgress> reportProgress, FolderLoadProgress folderLoadProgress, int progressIntervalMilliseconds)
        {
            this.pass1.Start();

            Timer t = new Timer((state) =>
            {
                //FolderLoadProgress folderLoadProgressState = state as FolderLoadProgress;

                folderLoadProgress.BitmapSource = this.LastLoadComplete?.BitmapSource;
                folderLoadProgress.CurrentFile = this.ImagesLoaded;
                folderLoadProgress.CurrentFileName = this.LastLoadComplete?.File.File;
                int percentProgress = (int)(100.0 * this.ImagesLoaded / (double)this.ImagesToLoad);
                reportProgress(percentProgress, folderLoadProgress);
            }, null, 0, progressIntervalMilliseconds);

            await this.pass1;

            t.Change(-1, -1);
            t.Dispose();

            folderLoadProgress.CurrentPass = 2;

            this.pass2.Start();

            t = new Timer((state) =>
            {
                //FolderLoadProgress folderLoadProgressState = state as FolderLoadProgress;

                folderLoadProgress.BitmapSource = null;
                folderLoadProgress.CurrentFile = this.LastIndexInsertComplete;
                folderLoadProgress.CurrentFileName = this.LastInsertComplete?.File;
                int percentProgress = (int)(100.0 * folderLoadProgress.CurrentFile / (double)this.imagesToInsert.Count);
                reportProgress(percentProgress, folderLoadProgress);
            }, null, 0, progressIntervalMilliseconds);

            await this.pass2;

            t.Change(-1, -1);
            t.Dispose();
        }
    }
}
