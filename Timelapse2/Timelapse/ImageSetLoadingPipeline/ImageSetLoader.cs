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
            // Order the files by full name, which should sort to the same order as the original pass 2.
            FileInfo[] fileInfoArray = fileInfos.OrderBy(f => f.FullName).ToArray();
            this.ImagesToLoad = fileInfoArray.Length;

            // The queue will take image rows ready for insertion to the second pass, the event
            // indicates explicitly when the first pass is done.
            ConcurrentQueue<ImageRow> databaseInsertionQueue = new ConcurrentQueue<ImageRow>();

            this.pass1 = new Task(() => 
            {
                List<Task> loadTasks = new List<Task>();

                // Fan out the loader tasks
                foreach (FileInfo fileInfo in fileInfoArray)
                {
                    ImageLoader loader = new ImageLoader(fileInfo, dataHandler, state);

                    Task loaderTask = loader.LoadImageAsync(() => 
                    {
                        // Both of these operations are atomic, the specific number and the specific loader at any given
                        // time may not coorespond.
                        Interlocked.Increment(ref imagesLoaded);
                        this.LastLoadComplete = loader;

                        if (loader.RequiresDatabaseInsert)
                        {
                            // This requires database insertion. Enqueue for pass 2
                            // Note that there is no strict ordering here, anything may finish and insert in
                            // any order. By sorting the file infos above, things that sort first in the database should
                            // be done first, BUT THIS MAY REQUIRE ADDITIONAL FINESSE TO KEEP THE EXPLICIT ORDER CORRECT.
                            databaseInsertionQueue.Enqueue(loader.File);
                        }
                    });

                    loadTasks.Add(loaderTask);
                }

                Task[] taskArray = loadTasks.ToArray();

                // Allow all the tasks to complete. Note that this may complete in a synchronous manner,
                // or may not, depending on the specifics of the system running this code.

                Task.WaitAll(taskArray);

                // With all tasks complete, collect those loaders that need to be inserted into the DB
                /*this.imagesToInsert.AddRange(from task in taskArray
                                             let loader = task.Result
                                             where loader.RequiresDatabaseInsert
                                             select loader);*/
            });
            
            
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
            

            /*
            // TODO: Seems to be a lot of memory pressure from lots of these ImageRows and accompanying ImageLoaders. Can
            // we set up pass 2 to run on batches of these files and clear them out of memory? What happens if we spin up
            // a pass 2 for every thousand images loaded?
            // If we sort the list of file names to be inserted, can we still preserve the order?
            this.pass2 = new Task(() =>
            {
                pass1DataStart.WaitOne();
                List<ImageRow> filesToInsert = new List<ImageRow>();

                do
                {
                    ImageRow row;
                    while (filesToInsert.Count < 1024 &&
                           databaseInsertionQueue.Count > 0 &&
                           databaseInsertionQueue.TryDequeue(out row) == true)
                    {
                        filesToInsert.Add(row);
                    }

                    if (filesToInsert.Count == 1024 || (pass1Done.WaitOne(0) == true && databaseInsertionQueue.Count == 0))
                    {
                        dataHandler.FileDatabase.AddFiles(filesToInsert.OrderBy(f => Path.Combine(f.RelativePath, f.File)).ToList(), 
                                                          (ImageRow file, int fileIndex) =>
                        {
                            this.LastInsertComplete = file;
                            this.LastIndexInsertComplete = fileIndex;
                        });

                        TraceDebug.PrintMessage("Inserted " + filesToInsert.Count + " files");

                        filesToInsert.Clear(); // This drops out all the ImageRow objects in memory that are now part of the database, and frees them for GC
                    }
                    else
                    {
                        // No files to insert. Wait 100ms and see if we're done or there's more work to do.
                        Task.Delay(100).Wait();
                    }
                } while (pass1Done.WaitOne(0) == false);

                // Load the necessary items into the database.
                    //List<ImageRow> filesToInsert = (from imageLoader in this.imagesToInsert
                    //                            select imageLoader.File)
                    //                           .OrderBy(file => Path.Combine(file.RelativePath, file.File))
                    //                          .ToList();

                

            });*/
        }

        internal async Task LoadAsync(Action<int, FolderLoadProgress> reportProgress, FolderLoadProgress folderLoadProgress, int progressIntervalMilliseconds)
        {
            this.pass1.Start();
            //this.pass2.Start();

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
