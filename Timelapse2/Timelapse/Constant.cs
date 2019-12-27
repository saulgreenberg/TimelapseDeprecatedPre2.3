using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Timelapse.Controls;
using Xceed.Wpf.Toolkit;

namespace Timelapse.Constant
{
    [SuppressMessage("StyleCop.CSharp.OrderingRules", "SA1203:ConstantsMustAppearBeforeFields", Justification = "Reviewed.")]
    public static class AvalonDockValues
    {
        public const double DefaultTimelapseWindowHeight = 900.0;
        public const double DefaultTimelapseWindowWidth = 1350.0;
        public const double FloatingWindowMinimumHeight = 119;
        public const double FloatingWindowMinimumWidth = 275;
        public const double FloatingWindowLimitSizeHeightCorrection = 40.0;
        public const double FloatingWindowLimitSizeWidthCorrection = 18.0;
        public const string FloatingWindowFloatingWidthProperty = "FloatingWidth";
        public const string FloatingWindowFloatingHeightProperty = "FloatingHeight";
        public const string WindowRegistryKeySuffix = "_window";
        public const string WindowMaximizeStateRegistryKeySuffix = "_windowmaximized";
    }

    public static class AvalonLayoutResourcePaths
    {
        public const string DataEntryOnTop = "pack://application:,,/Resources/AvalonLayout_DataEntryOnTop.config";
        public const string DataEntryOnSide = "pack://application:,,/Resources/AvalonLayout_DataEntryOnSide.config";
        public const string DataEntryFloating = "pack://application:,,/Resources/AvalonLayout_DataEntryFloating.config";
    }
    public static class AvalonLayoutTags
    {
        public const string LastUsed = "AvalonLayout_LastUsed";
        public const string DataEntryOnTop = "AvalonLayout_DataEntryOnTop";
        public const string DataEntryOnSide = "AvalonLayout_DataEntryOnSide";
        public const string DataEntryFloating = "AvalonLayout_DataEntryFloating";
        public const string Custom1 = "AvalonLayout_Custom1";
        public const string Custom2 = "AvalonLayout_Custom2";
        public const string Custom3 = "AvalonLayout_Custom3";
    }

    // Boolean - preferred string representations 
    public static class BooleanValue
    {
        public const string True = "true";
        public const string False = "false";
    }

    public static class Control
    {
        // columns unique to the template table
        public const string ControlOrder = "ControlOrder";
        public const string Copyable = "Copyable";     // whether the content of this item should be copied from previous values
        public const string DataLabel = "DataLabel";   // if not empty, its used instead of the label as the header for the column when writing the spreadsheet
        public const string DefaultValue = "DefaultValue"; // a default value for that code
        public const string Label = "Label";           // a label used to describe that code
        public const string List = "List";             // indicates a list of items
        public const string SpreadsheetOrder = "SpreadsheetOrder";
        public const string TextBoxWidth = "TXTBOXWIDTH";  // the width of the textbox
        public const string Tooltip = "Tooltip";       // the tooltip text that describes the code
        public const string Type = "Type";             // the data type
        public const string Visible = "Visible";       // whether an item should be visible (used by standard items)

        // control types
        public const string Counter = "Counter";       // a counter
        public const string FixedChoice = "FixedChoice";  // a fixed choice
        public const string Flag = "Flag";             // A boolean
        public const string Note = "Note";             // A note

        // default data labels
        public const string Choice = "Choice";         // Label for a fixed choice

        // highlight / non-hightlight border thicknesses of a control
        public const double BorderThicknessNormal = 1;
        public const double BorderThicknessHighlight = 3;
        public static readonly SolidColorBrush BorderColorNormal = new SolidColorBrush(Colors.LightBlue);
        public static readonly SolidColorBrush BorderColorHighlight = new SolidColorBrush(Colors.Blue);

        // a minty green
        public static readonly SolidColorBrush CopyableFieldHighlightBrush = new SolidColorBrush(Color.FromArgb(255, 200, 251, 200));
        public static readonly SolidColorBrush QuickPasteFieldHighlightBrush = new SolidColorBrush(Color.FromArgb(255, 200, 251, 200));

        public static readonly ReadOnlyCollection<Type> KeyboardInputTypes = new List<Type>()
            {
                typeof(AutocompleteTextBox), // note controls
                typeof(Calendar),          // date time control
                typeof(CalendarDayButton), // date time control
                typeof(CheckBox),          // flag controls
                typeof(ComboBox),          // choice controls
                typeof(ComboBoxItem),      // choice controls
                typeof(TextBox),           // note controls
                typeof(WatermarkTextBox)   // date time or counter control
            }.AsReadOnly();

        public static readonly ReadOnlyCollection<string> StandardTypes = new List<string>()
            {
                Constant.DatabaseColumn.Date,
                Constant.DatabaseColumn.DateTime,
                Constant.DatabaseColumn.DeleteFlag,
                Constant.DatabaseColumn.File,
                Constant.DatabaseColumn.Folder,
                Constant.DatabaseColumn.ImageQuality,
                Constant.DatabaseColumn.RelativePath,
                Constant.DatabaseColumn.Time,
                Constant.DatabaseColumn.UtcOffset
            }.AsReadOnly();
    }

    // see also ControlLabelStyle and ControlContentStyle
    public static class ControlStyle
    {
        public const string ContainerStyle = "ContainerStyle";
    }

    public static class ControlDefault
    {
        // general defaults
        public const string Value = "";

        // user defined controls
        public const string CounterTooltip = "Click the counter button, then click on the image to count the entity. Or just type in a count";
        public const string CounterValue = "0";              // Default for: counters
        public const int CounterWidth = 30;
        public const string FixedChoiceTooltip = "Choose an item from the menu";
        public const int FixedChoiceWidth = 100;

        public const string FlagTooltip = "Toggle between true and false";
        public const string FlagValue = Constant.BooleanValue.False;             // Default for: flags
        public const int FlagWidth = 20;
        public const string NoteTooltip = "Write a textual note";
        public const int NoteWidth = 100;

        // standard controls
        public const string DateTooltip = "Date taken";
        public const string DateWidth = "85";

        public const string DateTimeTooltip = "Date and time taken (Year-Month-Day Hours:Minutes:Seconds:Milliseconds)";
        public const string DateTimeWidth = "160";

        public const string FileTooltip = "The file name";
        public const string FileWidth = "100";
        public const string RelativePathTooltip = "Path from the folder containing the template and image data files to the file";
        public const string RelativePathWidth = "100";
        public const string FolderTooltip = "Name of the folder originally containing the template and image data files";
        public const string FolderWidth = "100";

        public const string ImageQualityTooltip = "System-determined image quality: Dark if mostly black, otherwise Ok";
        public const string ImageQualityWidth = "85";

        public const string TimeTooltip = "Time taken";
        public const string TimeWidth = "60";

        public const string DeleteFlagLabel = "Delete?";    // a flag data type for marking deletion
        public const string DeleteFlagTooltip = "Mark a file as one to be deleted. You can then confirm deletion through the Edit Menu";

        public const string UtcOffsetTooltip = "Universal Time offset of the time zone for date and time taken";
        public const string UtcOffsetWidth = "60";

        public static readonly DateTimeOffset DateTimeValue = new DateTimeOffset(1900, 1, 1, 12, 0, 0, 0, TimeSpan.Zero);
    }

    public static class ControlMiscellaneous
    {
        public const string EmptyChoiceItem = "<EMPTY>"; // Indicates an empty item included in the choice menu list
    }
    public static class ControlsDeprecated
    {
        // MarkForDeletion data label was split between editor and Timelapse and normalized to DeleteFlag in 2.1.0.4
        public const string MarkForDeletion = "MarkForDeletion";
    }

    public static class DatabaseValues
    {
        // default values
        public const long DefaultFileID = 1;
        public const int DateTimePosition = 4;
        public const string ImageSetDefaultLog = "Add text here";
        public const long ImageSetRowID = 1;
        public const long InvalidID = -1;
        public const int InvalidRow = -1;
        public const int RelativePathPosition = 2;
        public const int RowsPerInsert = 5000;
        public const int UtcOffsetPosition = 5;
        public const string DefaultSortTerms = Constant.DatabaseColumn.ID + "," + Constant.DatabaseColumn.ID + "," + Sql.IntegerType + "," + Constant.BooleanValue.True + ",,," + Constant.BooleanValue.True;
        public const string DefaultQuickPasteXML = "<Entries></Entries>";
        public const string IndexRelativePath = "IndexRelativePath";
        public const string IndexFile = "IndexFile";
        public const string IndexID = "IndexDetectionID";
        public const string IndexDetectionID = "IndexDetectionID";

        // Special characters
        public const char MarkerBar = '|';              // Separator used to separate marker points in the database i.e. "2.3,5.6 | 7.1, 3.3"
    }

    // Names of standard database columns, always included but not always made visible in the user controls
    public static class DatabaseColumn
    {
        public const string ID = "Id";

        // columns in ImageDataTable
        public const string Date = "Date";
        public const string DateTime = "DateTime";
        public const string File = "File";
        public const string Folder = "Folder";
        public const string ImageQuality = "ImageQuality";
        public const string DeleteFlag = "DeleteFlag";
        public const string RelativePath = "RelativePath";
        public const string Time = "Time";
        public const string UtcOffset = "UtcOffset";

        // columns in ImageSetTable
        public const string Log = "Log";                    // String holding a user-created text log
        public const string MagnifyingGlass = "Magnifier";        // string holding the true/false state of the magnifying glass (on or off)
        public const string MostRecentFileID = "Row";       // ID of the last image displayed. It used to hold the current row #, but we repurposed ROW to  hold the MostRecentFileID as it simplifies backwards compatability
        public const string Selection = "Filter";           // string holding the currently selected selection. For backwards compatability, leave the actual column name as Filter.
        public const string TimeZone = "TimeZone";
        public const string WhiteSpaceTrimmed = "WhiteSpaceTrimmed";        // string holding the true/false state of whether the white space has been trimmed from the data.
        public const string VersionCompatabily = "VersionCompatabily";      // The latest version of Timelapse that opened this database. Useful for cases when we want to check for backwards compatability
        public const string SortTerms = "SortTerms";                     // a comma-separated list that indicates the Primary 1st and 2nd sort terms and their attribute
        public const string QuickPasteXML = "QuickPasteXML";              // an XML description that specifies the user's quickpaste entries and values.
        public const string SelectedFolder = "SelectedFolder";              // a string identifying the folder selected by a user via the Select|Folders menu. Otherwise empty if another selection was done, or if its all files

        // other columns found in Old XML files
        public const string Data = "Data";                 // the data describing the attributes of that control
        public const string Image = "Image";               // A single image and its associated data
        public const string Point = "Point";               // a single point
        public const string X = "X";                       // Every point has an X and Y
        public const string Y = "Y";
    }

    public static class DBTables
    {
        // database table names
        public const string Controls = "TemplateTable"; // the table containing the template data
        public const string FileData = "DataTable";         // the table containing the image data
        public const string ImageSet = "ImageSetTable"; // the table containing information common to the entire image set
        public const string Markers = "MarkersTable";         // the table containing the marker data
        public const string Info = "Info";
        public const string DetectionCategories = "DetectionCategories";
        public const string ClassificationCategories = "ClassificationCategories";
        public const string Images = "Images";
        public const string Detections = "Detections";
        public const string Classifications = "Classifications";
    }

    // Default Settings
    public static class Defaults
    {
        public const string MainWindowBaseTitle = "Timelapse: Helping You Analyze Images and Videos Captured from Field Cameras";
        public const int NumberOfMostRecentDatabasesToTrack = 9;
        public const string StandardColour = "Gold";
        public const string SelectionColour = "MediumBlue";
    }

    public static class EpisodeDefaults
    {
        public static readonly double TimeThresholdDefault = 2; // 2 Minutes
        public static readonly double TimeThresholdMinimum = 0.25; // 15 seconds
        public static readonly double TimeThresholdMaximum = 10; // 10 minutes
        public static readonly int MaxRangeToSearch = 40; // How many files ahead / behind to search for the episode limits
    }

    public static class ExceptionTypes
    {
        public const string TemplateReadWriteException = "TemplateReadWriteException";
    }

    // External URLs
    public static class ExternalLinks
    {
        public static readonly Uri TimlapseVersionChangesLink = new Uri("http://saul.cpsc.ucalgary.ca/timelapse/pmwiki.php?n=Main.TimelapseVersions");
        public static readonly Uri UserManualLink = new Uri("http://saul.cpsc.ucalgary.ca/timelapse/uploads/Installs/Timelapse2/Timelapse2Manual.pdf");
        public static readonly Uri CreativeCommonsLicenseLink = new Uri("https://creativecommons.org/licenses/by-nc-sa/4.0/");
        public static readonly Uri AdditionalLicenseDetailsLink = new Uri("https://github.com/saulgreenberg/TimelapseImageAnalyzer/blob/master/LICENSE.md");
    }

    public static class File
    {
        public const string AviFileExtension = ".avi";
        public const string BackupFolder = "Backups"; // Sub-folder that will contain database and csv file backups  

        public const string CsvFileExtension = ".csv";
        public const string DeletedFilesFolder = "DeletedFiles"; // Sub-folder that will contain backups of deleted images 
        public const string DefaultFileDatabaseFileName = "TimelapseData.ddb";
        public const string DefaultTemplateDatabaseFileName = "TimelapseTemplate.tdb";
        public const string FileDatabaseFileExtension = ".ddb";
        public const string JpgFileExtension = ".jpg";
        public const string JsonFileExtension = ".json";
        public const string Mp4FileExtension = ".mp4";
        public const string MacOSXHiddenFilePrefix = "._";
        public const int NumberOfBackupFilesToKeep = 8; // Maximum number of backup files to keep
        public const string RecognitionDataFileName = "recognitionData.csv";
        public const string RecognitionJsonDataFileName = "recognitionData.json";
        public const string TemplateDatabaseFileExtension = ".tdb";
        public const string VideoThumbnailFolderName = ".vthumb";
        public const string XmlTemplateFileName = "CodeTemplate.xml";
        public const string XmlDataFileName = "ImageData.xml";
        public const int MaxPathLength = 250;
        public const int MaxAdditionalLengthOfBackupFiles = 28;

        public static readonly TimeSpan BackupInterval = TimeSpan.FromMinutes(10);

        public const string TraceFile = "Trace.txt"; // FIle name for file containing debug information. Usually written in the same folder containing the template.
    }

    // Default settings for the FilePlayer
    public static class FilePlayerValues
    {
        public static readonly TimeSpan PlaySlowMinimum = TimeSpan.FromMilliseconds(500.0);
        public static readonly TimeSpan PlaySlowDefault = TimeSpan.FromMilliseconds(500.0);
        public static readonly TimeSpan PlaySlowMaximum = TimeSpan.FromMilliseconds(5000.0);

        public static readonly TimeSpan PlayFastMinimum = TimeSpan.FromMilliseconds(40.0);
        public static readonly TimeSpan PlayFastDefault = TimeSpan.FromMilliseconds(100.0);
        public static readonly TimeSpan PlayFastMaximum = TimeSpan.FromMilliseconds(500.0);
    }

    // shorthands for FileSelection.<value>.ToString()
    public static class ImageQuality
    {
        public const string Ok = "Ok";
        public const string Dark = "Dark";
        public const string Missing = "Missing";
        public const string Corrupted = "Corrupted";
        public const string ListOfValues = "Ok|Dark|Corrupted|Missing";
    }

    public static class ImageValues
    {
        public const int BitmapCacheSize = 9;

        // The default threshold where the ratio of pixels below a given darkness in an image is used to determine whether the image is classified as 'dark'
        public const double DarkPixelRatioThresholdDefault = 0.9;
        public const int DarkPixelSampleStrideDefault = 20;
        // The default threshold where a pixel color should be considered as 'dark' when checking image darkness. The Range is 0  (black) - 255 (white)
        public const int DarkPixelThresholdDefault = 60;

        // The threshold to determine differences between images
        public const int DifferenceThresholdDefault = 20;
        public const byte DifferenceThresholdMax = 255;
        public const byte DifferenceThresholdMin = 0;

        // A greyscale image (given the above slop) will typically have about 90% of its pixels as grey scale
        public const double GreyscaleImageThreshold = 0.9;
        // A grey scale pixel has r = g = b. But we will allow some slop in here just in case a bit of color creeps in
        public const int GreyscalePixelThreshold = 40;

        // The number of images that are considered large enough to deserve special treatment on deletion
        public const int LargeNumberOfDeletedImages = 30;

        // Image sizes
        public const int TooltipWidth = 480;
        public const int ThumbnailWidthWhenNavigating = 300;
        public const int ThumbnailWidth = 128;

        public static readonly Lazy<BitmapImage> Corrupt = ImageValues.Load("Corrupted.jpg");
        public static readonly Lazy<BitmapImage> FileNoLongerAvailable = ImageValues.Load("FileNoLongerAvailable.jpg");
        public static readonly Lazy<BitmapImage> NoFilesAvailable = ImageValues.Load("NoFilesAvailable.jpg");
        public static readonly Lazy<BitmapImage> BlankVideo = ImageValues.Load("BlankVideo.jpg");
        public static readonly Lazy<BitmapImage> BlankVideo512 = ImageValues.Load("BlankVideo512.jpg");
        public static readonly Lazy<BitmapImage> BlankVideo256 = ImageValues.Load("BlankVideo256.jpg");
        public static readonly Lazy<BitmapImage> BlankVideo128 = ImageValues.Load("BlankVideo128.jpg");
        public static readonly Lazy<BitmapImage> FileAlreadyLoaded = ImageValues.Load("FileAlreadyLoaded.jpg");

        private static Lazy<BitmapImage> Load(string fileName)
        {
            return new Lazy<BitmapImage>(() =>
            {
                // if the requested image is available as an application resource, prefer that
                if (Application.Current != null && Application.Current.Resources.Contains(fileName))
                {
                    return (BitmapImage)Application.Current.Resources[fileName];
                }

                // if it's not (editor, resource not listed in App.xaml) fall back to loading from the resources assembly
                BitmapImage image = new BitmapImage();
                image.BeginInit();
                image.UriSource = new Uri("pack://application:,,/Resources/" + fileName);
                image.EndInit();
                image.Freeze();
                return image;
            });
        }
    }
    public static class ImageXml
    {
        // standard elements, always included but not always made visible
        public const string Date = "_Date";
        public const string File = "_File";
        public const string Folder = "_Folder";
        public const string Time = "_Time";

        // paths to standard elements, always included but not always made visible
        public const string DatePath = "Codes/_Date";
        public const string FilePath = "Codes/_File";
        public const string FolderPath = "Codes/_Folder";
        public const string ImageQualityPath = "Codes/_ImageQuality";
        public const string TimePath = "Codes/_Time";

        // elements
        public const string Codes = "Codes";
        public const string Data = "Data";             // the data describing the attributes of that code
        public const string Images = "Images";
        public const string Item = "Item";             // and item in a list
        public const string Slash = "/";

        // paths to notes, counters, and fixed choices
        public const string CounterPath = ImageXml.Codes + ImageXml.Slash + Constant.Control.Counter;
        public const string FixedChoicePath = ImageXml.Codes + ImageXml.Slash + Constant.Control.FixedChoice;
        public const string NotePath = ImageXml.Codes + ImageXml.Slash + Constant.Control.Note;
    }

    public static class MarkableCanvas
    {
        public const double ImageZoomMaximum = 10.0;   // User configurable maximum amount of zoom in a display image
        public const double ImageZoomMaximumRangeAllowed = 50.0;   // the highest zoom a user can configure for a display image
        public const double ImageZoomMinimum = 1.0;   // Minimum amount of zoom
        public const double ImageZoomStep = 1.2;   // Amount to scale on each increment

        public const double MagnifyingGlassDefaultZoom = 60;
        public const double MagnifyingGlassMaximumZoom = 15;  // Max is a smaller number
        public const double MagnifyingGlassMinimumZoom = 100; // Min is the larger number
        public const double MagnifyingGlassZoomIncrement = 1.2;

        public const int MagnifyingGlassDiameter = 250;
        public const int MagnifyingGlassHandleStart = 200;
        public const int MagnifyingGlassHandleEnd = 250;

        public const int MarkerDiameter = 10;
        public const int MarkerGlowDiameterIncrease = 14;
        public const double MarkerGlowOpacity = 0.35;
        public const int MarkerGlowStrokeThickness = 7;
        public const int MarkerStrokeThickness = 2;

        public const string BoundingBoxCanvasTag = "BoundingBoxCanvas";
        public const double BoundingBoxDisplayThresholdDefault = 0.5;

        // Threshold for a double click duration and for differentiating between marking and panning
        public static readonly TimeSpan DoubleClickTimeThreshold = TimeSpan.FromMilliseconds(250.0);
        public static readonly double MarkingVsPanningDistanceThreshold = 2.0;
    }

    // Various keys used to access and save state in the Windows Registry
    public static class WindowRegistryKeys
    {
        // Defines the KEY path under HKEY_CURRENT_USER where Timelapse registry information is stored
        public const string RootKey = @"Software\Greenberg Consulting\Timelapse\2.0";

        // Avalon doc state
        public const string AvalonDockSavedLayout = "AvalonDockSavedLayout";

        // Various Recognition-related values
        public const string BoundingBoxDisplayThreshold = "BoundingBoxDisplayThreshold";
        public const string SpeciesDetectedThreshold = "SpeciesDetectedThreshold";
        public const string UseDetections = "UseDetections";

        // whether audio feedback is enabled
        public const string AudioFeedback = "AudioFeedback";

        // key containing the top left location of the Timelapse Window, as a point
        public const string TimelapseWindowLocation = "TimelapseWindowLocation";
        // key containing the size of the Timelapse Window, as a Height
        public const string TimelapseWindowSize = "TimelapseWindowSize";
        public const string TimelapseWindowPosition = "TimelapseWindowPosition";

        // key containing the position of the QuickPaste window, as a rect
        public const string QuickPasteWindowPosition = "QuickPasteWindowPosition";

        // most recently used operator for custom selections
        public const string CustomSelectionTermCombiningOperator = "CustomSelectionTermCombiningOperator";

        // DarkPixelThreshold and Ratio used to determine image darkness
        public const string DarkPixelThreshold = "DarkPixelThreshold";
        public const string DarkPixelRatio = "DarkPixelRatio";

        // How the DeleteFolder is managed (e.g.,manual, by asking, or automatic deletion)
        public const string DeleteFolderManagementValue = "DeleteFolderManagement";

        public const string EpisodeTimeThreshold = "EpisodeTimeThreshold";

        // File Player play speeds (slow and fast)
        public const string FilePlayerSlowValue = "FilePlayerSlowValue";
        public const string FilePlayerFastValue = "FilePlayerFastValue";

        // Rendering image speed
        public const string DesiredImageRendersPerSecond = "DesiredImageRendersPerSecond";

        // The date/time the last check for timelapse updates was done (used to decide whether to check for updates)
        public const string MostRecentCheckForUpdates = "MostRecentCheckForUpdates";

        // list of most recently image sets opened by Timelapse
        public const string MostRecentlyUsedImageSets = "MostRecentlyUsedImageSets";

        // Set Bookmark scale and transform coordinates
        public const string BookmarkScaleX = "BookmarkScaleX";
        public const string BookmarkScaleY = "BookmarkScaleY";
        public const string BookmarkTranslationX = "BookmarkTransformX";
        public const string BookmarkTranslationY = "BookmarkTransformY";

        // dialog opt outs
        public const string SuppressAmbiguousDatesDialog = "SuppressAmbiguousDatesDialog";
        public const string SuppressCsvExportDialog = "SuppressCsvExportDialog";
        public const string SuppressCsvImportPrompt = "SuppressCsvImportPrompt";
        public const string SuppressFileCountOnImportDialog = "SuppressFileCountOnImportDialog";
        public const string SuppressSelectedAmbiguousDatesPrompt = "SuppressSelectedAmbiguousDatesPrompt";
        public const string SuppressSelectedCsvExportPrompt = "SuppressSelectedCsvExportPrompt";
        public const string SuppressSelectedDarkThresholdPrompt = "SuppressSelectedDarkThresholdPrompt";
        public const string SuppressSelectedDateTimeFixedCorrectionPrompt = "SuppressSelectedDateTimeFixedCorrectionPrompt";
        public const string SuppressSelectedDateTimeLinearCorrectionPrompt = "SuppressSelectedDateTimeLinearCorrectionPrompt";
        public const string SuppressSelectedDaylightSavingsCorrectionPrompt = "SuppressSelectedDaylightSavingsCorrectionPrompt";
        public const string SuppressSelectedPopulateFieldFromMetadataPrompt = "SuppressSelectedPopulateFieldFromMetadataPrompt";
        public const string SuppressSelectedRereadDatesFromFilesPrompt = "SuppressSelectedRereadDatesFromFilesPrompt";
        public const string SuppressSelectedSetTimeZonePrompt = "SuppressSelectedSetTimeZonePrompt";

        // TabOrderInclude
        public const string TabOrderIncludeDateTime = "TabOrderIncludeDateTime";
        public const string TabOrderIncludeDeleteFlag = "TabOrderIncludeDeleteFlag";
        public const string TabOrderIncludeImageQuality = "TabOrderIncludeImageQuality";

    }

    public static class SearchTermOperator
    {
        public const string Equal = "\u003D";
        public const string Glob = " GLOB ";
        public const string GreaterThan = "\u003E";
        public const string GreaterThanOrEqual = "\u2265";
        public const string LessThan = "\u003C";
        public const string LessThanOrEqual = "\u2264";
        public const string NotEqual = "\u2260";
    }

    public static class SortTermValues
    {
        public const string NoneDisplayLabel = "-- None --";
        public const string DateDisplayLabel = "Date/Time";
        public const string FileDisplayLabel = "File Path (relative path + file name)";

        public const string DateStatusBarLabel = "Date/Time{0}";
        public const string FileStatusBarLabel = "File Path{0}";
        public const string IDStatusBarLabel = "Id{0} (the order files were added to Timelapse)";
    }

    public static class ThrottleValues
    {
        public const double DesiredMaximumImageRendersPerSecondLowerBound = 3.0;     // Likely a very safe render rate 
        public const double DesiredMaximumImageRendersPerSecondDefault = 7.0;   // Default render rate - could exhibit stalls on poor machines
        public const double DesiredMaximumImageRendersPerSecondUpperBound = 15.0;    // Somewhat riskier render rate that I know works on high end machines without stuttering
        public const int MaximumRenderAttempts = 10;
        public const int SleepForImageRenderInterval = 100;
        public const int ShowOnlyEveryNthImageWhenLoading = 10;

        public static readonly TimeSpan PollIntervalForVideoLoad = TimeSpan.FromMilliseconds(1.0);
        public static readonly TimeSpan RenderingBackoffTime = TimeSpan.FromMilliseconds(25.0);
        public static readonly TimeSpan VideoRenderingBackoffTime = TimeSpan.FromMilliseconds(10.0);
        public static readonly TimeSpan DataGridTimerInterval = TimeSpan.FromMilliseconds(250);
        public static readonly TimeSpan LoadingImageDisplayInterval = TimeSpan.FromMilliseconds(500);
        public static readonly TimeSpan ProgressBarRefreshInterval = TimeSpan.FromMilliseconds(100);
    }

    public static class Time
    {
        // The standard date format, e.g., 05-Apr-2011
        public const string DateFormat = "dd-MMM-yyyy";
        public const string DateTimeDatabaseFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
        public const string DateTimeDisplayFormat = "dd-MMM-yyyy HH:mm:ss";

        public const int MonthsInYear = 12;
        public const string TimeFormat = "HH:mm:ss";
        public const string TimeSpanDisplayFormat = @"hh\:mm\:ss";
        public const string UtcOffsetDatabaseFormat = "0.00";
        public const string UtcOffsetDisplayFormat = @"hh\:mm";
        public const string VideoPositionFormat = @"mm\:ss";

        public static readonly TimeSpan DateTimeDatabaseResolution = TimeSpan.FromMilliseconds(1.0);
        public static readonly TimeSpan MaximumUtcOffset = TimeSpan.FromHours(14.0);
        public static readonly TimeSpan MinimumUtcOffset = TimeSpan.FromHours(-12.0);
        public static readonly TimeSpan UtcOffsetGranularity = TimeSpan.FromTicks(9000000000); // 15 minutes

        public static readonly string[] DateTimeMetadataFormats =
        {
                // known formats supported by Metadata Extractor
                "yyyy:MM:dd HH:mm:ss.fff",
                "yyyy:MM:dd HH:mm:ss",
                "yyyy:MM:dd HH:mm",
                "yyyy-MM-dd HH:mm:ss",
                "yyyy-MM-dd HH:mm",
                "yyyy.MM.dd HH:mm:ss",
                "yyyy.MM.dd HH:mm",
                "yyyy-MM-ddTHH:mm:ss.fff",
                "yyyy-MM-ddTHH:mm:ss.ff",
                "yyyy-MM-ddTHH:mm:ss.f",
                "yyyy-MM-ddTHH:mm:ss",
                "yyyy-MM-ddTHH:mm.fff",
                "yyyy-MM-ddTHH:mm.ff",
                "yyyy-MM-ddTHH:mm.f",
                "yyyy-MM-ddTHH:mm",
                "yyyy:MM:dd",
                "yyyy-MM-dd",
                "yyyy-MM",
                "yyyy",
                // File.File Modified Date
                "ddd MMM dd HH:mm:ss K yyyy"
            };
    }

    public static class Unicode
    {
        public const string DownArrow = "\u2193";
        public const string Ellipsis = "\u2026";
        public const string UpArrow = "\u2191";
    }

    // Update Information, for checking for updates in the timelapse xml file stored on the web site
    public static class VersionUpdates
    {
        public const string ApplicationName = "Timelapse";
        public static readonly Uri LatestVersionBaseAddress = new Uri("http://saul.cpsc.ucalgary.ca/timelapse/uploads/Installs/timelapse_version.xml");
        public static readonly string LatestVersionFileNamePrefix = "timelapse_version_";
        public static readonly string LatestVersionFileNameSuffix = ".rtf";
        public static readonly Uri LatestVersionFileNameXML = new Uri(VersionUpdates.LatestVersionBaseAddress, "timelapse_version.xml");
    }

    public static class VersionXml
    {
        public const string Changes = "changes";
        public const string Timelapse = "timelapse";
        public const string Url = "url";
        public const string Version = "version";
    }

    // DETECTION Columns and values  
    #region Detection Constants.
    public static class InfoColumns
    {
        public const string InfoID = "infoID";
        public const string Detector = "detector";
        public const string DetectionCompletionTime = "detection_completion_time";
        public const string Classifier = "classifier";
        public const string ClassificationCompletionTime = "classification_completion_time";
    }

    public static class DetectionCategoriesColumns
    {
        public const string Category = "category";
        public const string Label = "label";
    }

    public static class ClassificationCategoriesColumns
    {
        public const string Category = "classification";
        public const string Label = "label";
    }

    public static class DetectionColumns
    {
        public const string DetectionID = "detectionID";
        public const string ImageID = Constant.DatabaseColumn.ID; // Foreign key
        public const string Category = "category";
        public const string Conf = "conf";
        public const string BBox = "bbox";
    }

    public static class DetectionValues
    {
        public const string NoDetectionCategory = "0";
        public const string NoDetectionLabel = "Empty";
        public const string AllDetectionLabel = "All";
    }

    public static class ClassificationColumns
    {
        public const string ClassificationID = "classificationID";
        public const string DetectionID = Constant.DetectionColumns.DetectionID; // Foreign key
        public const string Category = "category";
        public const string Conf = "conf";
    }
    #endregion
}
