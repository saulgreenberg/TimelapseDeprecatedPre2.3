using NReco.VideoConverter;
using System;
using System.Data;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Timelapse.Enums;
using Timelapse.Images;
namespace Timelapse.Database
{
    // A VideoRow is an ImageRow specialized to videos instead of images.
    // In particular, it knows how to retrieve a bitmap from a video file
    // See ImageRow for details
    public class VideoRow : ImageRow
    {
        public VideoRow(DataRow row)
            : base(row)
        {
        }

        // We can't easily tell if a video is displayable. Instead, just see if the file exists.
        public override bool IsDisplayable(string pathToRootFolder)
        {
            return System.IO.File.Exists(Path.Combine(pathToRootFolder, this.RelativePath, this.File));
        }

        // This will be invoked only on a video file, so always returns true
        public override bool IsVideo
        {
            get { return true; }
        }

        // TODO Update so it works with cached thumbnails
        // Get the bitmap representing a video file
        // Note that displayIntent is ignored as it's specific to interaction with WCF's bitmap cache, which doesn't occur in rendering video preview frames (#77, to some exent)
        public override BitmapSource GetBitmapFromFile(string imageFolderPath, Nullable<int> desiredWidth, ImageDisplayIntentEnum displayIntent, out bool isCorruptOrMissing)
        {
            string path = this.GetFilePath(imageFolderPath);
            if (!System.IO.File.Exists(path))
            {
                isCorruptOrMissing = true;
                return Constant.ImageValues.FileNoLongerAvailable.Value;
            }

            try
            {
                //Saul TO DO:
                // Note: not sure of the cost of creating a new converter every time. May be better to reuse it?
                Stream outputBitmapAsStream = new MemoryStream();
                FFMpegConverter ffMpeg = new NReco.VideoConverter.FFMpegConverter();

                // SAULXXX: Experiment with getting smaller thumbnails from FFMpeg. My initial attept (commented out) doesn't seem to make a huge difference.
                // Note that we can use NRECO probe to get the aspect ratio and thus set a reasonable frame size
                //ConvertSettings convertSettings = new ConvertSettings();
                //convertSettings.SetVideoFrameSize(desiredWidth.Value, desiredHeight.Value);
                //ffMpeg.GetVideoThumbnail(path, outputBitmapAsStream, 1, convertSettings);
                ffMpeg.GetVideoThumbnail(path, outputBitmapAsStream);
                outputBitmapAsStream.Position = 0;

                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                if (desiredWidth != null)
                { 
                    bitmap.DecodePixelWidth = desiredWidth.Value;
                }
                bitmap.CacheOption = BitmapCacheOption.None;
                bitmap.StreamSource = outputBitmapAsStream;
                bitmap.EndInit();
                bitmap.Freeze();
                isCorruptOrMissing = false;
                return bitmap;
            }
            catch (FFMpegException e)
            {
                System.Diagnostics.Debug.Print(e.Message);
                // We don't print the exception // (Exception exception)
                // TraceDebug.PrintMessage(String.Format("VideoRow/LoadBitmap: Loading of {0} failed in Video - LoadBitmap. {0}", imageFolderPath));
                isCorruptOrMissing = true;
                return BitmapUtilities.GetBitmapFromFileWithPlayButton("pack://application:,,,/Resources/BlankVideo.jpg", desiredWidth);
            }
        }

        // Return the aspect ratio (as Width/Height) of a bitmap or its placeholder as efficiently as possible
        // Timing tests suggests this can be done very quickly i.e., 0 - 10 msecs
        //public override double GetBitmapAspectRatioFromFile(string rootFolderPath)
        //{
        //    string path = this.GetFilePath(rootFolderPath);
        //    if (!System.IO.File.Exists(path))
        //    {
        //        return Constant.ImageValues.FileNoLongerAvailable.Value.Width / Constant.ImageValues.FileNoLongerAvailable.Value.Height;
        //    }
        //    try
        //    {
        //        // FFMpegConverter ffMpeg = new NReco.VideoInfo.FFProbe();
        //    }
        //    catch
        //    {
        //        return Constant.ImageValues.Corrupt.Value.Width / Constant.ImageValues.Corrupt.Value.Height;
        //    }
        //}
        // This alternate way to get an image from a video file used the media encoder. 
        // While it works, its ~twice as slow as using NRECO FFMPeg
        //public override BitmapSource GetBitmapFromFile(string imageFolderPath, Nullable<int> desiredWidth, ImageDisplayIntentEnum displayIntent, out bool isCorruptOrMissing)
        //{
        //    isCorruptOrMissing = true;
        //    string path = this.GetFilePath(imageFolderPath);
        //    if (!System.IO.File.Exists(path))
        //    {
        //        return Constant.ImageValues.FileNoLongerAvailable.Value;
        //    }

        //    MediaPlayer mediaPlayer = new MediaPlayer
        //    {
        //        Volume = 0.0
        //    };
        //    try
        //    {
        //        // If the thumbnail exists, show that.
        //        string thumbnailpath = Path.Combine(Path.GetDirectoryName(path), Constant.File.VideoThumbnailFolderName, Path.GetFileNameWithoutExtension(path) + Constant.File.JpgFileExtension);
        //        if (System.IO.File.Exists(thumbnailpath))
        //        {
        //            isCorruptOrMissing = false;
        //            return BitmapUtilities.GetBitmapFromFileWithPlayButton(thumbnailpath, desiredWidth);
        //        }

        //        // As there isn't any thumbnail to show, we hae to open the mediaplayer and play it until we actually get a video frame.
        //        // Unfortunately, its very time inefficient...
        //        mediaPlayer.Open(new Uri(path));
        //        mediaPlayer.Play();

        //        // MediaPlayer is not actually synchronous despite exposing synchronous APIs, so wait for it get the video loaded.  Otherwise
        //        // the width and height properties are zero and only black pixels are drawn.  The properties will populate with just a call to
        //        // Open() call but without also Play() only black is rendered

        //        // TODO Rapidly show videos as it is too slow now, where:
        //        // - ONLOAD It currently loads a blank video image when scouring thorugh the videos 
        //        // - Rapid navigation: loads a blank video image in the background, then the video on pause
        //        // - Multiview: very slow as only loads the  video.
        //        // This will be fixed when we pre-process thumbnails
        //        int timesTried = (displayIntent == ImageDisplayIntentEnum.Persistent) ? 1000 : 0;
        //        while ((mediaPlayer.NaturalVideoWidth < 1) || (mediaPlayer.NaturalVideoHeight < 1))
        //        {
        //            // back off briefly to let MediaPlayer do its loading, which typically takes perhaps 75ms
        //            // a brief Sleep() is used rather than Yield() to reduce overhead as 500k to 1M+ yields typically occur
        //            Thread.Sleep(Constant.ThrottleValues.PollIntervalForVideoLoad);
        //            if (timesTried-- <= 0)
        //            {
        //                isCorruptOrMissing = false;
        //                return BitmapUtilities.GetBitmapFromFileWithPlayButton("pack://application:,,,/Resources/BlankVideo.jpg", desiredWidth);
        //            }
        //        }

        //        // sleep one more time as MediaPlayer has a tendency to still return black frames for a moment after the width and height have populated
        //        Thread.Sleep(Constant.ThrottleValues.PollIntervalForVideoLoad);

        //        int pixelWidth = mediaPlayer.NaturalVideoWidth;
        //        int pixelHeight = mediaPlayer.NaturalVideoHeight;
        //        if (desiredWidth.HasValue)
        //        {
        //            double scaling = desiredWidth.Value / (double)pixelWidth;
        //            pixelWidth = (int)(scaling * pixelWidth);
        //            pixelHeight = (int)(scaling * pixelHeight);
        //        }

        //        // set up to render frame from the video
        //        mediaPlayer.Pause();
        //        mediaPlayer.Position = TimeSpan.FromMilliseconds(1.0);

        //        DrawingVisual drawingVisual = new DrawingVisual();
        //        using (DrawingContext drawingContext = drawingVisual.RenderOpen())
        //        {
        //            drawingContext.DrawVideo(mediaPlayer, new Rect(0, 0, pixelWidth, pixelHeight));
        //        }

        //        // render and check for black frame
        //        // it's assumed the camera doesn't yield all black frames
        //        for (int renderAttempt = 1; renderAttempt <= Constant.ThrottleValues.MaximumRenderAttempts; ++renderAttempt)
        //        {
        //            // try render
        //            RenderTargetBitmap renderBitmap = new RenderTargetBitmap(pixelWidth, pixelHeight, 96, 96, PixelFormats.Default);
        //            renderBitmap.Render(drawingVisual);
        //            renderBitmap.Freeze();

        //            // check if render succeeded
        //            // hopefully it did and most of the overhead here is WriteableBitmap conversion though, at 2-3ms for a 1280x720 frame, this 
        //            // is not an especially expensive operation relative to the  O(175ms) cost of this function
        //            WriteableBitmap writeableBitmap = renderBitmap.AsWriteable();
        //            if (writeableBitmap.IsBlack() == false)
        //            {
        //                // if the media player is closed before Render() only black is rendered
        //                // TraceDebug.PrintMessage(String.Format("Video render returned a non-black frame after {0} times.", renderAttempt - 1));
        //                mediaPlayer.Close();
        //                isCorruptOrMissing = false;
        //                return writeableBitmap;
        //            }

        //            // SAULXX: Original version, replaced by the line below, uses linear backoff which is too long on some machines.
        //            // SAULXX:black frame was rendered; apply linear backoff and try again
        //            // SAULXX:Thread.Sleep(TimeSpan.FromMilliseconds(Constant.ThrottleValues.RenderingBackoffTime.TotalMilliseconds * renderAttempt));

        //            // black frame was rendered; backoff slightly to try again
        //            Thread.Sleep(TimeSpan.FromMilliseconds(Constant.ThrottleValues.VideoRenderingBackoffTime.TotalMilliseconds));
        //        }
        //        throw new ApplicationException(String.Format("Limit of {0} render attempts was reached.", Constant.ThrottleValues.MaximumRenderAttempts));
        //    }
        //    catch
        //    {
        //        // We don't print the exception // (Exception exception)
        //        // TraceDebug.PrintMessage(String.Format("VideoRow/LoadBitmap: Loading of {0} failed in Video - LoadBitmap. {0}", imageFolderPath));
        //        return BitmapUtilities.GetBitmapFromFileWithPlayButton("pack://application:,,,/Resources/BlankVideo.jpg", desiredWidth);
        //    }
        //}
    }
}
