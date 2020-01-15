using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Timelapse.Controls;
using Timelapse.Database;
using Timelapse.Images;
using Timelapse.Util;

namespace Timelapse.ImageSetLoadingPipeline
{
    /// <summary>
    /// Encapsulates logic to load a set of images into the system.
    /// </summary>
    public class ImageSetLoader : IDisposable
    {
        private int imagesLoaded = 0;
        private int imagesToInsert = 0;

        private readonly Task pass1 = null;

        private readonly Task pass2 = null;

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

        public ImageSetLoader(string imageSetFolderPath, IEnumerable<FileInfo> fileInfos, DataEntryHandler dataHandler, TimelapseState state)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(dataHandler, nameof(dataHandler));

            // Don't add a file if it already exists in the database.
            // Rather than check every file one by one to see if it exists in the database 
            // - get all the current files in the database (as existing full paths) in a single database call,
            // - create a new file list (fileInfoArray) that only adds files (as fileInfos) that are NOT present in the database. 
            HashSet<string> existingPaths;
            using (FileTable filetable = dataHandler.FileDatabase.SelectAllFiles())
            {
                existingPaths = new HashSet<string>(from file in filetable
                                                    select Path.Combine(imageSetFolderPath, Path.Combine(file.RelativePath, file.File)).ToLowerInvariant());
            }
            FileInfo[] filesToAddInfoArray = (from fileInfo in fileInfos
                                              where existingPaths.Contains(fileInfo.FullName.ToLowerInvariant()) == false
                                              select fileInfo).OrderBy(f => f.FullName).ToArray();

            this.ImagesToLoad = filesToAddInfoArray.Length;

            // The queue will take image rows ready for insertion to the second pass
            // The eventindicates explicitly when the first pass is done.
            ConcurrentQueue<ImageRow> databaseInsertionQueue = new ConcurrentQueue<ImageRow>();

            string absolutePathPart = imageSetFolderPath + @"\";

            // Pass 1
            this.pass1 = new Task(() =>
            {
                List<Task> loadTasks = new List<Task>();

                // Fan out the loader tasks
                foreach (FileInfo fileInfo in filesToAddInfoArray)
                {
                    // Parse the relative path from the full name. 
                    // As GetDirectoryName does not end with a \ on a file name, we add the' '\' as needed
                    string directoryName = Path.GetDirectoryName(fileInfo.FullName);
                    if (directoryName.EndsWith(@"\") == false)
                    {
                        directoryName += @"\";
                    }
                    string relativePath = directoryName.Replace(absolutePathPart, string.Empty).TrimEnd(Path.DirectorySeparatorChar);

                    ImageLoader loader = new ImageLoader(imageSetFolderPath, relativePath, fileInfo, dataHandler, state);

                    Task loaderTask = loader.LoadImageAsync(() =>
                    {
                        // Both of these operations are atomic, the specific number and the specific loader at any given
                        // time may not coorespond.
                        Interlocked.Increment(ref this.imagesLoaded);
                        this.LastLoadComplete = loader;

                        if (loader.RequiresDatabaseInsert)
                        {
                            // This requires database insertion. Enqueue for pass 2
                            // Note that there is no strict ordering here, anything may finish and insert in
                            // any order. By sorting the file infos above, things that sort first in the database should
                            // be done first, BUT THIS MAY REQUIRE ADDITIONAL FINESSE TO KEEP THE EXPLICIT ORDER CORRECT.
                            databaseInsertionQueue.Enqueue(loader.File);
                            Interlocked.Increment(ref this.imagesToInsert);
                        }
                    });
                    loadTasks.Add(loaderTask);
                }
                Task.WaitAll(loadTasks.ToArray());
            });
            // End Pass 1

            // Pass 2
            this.pass2 = new Task(() =>
            {
                // This pass2 starts after pass1 is fully complete
                List<ImageRow> imagesToInsert = databaseInsertionQueue.OrderBy(f => Path.Combine(f.RelativePath, f.File)).ToList();
                dataHandler.FileDatabase.AddFiles(imagesToInsert,
                                                  (ImageRow file, int fileIndex) =>
                                                  {
                                                      this.LastInsertComplete = file;
                                                      this.LastIndexInsertComplete = fileIndex;
                                                  });
            });
            // End Pass 2
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
                        folderLoadProgress.BitmapSource = this.LastLoadComplete.BitmapSource;
                    }
                }
                else
                {
                    folderLoadProgress.BitmapSource = null;
                }

                folderLoadProgress.CurrentFile = this.ImagesLoaded;
                folderLoadProgress.CurrentFileName = this.LastLoadComplete?.File.File;
                int percentProgress = (int)(100.0 * this.ImagesLoaded / this.ImagesToLoad);
                reportProgress(percentProgress, folderLoadProgress);
            }, null, 0, progressIntervalMilliseconds);

            await this.pass1.ConfigureAwait(false);

            t.Change(-1, -1);
            t.Dispose();

            folderLoadProgress.CurrentPass = 2;

            this.pass2.Start();

            t = new Timer((state) =>
            {
                folderLoadProgress.BitmapSource = null;
                folderLoadProgress.CurrentFile = this.LastIndexInsertComplete;
                folderLoadProgress.CurrentFileName = this.LastInsertComplete?.File;
                int percentProgress = (int)(100.0 * folderLoadProgress.CurrentFile / this.imagesToInsert);
                reportProgress(percentProgress, folderLoadProgress);
            }, null, 0, progressIntervalMilliseconds);

            await this.pass2.ConfigureAwait(false);

            t.Change(-1, -1);
            t.Dispose();
        }

        // To follow design pattern in  CA1001 Types that own disposable fields should be disposable
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (pass1 != null)
                {
                    pass1.Dispose();
                }
                if (pass2 != null)
                {
                    pass2.Dispose();
                }
            }
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
