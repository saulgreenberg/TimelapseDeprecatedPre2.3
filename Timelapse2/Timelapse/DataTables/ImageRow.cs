﻿using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Exif.Makernotes;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Timelapse.Enums;
using Timelapse.Images;
using Timelapse.Util;
using Directory = System.IO.Directory;
using MetadataDirectory = MetadataExtractor.Directory;

namespace Timelapse.Database
{
    /// <summary>
    /// Represents the data in a row in the file database describing a single image or video.
    /// Also returns the bitmap representing the image
    /// </summary>
    public class ImageRow : DataRowBackedObject
    {
        #region Public Properties - get /set  standard fields from the image row
        public string Date
        {
            get { return this.Row.GetStringField(Constant.DatabaseColumn.Date); }
            private set { this.Row.SetField(Constant.DatabaseColumn.Date, value); }
        }

        // Set/Get the raw datetime value
        public DateTime DateTime
        {
            get { return this.Row.GetDateTimeField(Constant.DatabaseColumn.DateTime); }
            private set { this.Row.SetField(Constant.DatabaseColumn.DateTime, value); }
        }

        // Get a version of the date/time suitable to display to the user 
        public string DateTimeAsDisplayable
        {
            get { return DateTimeHandler.ToStringDisplayDateTime(this.DateTimeIncorporatingOffset); }
        }

        // Get the date/time with the UTC offset added into it
        public DateTimeOffset DateTimeIncorporatingOffset
        {
            get { return DateTimeHandler.FromDatabaseDateTimeIncorporatingOffset(this.DateTime, this.UtcOffset); }
        }

        public bool DeleteFlag
        {
            get { return this.Row.GetBooleanField(Constant.DatabaseColumn.DeleteFlag); }
            set { this.Row.SetField(Constant.DatabaseColumn.DeleteFlag, value); }
        }

        public string File
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
                    case FileSelectionEnum.Missing:
                    case FileSelectionEnum.Ok:
                    case FileSelectionEnum.Dark:
                        this.Row.SetField<FileSelectionEnum>(Constant.DatabaseColumn.ImageQuality, value);
                        break;
                    default:
                        TracePrint.PrintMessage(String.Format("Value: {0} is not an ImageQuality.  ImageQuality must be one of CorruptFile, Dark, FileNoLongerAvailable, or Ok.", value));
                        throw new ArgumentOutOfRangeException(ParamName, String.Format("{0} is not an ImageQuality.  ImageQuality must be one of CorruptFile, Dark, FileNoLongerAvailable, or Ok.", value));
                }
            }
        }

        public string Folder
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
        #endregion

        #region Private Constants
        private const string ParamName = "value";
        #endregion

        #region Constructors
        public ImageRow(DataRow row)
            : base(row)
        {
        }
        #endregion

        #region Public Methods - Various boolean tests
        public bool FileExists(string pathToRootFolder)
        {
            return System.IO.File.Exists(Path.Combine(pathToRootFolder, this.RelativePath, this.File));
        }

        public virtual bool IsDisplayable(string pathToRootFolder)
        {
            return BitmapUtilities.IsBitmapFileDisplayable(Path.Combine(pathToRootFolder, this.RelativePath, this.File));
        }

        // This will be invoked only on an image file, so always returns false
        // That is, if its a video, an VideoRow would have been created and the IsVideo test method in that would have been invoked
        public virtual bool IsVideo
        {
            get { return false; }
        }

        // Check if a datalabel is present in the ImageRow
        public bool Contains(string dataLabel)
        {
            return this.Row.Table.Columns.Contains(dataLabel);
        }
        #endregion

        #region Public Methods - Various Gets

        // Return a FileInfo to the full path of the file
        public FileInfo GetFileInfo(string rootFolderPath)
        {
            return new FileInfo(this.GetFilePath(rootFolderPath));
        }

        // Given the root folder path, 
        // return a full path to the file by combining the root folder path, the relative path, and the file name
        public string GetFilePath(string rootFolderPath)
        {
            // see RelativePath remarks in constructor
            return String.IsNullOrEmpty(this.RelativePath)
                ? Path.Combine(rootFolderPath, this.File)
                : Path.Combine(rootFolderPath, this.RelativePath, this.File);
        }

        // Given a data label, get its value as a string as it exists in the database
        public string GetValueDatabaseString(string dataLabel)
        {
            return (dataLabel == Constant.DatabaseColumn.DateTime)
               ? DateTimeHandler.ToStringDatabaseDateTime(this.DateTime)
               : this.GetValueDisplayString(dataLabel);
        }

        // Given a data label, get its value as a string to display to the user in the UI
        // This requires a few values to be transformed (e.g., DateTime, UTCOffsets, ImageQuality)
        public string GetValueDisplayString(string dataLabel)
        {
            switch (dataLabel)
            {
                case Constant.DatabaseColumn.DateTime:
                    return this.DateTimeAsDisplayable;
                case Constant.DatabaseColumn.UtcOffset:
                    return DateTimeHandler.ToStringDatabaseUtcOffset(this.UtcOffset);
                //return DateTimeHandler.ToStringDisplayUtcOffset(this.UtcOffset);
                case Constant.DatabaseColumn.ImageQuality:
                    return this.ImageQuality.ToString();
                default:
                    return this.Row.GetStringField(dataLabel);
            }
        }
        #endregion

        #region Public Methods -Set Value from database string
        // Set the value for the column identified by its datalabel. 
        // We don't do this directly, as some values have to be converted
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
                    if (Enum.TryParse(value, out FileSelectionEnum result))
                    {
                        // The parse succeeded, where the  result is in result
                        this.ImageQuality = result;
                    }
                    else
                    {
                        // The parse did not succeeded. The result contains the default enum value, ie, the same as returning default(Enum)
                        this.ImageQuality = default;
                    }
                    // this.ImageQuality = (FileSelectionEnum)Enum.Parse(typeof(FileSelectionEnum), value);
                    break;
                default:
                    this.Row.SetField(dataLabel, value);
                    break;
            }
        }
        #endregion

        #region ColumnTuplesWithWhere - Create it based on the stock Image Row values of the current row
        // Build a ColumnTuplesWithWhere containing the stock column values from the current image row  
        // Where identifies the ID of the current image row - note that this is done in the GetDateTimeColumnTuples()
        public override ColumnTuplesWithWhere CreateColumnTuplesWithWhereByID()
        {
            ColumnTuplesWithWhere columnTuples = this.GetDateTimeColumnTuples();
            columnTuples.Columns.Add(new ColumnTuple(Constant.DatabaseColumn.File, this.File));
            columnTuples.Columns.Add(new ColumnTuple(Constant.DatabaseColumn.ImageQuality, this.ImageQuality.ToString()));
            columnTuples.Columns.Add(new ColumnTuple(Constant.DatabaseColumn.Folder, this.Folder));
            columnTuples.Columns.Add(new ColumnTuple(Constant.DatabaseColumn.RelativePath, this.RelativePath));
            return columnTuples;
        }

        // Build a ColumnTuplesWithWhere which will update the various Date / Time / Offset column values 
        // Where identifies the ID of the current image row
        public ColumnTuplesWithWhere GetDateTimeColumnTuples()
        {
            List<ColumnTuple> columnTuples = new List<ColumnTuple>(4)
            {
                new ColumnTuple(Constant.DatabaseColumn.Date, this.Date),
                new ColumnTuple(Constant.DatabaseColumn.DateTime, this.DateTime),
                new ColumnTuple(Constant.DatabaseColumn.Time, this.Time),
                new ColumnTuple(Constant.DatabaseColumn.UtcOffset, this.UtcOffset)
            };
            return new ColumnTuplesWithWhere(columnTuples, this.ID);
        }
        #endregion

        #region DateTime Methods - sets various date-related values, possibly using various transformation 
        public void SetDateTimeOffset(DateTimeOffset dateTime)
        {
            this.Date = DateTimeHandler.ToStringDisplayDate(dateTime);
            this.DateTime = dateTime.UtcDateTime;
            this.UtcOffset = dateTime.Offset;
            this.Time = DateTimeHandler.ToStringDisplayTime(dateTime);
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
        #endregion

        #region Public Methods - Try to Read the Date from the file's Metadata
        public DateTimeAdjustmentEnum TryReadDateTimeOriginalFromMetadata(string folderPath, TimeZoneInfo imageSetTimeZone)
        {
            // Use only on images, as video files don't contain the desired metadata. 
            try
            {
                IReadOnlyList<MetadataDirectory> metadataDirectories = null;
                // PERFORMANCE
                // IReadOnlyList<MetadataDirectory> metadataDirectories = ImageMetadataReader.ReadMetadata(this.GetFilePath(folderPath));

                // Reading in sequential scan, does this speed up? Under the covers, the MetadataExtractor is using a sequential read, allowing skip forward but not random access.
                // Exif is small, do we need a big block?
                using (FileStream fS = new FileStream(this.GetFilePath(folderPath), FileMode.Open, FileAccess.Read, FileShare.Read, 64, FileOptions.SequentialScan))
                {
                    metadataDirectories = ImageMetadataReader.ReadMetadata(fS);
                }

                ExifSubIfdDirectory exifSubIfd = metadataDirectories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
                if (exifSubIfd == null)
                {
                    return DateTimeAdjustmentEnum.MetadataNotUsed;
                }
                if (exifSubIfd.TryGetDateTime(ExifSubIfdDirectory.TagDateTimeOriginal, out DateTime dateTimeOriginal) == false)
                {
                    // We couldn't read the metadata. In case its a reconyx camera, the fallback is to use the Reconyx-specific metadata 
                    ReconyxHyperFireMakernoteDirectory reconyxMakernote = metadataDirectories.OfType<ReconyxHyperFireMakernoteDirectory>().FirstOrDefault();
                    if ((reconyxMakernote == null) || (reconyxMakernote.TryGetDateTime(ReconyxHyperFireMakernoteDirectory.TagDateTimeOriginal, out dateTimeOriginal) == false))
                    {
                        return DateTimeAdjustmentEnum.MetadataNotUsed;
                    }
                }
                DateTimeOffset exifDateTime = DateTimeHandler.CreateDateTimeOffset(dateTimeOriginal, imageSetTimeZone);

                // get the current date time
                DateTimeOffset currentDateTime = this.DateTimeIncorporatingOffset;
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
        #endregion

        #region Delete File
        // Delete the file, where we also try to back it up by moving it into the Deleted folder
        // TODO File deletion backups is problematic as files in different relative paths could have the same file name (overwritting possible, ambiguity). Perhaps mirror the file structure as otherwise a previously deleted file could be overwritten
        // CODECLEANUP Should this method really be part of an image row? 
        public bool TryMoveFileToDeletedFilesFolder(string folderPath)
        {
            string sourceFilePath = this.GetFilePath(folderPath);
            if (!System.IO.File.Exists(sourceFilePath))
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
            string destinationFilePath = Path.Combine(deletedFilesFolderPath, this.File);
            if (System.IO.File.Exists(destinationFilePath))
            {
                try
                {
                    // Because move doesn't allow overwriting, delete the destination file if it already exists.
                    System.IO.File.Delete(sourceFilePath);
                    return true;
                }
                catch (UnauthorizedAccessException exception)
                {
                    TracePrint.PrintMessage("Could not delete " + sourceFilePath + Environment.NewLine + exception.Message + ": " + exception.ToString());
                    return false;
                }
            }
            try
            {
                System.IO.File.Move(sourceFilePath, destinationFilePath);
                return true;
            }
            catch (Exception exception)
            {
                // This may occur if for some reason we could not move the file, for example, if we have loaded the image in a way that it locks the file.
                // I've changed image loading to avoid this, but its something to watch out for.
                TracePrint.PrintMessage("Could not move " + sourceFilePath + Environment.NewLine + exception.Message + ": " + exception.ToString());
                return false;
            }
        }
        #endregion

        #region LoadBitmap - Various Forms
        // LoadBitmap Wrapper: defaults to full size image, Persistent. 
        public BitmapSource LoadBitmap(string baseFolderPath, out bool isCorruptOrMissing)
        {
            // ImageDimension doesn't do anything in this context, as the full size image is returned
            return this.LoadBitmap(baseFolderPath, null, ImageDisplayIntentEnum.Persistent, ImageDimensionEnum.UseWidth, out isCorruptOrMissing);
        }

        // LoadBitmap Wrapper: defaults to Persistent, Decode to the given width
        public virtual BitmapSource LoadBitmap(string baseFolderPath, Nullable<int> desiredWidth, out bool isCorruptOrMissing)
        {
            return this.LoadBitmap(baseFolderPath, desiredWidth, ImageDisplayIntentEnum.Persistent, ImageDimensionEnum.UseWidth, out isCorruptOrMissing);
        }

        // LoadBitmap Wrapper: If Ephemeral, generate a low-res thumbnail suitable for previewing. Otherwise full size
        public virtual BitmapSource LoadBitmap(string baseFolderPath, ImageDisplayIntentEnum imageExpectedUsage, out bool isCorruptOrMissing)
        {
            return this.LoadBitmap(baseFolderPath,
                     imageExpectedUsage == ImageDisplayIntentEnum.Ephemeral ? (int?)Constant.ImageValues.PreviewWidth128 : null,
                     imageExpectedUsage,
                     ImageDimensionEnum.UseWidth,
                     out isCorruptOrMissing);
        }

        /// <summary>
        /// Async Wrapper for LoadBitmap
        /// </summary>
        /// <returns>Tuple of the BitmapSource and boolean isCorruptOrMissing output of the underlying load logic</returns>
        public virtual Task<Tuple<BitmapSource, bool>> LoadBitmapAsync(string baseFolderPath, ImageDisplayIntentEnum imageExpectedUsage, ImageDimensionEnum imageDimension)
        {
            // 'out' arguments not allowed in tasks, so it returns a tuple containg the bitmap and the isCorruptOrMissingflag flag indicating bitmap retrieval state 
            return Task.Run(() =>
            {
                BitmapSource bitmap = this.LoadBitmap(baseFolderPath, imageExpectedUsage == ImageDisplayIntentEnum.Ephemeral ? (int?)Constant.ImageValues.PreviewWidth128 : null,
                                               imageExpectedUsage,
                                               ImageDimensionEnum.UseWidth,
                                               out bool isCorruptOrMissing);
                return Tuple.Create(bitmap, isCorruptOrMissing);
            });
        }

        /// <summary>
        //// Load: Full form
        /// Get a bitmap of the desired width. If its not there or something is wrong it will return a placeholder bitmap displaying the 'error'.
        /// Also sets a flag (isCorruptOrMissing) indicating if the bitmap wasn't retrieved (signalling a placeholder bitmap was returned)
        /// </summary>
        public virtual BitmapSource LoadBitmap(string rootFolderPath, Nullable<int> desiredWidthOrHeight, ImageDisplayIntentEnum displayIntent, ImageDimensionEnum imageDimension, out bool isCorruptOrMissing)
        {
            // Invoke the static version. The only change is that we get the full file path and pass that as a parameter
            return BitmapUtilities.GetBitmapFromImageFile(this.GetFilePath(rootFolderPath), desiredWidthOrHeight, displayIntent, imageDimension, out isCorruptOrMissing);
        }

        // Return the aspect ratio (as Width/Height) of a bitmap or its placeholder as efficiently as possible
        // Timing tests suggests this can be done very quickly i.e., 0 - 10 msecs
        // While this is marked as virtual, there is currently no over-ride for getting it from a video.
        // So it should only be invoked if we know the file is an image
        public virtual double GetBitmapAspectRatioFromFile(string rootFolderPath)
        {
            return BitmapUtilities.GetBitmapAspectRatioFromImageFile(this.GetFilePath(rootFolderPath));
        }
        #endregion
    }
}
