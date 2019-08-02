namespace Timelapse.Data.FileData
{
    using System;
    using System.Collections.Generic;
    using Timelapse.Common;

    public class FileModel
    {
        /// <summary>
        /// The backing variable for the ImageQuality property.
        /// </summary>
        private FileSelectionType imageQuality;

        /// <summary>
        /// Gets or sets the date value.
        /// </summary>
        public string Date { get; set; }

        /// <summary>
        /// Gets or sets the raw datetime value
        /// </summary>
        public DateTime DateTime { get; set; }

        /// <summary>
        /// Gets the formated date time value.
        /// </summary>
        public string DateTimeAsDisplayable
        {
            get { return DateTimeHandler.ToDisplayDateTimeString(this.DateTimeIncorporatingOffset); }
        }

        /// <summary>
        /// Gets the date time offset value.
        /// </summary>
        public DateTimeOffset DateTimeIncorporatingOffset
        {
            get { return DateTimeHandler.FromDatabaseDateTimeIncorporatingOffset(this.DateTime, this.UtcOffset); }
        }

        /// <summary>
        /// Gets or sets if the model has been deleted.
        /// </summary>
        public bool DeleteFlag { get; set; }

        /// <summary>
        /// Gets or sets the file name value.
        /// </summary>
        public string File { get; set; }

        /// <summary>
        /// Gets or sets the folder value.
        /// </summary>
        public string Folder { get; set; }

        /// <summary>
        /// Gets or sets the image quality value.
        /// </summary>
        public FileSelectionType ImageQuality
        {
            get { return this.imageQuality; }

            set
            {
                switch (value)
                {
                    case FileSelectionType.Corrupted:
                    case FileSelectionType.Missing:
                    case FileSelectionType.Ok:
                    case FileSelectionType.Dark:
                        this.imageQuality = value;
                        break;

                    default:
                        TraceDebug.PrintMessage(String.Format("Value: {0} is not an ImageQuality.  ImageQuality must be one of CorruptFile, Dark, FileNoLongerAvailable, or Ok.", value));
                        throw new ArgumentOutOfRangeException("value", String.Format("{0} is not an ImageQuality.  ImageQuality must be one of CorruptFile, Dark, FileNoLongerAvailable, or Ok.", value));
                }
            }
        }

        /// <summary>
        /// Gets or sets the relative path of the image.
        /// </summary>
        public string RelativePath { get; set; }

        /// <summary>
        /// Gets or sets the time value.
        /// </summary>
        public string Time { get; set; }

        /// <summary>
        /// Gets the collection of user defined data for the model.
        /// </summary>
        /// <remarks>This is where the Control based data / values are stored.</remarks>
        public List<UserDefinedFileData> UserDefinedData { get; } = new List<UserDefinedFileData>();

        /// <summary>
        /// Gets or sets the utc offset value.
        /// </summary>
        public TimeSpan UtcOffset { get; set; }
    }
}