using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Exif.Makernotes;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;
using Timelapse.Enums;
using Timelapse.Images;
using Timelapse.Util;
using Directory = System.IO.Directory;
using MetadataDirectory = MetadataExtractor.Directory;

namespace Timelapse.Database
{
    /// <summary>
    /// A row in the file database representing a single image or video.
    /// </summary>
    public class ImageRow : DataRowBackedObject
    {
        public ImageRow(DataRow row)
            : base(row)
        {
        }

        public string Date  
        {
            get { return this.Row.GetStringField(Constant.DatabaseColumn.Date); }
            private set { this.Row.SetField(Constant.DatabaseColumn.Date, value); }
        }

        public DateTime DateTime
        {
            get { return this.Row.GetDateTimeField(Constant.DatabaseColumn.DateTime); }
            private set { this.Row.SetField(Constant.DatabaseColumn.DateTime, value); }
        }

        public bool DeleteFlag
        {
            get { return this.Row.GetBooleanField(Constant.DatabaseColumn.DeleteFlag); }
            set { this.Row.SetField(Constant.DatabaseColumn.DeleteFlag, value); }
        }

        public string FileName
        {
            get { return this.Row.GetStringField(Constant.DatabaseColumn.File); }
            set { this.Row.SetField(Constant.DatabaseColumn.File, value); }
        }

        public FileSelectionEnum ImageQuality
        {
            get
            {
                return this.Row.GetEnumField<FileSelectionEnum>(Constant.DatabaseColumn.ImageQuality);
            }
            set
            {
                switch (value)
                {
                    case FileSelectionEnum.Corrupted:
                    case FileSelectionEnum.Dark:
                    case FileSelectionEnum.Missing:
                    case FileSelectionEnum.Ok:
                        this.Row.SetField<FileSelectionEnum>(Constant.DatabaseColumn.ImageQuality, value);
                        break;
                    default:
                        TraceDebug.PrintMessage(String.Format("Value: {0} is not an ImageQuality.  ImageQuality must be one of CorruptFile, Dark, FileNoLongerAvailable, or Ok.", value));
                        throw new ArgumentOutOfRangeException("value", String.Format("{0} is not an ImageQuality.  ImageQuality must be one of CorruptFile, Dark, FileNoLongerAvailable, or Ok.", value));
                }
            }
        }

        public virtual bool IsVideo
        {
            get { return false; }
        }

        public string InitialRootFolderName
        {
            get { return this.Row.GetStringField(Constant.DatabaseColumn.Folder); }
            set { this.Row.SetField(Constant.DatabaseColumn.Folder, value); }
        }

        public string RelativePath
        {
            get { return this.Row.GetStringField(Constant.DatabaseColumn.RelativePath); }
            set { this.Row.SetField(Constant.DatabaseColumn.RelativePath, value); }
        }

        public string Time
        {
            get { return this.Row.GetStringField(Constant.DatabaseColumn.Time); }
            private set { this.Row.SetField(Constant.DatabaseColumn.Time, value); }
        }

        public TimeSpan UtcOffset
        {
            get { return this.Row.GetUtcOffsetField(Constant.DatabaseColumn.UtcOffset); }
            private set { this.Row.SetUtcOffsetField(Constant.DatabaseColumn.UtcOffset, value); }
        }

        public override ColumnTuplesWithWhere GetColumnTuples()
        {
            ColumnTuplesWithWhere columnTuples = this.GetDateTimeColumnTuples();
            columnTuples.Columns.Add(new ColumnTuple(Constant.DatabaseColumn.File, this.FileName));
            columnTuples.Columns.Add(new ColumnTuple(Constant.DatabaseColumn.ImageQuality, this.ImageQuality.ToString()));
            columnTuples.Columns.Add(new ColumnTuple(Constant.DatabaseColumn.Folder, this.InitialRootFolderName));
            columnTuples.Columns.Add(new ColumnTuple(Constant.DatabaseColumn.RelativePath, this.RelativePath));
            return columnTuples;
        }

        public ColumnTuplesWithWhere GetDateTimeColumnTuples()
        {
            List<ColumnTuple> columnTuples = new List<ColumnTuple>(3)
            {
                new ColumnTuple(Constant.DatabaseColumn.Date, this.Date),
                new ColumnTuple(Constant.DatabaseColumn.DateTime, this.DateTime),
                new ColumnTuple(Constant.DatabaseColumn.Time, this.Time),
                new ColumnTuple(Constant.DatabaseColumn.UtcOffset, this.UtcOffset)
            };
            return new ColumnTuplesWithWhere(columnTuples, this.ID);
        }

        public DateTimeOffset GetDateTime()
        {
            return DateTimeHandler.FromDatabaseDateTimeOffset(this.DateTime, this.UtcOffset); 
        }

        public string GetDisplayDateTime()
        {
            return DateTimeHandler.ToDisplayDateTimeString(this.GetDateTime());
        }

        public FileInfo GetFileInfo(string rootFolderPath)
        {
            return new FileInfo(this.GetFilePath(rootFolderPath));
        }

        public string GetFilePath(string rootFolderPath)
        {
            // see RelativePath remarks in constructor
            if (String.IsNullOrEmpty(this.RelativePath))
            {
                return Path.Combine(rootFolderPath, this.FileName);
            }
            return Path.Combine(rootFolderPath, this.RelativePath, this.FileName);
        }

        public string GetValueDatabaseString(string dataLabel)
        {
            switch (dataLabel)
            {
                case Constant.DatabaseColumn.DateTime:
                    return DateTimeHandler.ToDatabaseDateTimeString(this.DateTime);
                default:
                    return this.GetValueDisplayString(dataLabel);
            }
        }

        public string GetValueDisplayString(string dataLabel)
        {
            switch (dataLabel)
            {
                case Constant.DatabaseColumn.DateTime:
                    return this.GetDisplayDateTime();
                case Constant.DatabaseColumn.UtcOffset:
                    return DateTimeHandler.ToDatabaseUtcOffsetString(this.UtcOffset);
                case Constant.DatabaseColumn.ImageQuality:
                    return this.ImageQuality.ToString();
                default:
                    return this.Row.GetStringField(dataLabel);
            }
        }

        public bool IsDisplayable()
        {
            if (this.ImageQuality == FileSelectionEnum.Corrupted || this.ImageQuality == FileSelectionEnum.Missing)
            {
                return false;
            }
            return true;
        }

        // Load defaults to full size image, and to Persistent (as its safer)
        public BitmapSource LoadBitmap(string baseFolderPath)
        {
            return this.LoadBitmap(baseFolderPath, null, ImageDisplayIntentEnum.Persistent);
        }

        // Load defaults to Persistent (as its safer)
        public virtual BitmapSource LoadBitmap(string baseFolderPath, Nullable<int> desiredWidth)
        {
            return this.LoadBitmap(baseFolderPath, desiredWidth, ImageDisplayIntentEnum.Persistent);
        }

        // Load defaults to thumbnail size if we are TransientNavigating, else full size
        public virtual BitmapSource LoadBitmap(string baseFolderPath, ImageDisplayIntentEnum imageExpectedUsage)
        {
            if (imageExpectedUsage == ImageDisplayIntentEnum.TransientNavigating)
            { 
                return this.LoadBitmap(baseFolderPath, Constant.ImageValues.ThumbnailWidth, imageExpectedUsage);
            }
            else
            {
                return this.LoadBitmap(baseFolderPath, null, imageExpectedUsage);
            }
        }

        // Load full form
        public virtual BitmapSource LoadBitmap(string baseFolderPath, Nullable<int> desiredWidth, ImageDisplayIntentEnum displayIntent)
        {
            // If its a transient image, BitmapCacheOption of None as its faster than OnLoad. 
            // TODOSAUL: why isn't the other case, ImageDisplayIntent.TransientNavigating, also treated as transient?
            BitmapCacheOption bitmapCacheOption = (displayIntent == ImageDisplayIntentEnum.TransientLoading) ? BitmapCacheOption.None : BitmapCacheOption.OnLoad;
            string path = this.GetFilePath(baseFolderPath);
            if (!File.Exists(path))
            {
                 return Constant.ImageValues.FileNoLongerAvailable.Value;
            }
            try
            {
                // TODO DISCRETIONARY: Look at CA1001 https://msdn.microsoft.com/en-us/library/ms182172.aspx as a different strategy
                // Scanning through images with BitmapCacheOption.None results in less than 6% CPU in BitmapFrame.Create() and
                // 90% in System.Windows.Application.Run(), suggesting little scope for optimization within Timelapse proper
                // this is significantly faster than BitmapCacheOption.Default
                // However, using BitmapCacheOption.None locks the file as it is being accessed (rather than a memory copy being created when using a cache)
                // This means we cannot do any file operations on it as it will produce an access violation.
                // For now, we use the (slower) form of BitmapCacheOption.OnLoad.
                // SAULXXX ADD: To CHECK OUT AND MAYBE TRY https://stackoverflow.com/questions/1684489/how-do-you-make-sure-wpf-releases-large-bitmapsource-from-memory 
                // SAULXXX ALSO CHECK http://faithlife.codes/blog/2010/07/exceptions_thrown_by_bitmapimage_and_bitmapframe/ 
                if (desiredWidth.HasValue == false)
                {
                    BitmapFrame frame = BitmapFrame.Create(new Uri(path), BitmapCreateOptions.None, bitmapCacheOption);
                    frame.Freeze();
                    return frame;
                }

                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.DecodePixelWidth = desiredWidth.Value;
                bitmap.CacheOption = bitmapCacheOption;
                bitmap.UriSource = new Uri(path);
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch (Exception exception)
            {
                // Optional messages for eventual debugging of catch errors, 
                if (exception is InsufficientMemoryException)
                {
                    TraceDebug.PrintMessage(String.Format("ImageRow/LoadBitmap: General exception: {0}\n.** Insufficient Memory Exception: {1}.\n--------------\n**StackTrace: {2}.\nXXXXXXXXXXXXXX\n\n", this.FileName, exception.Message, exception.StackTrace));
                }
                else
                { 
                    TraceDebug.PrintMessage(String.Format("ImageRow/LoadBitmap: General exception: {0}\n.**Exception: {1}.\n--------------\n**StackTrace: {2}.\nXXXXXXXXXXXXXX\n\n", this.FileName, exception.Message, exception.StackTrace));
                }
                return Constant.ImageValues.Corrupt.Value;
            }
        }

        public void SetDateTimeOffset(DateTimeOffset dateTime)
        {
            this.Date = DateTimeHandler.ToDisplayDateString(dateTime);
            this.DateTime = dateTime.UtcDateTime;
            this.UtcOffset = dateTime.Offset;
            this.Time = DateTimeHandler.ToDisplayTimeString(dateTime);
        }

        public void SetDateTimeOffsetFromFileInfo(string folderPath)
        {
            // populate new image's default date and time
            // Typically the creation time is the time a file was created in the local file system and the last write time when it was
            // last modified ever in any file system.  So, for example, copying an image from a camera's SD card to a computer results
            // in the image file on the computer having a write time which is before its creation time.  Check both and take the lesser 
            // of the two to provide a best effort default.  In most cases it's desirable to see if a more accurate time can be obtained
            // from the image's EXIF metadata.
            FileInfo fileInfo = this.GetFileInfo(folderPath);
            DateTime earliestTimeLocal = fileInfo.CreationTime < fileInfo.LastWriteTime ? fileInfo.CreationTime : fileInfo.LastWriteTime;
            this.SetDateTimeOffset(new DateTimeOffset(earliestTimeLocal));
        }

        public void SetValueFromDatabaseString(string dataLabel, string value)
        {
            switch (dataLabel)
            {
                case Constant.DatabaseColumn.DateTime:
                    this.DateTime = DateTimeHandler.ParseDatabaseDateTimeString(value);
                    break;
                case Constant.DatabaseColumn.UtcOffset:
                    this.UtcOffset = DateTimeHandler.ParseDatabaseUtcOffsetString(value);
                    break;
                case Constant.DatabaseColumn.ImageQuality:
                    this.ImageQuality = (FileSelectionEnum)Enum.Parse(typeof(FileSelectionEnum), value);
                    break;
                default:
                    this.Row.SetField(dataLabel, value);
                    break;
            }
        }

        /// <summary>
        /// Move the file in the backup folder
        /// </summary>
        public bool TryMoveFileToDeletedFilesFolder(string folderPath)
        {
            string sourceFilePath = this.GetFilePath(folderPath);
            if (!File.Exists(sourceFilePath))
            {
                return false;  // If there is no source file, its a missing file so we can't back it up
            }

            // Create a new target folder, if necessary.
            string deletedFilesFolderPath = Path.Combine(folderPath, Constant.File.DeletedFilesFolder);
            if (!Directory.Exists(deletedFilesFolderPath))
            {
                Directory.CreateDirectory(deletedFilesFolderPath);
            }

            // Move the file to the backup location.           
            string destinationFilePath = Path.Combine(deletedFilesFolderPath, this.FileName);
            if (File.Exists(destinationFilePath))
            {
                try
                {
                    // Because move doesn't allow overwriting, delete the destination file if it already exists.
                    File.Delete(sourceFilePath);
                    return true;
                }
                catch (UnauthorizedAccessException exception)
                {
                    TraceDebug.PrintMessage("Could not delete " + sourceFilePath + Environment.NewLine + exception.Message + ": " + exception.ToString());
                    return false;
                }
            }
            try
            {
                File.Move(sourceFilePath, destinationFilePath);
                return true;
            }
            catch (UnauthorizedAccessException exception)
            {
                TraceDebug.PrintMessage("Could not move " + sourceFilePath + Environment.NewLine + exception.Message + ": " + exception.ToString());
                return false;
            }
        }

        public DateTimeAdjustmentEnum TryReadDateTimeOriginalFromMetadata(string folderPath, TimeZoneInfo imageSetTimeZone)
        {
            // SAULXX Fails on reading metadata dates on video files. See ToDo.
            try
            {
                IList<MetadataDirectory> metadataDirectories = ImageMetadataReader.ReadMetadata(this.GetFilePath(folderPath));
                ExifSubIfdDirectory exifSubIfd = metadataDirectories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
                if (exifSubIfd == null)
                {
                    return DateTimeAdjustmentEnum.MetadataNotUsed;
                }
                if (exifSubIfd.TryGetDateTime(ExifSubIfdDirectory.TagDateTimeOriginal, out DateTime dateTimeOriginal) == false)
                {
                    ReconyxHyperFireMakernoteDirectory reconyxMakernote = metadataDirectories.OfType<ReconyxHyperFireMakernoteDirectory>().FirstOrDefault();
                    if ((reconyxMakernote == null) || (reconyxMakernote.TryGetDateTime(ReconyxHyperFireMakernoteDirectory.TagDateTimeOriginal, out dateTimeOriginal) == false))
                    {
                        return DateTimeAdjustmentEnum.MetadataNotUsed;
                    }
                }
                DateTimeOffset exifDateTime = DateTimeHandler.CreateDateTimeOffset(dateTimeOriginal, imageSetTimeZone);

                // get the current date time
                DateTimeOffset currentDateTime = this.GetDateTime();
                // measure the extent to which the file time and 'image taken' metadata are consistent
                bool dateAdjusted = currentDateTime.Date != exifDateTime.Date;
                bool timeAdjusted = currentDateTime.TimeOfDay != exifDateTime.TimeOfDay;
                if (dateAdjusted || timeAdjusted)
                {
                    this.SetDateTimeOffset(exifDateTime);
                }

                // At least with several Bushnell Trophy HD and Aggressor models (119677C, 119775C, 119777C) file times are sometimes
                // indicated an hour before the image taken time during standard time.  This is not known to occur during daylight 
                // savings time and does not occur consistently during standard time.  It is problematic in the sense time becomes
                // scrambled, meaning there's no way to detect and correct cases where an image taken time is incorrect because a
                // daylight-standard transition occurred but the camera hadn't yet been serviced to put its clock on the new time,
                // and needs to be reported separately as the change of day in images taken just after midnight is not an indicator
                // of day-month ordering ambiguity in the image taken metadata.
                bool standardTimeAdjustment = exifDateTime - currentDateTime == TimeSpan.FromHours(1);

                // snap to metadata time and return the extent of the time adjustment
                if (standardTimeAdjustment)
                {
                    return DateTimeAdjustmentEnum.MetadataDateAndTimeOneHourLater;
                }
                if (dateAdjusted && timeAdjusted)
                {
                    return DateTimeAdjustmentEnum.MetadataDateAndTimeUsed;
                }
                if (dateAdjusted)
                {
                    return DateTimeAdjustmentEnum.MetadataDateUsed;
                }
                if (timeAdjusted)
                {
                    return DateTimeAdjustmentEnum.MetadataTimeUsed;
                }
                return DateTimeAdjustmentEnum.SameFileAndMetadataTime;
            }
            catch
            {
                return DateTimeAdjustmentEnum.MetadataNotUsed;
            }
        }
    }
}
