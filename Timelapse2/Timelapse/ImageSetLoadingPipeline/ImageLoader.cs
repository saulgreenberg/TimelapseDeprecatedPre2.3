using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Timelapse.Controls;
using Timelapse.Database;
using Timelapse.Enums;
using Timelapse.Util;

namespace Timelapse.ImageSetLoadingPipeline
{
    /// <summary>
    /// Encapsulates the logic necessary to load an image from disk into the system.
    /// </summary>
    public class ImageLoader
    {
        private readonly FileInfo fileInfo;
        private readonly DataEntryHandler dataHandler;
        private readonly TimelapseState state;
        private readonly string relativePath;
        private readonly string imageSetFolderPath;

        public string FolderPath
        {
            get { return this.dataHandler.FileDatabase.FolderPath; }
        }

        public TimeZoneInfo ImageSetTimeZone
        {
            get { return this.dataHandler.FileDatabase.ImageSet.GetSystemTimeZone(); }
        }

        public bool RequiresDatabaseInsert
        {
            get;
            private set;
        }

        public ImageRow File
        {
            get;
            private set;
        }

        private BitmapSource bitmapSource = null;
        public BitmapSource BitmapSource
        {
            get
            {
                if (this.bitmapSource == null)
                {
                    // Lazy load
                    var task = this.File.LoadBitmapAsync(this.FolderPath, ImageDisplayIntentEnum.Ephemeral, ImageDimensionEnum.UseWidth);
                    task.Wait();

                    var loadResult = task.Result;
                    this.bitmapSource = loadResult.Item1;
                }

                return this.bitmapSource;
            }
            private set
            {
                this.bitmapSource = value;
            }
        }

        public ImageLoader(string imageSetFolderPath, string relativePath, FileInfo fileInfo, DataEntryHandler dataHandler, TimelapseState state)
        {
            this.fileInfo = fileInfo;
            this.dataHandler = dataHandler;
            this.state = state;
            this.imageSetFolderPath = imageSetFolderPath;
            this.relativePath = relativePath;
        }

        public Task LoadImageAsync(Action OnImageLoadComplete)
        {
            // Set the loader's file member. 
            this.RequiresDatabaseInsert = true;

            // Skip the per-file call to the database
            this.File = this.dataHandler.FileDatabase.FileTable.NewRow(this.fileInfo);
            this.File.Folder = Path.GetFileName(this.imageSetFolderPath);
            this.File.RelativePath = this.relativePath;
            this.File.SetDateTimeOffsetFromFileInfo(this.FolderPath);

            // By default, file image quality is ok (i.e., not dark)
            this.File.ImageQuality = FileSelectionEnum.Ok;

            return Task.Run(() =>
            {
                // Try to update the datetime (which is currently recorded as the file's date) with the metadata date time the image was taken instead
                // We only do this for files, as videos do not have these metadata fields
                // PERFORMANCE Trying to read the date/time from the image data also seems like a somewhat expensive operation. 
                this.File.TryReadDateTimeOriginalFromMetadata(this.FolderPath, this.ImageSetTimeZone);

                // This completes processing, but it may be some time before the task is checked for completion.
                // for purposes of reporting progress, call the completion delegate provided.

                OnImageLoadComplete?.Invoke();
            });
        }
    }
}
