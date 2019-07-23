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

                    Task<ImageLoader> loaderTask = loader.LoadImageAsync(() => 
                    {
                        // Both of these operations are atomic, the specific number and the specific loader at any given
                        // time may not coorespond.
                        Interlocked.Increment(ref imagesLoaded);
                        this.LastLoadComplete = loader;
                    });

                    loadTasks.Add(loaderTask);
                }

                Task<ImageLoader>[] taskArray = loadTasks.ToArray();

                // Allow all the tasks to complete.

                Task.WaitAll(taskArray);

                // With all tasks complete, collect those loaders that need to be inserted into the DB
                this.imagesToInsert.AddRange(from task in taskArray
                                             let loader = task.Result
                                             where loader.RequiresDatabaseInsert
                                             select loader);
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
                if (this.LastLoadComplete != null)
                {
                    if (this.LastLoadComplete.File.IsVideo)
                    {
                        folderLoadProgress.BitmapSource = Constant.ImageValues.BlankVideo512.Value;
                    }
                    else
                    {
                        folderLoadProgress.BitmapSource = this.LastLoadComplete?.BitmapSource;
                    }
                }
                else
                {
                    folderLoadProgress.BitmapSource = null;
                }

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
