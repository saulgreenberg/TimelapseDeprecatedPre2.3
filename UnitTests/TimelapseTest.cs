using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using Timelapse.Database;
using Timelapse.Util;

namespace Timelapse.UnitTests
{
    public class TimelapseTest
    {
        protected TimelapseTest()
        {
            this.WorkingDirectory = Environment.CurrentDirectory;

            // Constants.Images needs to load resources from Carnassial.exe and falls back to Application.ResourceAssembly if Application.Current isn't set
            // for unit tests neither Current or ResourceAssembly gets set as Carnassial.exe is not the entry point
            if (Application.ResourceAssembly == null)
            {
                Application.ResourceAssembly = typeof(Constant.Images).Assembly;
            }
        }

        public TestContext TestContext { get; set; }

        /// <summary>
        /// Gets the name of an optional subdirectory under the working directory from which all tests in the current class access database and image files.
        /// Classes wishing to use this mechanism should call EnsureTestClassSubdirectory() in their constructor to set up the subdirectory.
        /// </summary>
        protected string TestClassSubdirectory { get; private set; }

        /// <summary>
        /// Gets the path to the root folder under which all tests execute.
        /// </summary>
        protected string WorkingDirectory { get; private set; }

        /// <summary>
        /// Clones the specified template and image databases and opens the image database's clone.
        /// </summary>
        protected FileDatabase CloneFileDatabase(string templateDatabaseBaseFileName, string fileDatabaseFileName)
        {
            return this.CloneFileDatabase(String.Empty, templateDatabaseBaseFileName, fileDatabaseFileName);
        }
        protected FileDatabase CloneFileDatabase(string relativePath, string templateDatabaseBaseFileName, string fileDatabaseFileName)
        {
            TemplateDatabase templateDatabase;
            if (relativePath == String.Empty)
            {
                templateDatabase = this.CloneTemplateDatabase(templateDatabaseBaseFileName);
            }
            else
            {
                templateDatabase = this.CloneTemplateDatabase(relativePath, templateDatabaseBaseFileName);
            }

            string fileDatabaseSourceFilePath = Path.Combine(this.WorkingDirectory, relativePath, fileDatabaseFileName);
            string fileDatabaseCloneFilePath = this.GetUniqueFilePathForTest(fileDatabaseFileName);
            File.Copy(fileDatabaseSourceFilePath, fileDatabaseCloneFilePath, true);

            return FileDatabase.CreateOrOpen(fileDatabaseCloneFilePath, templateDatabase, CustomSelectionOperator.And);
        }

        /// <summary>
        /// Clones the specified template database and opens the clone.
        /// </summary>
        protected TemplateDatabase CloneTemplateDatabase(string templateDatabaseFileName)
        {
            return this.CloneTemplateDatabase(String.Empty, templateDatabaseFileName);
        }

        protected TemplateDatabase CloneTemplateDatabase(string relativePath, string templateDatabaseFileName)
        {
            string templateDatabaseSourceFilePath;
            if (relativePath == String.Empty)
            {
                templateDatabaseSourceFilePath = Path.Combine(this.WorkingDirectory, templateDatabaseFileName);
            }
            else
            { 
                templateDatabaseSourceFilePath = Path.Combine(this.WorkingDirectory, relativePath, templateDatabaseFileName);
            }
            string templateDatabaseCloneFilePath = this.GetUniqueFilePathForTest(templateDatabaseFileName);
            File.Copy(templateDatabaseSourceFilePath, templateDatabaseCloneFilePath, true);

            TemplateDatabase clone;
            bool result = TemplateDatabase.TryCreateOrOpen(templateDatabaseCloneFilePath, out clone);
            Assert.IsTrue(result, "Open of template database '{0}' failed.", templateDatabaseCloneFilePath);
            return clone;
        }

        /// <summary>
        /// Creates an ImageRow from the specified ImageExpectation.  Verifies the image isn't already in the database and does not add it to the database.
        /// </summary>
        protected ImageRow CreateFile(FileDatabase fileDatabase, TimeZoneInfo imageSetTimeZone, FileExpectations imageExpectation, out DateTimeAdjustment imageAdjustment)
        {
            FileInfo imageFileInfo = new FileInfo(Path.Combine(this.WorkingDirectory, imageExpectation.RelativePath, imageExpectation.FileName));
            ImageRow image;
            Assert.IsFalse(fileDatabase.GetOrCreateFile(imageFileInfo, imageSetTimeZone, out image));
            imageAdjustment = image.TryReadDateTimeOriginalFromMetadata(fileDatabase.FolderPath, imageSetTimeZone);
            return image;
        }

        /// <summary>
        /// Clones the specified template database and creates an image database unique to the calling test.
        /// </summary>
        protected FileDatabase CreateFileDatabase(string templateDatabaseBaseFileName, string fileDatabaseBaseFileName)
        {
            return this.CreateFileDatabase(String.Empty, templateDatabaseBaseFileName, fileDatabaseBaseFileName);
        }
        protected FileDatabase CreateFileDatabase(string relativePath, string templateDatabaseBaseFileName, string fileDatabaseBaseFileName)
        {
            TemplateDatabase templateDatabase = this.CloneTemplateDatabase(relativePath, templateDatabaseBaseFileName);
            return this.CreateFileDatabase(templateDatabase, fileDatabaseBaseFileName);
        }

        /// <summary>
        /// Creates an image database unique to the calling test.
        /// </summary>
        protected FileDatabase CreateFileDatabase(TemplateDatabase templateDatabase, string fileDatabaseBaseFileName)
        {
            string fileDatabaseFilePath = this.GetUniqueFilePathForTest(fileDatabaseBaseFileName);
            if (File.Exists(fileDatabaseFilePath))
            {
                File.Delete(fileDatabaseFilePath);
            }

            return FileDatabase.CreateOrOpen(fileDatabaseFilePath, templateDatabase, CustomSelectionOperator.And);
        }

        /// <summary>
        /// Creates a template database unique to the calling test.
        /// </summary>
        protected TemplateDatabase CreateTemplateDatabase(string templateDatabaseBaseFileName)
        {
            // remove any previously existing database
            string templateDatabaseFilePath = this.GetUniqueFilePathForTest(templateDatabaseBaseFileName);
            if (File.Exists(templateDatabaseFilePath))
            {
                File.Delete(templateDatabaseFilePath);
            }

            // create the new database
            return TemplateDatabase.CreateOrOpen(templateDatabaseFilePath);
        }

        protected void EnsureTestClassSubdirectory()
        {
            // ensure subdirectory exists
            this.TestClassSubdirectory = this.GetType().Name;
            string subdirectoryPath = Path.Combine(this.WorkingDirectory, this.TestClassSubdirectory);
            if (Directory.Exists(subdirectoryPath) == false)
            {
                Directory.CreateDirectory(subdirectoryPath);
            }

            // ensure subdirectory contains default images
            List<string> defaultImages = new List<string>()
            {
                Path.Combine(TestConstant.ImageExpectation.BushnellTrophyHDAggressor2.RelativePath, TestConstant.ImageExpectation.BushnellTrophyHDAggressor2.FileName),
                Path.Combine(TestConstant.ImageExpectation.BushnellTrophyHDAggressor3.RelativePath, TestConstant.ImageExpectation.BushnellTrophyHDAggressor3.FileName),
            };

            foreach (string imageFileName in defaultImages)
            {
                FileInfo sourceImageFile = new FileInfo(Path.Combine(this.WorkingDirectory, imageFileName));
                FileInfo destinationImageFile = new FileInfo(Path.Combine(this.WorkingDirectory, this.TestClassSubdirectory, Path.GetFileName(imageFileName)));
                if (destinationImageFile.Exists == false ||
                    destinationImageFile.LastWriteTimeUtc < sourceImageFile.LastWriteTimeUtc)
                {
                    sourceImageFile.CopyTo(destinationImageFile.FullName, true);
                }
            }
        }

        protected Dictionary<string, Metadata> LoadMetadata(FileDatabase fileDatabase, FileExpectations imageExpectation)
        {
            string imageFilePath = Path.Combine(this.WorkingDirectory, imageExpectation.RelativePath, imageExpectation.FileName);
            Dictionary<string, Metadata> metadata;
            metadata = MetadataDictionary.LoadMetadata(imageFilePath);
            Assert.IsTrue(metadata.Count > 40, "Expected at least 40 metadata fields to be retrieved from {0}", imageFilePath);
            // example information returned from ExifTool
            // field name                   Bushnell                                    Reconyx
            // --- fields of likely interest for image analysis ---
            // Create Date                  2016.02.24 04:59:46                         [Bushnell only]
            // Date/Time Original           2016.02.24 04:59:46                         2015.01.28 11:17:34 [but located in Makernote rather than the standard EXIF field!]
            // Modify Date                  2016.02.24 04:59:46                         [Bushnell only]
            // --- fields possibly of interest interest for image analysis ---
            // Exposure Time                0                                           1/84
            // Shutter Speed                0                                           1/84
            // Software                     BS683BWYx05209                              [Bushnell only]
            // Firmware Version             [Reconyx only]                              3.3.0
            // Trigger Mode                 [Reconyx only]                              Motion Detection
            // Sequence                     [Reconyx only]                              1 of 5
            // Ambient Temperature          [Reconyx only]                              0 C
            // Serial Number                [Reconyx only]                              H500DE01120343
            // Infrared Illuminator         [Reconyx only]                              Off
            // User Label                   [Reconyx only]                              CCPF DS02
            // --- fields of little interest (Bushnell often uses placeholder values which don't change) ---
            // ExifTool Version Number      10.14                                       10.14
            // File Name                    BushnellTrophyHD-119677C-20160224-056.JPG   Reconyx-HC500-20150128-201.JPG
            // Directory                    Timelapse/UnitTests/bin/Debug               Timelapse/UnitTests/bin/Debug/TimelapseTestImages
            // File Size                    731 kB                                      336 kB
            // File Modification Date/Time  <file time from last build>                 <file time from last build>
            // File Access Date/Time        <file time from last build>                 <file time from last build>
            // File Creation Date/Time      <file time from last build>                 <file time from last build>
            // File Permissions             rw-rw-rw-                                   rw-rw-rw-
            // File Type                    JPEG                                        JPEG
            // File Type Extension          jpg                                         jpg
            // MIME Type                    image/jpeg                                  image/jpeg
            // Exif Byte Order              Little-endian(Intel, II)                    Little-endian(Intel, II)
            // Image Description            M2E6L0-0R350B362                            [Bushnell only]
            // Make                         [blank]                                     [Bushnell only]
            // Camera Model Name            [blank]                                     [Bushnell only]
            // Orientation                  Horizontal(normal)                          [Bushnell only]
            // X Resolution                 96                                          72
            // Y Resolution                 96                                          72
            // Resolution Unit              inches                                      inches
            // Y Cb Cr Positioning          Co-sited                                    Co-sited
            // Copyright                    Copyright 2002                              [Bushnell only]
            // F Number                     2.8                                         [Bushnell only]
            // Exposure Program             Aperture-priority AE                        [Bushnell only]
            // ISO                          100                                         50
            // Exif Version                 0210                                        0220
            // Components Configuration     Y, Cb, Cr, -                                Y, Cb, Cr, -
            // Compressed Bits Per Pixel    0.7419992711                                [Bushnell only]
            // Shutter Speed Value          1                                           [Bushnell only]
            // Aperture Value               2.6                                         [Bushnell only]
            // Exposure Compensation        +2                                          [Bushnell only]
            // Max Aperture Value           2.6                                         [Bushnell only]
            // Metering Mode                Average                                     [Bushnell only]
            // Light Source                 Daylight                                    [Bushnell only]
            // Flash                        No Flash                                    [Bushnell only]
            // Warning                      [minor]Unrecognized MakerNotes              [Bushnell only]
            // Flashpix Version             0100                                        0100
            // Color Space                  sRGB                                        sRGB
            // Exif Image Width             3264                                        2048
            // Exif Image Height            2448                                        1536
            // Related Sound File           RelatedSound                                [Bushnell only]
            // Interoperability Index       R98 - DCF basic file(sRGB)                  [Bushnell only]
            // Interoperability Version     0100                                        [Bushnell only]
            // Exposure Index               1                                           [Bushnell only]
            // Sensing Method               One-chip color area                         [Bushnell only]
            // File Source                  Digital Camera                              [Bushnell only]
            // Scene Type                   Directly photographed                       [Bushnell only]
            // Compression                  JPEG(old - style)                           [Bushnell only]
            // Thumbnail Offset             1312                                        [Bushnell only]
            // Thumbnail Length             5768                                        [Bushnell only]
            // Image Width                  3264                                        2048
            // Image Height                 2448                                        1536
            // Encoding Process             Baseline DCT, Huffman coding                Baseline DCT, Huffman coding
            // Bits Per Sample              8                                           8
            // Color Components             3                                           3
            // Y Cb Cr Sub Sampling         YCbCr4:2:2 (2 1)                            YCbCr4:2:2 (2 1)
            // Aperture                     2.8                                         [Bushnell only]
            // Image Size                   3264x2448                                   2048x1536
            // Megapixels                   8.0                                         3.1
            // Thumbnail Image              <binary data>                               [Bushnell only]
            // Ambient Temperature Fahrenheit [Reconyx only]                            31 F
            // Battery Voltage              [Reconyx only]                              8.65 V
            // Contrast                     [Reconyx only]                              142
            // Brightness                   [Reconyx only]                              238
            // Event Number                 [Reconyx only]                              39
            // Firmware Date                [Reconyx only]                              2011:01:10
            // Maker Note Version           [Reconyx only]                              0xf101
            // Moon Phase                   [Reconyx only]                              First Quarter
            // Motion Sensitivity           [Reconyx only]                              100
            // Sharpness                    [Reconyx only]                              64
            // Saturation                   [Reconyx only]                              144
            return metadata;
        }

        protected List<FileExpectations> PopulateCarnivoreDatabase(FileDatabase fileDatabase)
        {
            TimeZoneInfo imageSetTimeZone = fileDatabase.ImageSet.GetTimeZone();

            DateTimeAdjustment martenTimeAdjustment;
            ImageRow martenImage = this.CreateFile(fileDatabase, imageSetTimeZone, TestConstant.ImageExpectation.ReconyxHC500Hyperfire, out martenTimeAdjustment);
            Assert.IsTrue(martenTimeAdjustment == DateTimeAdjustment.MetadataDateAndTimeUsed);

            DateTimeAdjustment coyoteTimeAdjustment;
            ImageRow coyoteImage = this.CreateFile(fileDatabase, imageSetTimeZone, TestConstant.ImageExpectation.BushnellTrophyHDAggressor1, out coyoteTimeAdjustment);
            Assert.IsTrue(coyoteTimeAdjustment == DateTimeAdjustment.MetadataDateAndTimeUsed);

            fileDatabase.AddFiles(new List<ImageRow>() { martenImage, coyoteImage }, null);
            fileDatabase.SelectFiles(FileSelection.All);

            FileTableEnumerator imageEnumerator = new FileTableEnumerator(fileDatabase);
            Assert.IsTrue(imageEnumerator.TryMoveToFile(0));
            Assert.IsTrue(imageEnumerator.MoveNext());

            // cover CSV or image XML import path
            ColumnTuplesWithWhere coyoteImageUpdate = new ColumnTuplesWithWhere();
            coyoteImageUpdate.Columns.Add(new ColumnTuple("Station", "DS02"));
            coyoteImageUpdate.Columns.Add(new ColumnTuple("TriggerSource", "critter"));
            coyoteImageUpdate.Columns.Add(new ColumnTuple("Identification", "coyote"));
            coyoteImageUpdate.Columns.Add(new ColumnTuple("Confidence", "high"));
            coyoteImageUpdate.Columns.Add(new ColumnTuple("GroupType", "single"));
            coyoteImageUpdate.Columns.Add(new ColumnTuple("Age", "adult"));
            coyoteImageUpdate.Columns.Add(new ColumnTuple("Pelage", String.Empty));
            coyoteImageUpdate.Columns.Add(new ColumnTuple("Activity", "unknown"));
            coyoteImageUpdate.Columns.Add(new ColumnTuple("Comments", "escaped field, because a comma is present"));
            coyoteImageUpdate.Columns.Add(new ColumnTuple("Survey", "Timelapse carnivore database unit tests"));
            coyoteImageUpdate.SetWhere(imageEnumerator.Current.ID);
            fileDatabase.UpdateFiles(new List<ColumnTuplesWithWhere>() { coyoteImageUpdate });

            // simulate data entry
            long martenImageID = fileDatabase.Files[0].ID;
            fileDatabase.UpdateFile(martenImageID, "Station", "DS02");
            fileDatabase.UpdateFile(martenImageID, "TriggerSource", "critter");
            fileDatabase.UpdateFile(martenImageID, "Identification", "American marten");
            fileDatabase.UpdateFile(martenImageID, "Confidence", "high");
            fileDatabase.UpdateFile(martenImageID, "GroupType", "pair");
            fileDatabase.UpdateFile(martenImageID, "Age", "adult");
            fileDatabase.UpdateFile(martenImageID, "Pelage", String.Empty);
            fileDatabase.UpdateFile(martenImageID, "Activity", "unknown");
            fileDatabase.UpdateFile(martenImageID, "Comments", "escaped field due to presence of \",\"");
            fileDatabase.UpdateFile(martenImageID, "Survey", "Timelapse carnivore database unit tests");

            // pull the image data table again so the updates are visible to .csv export
            fileDatabase.SelectFiles(FileSelection.All);

            // generate expectations
            List<FileExpectations> imageExpectations = new List<FileExpectations>()
            {
                new FileExpectations(TestConstant.ImageExpectation.ReconyxHC500Hyperfire),
                new FileExpectations(TestConstant.ImageExpectation.BushnellTrophyHDAggressor1)
            };

            string initialRootFolderName = Path.GetFileName(fileDatabase.FolderPath);
            for (int image = 0; image < fileDatabase.Files.RowCount; ++image)
            {
                FileExpectations imageExpectation = imageExpectations[image];
                imageExpectation.ID = image + 1;
                imageExpectation.InitialRootFolderName = initialRootFolderName;
            }

            return imageExpectations;
        }

        protected List<FileExpectations> PopulateDefaultDatabase(FileDatabase fileDatabase)
        {
            TimeZoneInfo imageSetTimeZone = fileDatabase.ImageSet.GetTimeZone();

            DateTimeAdjustment martenTimeAdjustment;
            ImageRow martenImage = this.CreateFile(fileDatabase, imageSetTimeZone, TestConstant.ImageExpectation.BushnellTrophyHDAggressor2, out martenTimeAdjustment);
            Assert.IsTrue(martenTimeAdjustment == DateTimeAdjustment.MetadataDateAndTimeOneHourLater ||
                          martenTimeAdjustment == DateTimeAdjustment.MetadataDateAndTimeUsed);

            DateTimeAdjustment bobcatTimeAdjustment;
            ImageRow bobcatImage = this.CreateFile(fileDatabase, imageSetTimeZone, TestConstant.ImageExpectation.BushnellTrophyHDAggressor3, out bobcatTimeAdjustment);
            Assert.IsTrue(bobcatTimeAdjustment == DateTimeAdjustment.MetadataDateAndTimeUsed);

            fileDatabase.AddFiles(new List<ImageRow>() { martenImage, bobcatImage }, null);
            fileDatabase.SelectFiles(FileSelection.All);

            FileTableEnumerator imageEnumerator = new FileTableEnumerator(fileDatabase);
            Assert.IsTrue(imageEnumerator.TryMoveToFile(0));
            Assert.IsTrue(imageEnumerator.MoveNext());

            // cover CSV or image XML import path
            ColumnTuplesWithWhere bobcatUpdate = new ColumnTuplesWithWhere();
            bobcatUpdate.Columns.Add(new ColumnTuple(TestConstant.DefaultDatabaseColumn.Choice0, "choice b"));
            bobcatUpdate.Columns.Add(new ColumnTuple(TestConstant.DefaultDatabaseColumn.Counter0, 1.ToString()));
            bobcatUpdate.Columns.Add(new ColumnTuple(TestConstant.DefaultDatabaseColumn.FlagNotVisible, true));
            bobcatUpdate.Columns.Add(new ColumnTuple(TestConstant.DefaultDatabaseColumn.Note3, "bobcat"));
            bobcatUpdate.Columns.Add(new ColumnTuple(TestConstant.DefaultDatabaseColumn.NoteNotVisible, "adult"));
            bobcatUpdate.SetWhere(imageEnumerator.Current.ID);
            fileDatabase.UpdateFiles(new List<ColumnTuplesWithWhere>() { bobcatUpdate });

            // simulate data entry
            long martenImageID = fileDatabase.Files[0].ID;
            fileDatabase.UpdateFile(martenImageID, TestConstant.DefaultDatabaseColumn.Choice0, "choice b");
            fileDatabase.UpdateFile(martenImageID, TestConstant.DefaultDatabaseColumn.Counter0, 1.ToString());
            fileDatabase.UpdateFile(martenImageID, TestConstant.DefaultDatabaseColumn.FlagNotVisible, Constant.Boolean.True);
            fileDatabase.UpdateFile(martenImageID, TestConstant.DefaultDatabaseColumn.Note3, "American marten");
            fileDatabase.UpdateFile(martenImageID, TestConstant.DefaultDatabaseColumn.NoteNotVisible, "adult");

            // pull the image data table again so the updates are visible to .csv export
            fileDatabase.SelectFiles(FileSelection.All);

            // generate expectations
            List<FileExpectations> imageExpectations = new List<FileExpectations>()
            {
                new FileExpectations(TestConstant.ImageExpectation.BushnellTrophyHDAggressor2),
                new FileExpectations(TestConstant.ImageExpectation.BushnellTrophyHDAggressor3)
            };

            string initialRootFolderName = Path.GetFileName(fileDatabase.FolderPath);
            for (int image = 0; image < fileDatabase.Files.RowCount; ++image)
            {
                FileExpectations imageExpectation = imageExpectations[image];
                imageExpectation.ID = image + 1;
                imageExpectation.InitialRootFolderName = initialRootFolderName;
            }
            return imageExpectations;
        }

        protected string GetUniqueFilePathForTest(string baseFileName)
        {
            string uniqueTestIdentifier = String.Format("{0}.{1}", this.GetType().FullName, this.TestContext.TestName);
            string uniqueFileName = String.Format("{0}.{1}{2}", Path.GetFileNameWithoutExtension(baseFileName), uniqueTestIdentifier, Path.GetExtension(baseFileName));

            if (String.IsNullOrWhiteSpace(this.TestClassSubdirectory))
            {
                return Path.Combine(this.WorkingDirectory, uniqueFileName);
            }
            return Path.Combine(this.WorkingDirectory, this.TestClassSubdirectory, uniqueFileName);
        }
    }
}
