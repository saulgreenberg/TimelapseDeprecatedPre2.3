using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Timelapse.Database;
using Timelapse.Util;

namespace Timelapse.UnitTests
{
    public class FileExpectations
    {
        private TimeZoneInfo timeZoneForDateTime;

        public DateTimeOffset DateTime { get; set; }

        public double DarkPixelFraction { get; set; }
  
        public bool DeleteFlag { get; set; }

        public string FileName { get; set; }

        public long ID { get; set; }

        public string InitialRootFolderName { get; set; }

        public bool IsColor { get; set; }

        public FileSelection Quality { get; set; }

        public string RelativePath { get; set; }

        public bool SkipDateTimeVerification { get; set; }

        public Dictionary<string, string> UserDefinedColumnsByDataLabel { get; private set; }

        public FileExpectations(TimeZoneInfo timeZone)
        {
            this.RelativePath = String.Empty;
            this.UserDefinedColumnsByDataLabel = new Dictionary<string, string>();
            this.timeZoneForDateTime = timeZone;
        }

        public FileExpectations(FileExpectations other)
            : this(other.timeZoneForDateTime)
        {
            this.DateTime = other.DateTime;
            this.DarkPixelFraction = other.DarkPixelFraction;
            this.DeleteFlag = other.DeleteFlag;
            this.FileName = other.FileName;
            this.ID = other.ID;
            this.InitialRootFolderName = other.InitialRootFolderName;
            this.IsColor = other.IsColor;
            this.Quality = other.Quality;
            this.RelativePath = other.RelativePath;
            this.SkipDateTimeVerification = other.SkipDateTimeVerification;

            foreach (KeyValuePair<string, string> columnExpectation in other.UserDefinedColumnsByDataLabel)
            {
                this.UserDefinedColumnsByDataLabel.Add(columnExpectation.Key, columnExpectation.Value);
            }
        }

        private DateTimeOffset ConvertDateTimeToTimeZone(TimeZoneInfo imageSetDateTime)
        {
            bool currentlyDaylightSavings = this.timeZoneForDateTime.IsDaylightSavingTime(this.DateTime);
            TimeSpan utcOffsetChange = imageSetDateTime.BaseUtcOffset - this.timeZoneForDateTime.BaseUtcOffset;
            DateTimeOffset expectedDateTime = this.DateTime.ToNewOffset(utcOffsetChange);

            bool newlyDaylightSavings = imageSetDateTime.IsDaylightSavingTime(this.DateTime);
            if (newlyDaylightSavings != currentlyDaylightSavings)
            {
                TimeSpan daylightSavingsAdjustment = TimeSpan.FromHours(-1.0);
                if (newlyDaylightSavings && (currentlyDaylightSavings == false))
                {
                    daylightSavingsAdjustment = daylightSavingsAdjustment.Duration();
                }
                expectedDateTime = expectedDateTime.ToNewOffset(daylightSavingsAdjustment);
            }

            return expectedDateTime;
        }

        public ImageRow GetImageProperties(FileDatabase fileDatabase)
        {
            string imageFilePath;
            if (String.IsNullOrEmpty(this.RelativePath))
            {
                imageFilePath = Path.Combine(fileDatabase.FolderPath, this.FileName);
            }
            else
            {
                imageFilePath = Path.Combine(fileDatabase.FolderPath, this.RelativePath, this.FileName);
            }

            TimeZoneInfo imageSetTimeZone = fileDatabase.ImageSet.GetTimeZone();
            ImageRow imageProperties;
            fileDatabase.GetOrCreateFile(new FileInfo(imageFilePath), imageSetTimeZone, out imageProperties);
            // imageProperties.Date is defaulted by constructor
            // imageProperties.DateTime is defaulted by constructor
            // imageProperties.FileName is set by constructor
            // imageProperties.ID is set when images are refreshed after being added to the database
            // imageProperties.ImageQuality is defaulted by constructor
            // imageProperties.ImageTaken is set by constructor
            // imageProperties.InitialRootFolderName is set by constructor
            // imageProperties.RelativePath is set by constructor
            // imageProperties.Time is defaulted by constructor
            return imageProperties;
        }

        public static DateTimeOffset ParseDateTimeOffsetString(string dateTimeAsString)
        {
            return DateTimeOffset.ParseExact(dateTimeAsString, TestConstant.DateTimeWithOffsetFormat, CultureInfo.InvariantCulture);
        }

        public void Verify(ImageRow image, TimeZoneInfo timeZone)
        {
            // this.DarkPixelFraction isn't applicable
            Assert.IsTrue(image.DateTime.Kind == DateTimeKind.Utc);
            Assert.IsTrue(image.FileName == this.FileName, "{0}: Expected FileName '{1}' but found '{2}'.", this.FileName, this.FileName, image.FileName);
            Assert.IsTrue(image.ID == this.ID, "{0}: Expected ID '{1}' but found '{2}'.", this.FileName, this.ID, image.ID);
            Assert.IsTrue(image.ImageQuality == this.Quality, "{0}: Expected ImageQuality '{1}' but found '{2}'.", this.FileName, this.Quality, image.ImageQuality);
            Assert.IsTrue(image.InitialRootFolderName == this.InitialRootFolderName, "{0}: Expected InitialRootFolderName '{1}' but found '{2}'.", this.FileName, this.InitialRootFolderName, image.InitialRootFolderName);
            // this.IsColor isn't applicable
            Assert.IsTrue(image.RelativePath == this.RelativePath, "{0}: Expected RelativePath '{1}' but found '{2}'.", this.FileName, this.RelativePath, image.RelativePath);
            // this.UserDefinedColumnsByDataLabel isn't current applicable

            // bypass checking of Date and Time properties if requested, for example if the camera didn't generate image taken metadata
            if (this.SkipDateTimeVerification == false)
            {
                DateTimeOffset imageDateTime = image.GetDateTime(timeZone);
                DateTimeOffset expectedDateTime = this.ConvertDateTimeToTimeZone(timeZone);
                Assert.IsTrue(imageDateTime.UtcDateTime == expectedDateTime.UtcDateTime, "{0}: Expected date time '{1}' but found '{2}'.", this.FileName, DateTimeHandler.ToDatabaseDateTimeString(expectedDateTime), DateTimeHandler.ToDatabaseDateTimeString(imageDateTime.UtcDateTime));
                Assert.IsTrue(imageDateTime.Offset == expectedDateTime.Offset, "{0}: Expected date time offset '{1}' but found '{2}'.", this.FileName, expectedDateTime.Offset, imageDateTime.Offset);

                string expectedDate = DateTimeHandler.ToDisplayDateString(expectedDateTime);
                Assert.IsTrue(image.Date == expectedDate, "{0}: Expected Date '{1}' but found '{2}'.", this.FileName, expectedDate, image.Date);
                Assert.IsTrue(image.DateTime == expectedDateTime.UtcDateTime, "{0}: Expected DateTime '{1}' but found '{2}'.", this.FileName, DateTimeHandler.ToDatabaseDateTimeString(expectedDateTime), DateTimeHandler.ToDatabaseDateTimeString(image.DateTime));
                string expectedTime = DateTimeHandler.ToDisplayTimeString(expectedDateTime);
                Assert.IsTrue(image.Time == expectedTime, "{0}: Expected Time '{1}' but found '{2}'.", this.FileName, expectedTime, image.Time);
                Assert.IsTrue(image.UtcOffset == expectedDateTime.Offset, "{0}: Expected UtcOffset '{1}' but found '{2}'.", this.FileName, expectedDateTime.Offset, image.UtcOffset);
            }
        }
    }
}
