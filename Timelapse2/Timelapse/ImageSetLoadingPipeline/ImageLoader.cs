using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Timelapse.Controls;
using Timelapse.Database;
using Timelapse.Enums;
using Timelapse.Images;
using Timelapse.Util;

namespace Timelapse.ImageSetLoadingPipeline
{
    /// <summary>
    /// Encapsulates the logic necessary to load an image from disk into the system.
    /// </summary>
    public class ImageLoader
    {
        private FileInfo fileInfo;
        private DataEntryHandler dataHandler;
        private TimelapseState state;

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

        public BitmapSource BitmapSource
        {
            get;
            private set;
        }

        public ImageLoader(FileInfo fileInfo, DataEntryHandler dataHandler, TimelapseState state)
        {
            this.fileInfo = fileInfo;
            this.dataHandler = dataHandler;
            this.state = state;
        }

        public async Task<ImageLoader> LoadImageAsync(Action OnImageLoadComplete)
        {
            // First check to see if the file is already in the database. If it is, there's basically nothing to do here.
            // Set the loader's file member.
            this.RequiresDatabaseInsert = !this.dataHandler.FileDatabase.GetOrCreateFile(this.fileInfo, out ImageRow file);

            this.File = file;

            if (this.RequiresDatabaseInsert == true)
            {
                // The image is not already in the database, load the image from the disk

                try
                {
                    // By default, file image quality is ok (i.e., not dark)
                    File.ImageQuality = FileSelectionEnum.Ok;
                    if (this.state.ClassifyDarkImagesWhenLoading == true && this.BitmapSource != Constant.ImageValues.Corrupt.Value)
                    {
                        // Create the bitmap and determine its quality
                        // avoid ImageProperties.LoadImage() here as the create exception needs to surface to set the image quality to corrupt
                        // framework bug: WriteableBitmap.Metadata returns null rather than metatada offered by the underlying BitmapFrame, so 
                        // retain the frame and pass its metadata to TryUseImageTaken().
                        // PERFORMANCE Loading a bitmap is an expensive operation (~24 ms while stepping through the debugger) as it has to be done on every image. 
                        // If larger images are used, it could be even slower. I'm not sure if there is better way of loading bitmaps. This is also complicated by 
                        // caching issues: if the loadbitmap keeps a handle to the image, it means that image cannot be deleted later via an Edit|Delete options. 

                        var loadResult = await File.LoadBitmapAsync(this.FolderPath, ImageDisplayIntentEnum.TransientLoading);

                        this.BitmapSource = loadResult.Item1;

                        File.ImageQuality = FileSelectionEnum.Ok;

                        // Dark Image Classification during loading if the automatically classify dark images option is set  
                        // Bug: invoking GetImageQuality here (i.e., on initial image loading ) would sometimes crash the system on older machines/OS, 
                        // likely due to some threading issue that I can't debug.
                        // This is caught by GetImageQuality, where it signals failure by returning ImageSelection.Corrupted
                        // As this is a non-deterministic failure (i.e., there may be nothing wrong with the image), we try to resolve this failure by restarting the loop.
                        // We will do try this at most MAX_RETRIES per image, after which we will just skip it and set the ImageQuality to Ok.
                        // Yup, its a hack, and there is a small chance that the failure may overwrite some vital memory, but not sure what else to do besides banging my head against the wall.
                        const int MAX_RETRIES = 3;
                        int retries_attempted = 0;
                        // PERFORMANCE: Dark Image Classification slows things down even further, as this method determines whether an image is a dark (nighttime) shot by examining image pixels against a threshold.
                        // The good news is that most users don't need this - the use case is for those who put their camera on 'timelapse' vs. 'motion detection' mode,
                        // and want to filter out the night-time shots. Thus while we could get performance gain by optimizing the 'GetImageQuality' method below,
                        // a better use of time is to optimize the loop in other places. Users can also classify dark images any time later. via an option on the Edit menu
                        File.ImageQuality = this.BitmapSource.AsWriteable().GetImageQuality(this.state.DarkPixelThreshold, this.state.DarkPixelRatioThreshold);

                        // We don't check videos for darkness, so set it as Unknown.
                        if (File.IsVideo)
                        {
                            File.ImageQuality = FileSelectionEnum.Ok;
                        }
                        else
                        {
                            while (File.ImageQuality == FileSelectionEnum.Corrupted && retries_attempted < MAX_RETRIES)
                            {
                                // See what images were retried
                                TraceDebug.PrintMessage("Retrying dark image classification : " + retries_attempted.ToString() + " " + fileInfo);
                                retries_attempted++;
                                File.ImageQuality = this.BitmapSource.AsWriteable().GetImageQuality(this.state.DarkPixelThreshold, this.state.DarkPixelRatioThreshold);
                            }
                            if (retries_attempted == MAX_RETRIES && File.ImageQuality == FileSelectionEnum.Corrupted)
                            {
                                // We've reached the maximum number of retires. Give up, and just set the image quality (perhaps incorrectly) to ok
                                File.ImageQuality = FileSelectionEnum.Ok;
                            }
                        }
                    }
                    else
                    {
                        // Not opening things to check for dark images at this time, but still need to report some progress.

                        var loadResult = await File.LoadBitmapAsync(this.FolderPath, ImageDisplayIntentEnum.TransientLoading);
                        this.BitmapSource = loadResult.Item1;
                    }
                }
                catch (Exception exception)
                {
                    // We couldn't manage the image for whatever reason, so mark it as corrupted.
                    TraceDebug.PrintMessage(String.Format("Load of {0} failed as it's likely corrupted, in TryBeginImageFolderLoadAsync. {1}", File.File, exception.ToString()));
                    this.BitmapSource = Constant.ImageValues.Corrupt.Value;
                    File.ImageQuality = FileSelectionEnum.Ok;
                }

                // Try to update the datetime (which is currently recorded as the file's date) with the metadata date time the image was taken instead
                // We only do this for files, as videos do not have these metadata fields
                // PERFORMANCE Trying to read the date/time from the image data also seems like a somewhat expensive operation. 
                File.TryReadDateTimeOriginalFromMetadata(this.FolderPath, this.ImageSetTimeZone);

            }

            // This completes processing, but it may be some time before the task is checked for completion.
            // for purposes of reporting progress, call the completion delegate provided.

            if (OnImageLoadComplete != null)
            {
                OnImageLoadComplete();
            }

            return this;
        }
    }
}
