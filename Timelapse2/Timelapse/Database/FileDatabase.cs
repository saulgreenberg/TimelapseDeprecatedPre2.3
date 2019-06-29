using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using Timelapse.Detection;
using Timelapse.Dialog;
using Timelapse.Enums;
using Timelapse.Images;
using Timelapse.Util;

namespace Timelapse.Database
{
    public class FileDatabase : TemplateDatabase
    {
        #region Private variables
        private DataGrid boundGrid;
        private bool disposed;
        private DataRowChangeEventHandler onFileDataTableRowChanged;
        // These two dictionaries mirror the contents of the detectionCategory and classificationCategory database table
        // for faster access
        private Dictionary<string, string> detectionCategoriesDictionary = null;
        private Dictionary<string, string> classificationCategoriesDictionary = null;
        private DataTable detectionDataTable; // Mirrors the database detection table
        private DataTable classificationsDataTable; // Mirrors the database classification table
        #endregion

        #region Properties 
        public CustomSelection CustomSelection { get; private set; }

        /// <summary>Gets the file name of the database on disk.</summary>
        public string FileName { get; private set; }

        /// <summary>Gets the complete path to the folder containing the database.</summary>
        public string FolderPath { get; private set; }

        public Dictionary<string, string> DataLabelFromStandardControlType { get; private set; }

        public Dictionary<string, FileTableColumn> FileTableColumnsByDataLabel { get; private set; }

        // contains the results of the data query
        public FileTable FileTable { get; private set; }

        public ImageSetRow ImageSet { get; private set; }

        // contains the markers
        public DataTableBackedList<MarkerRow> Markers { get; private set; }
        #endregion

        #region Create or Open the Database
        private FileDatabase(string filePath)
            : base(filePath)
        {
            this.DataLabelFromStandardControlType = new Dictionary<string, string>();
            this.disposed = false;
            this.FolderPath = Path.GetDirectoryName(filePath);
            this.FileName = Path.GetFileName(filePath);
            this.FileTableColumnsByDataLabel = new Dictionary<string, FileTableColumn>();
        }

        public static FileDatabase CreateOrOpen(string filePath, TemplateDatabase templateDatabase, CustomSelectionOperatorEnum customSelectionTermCombiningOperator, TemplateSyncResults templateSyncResults)
        {
            // check for an existing database before instantiating the database as SQL wrapper instantiation creates the database file
            bool populateDatabase = !File.Exists(filePath);

            FileDatabase fileDatabase = new FileDatabase(filePath);

            if (populateDatabase)
            {
                // initialize the database if it's newly created
                fileDatabase.OnDatabaseCreated(templateDatabase);
            }
            else
            {
                // if it's an existing database check if it needs updating to current structure and load data tables
                fileDatabase.OnExistingDatabaseOpened(templateDatabase, templateSyncResults);
            }

            // ensure all tables have been loaded from the database
            if (fileDatabase.ImageSet == null)
            {
                fileDatabase.GetImageSet();
            }
            if (fileDatabase.Markers == null)
            {
                fileDatabase.GetMarkers();
            }
            fileDatabase.CustomSelection = new CustomSelection(fileDatabase.Controls, customSelectionTermCombiningOperator);
            fileDatabase.PopulateDataLabelMaps();
            return fileDatabase;
        }

        public static string TryGetQuickPasteXMLFromDatabase(string filePath)
        {
            // Open the database if it exists
            SQLiteWrapper sqliteWrapper = new SQLiteWrapper(filePath);
            if (sqliteWrapper.IsColumnInTable(Constant.DatabaseTable.ImageSet, Constant.DatabaseColumn.QuickPasteXML) == false)
            {
                // The column isn't in the table, so give up
                return String.Empty;
            }

            List<object> listOfObjects = sqliteWrapper.GetDistinctValuesInColumn(Constant.DatabaseTable.ImageSet, Constant.DatabaseColumn.QuickPasteXML);
            if (listOfObjects.Count == 1)
            {
                return (string)listOfObjects[0];
            }
            return String.Empty;
        }

        /// <summary>
        /// Make an empty Data Table based on the information in the Template Table.
        /// Assumes that the database has already been opened and that the Template Table is loaded, where the DataLabel always has a valid value.
        /// Then create both the ImageSet table and the Markers table
        /// </summary>
        protected override void OnDatabaseCreated(TemplateDatabase templateDatabase)
        {
            // copy the template's TemplateTable
            base.OnDatabaseCreated(templateDatabase);

            // Create the DataTable from the template
            // First, define the creation string based on the contents of the template. 
            List<ColumnDefinition> columnDefinitions = new List<ColumnDefinition>
            {
                new ColumnDefinition(Constant.DatabaseColumn.ID, Constant.Sqlite.CreationStringPrimaryKey)  // It begins with the ID integer primary key
            };
            foreach (ControlRow control in this.Controls)
            {
                columnDefinitions.Add(CreateFileDataColumnDefinition(control));
            }
            this.Database.CreateTable(Constant.DatabaseTable.FileData, columnDefinitions);

            // SAULXX THIS IS IN TODDs - Don't uncomment it until we figure out how he uses it. He creates it but I am unsure if he actually uses it
            // Index the DateTime column
            // this.Database.ExecuteNonQuery("CREATE INDEX 'FileDateTimeIndex' ON 'FileData' ('DateTime')");

            // Create the ImageSetTable and initialize a single row in it
            columnDefinitions.Clear();
            columnDefinitions.Add(new ColumnDefinition(Constant.DatabaseColumn.ID, Constant.Sqlite.CreationStringPrimaryKey));  // It begins with the ID integer primary key
            columnDefinitions.Add(new ColumnDefinition(Constant.DatabaseColumn.Log, Constant.Sqlite.Text, Constant.DatabaseValues.ImageSetDefaultLog));
            columnDefinitions.Add(new ColumnDefinition(Constant.DatabaseColumn.MagnifyingGlass, Constant.Sqlite.Text, Constant.BooleanValue.True));
            columnDefinitions.Add(new ColumnDefinition(Constant.DatabaseColumn.MostRecentFileID, Constant.Sqlite.Text));
            int allImages = (int)FileSelectionEnum.All;
            columnDefinitions.Add(new ColumnDefinition(Constant.DatabaseColumn.Selection, Constant.Sqlite.Text, allImages));
            columnDefinitions.Add(new ColumnDefinition(Constant.DatabaseColumn.WhiteSpaceTrimmed, Constant.Sqlite.Text));
            columnDefinitions.Add(new ColumnDefinition(Constant.DatabaseColumn.TimeZone, Constant.Sqlite.Text));
            columnDefinitions.Add(new ColumnDefinition(Constant.DatabaseColumn.VersionCompatabily, Constant.Sqlite.Text));  // Records the highest Timelapse version number ever used to open this database
            columnDefinitions.Add(new ColumnDefinition(Constant.DatabaseColumn.SortTerms, Constant.Sqlite.Text));        // A comma-separated list of 4 sort terms
            columnDefinitions.Add(new ColumnDefinition(Constant.DatabaseColumn.SelectedFolder, Constant.Sqlite.Text));
            columnDefinitions.Add(new ColumnDefinition(Constant.DatabaseColumn.QuickPasteXML, Constant.Sqlite.Text));        // A comma-separated list of 4 sort terms

            this.Database.CreateTable(Constant.DatabaseTable.ImageSet, columnDefinitions);

            // Populate the data for the image set with defaults
            // VersionCompatabily
            Version timelapseCurrentVersionNumber = VersionClient.GetTimelapseCurrentVersionNumber();
            List<ColumnTuple> columnsToUpdate = new List<ColumnTuple>
            {
                new ColumnTuple(Constant.DatabaseColumn.Log, Constant.DatabaseValues.ImageSetDefaultLog),
                new ColumnTuple(Constant.DatabaseColumn.MagnifyingGlass, Constant.BooleanValue.True),
                new ColumnTuple(Constant.DatabaseColumn.MostRecentFileID, Constant.DatabaseValues.InvalidID),
                new ColumnTuple(Constant.DatabaseColumn.Selection, allImages.ToString()),
                new ColumnTuple(Constant.DatabaseColumn.WhiteSpaceTrimmed, Constant.BooleanValue.True),
                new ColumnTuple(Constant.DatabaseColumn.TimeZone, TimeZoneInfo.Local.Id),
                new ColumnTuple(Constant.DatabaseColumn.VersionCompatabily, timelapseCurrentVersionNumber.ToString()),
                new ColumnTuple(Constant.DatabaseColumn.SortTerms, Constant.DatabaseValues.DefaultSortTerms),
                new ColumnTuple(Constant.DatabaseColumn.QuickPasteXML, Constant.DatabaseValues.DefaultQuickPasteXML)
            };
            List<List<ColumnTuple>> insertionStatements = new List<List<ColumnTuple>>
            {
                columnsToUpdate
            };
            this.Database.Insert(Constant.DatabaseTable.ImageSet, insertionStatements);

            this.GetImageSet();

            // create the Files table
            // This is necessary as files can't be added unless the Files Column is available.  Thus SelectFiles() has to be called after the ImageSetTable is created
            // so that the selection can be persisted.
            this.SelectFiles(FileSelectionEnum.All);

            // Create the MarkersTable and initialize it from the template table
            columnDefinitions.Clear();
            columnDefinitions.Add(new ColumnDefinition(Constant.DatabaseColumn.ID, Constant.Sqlite.CreationStringPrimaryKey));  // It begins with the ID integer primary key
            foreach (ControlRow control in this.Controls)
            {
                if (control.Type.Equals(Constant.Control.Counter))
                {
                    columnDefinitions.Add(new ColumnDefinition(control.DataLabel, Constant.Sqlite.Text, String.Empty));
                }
            }
            this.Database.CreateTable(Constant.DatabaseTable.Markers, columnDefinitions);
        }
        #endregion

        public List<object> GetDistinctValuesInColumn(string table, string columnName)
        {
            return this.Database.GetDistinctValuesInColumn(table, columnName);
        }

        /// <summary>Gets the number of files currently in the file table.</summary>
        public int CurrentlySelectedFileCount
        {
            get { return (this.FileTable == null) ? 0 : this.FileTable.RowCount; }
        }

        #region Adding Files to the Database
        public void AddFiles(List<ImageRow> files, Action<ImageRow, int> onFileAdded)
        {
            // We need to get a list of which columns are counters vs notes or fixed coices, 
            // as we will shortly have to initialize them to some defaults
            List<string> counterList = new List<string>();
            List<string> notesAndFixedChoicesList = new List<string>();
            List<string> flagsList = new List<string>();
            foreach (string columnName in this.FileTable.ColumnNames)
            {
                if (columnName == Constant.DatabaseColumn.ID)
                {
                    // skip the ID column as it's not associated with a data label and doesn't need to be set as it's autoincrement
                    continue;
                }

                string controlType = this.FileTableColumnsByDataLabel[columnName].ControlType;
                if (controlType.Equals(Constant.Control.Counter))
                {
                    counterList.Add(columnName);
                }
                else if (controlType.Equals(Constant.Control.Note) || controlType.Equals(Constant.Control.FixedChoice))
                {
                    notesAndFixedChoicesList.Add(columnName);
                }
                else if (controlType.Equals(Constant.Control.Flag))
                {
                    flagsList.Add(columnName);
                }
            }

            // Create a dataline from each of the image properties, add it to a list of data lines,
            // then do a multiple insert of the list of datalines to the database 
            for (int image = 0; image < files.Count; image += Constant.DatabaseValues.RowsPerInsert)
            {
                // Create a dataline from the image properties, add it to a list of data lines,
                // then do a multiple insert of the list of datalines to the database 
                List<List<ColumnTuple>> fileDataRows = new List<List<ColumnTuple>>();
                List<List<ColumnTuple>> markerRows = new List<List<ColumnTuple>>();
                for (int insertIndex = image; (insertIndex < (image + Constant.DatabaseValues.RowsPerInsert)) && (insertIndex < files.Count); insertIndex++)
                {
                    List<ColumnTuple> imageRow = new List<ColumnTuple>();
                    List<ColumnTuple> markerRow = new List<ColumnTuple>();
                    foreach (string columnName in this.FileTable.ColumnNames)
                    {
                        // Fill up each column in order
                        if (columnName == Constant.DatabaseColumn.ID)
                        {
                            // don't specify an ID in the insert statement as it's an autoincrement primary key
                            continue;
                        }

                        string controlType = this.FileTableColumnsByDataLabel[columnName].ControlType;
                        ImageRow imageProperties = files[insertIndex];
                        switch (controlType)
                        {
                            case Constant.DatabaseColumn.File: // Add The File name
                                string dataLabel = this.DataLabelFromStandardControlType[Constant.DatabaseColumn.File];
                                imageRow.Add(new ColumnTuple(dataLabel, imageProperties.File));
                                break;
                            case Constant.DatabaseColumn.RelativePath: // Add the relative path name
                                dataLabel = this.DataLabelFromStandardControlType[Constant.DatabaseColumn.RelativePath];
                                imageRow.Add(new ColumnTuple(dataLabel, imageProperties.RelativePath));
                                break;
                            case Constant.DatabaseColumn.Folder: // Add The Folder name
                                dataLabel = this.DataLabelFromStandardControlType[Constant.DatabaseColumn.Folder];
                                imageRow.Add(new ColumnTuple(dataLabel, imageProperties.Folder));
                                break;
                            case Constant.DatabaseColumn.Date:
                                // Add the date
                                dataLabel = this.DataLabelFromStandardControlType[Constant.DatabaseColumn.Date];
                                imageRow.Add(new ColumnTuple(dataLabel, imageProperties.Date));
                                break;
                            case Constant.DatabaseColumn.DateTime:
                                dataLabel = this.DataLabelFromStandardControlType[Constant.DatabaseColumn.DateTime];
                                imageRow.Add(new ColumnTuple(dataLabel, imageProperties.DateTime));
                                break;
                            case Constant.DatabaseColumn.UtcOffset:
                                dataLabel = this.DataLabelFromStandardControlType[Constant.DatabaseColumn.UtcOffset];
                                imageRow.Add(new ColumnTuple(dataLabel, imageProperties.UtcOffset));
                                break;
                            case Constant.DatabaseColumn.Time:
                                // Add the time
                                dataLabel = this.DataLabelFromStandardControlType[Constant.DatabaseColumn.Time];
                                imageRow.Add(new ColumnTuple(dataLabel, imageProperties.Time));
                                break;
                            case Constant.DatabaseColumn.ImageQuality: // Add the Image Quality
                                dataLabel = this.DataLabelFromStandardControlType[Constant.DatabaseColumn.ImageQuality];
                                imageRow.Add(new ColumnTuple(dataLabel, imageProperties.ImageQuality.ToString()));
                                break;
                            case Constant.DatabaseColumn.DeleteFlag: // Add the Delete flag
                                dataLabel = this.DataLabelFromStandardControlType[Constant.DatabaseColumn.DeleteFlag];
                                imageRow.Add(new ColumnTuple(dataLabel, this.GetControlDefaultValue(dataLabel))); // Default as specified in the template file, which should be "false"
                                break;
                            case Constant.Control.Note:        // Find and then Add the Note or Fixed Choice
                            case Constant.Control.FixedChoice:
                                // Now initialize notes, counters, and fixed choices to the defaults
                                foreach (string controlName in notesAndFixedChoicesList)
                                {
                                    if (columnName.Equals(controlName))
                                    {
                                        imageRow.Add(new ColumnTuple(controlName, this.GetControlDefaultValue(controlName))); // Default as specified in the template file
                                    }
                                }
                                break;
                            case Constant.Control.Flag:
                                // Now initialize flags to the defaults
                                foreach (string controlName in flagsList)
                                {
                                    if (columnName.Equals(controlName))
                                    {
                                        imageRow.Add(new ColumnTuple(controlName, this.GetControlDefaultValue(controlName))); // Default as specified in the template file
                                    }
                                }
                                break;
                            case Constant.Control.Counter:
                                foreach (string controlName in counterList)
                                {
                                    if (columnName.Equals(controlName))
                                    {
                                        imageRow.Add(new ColumnTuple(controlName, this.GetControlDefaultValue(controlName))); // Default as specified in the template file
                                        markerRow.Add(new ColumnTuple(controlName, String.Empty));
                                    }
                                }
                                break;

                            default:
                                TraceDebug.PrintMessage(String.Format("Unhandled control type '{0}' in AddImages.", controlType));
                                break;
                        }
                    }
                    fileDataRows.Add(imageRow);
                    if (markerRow.Count > 0)
                    {
                        markerRows.Add(markerRow);
                    }
                }

                this.CreateBackupIfNeeded();
                this.InsertRows(Constant.DatabaseTable.FileData, fileDataRows);
                this.InsertRows(Constant.DatabaseTable.Markers, markerRows);

                if (onFileAdded != null)
                {
                    int lastImageInserted = Math.Min(files.Count - 1, image + Constant.DatabaseValues.RowsPerInsert);
                    onFileAdded.Invoke(files[lastImageInserted], lastImageInserted);
                }
            }

            // Load / refresh the marker table from the database to keep it in sync - Doing so here will make sure that there is one row for each image.
            this.GetMarkers();
        }
        #endregion
        public void AppendToImageSetLog(StringBuilder logEntry)
        {
            this.ImageSet.Log += logEntry;
            this.SyncImageSetToDatabase();
        }

        public void BindToDataGrid(DataGrid dataGrid, DataRowChangeEventHandler onRowChanged)
        {
            if (this.FileTable == null)
            {
                return;
            }
            this.boundGrid = dataGrid;
            this.onFileDataTableRowChanged = onRowChanged;
            this.FileTable.BindDataGrid(dataGrid, onRowChanged);
        }

        #region Upgrade Databases
        public static FileDatabase UpgradeDatabasesAndCompareTemplates(string filePath, TemplateDatabase templateDatabase, TemplateSyncResults templateSyncResults)
        {
            // If the file doesn't exist, then no immediate action is needed
            if (!File.Exists(filePath))
            {
                return null;
            }

            FileDatabase fileDatabase = new FileDatabase(filePath);
            fileDatabase.UpgradeDatabasesAndCompareTemplates(templateDatabase, templateSyncResults);
            return fileDatabase;
        }

        public Dictionary<string, string> GetColumnsAndDefaultValuesFromSchema(string tableName)
        {
            return this.Database.GetColumnsAndDefaultValuesFromSchema(tableName);
        }

        protected override void UpgradeDatabasesAndCompareTemplates(TemplateDatabase templateDatabase, TemplateSyncResults templateSyncResults)
        {
            // perform TemplateTable initializations and migrations, then check for synchronization issues
            base.UpgradeDatabasesAndCompareTemplates(templateDatabase, null);

            // Upgrade the database from older to newer formats to preserve backwards compatability
            this.UpgradeDatabasesForBackwardsCompatability(templateDatabase);

            // Get the datalabels in the various templates 
            Dictionary<string, string> templateDataLabels = templateDatabase.GetTypedDataLabelsExceptIDInSpreadsheetOrder();
            Dictionary<string, string> imageDataLabels = this.GetTypedDataLabelsExceptIDInSpreadsheetOrder();
            templateSyncResults.DataLabelsInTemplateButNotImageDatabase = Utilities.Dictionary1ExceptDictionary2(templateDataLabels, imageDataLabels);
            templateSyncResults.DataLabelsInImageButNotTemplateDatabase = Utilities.Dictionary1ExceptDictionary2(imageDataLabels, templateDataLabels);

            // Check for differences between the TemplateTable in the .tdb and .ddb database.
            bool areNewColumnsInTemplate = templateSyncResults.DataLabelsInTemplateButNotImageDatabase.Count > 0;
            bool areDeletedColumnsInTemplate = templateSyncResults.DataLabelsInImageButNotTemplateDatabase.Count > 0;

            // Synchronization Issues 1: Mismatch control types. Unable to update as there is at least one control type mismatch 
            // We need to check that the dataLabels in the .ddb template are of the same type as those in the .ttd template
            // If they are not, then we need to flag that.
            foreach (string dataLabel in imageDataLabels.Keys)
            {
                // if the .ddb dataLabel is not in the .tdb template, this will be dealt with later 
                if (!templateDataLabels.Keys.Contains(dataLabel))
                {
                    continue;
                }
                ControlRow imageDatabaseControl = this.GetControlFromTemplateTable(dataLabel);
                ControlRow templateControl = templateDatabase.GetControlFromTemplateTable(dataLabel);

                if (imageDatabaseControl.Type != templateControl.Type)
                {
                    templateSyncResults.ControlSynchronizationErrors.Add(String.Format("- The field with DataLabel '{0}' is of type '{1}' in the image data file but of type '{2}' in the template.{3}", dataLabel, imageDatabaseControl.Type, templateControl.Type, Environment.NewLine));
                }

                // Check if  item(s) in the choice list has been removed. If so, a data field set with the removed value will not be displayable
                List<string> imageDatabaseChoices = imageDatabaseControl.GetChoices(false);
                List<string> templateChoices = templateControl.GetChoices(false);
                List<string> choiceValuesRemovedInTemplate = imageDatabaseChoices.Except(templateChoices).ToList<string>();
                if (choiceValuesRemovedInTemplate.Count > 0)
                {
                    // Add warnings due to changes in the Choice control's menu
                    templateSyncResults.ControlSynchronizationWarnings.Add(String.Format("- As the choice control '{0}' no longer includes the following menu items, it can't display data with corresponding values:", dataLabel));
                    templateSyncResults.ControlSynchronizationWarnings.Add(String.Format("   {0}", string.Join<string>(", ", choiceValuesRemovedInTemplate)));
                }

                // Check if there are any other changed values in any of the columns that may affect the UI appearance. If there are, then we need to signal syncing of the template
                if (imageDatabaseControl.ControlOrder != templateControl.ControlOrder ||
                    imageDatabaseControl.SpreadsheetOrder != templateControl.SpreadsheetOrder ||
                    imageDatabaseControl.DefaultValue != templateControl.DefaultValue ||
                    imageDatabaseControl.Label != templateControl.Label ||
                    imageDatabaseControl.Tooltip != templateControl.Tooltip ||
                    imageDatabaseControl.Width != templateControl.Width ||
                    imageDatabaseControl.Copyable != templateControl.Copyable ||
                    imageDatabaseControl.Visible != templateControl.Visible ||
                    templateChoices.Except(imageDatabaseChoices).ToList<string>().Count > 0)
                {
                    templateSyncResults.SyncRequiredAsNonCriticalFieldsDiffer = true;
                }
            }

            // Synchronization Issues 2: Unresolved warnings. Due to existence of other new / deleted columns.
            if (templateSyncResults.ControlSynchronizationErrors.Count > 0)
            {
                if (areNewColumnsInTemplate)
                {
                    string warning = "- ";
                    warning += templateSyncResults.DataLabelsInTemplateButNotImageDatabase.Count.ToString();
                    warning += (templateSyncResults.DataLabelsInTemplateButNotImageDatabase.Count == 1)
                        ? " new control was found in your .tdb template file: "
                        : " new controls were found in your .tdb template file: ";
                    warning += String.Format("'{0}'", string.Join(", ", templateSyncResults.DataLabelsInTemplateButNotImageDatabase.Keys));
                    templateSyncResults.ControlSynchronizationWarnings.Add(warning);
                }
                if (areDeletedColumnsInTemplate)
                {
                    string warning = "- ";
                    warning += templateSyncResults.DataLabelsInImageButNotTemplateDatabase.Count.ToString();
                    warning += (templateSyncResults.DataLabelsInImageButNotTemplateDatabase.Count == 1)
                        ? " data field in your.ddb data file has no corresponding control in your.tdb template file: "
                        : " data fields in your.ddb data file have no corresponding controls in your.tdb template file: ";
                    warning += String.Format("'{0}'", string.Join(", ", templateSyncResults.DataLabelsInImageButNotTemplateDatabase.Keys));
                    templateSyncResults.ControlSynchronizationWarnings.Add(warning);
                }
            }
        }

        // Only invoke this when we know the templateDBs are in sync, and the templateDB matches the FileDB (i.e., same control rows/columns) except for one or more defaults.
        public void UpgradeFileDBSchemaDefaultsFromTemplate()
        {
            // Initialize a schema 
            List<ColumnDefinition> columnDefinitions = new List<ColumnDefinition>
            {
                new ColumnDefinition(Constant.DatabaseColumn.ID, Constant.Sqlite.CreationStringPrimaryKey)  // It begins with the ID integer primary key
            };

            // Add the schema for the columns from the FileDB table
            foreach (ControlRow control in this.Controls)
            {
                columnDefinitions.Add(CreateFileDataColumnDefinition(control));
            }

            // Replace the schema in the FildDB table with the schema defined by the column definitions.
            this.Database.ReplaceTableSchemaWithNewColumnDefinitionsSchema(Constant.DatabaseTable.FileData, columnDefinitions);
        }

        // Upgrade the database as needed from older to newer formats to preserve backwards compatability 
        private void UpgradeDatabasesForBackwardsCompatability(TemplateDatabase templateDatabase)
        {
            // Note that we avoid Selecting * from the DataTable, as that could be an expensive operation
            // Instead, we operate directly on the database. There is only one exception (updating DateTime),
            // as we have to regenerate all the column's values

            // Get the image set. We will be checking some of its values as we go along
            this.GetImageSet();

            // Some comparisons are triggered by comparing the version number stored in the DB with 
            // particular version numbers where known changes occured 
            // Note: if we can't retrieve the version number from the image set, then set it to a very low version number to guarantee all checks will be made
            string lowestVersionNumber = "1.0.0.0";
            bool versionCompatabilityColumnExists = this.Database.IsColumnInTable(Constant.DatabaseTable.ImageSet, Constant.DatabaseColumn.VersionCompatabily);
            string imageSetVersionNumber = versionCompatabilityColumnExists ? this.ImageSet.VersionCompatability
                : lowestVersionNumber;
            string timelapseVersionNumberAsString = VersionClient.GetTimelapseCurrentVersionNumber().ToString();

            // Step 1. Check the FileTable for missing columns
            // RelativePath column (if missing) needs to be added 
            if (this.Database.IsColumnInTable(Constant.DatabaseTable.FileData, Constant.DatabaseColumn.RelativePath) == false)
            {
                long relativePathID = this.GetControlIDFromTemplateTable(Constant.DatabaseColumn.RelativePath);
                ControlRow relativePathControl = this.Controls.Find(relativePathID);
                ColumnDefinition columnDefinition = CreateFileDataColumnDefinition(relativePathControl);
                this.Database.AddColumnToTable(Constant.DatabaseTable.FileData, Constant.DatabaseValues.RelativePathPosition, columnDefinition);
            }

            // DateTime column (if missing) needs to be added 
            if (this.Database.IsColumnInTable(Constant.DatabaseTable.FileData, Constant.DatabaseColumn.DateTime) == false)
            {
                long dateTimeID = this.GetControlIDFromTemplateTable(Constant.DatabaseColumn.DateTime);
                ControlRow dateTimeControl = this.Controls.Find(dateTimeID);
                ColumnDefinition columnDefinition = CreateFileDataColumnDefinition(dateTimeControl);
                this.Database.AddColumnToTable(Constant.DatabaseTable.FileData, Constant.DatabaseValues.DateTimePosition, columnDefinition);
            }

            // UTCOffset column (if missing) needs to be added 
            if (this.Database.IsColumnInTable(Constant.DatabaseTable.FileData, Constant.DatabaseColumn.UtcOffset) == false)
            {
                long utcOffsetID = this.GetControlIDFromTemplateTable(Constant.DatabaseColumn.UtcOffset);
                ControlRow utcOffsetControl = this.Controls.Find(utcOffsetID);
                ColumnDefinition columnDefinition = CreateFileDataColumnDefinition(utcOffsetControl);
                this.Database.AddColumnToTable(Constant.DatabaseTable.FileData, Constant.DatabaseValues.UtcOffsetPosition, columnDefinition);
            }

            // Remove MarkForDeletion column and add DeleteFlag column(if needed)
            bool hasMarkForDeletion = this.Database.IsColumnInTable(Constant.DatabaseTable.FileData, Constant.ControlsDeprecated.MarkForDeletion);
            bool hasDeleteFlag = this.Database.IsColumnInTable(Constant.DatabaseTable.FileData, Constant.DatabaseColumn.DeleteFlag);
            if (hasMarkForDeletion && (hasDeleteFlag == false))
            {
                // migrate any existing MarkForDeletion column to DeleteFlag
                // this is likely the most typical case
                this.Database.RenameColumn(Constant.DatabaseTable.FileData, Constant.ControlsDeprecated.MarkForDeletion, Constant.DatabaseColumn.DeleteFlag);
            }
            else if (hasMarkForDeletion && hasDeleteFlag)
            {
                // if both MarkForDeletion and DeleteFlag are present drop MarkForDeletion
                // this is not expected to occur
                this.Database.DeleteColumn(Constant.DatabaseTable.FileData, Constant.ControlsDeprecated.MarkForDeletion);
            }
            else if (hasDeleteFlag == false)
            {
                // if there's neither a MarkForDeletion or DeleteFlag column add DeleteFlag
                long id = this.GetControlIDFromTemplateTable(Constant.DatabaseColumn.DeleteFlag);
                ControlRow control = this.Controls.Find(id);
                ColumnDefinition columnDefinition = CreateFileDataColumnDefinition(control);
                this.Database.AddColumnToEndOfTable(Constant.DatabaseTable.FileData, columnDefinition);
            }

            // STEP 2. Check the ImageTable for missing columns
            // Make sure that all the string data in the datatable has white space trimmed from its beginning and end
            // This is needed as the custom selection doesn't work well in testing comparisons if there is leading or trailing white space in it
            // Newer versions of Timelapse  trim the data as it is entered, but older versions did not, so this is to make it backwards-compatable.
            // The WhiteSpaceExists column in the ImageSet Table did not exist before this version, so we add it to the table if needed. If it exists, then 
            // we know the data has been trimmed and we don't have to do it again as the newer versions take care of trimmingon the fly.
            bool whiteSpaceColumnExists = this.Database.IsColumnInTable(Constant.DatabaseTable.ImageSet, Constant.DatabaseColumn.WhiteSpaceTrimmed);
            if (!whiteSpaceColumnExists)
            {
                // create the whitespace column
                this.Database.AddColumnToEndOfTable(Constant.DatabaseTable.ImageSet, new ColumnDefinition(Constant.DatabaseColumn.WhiteSpaceTrimmed, Constant.Sqlite.Text, Constant.BooleanValue.False));

                // trim whitespace from the data table
                this.Database.TrimWhitespace(Constant.DatabaseTable.FileData, this.GetDataLabelsExceptIDInSpreadsheetOrder());

                // mark image set as whitespace trimmed
                // This still has to be synchronized, which will occur after we prepare all missing columns
                this.GetImageSet();
                this.ImageSet.WhitespaceTrimmed = true;
            }

            // Null test check against the version number
            // Versions prior to 2.2.2.4 may have set nulls as default values, which don't interact well with some aspects of Timelapse. 
            // Repair by turning all nulls in FileTable, if any, into empty strings
            // SAULXX Note that we could likely remove the WhiteSpaceTrimmed column and use the version number instead but we need to check if that is backwards compatable before doing so.
            string firstVersionWithNullCheck = "2.2.2.4";
            if (VersionClient.IsVersion1GreaterThanVersion2(firstVersionWithNullCheck, imageSetVersionNumber))
            {
                this.Database.ChangeNullToEmptyString(Constant.DatabaseTable.FileData, this.GetDataLabelsExceptIDInSpreadsheetOrder());
            }

            // STEP 3. Check both templates and update if needed (including values)

            // ImageQuality Column and Data: For both templates, replace the ImageQuality List menu with the new one (that contains only Unknown, Light and Dark items)
            // IMMEDIATE Change to 2.2.2.6 For testing, set to a later version (e.g., 3..0.0.0 to force execution of this every time.
            string firstVersionWithAlteredImageQualityChoices = "2.2.2.6";
            if (VersionClient.IsVersion1GreaterThanVersion2(firstVersionWithAlteredImageQualityChoices, imageSetVersionNumber))
            {
                // Alter the template in the .ddb file
                ControlRow templateControl = this.GetControlFromTemplateTable(Constant.DatabaseColumn.ImageQuality);
                if (templateControl != null)
                {
                    templateControl.List = Constant.ImageQuality.ListOfValues;
                    templateControl.DefaultValue = Constant.ImageQuality.Unknown;
                    this.SyncControlToDatabase(templateControl);
                }
                // Alter the template in the .tdb file
                templateControl = templateDatabase.GetControlFromTemplateTable(Constant.DatabaseColumn.ImageQuality);
                if (templateControl != null)
                {
                    templateControl.List = Constant.ImageQuality.ListOfValues;
                    templateControl.DefaultValue = Constant.ImageQuality.Unknown;
                    templateDatabase.SyncControlToDatabase(templateControl);
                }
                // ImageQuality Values: Reset the Image Quality values. All Dark values remain as is, but other values are reset to unknown.
                // This is the only way to make this backwards compatable.
                // I suspect in most cases that the analysts aren't using the field anyways, although they can always regenerate it if needed.
                ColumnTuple columnTuple = new ColumnTuple(Constant.DatabaseColumn.ImageQuality, Constant.ImageQuality.Unknown);
                ColumnTuplesWithWhere columnTupleWithWhere = new ColumnTuplesWithWhere(columnTuple, Constant.ImageQuality.Dark, true);
                this.Database.Update(Constant.DatabaseTable.FileData, columnTupleWithWhere);
            }

            // Version Compatabillity Column: If the imageSetVersion is set to the lowest version number, then the column containing the VersionCompatabily does not exist in the image set table. 
            // Add it and update the entry to contain the version of Timelapse currently being used to open this database
            // Note that we do this after the version compatability tests as otherwise we would just get the current version number
            if (versionCompatabilityColumnExists == false)
            {
                // Create the versioncompatability column and update the image set. Syncronization happens later
                this.Database.AddColumnToEndOfTable(Constant.DatabaseTable.ImageSet, new ColumnDefinition(Constant.DatabaseColumn.VersionCompatabily, Constant.Sqlite.Text, timelapseVersionNumberAsString));
            }

            // Sort Criteria Column: Make sure that the column containing the SortCriteria exists in the image set table. 
            // If not, add it and set it to the default
            bool sortCriteriaColumnExists = this.Database.IsColumnInTable(Constant.DatabaseTable.ImageSet, Constant.DatabaseColumn.SortTerms);
            if (!sortCriteriaColumnExists)
            {
                // create the sortCriteria column and update the image set. Syncronization happens later
                this.Database.AddColumnToEndOfTable(Constant.DatabaseTable.ImageSet, new ColumnDefinition(Constant.DatabaseColumn.SortTerms, Constant.Sqlite.Text, Constant.DatabaseValues.DefaultSortTerms));
            }

            // SelectedFolder Column: Make sure that the column containing the SelectedFolder exists in the image set table. 
            // If not, add it and set it to the default
            string firstVersionWithSelectedFilesColumns = "2.2.2.6";
            if (VersionClient.IsVersion1GreaterOrEqualToVersion2(firstVersionWithSelectedFilesColumns, imageSetVersionNumber))
            {
                // Because we may be running this several times on the same version, we should still check to see if the column exists before adding it
                bool selectedFolderColumnExists = this.Database.IsColumnInTable(Constant.DatabaseTable.ImageSet, Constant.DatabaseColumn.SelectedFolder);
                if (!selectedFolderColumnExists)
                {
                    // create the sortCriteria column and update the image set. Syncronization happens later
                    this.Database.AddColumnToEndOfTable(Constant.DatabaseTable.ImageSet, new ColumnDefinition(Constant.DatabaseColumn.SelectedFolder, Constant.Sqlite.Text, String.Empty));
                    this.GetImageSet();
                }
            }
            // Make sure that the column containing the QuickPasteXML exists in the image set table. 
            // If not, add it and set it to the default
            bool quickPasteXMLColumnExists = this.Database.IsColumnInTable(Constant.DatabaseTable.ImageSet, Constant.DatabaseColumn.QuickPasteXML);
            if (!quickPasteXMLColumnExists)
            {
                // create the QuickPaste column and update the image set. Syncronization happens later
                this.Database.AddColumnToEndOfTable(Constant.DatabaseTable.ImageSet, new ColumnDefinition(Constant.DatabaseColumn.QuickPasteXML, Constant.Sqlite.Text, Constant.DatabaseValues.DefaultQuickPasteXML));
            }

            // Timezone column (if missing) needs to be added to the Imageset Table
            bool timeZoneColumnExists = this.Database.IsColumnInTable(Constant.DatabaseTable.ImageSet, Constant.DatabaseColumn.TimeZone);
            bool timeZoneColumnIsNotPopulated = timeZoneColumnExists;
            if (!timeZoneColumnExists)
            {
                // create default time zone entry and refresh the image set.
                this.Database.AddColumnToEndOfTable(Constant.DatabaseTable.ImageSet, new ColumnDefinition(Constant.DatabaseColumn.TimeZone, Constant.Sqlite.Text));
                this.Database.SetColumnToACommonValue(Constant.DatabaseTable.ImageSet, Constant.DatabaseColumn.TimeZone, TimeZoneInfo.Local.Id);
                this.GetImageSet();
            }

            // Populate DateTime column if the column has just been added
            if (!timeZoneColumnIsNotPopulated)
            {
                TimeZoneInfo imageSetTimeZone = this.ImageSet.GetSystemTimeZone();
                List<ColumnTuplesWithWhere> updateQuery = new List<ColumnTuplesWithWhere>();
                // PERFORMANCE, BUT RARE: Because we have to update various date/time values on all rows based on existing values, 
                // we  have to select all rows. However, this operation would only ever happen once, and only on legacy .ddb files
                this.SelectFiles(FileSelectionEnum.All);
                foreach (ImageRow image in this.FileTable)
                {
                    // NEED TO GET Legacy DATE TIME  (i.e., FROM DATE AND TIME fields) as the new DateTime did not exist in this old database. 
                    bool result = DateTimeHandler.TryParseLegacyDateTime(image.Date, image.Time, imageSetTimeZone, out DateTimeOffset imageDateTime);
                    if (!result)
                    {
                        // If we can't get the legacy date time, try getting the date time this way
                        imageDateTime = image.DateTimeIncorporatingOffset;
                    }
                    image.SetDateTimeOffset(imageDateTime);
                    updateQuery.Add(image.GetDateTimeColumnTuples());
                }
                this.Database.Update(Constant.DatabaseTable.FileData, updateQuery);
                // Note that the FileTable is now stale as we have updated the database directly
            }
        }
        #endregion

        [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1100:DoNotPrefixCallsWithBaseUnlessLocalImplementationExists", Justification = "StyleCop bug.")]
        protected override void OnExistingDatabaseOpened(TemplateDatabase templateDatabase, TemplateSyncResults templateSyncResults)
        {
            // Perform TemplateTable initializations.
            base.OnExistingDatabaseOpened(templateDatabase, null);

            // If directed to use the template found in the template database, 
            // check and repair differences between the .tdb and .ddb template tables due to  missing or added controls 
            if (templateSyncResults.UseTemplateDBTemplate)
            {
                // Check for differences between the TemplateTable in the .tdb and .ddb database.
                if (templateSyncResults.SyncRequiredAsDataLabelsDiffer || templateSyncResults.SyncRequiredAsChoiceMenusDiffer)
                {
                    // The TemplateTable in the .tdb and .ddb database differ. 
                    // Update the .ddb Template table by dropping the .ddb template table and replacing it with the .tdb table. 
                    base.Database.DropTable(Constant.DatabaseTable.Controls);
                    base.OnDatabaseCreated(templateDatabase);
                }

                // Condition 1: the tdb template table contains one or more datalabels not found in the ddb template table
                // That is, the .tdb defines additional controls
                // Action: For each new control in the template table, 
                //           - add a corresponding data column in the ImageTable
                //           - if it is a counter, add a corresponding data column in the MarkerTable

                foreach (string dataLabel in templateSyncResults.DataLabelsToAdd)
                {
                    long id = this.GetControlIDFromTemplateTable(dataLabel);
                    ControlRow control = this.Controls.Find(id);
                    ColumnDefinition columnDefinition = CreateFileDataColumnDefinition(control);
                    this.Database.AddColumnToEndOfTable(Constant.DatabaseTable.FileData, columnDefinition);

                    if (control.Type == Constant.Control.Counter)
                    {
                        ColumnDefinition markerColumnDefinition = new ColumnDefinition(dataLabel, Constant.Sqlite.Text);
                        this.Database.AddColumnToEndOfTable(Constant.DatabaseTable.Markers, markerColumnDefinition);
                    }
                }

                // Condition 2: The image template table had contained one or more controls not found in the template table.
                // That is, the .ddb DataTable contains data columns that now have no corresponding control 
                // Action: Delete those data columns
                foreach (string dataLabel in templateSyncResults.DataLabelsToDelete)
                {
                    this.Database.DeleteColumn(Constant.DatabaseTable.FileData, dataLabel);

                    // Delete the markers column associated with this data label (if it exists) from the Markers table
                    // Note that we do this for all column types, even though only counters have an associated entry in the Markers table.
                    // This is because we can't get the type of the data label as it no longer exists in the Template.
                    if (this.Database.IsColumnInTable(Constant.DatabaseTable.Markers, dataLabel))
                    {
                        this.Database.DeleteColumn(Constant.DatabaseTable.Markers, dataLabel);
                    }
                }

                // Condition 3: The user indicated that the following controls (add/delete) are actually renamed controls
                // Action: Rename those data columns
                foreach (KeyValuePair<string, string> dataLabelToRename in templateSyncResults.DataLabelsToRename)
                {
                    // Rename the column associated with that data label from the FileData table
                    this.Database.RenameColumn(Constant.DatabaseTable.FileData, dataLabelToRename.Key, dataLabelToRename.Value);

                    // Rename the markers column associated with this data label (if it exists) from the Markers table
                    // Note that we do this for all column types, even though only counters have an associated entry in the Markers table.
                    // This is because its easiest to code, as the function handles attempts to delete a column that isn't there (which also returns false).
                    if (this.Database.IsColumnInTable(Constant.DatabaseTable.Markers, dataLabelToRename.Key))
                    {
                        this.Database.RenameColumn(Constant.DatabaseTable.Markers, dataLabelToRename.Key, dataLabelToRename.Value);
                    }
                }

                // Refetch the data labels if needed, as they will have changed due to the repair
                List<string> dataLabels = this.GetDataLabelsExceptIDInSpreadsheetOrder();

                // Condition 4: There are non-critical updates in the template's row (e.g., that only change the UI). 
                // Synchronize the image database's TemplateTable with the template database's TemplateTable 
                if (templateSyncResults.SyncRequiredAsNonCriticalFieldsDiffer)
                {
                    foreach (string dataLabel in dataLabels)
                    {
                        ControlRow imageDatabaseControl = this.GetControlFromTemplateTable(dataLabel);
                        ControlRow templateControl = templateDatabase.GetControlFromTemplateTable(dataLabel);

                        if (imageDatabaseControl.Synchronize(templateControl))
                        {
                            this.SyncControlToDatabase(imageDatabaseControl);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Create lookup tables that allow us to retrieve a key from a type and vice versa
        /// </summary>
        private void PopulateDataLabelMaps()
        {
            foreach (ControlRow control in this.Controls)
            {
                FileTableColumn column = FileTableColumn.Create(control);
                this.FileTableColumnsByDataLabel.Add(column.DataLabel, column);

                // don't type map user defined controls as if there are multiple ones the key would not be unique
                if (Constant.Control.StandardTypes.Contains(column.ControlType))
                {
                    this.DataLabelFromStandardControlType.Add(column.ControlType, column.DataLabel);
                }
            }
        }

        #region File (image) retrieval and manipulation
        public void RenameFile(string newFileName)
        {
            if (File.Exists(Path.Combine(this.FolderPath, this.FileName)))
            {
                File.Move(Path.Combine(this.FolderPath, this.FileName),
                          Path.Combine(this.FolderPath, newFileName));  // Change the file name to the new file name
                this.FileName = newFileName; // Store the file name
                this.Database = new SQLiteWrapper(Path.Combine(this.FolderPath, newFileName));          // Recreate the database connecction
            }
        }

        private ImageRow GetFile(string where)
        {
            if (String.IsNullOrWhiteSpace(where))
            {
                throw new ArgumentOutOfRangeException("FileDatabase: 'where' clause is empty or null");
            }

            string query = Constant.Sqlite.SelectStarFrom + Constant.DatabaseTable.FileData + Constant.Sqlite.Where + where;
            DataTable images = this.Database.GetDataTableFromSelect(query);
            FileTable temporaryTable = new FileTable(images);
            if (temporaryTable.RowCount != 1)
            {
                return null;
            }
            return temporaryTable[0];
        }

        /// <summary> 
        /// Rebuild the file table with all files in the database table which match the specified selection.
        /// </summary>
        public void SelectFiles(FileSelectionEnum selection)
        {
            string query = String.Empty;
            bool useStandardQuery = false;
            if (this.CustomSelection == null)
            {
                useStandardQuery = true;
            }
            else
            {
                this.CustomSelection.SetCustomSearchFromSelection(selection, GetSelectedFolder());
                if (GlobalReferences.DetectionsExists && this.CustomSelection.ShowMissingDetections)
                {
                    query = "SELECT DataTable.* FROM DataTable LEFT JOIN Detections ON DataTable.ID = Detections.Id WHERE Detections.Id IS NULL";
                }
                else if (GlobalReferences.DetectionsExists && this.CustomSelection.DetectionSelections.Enabled == true)
                {
                    // Form: SELECT DataTable.* INNER JOIN DataTable ON DataTable.Id = Detections.Id  WHERE Detections.category = 1 Group By Detections.Id Having Max  (Detections.conf )  < .2
                    query = Constant.Sqlite.Select + Constant.DatabaseTable.FileData + ".*" + Constant.Sqlite.From + Constant.DBTableNames.Detections +
                        Constant.Sqlite.InnerJoin + Constant.DatabaseTable.FileData + Constant.Sqlite.On +
                         Constant.DatabaseTable.FileData + "." + Constant.DatabaseColumn.ID + Constant.Sqlite.Equal +
                         Constant.DBTableNames.Detections + "." + Constant.DetectionColumns.ImageID;
                }
                else
                {
                    useStandardQuery = true;
                }
            }

            if (useStandardQuery)
            {
                query = Constant.Sqlite.SelectStarFrom + Constant.DatabaseTable.FileData;
            }

            if (this.CustomSelection != null && (GlobalReferences.DetectionsExists == false || this.CustomSelection.ShowMissingDetections == false))
            {
                string conditionalExpression = this.GetFilesConditionalExpression(selection);
                if (String.IsNullOrEmpty(conditionalExpression) == false)
                {
                    query += conditionalExpression;
                }
            }

            // Sort by primary and secondary sort criteria if an image set is actually initialized (i.e., not null)
            if (this.ImageSet != null)
            {
                SortTerm[] sortTerm = new SortTerm[2];
                string[] term = new string[] { String.Empty, String.Empty };

                // Special case for DateTime sorting.
                // DateTime is UTC i.e., local time corrected by the UTCOffset. Although I suspect this is rare, 
                // this can result in odd DateTime sorts (assuming intermixed images with similar datetimes but with different UTCOffsets)
                // where the user is expected sorting by local time. 
                // To sort by true local time rather then UTC, we need to alter
                // OrderBy DateTime to OrderBy datetime(DateTime, UtcOffset || ' hours' )
                // This datetime function adds the number of hours in the UtcOffset to the date/time recorded in DateTime
                // that is, it turns it into local time, e.g., 2009-08-14T23:40:00.000Z, this can be sorted alphabetically
                // Given the format of the corrected DateTime
                for (int i = 0; i <= 1; i++)
                {
                    sortTerm[i] = this.ImageSet.GetSortTerm(i);

                    // If we see an empty data label, we don't have to construct any more terms as there will be nothing more to sort
                    if (sortTerm[i].DataLabel == String.Empty)
                    {
                        break;
                    }
                    else if (sortTerm[i].DataLabel == Constant.DatabaseColumn.DateTime)
                    {
                        // First Check for special cases, where we want to modify how sorting is done
                        // DateTime:the modified query adds the UTC Offset to it
                        term[i] = String.Format("datetime({0}, {1} || ' hours')", Constant.DatabaseColumn.DateTime, Constant.DatabaseColumn.UtcOffset);
                    }
                    else if (sortTerm[i].DataLabel == Constant.DatabaseColumn.File)
                    {
                        // File: the modified term creates a file path by concatonating relative path and file
                        term[i] = String.Format("{0}{1}{2}", Constant.DatabaseColumn.RelativePath, Constant.Sqlite.Comma, Constant.DatabaseColumn.File);
                    }
                    else if (sortTerm[i].ControlType == Constant.Control.Counter)
                    {
                        // Its a counter type: modify sorting of blanks by transforming it into a '-1' and then by casting it as an integer
                        term[i] = String.Format("Cast(COALESCE(NULLIF({0}, ''), '-1') as Integer)", sortTerm[i].DataLabel);
                    }
                    else
                    {
                        // Default: just sort by the data label
                        term[i] = sortTerm[i].DataLabel;
                    }
                    // Add Descending sort, if needed. Default is Ascending, so we don't have to add that
                    if (sortTerm[i].IsAscending == Constant.BooleanValue.False)
                    {
                        term[i] += Constant.Sqlite.Descending;
                    }
                }

                if (!String.IsNullOrEmpty(term[0]))
                {
                    query += Constant.Sqlite.OrderBy + term[0];

                    // If there is a second sort key, add it here
                    if (!String.IsNullOrEmpty(term[1]))
                    {
                        query += Constant.Sqlite.Comma + term[1];
                    }
                    query += Constant.Sqlite.Semicolon;
                }
            }

            //  System.Diagnostics.Debug.Print("Doit: " + query);
            DataTable images = this.Database.GetDataTableFromSelect(query);
            this.FileTable = new FileTable(images);
            this.FileTable.BindDataGrid(this.boundGrid, this.onFileDataTableRowChanged);
        }

        public bool SelectMissingFilesFromCurrentlySelectedFiles()
        {
            if (this.FileTable == null)
            {
                return false;
            }
            string filepath = String.Empty;
            string message = String.Empty;
            string commaSeparatedListOfIDs = String.Empty;
            List<ColumnTuplesWithWhere> imagesToUpdate = new List<ColumnTuplesWithWhere>();
            // Check if each file exists. Get all missing files in the selection as a list of file ids, e.g., "1,2,8,10" 
            foreach (ImageRow image in this.FileTable)
            {
                if (!File.Exists(Path.Combine(this.FolderPath, image.RelativePath, image.File)))
                {
                    commaSeparatedListOfIDs += image.ID + ",";
                }
            }
            // remove the trailing comma
            commaSeparatedListOfIDs = commaSeparatedListOfIDs.TrimEnd(',');
            this.FileTable = this.GetFilesInDataTableById(commaSeparatedListOfIDs);
            this.FileTable.BindDataGrid(this.boundGrid, this.onFileDataTableRowChanged);
            return (commaSeparatedListOfIDs == String.Empty) ? false : true;
        }

        public FileTable GetFilesMarkedForDeletion()
        {
            string where = this.DataLabelFromStandardControlType[Constant.DatabaseColumn.DeleteFlag] + "=" + Utilities.QuoteForSql(Constant.BooleanValue.True); // = value
            string query = Constant.Sqlite.SelectStarFrom + Constant.DatabaseTable.FileData + Constant.Sqlite.Where + where;
            DataTable images = this.Database.GetDataTableFromSelect(query);
            return new FileTable(images);
        }

        // Select * From DataTable Where  Id IN(1,2,4 )
        public FileTable GetFilesInDataTableById(string listOfIds)
        {
            string query = Constant.Sqlite.SelectStarFrom + Constant.DatabaseTable.FileData + Constant.Sqlite.WhereIDIn + Constant.Sqlite.OpenParenthesis + listOfIds + Constant.Sqlite.CloseParenthesis;
            DataTable images = this.Database.GetDataTableFromSelect(query);
            return new FileTable(images);
        }

        // SAULXXX: TEMPORARY - TO FIX DUPLICATE BUG. TO BE REMOVED IN FUTURE VERSIONS
        // Get a table containing a subset of rows that have duplicate File and RelativePaths 
        public FileTable GetDuplicateFiles()
        {
            string query = Constant.Sqlite.Select + " RelativePath, File, COUNT(*) " + Constant.Sqlite.From + Constant.DatabaseTable.FileData;
            query += Constant.Sqlite.GroupBy + " RelativePath, File HAVING COUNT(*) > 1";
            DataTable images = this.Database.GetDataTableFromSelect(query);
            return new FileTable(images);
        }

        public FileTable GetAllFiles()
        {
            string query = Constant.Sqlite.SelectStarFrom + Constant.DatabaseTable.FileData;
            DataTable images = this.Database.GetDataTableFromSelect(query);
            return new FileTable(images);
        }

        // SAULXXX: TEMPORARY - TO FIX DUPLICATE BUG. TO BE REMOVED IN FUTURE VERSIONS
        // Delete duplicate rows from the database, identified by identical File and RelativePath contents.
        public void DeleteDuplicateFiles()
        {
            string query = Constant.Sqlite.DeleteFrom + Constant.DatabaseTable.FileData + Constant.Sqlite.WhereIDNotIn;
            query += Constant.Sqlite.OpenParenthesis + Constant.Sqlite.Select + " MIN(Id) " + Constant.Sqlite.From + Constant.DatabaseTable.FileData + Constant.Sqlite.GroupBy + "RelativePath, File)";
            DataTable images = this.Database.GetDataTableFromSelect(query);
        }

        /// <summary>
        /// Get the row matching the specified image or create a new image.  The caller is responsible for adding newly created images the database and data table.
        /// </summary>
        /// <returns>true if the image is already in the database</returns>
        public bool GetOrCreateFile(FileInfo fileInfo, out ImageRow file)
        {
            // Path.GetFileName strips the last folder of the folder path,which in this case gives us the root folder..
            string initialRootFolderName = Path.GetFileName(this.FolderPath);

            // GetRelativePath() includes the image's file name; remove that from the relative path as it's stored separately
            // GetDirectoryName() returns String.Empty if there's no relative path; the SQL layer treats this inconsistently, resulting in 
            // DataRows returning with RelativePath = String.Empty even if null is passed despite setting String.Empty as a column default
            // resulting in RelativePath = null.  As a result, String.IsNullOrEmpty() is the appropriate test for lack of a RelativePath.
            string relativePath = NativeMethods.GetRelativePath(this.FolderPath, fileInfo.FullName);
            relativePath = Path.GetDirectoryName(relativePath);

            // Check if the file already exists in the database. If so, no need to recreate it.
            ColumnTuplesWithWhere fileQuery = new ColumnTuplesWithWhere();
            fileQuery.SetWhere(initialRootFolderName, relativePath, fileInfo.Name);
            file = this.GetFile(fileQuery.Where);

            if (file != null)
            {
                return true;
            }
            else
            {
                file = this.FileTable.NewRow(fileInfo);
                file.Folder = initialRootFolderName;
                file.RelativePath = relativePath;
                file.SetDateTimeOffsetFromFileInfo(this.FolderPath);
                return false;
            }
        }

        public Dictionary<FileSelectionEnum, int> GetFileCountsInAllFiles()
        {
            Dictionary<FileSelectionEnum, int> counts = new Dictionary<FileSelectionEnum, int>
            {
                [FileSelectionEnum.Dark] = this.GetFileCount(FileSelectionEnum.Dark),
                [FileSelectionEnum.Unknown] = this.GetFileCount(FileSelectionEnum.Unknown),
                [FileSelectionEnum.Light] = this.GetFileCount(FileSelectionEnum.Light)
            };
            return counts;
        }

        public Dictionary<FileSelectionEnum, int> GetFileCountsInCurrentSelection()
        {
            Dictionary<FileSelectionEnum, int> counts = new Dictionary<FileSelectionEnum, int>
            {
                [FileSelectionEnum.Dark] = this.FileTable.Count(p => p.ImageQuality == FileSelectionEnum.Dark),
                [FileSelectionEnum.Unknown] = this.FileTable.Count(p => p.ImageQuality == FileSelectionEnum.Unknown),
                [FileSelectionEnum.Light] = this.FileTable.Count(p => p.ImageQuality == FileSelectionEnum.Light)
            };
            return counts;
        }

        // Form examples
        // - Select Count(*) FROM DataTable WHERE ImageQuality='Light'
        // - Select Count(*) FROM (Select * From Detections INNER JOIN DataTable ON DataTable.Id = Detections.Id WHERE DataTable.ImageQuality='Light' GROUP BY Detections.Id HAVING  MAX  ( Detections.conf )  <= 0.9)
        public int GetFileCount(FileSelectionEnum fileSelection)
        {
            string query = String.Empty;
            bool skipWhere = false;
            if (fileSelection == FileSelectionEnum.Custom && GlobalReferences.DetectionsExists && this.CustomSelection.ShowMissingDetections)
            {
                query = "Select Count (DataTable.id) FROM DataTable LEFT JOIN Detections ON DataTable.ID = Detections.Id WHERE Detections.Id IS NULL";
                skipWhere = true;
            }
            else if (fileSelection == FileSelectionEnum.Custom && GlobalReferences.DetectionsExists && this.CustomSelection.DetectionSelections.Enabled == true)
            {
                // query = Constant.Sqlite.SelectCountStarFrom + Constant.DatabaseTable.FileData + // ".*" + Constant.Sqlite.From + Constant.DBTableNames.Detections +
                query = Constant.Sqlite.SelectCountStarFrom +
                    Constant.Sqlite.OpenParenthesis + Constant.Sqlite.SelectStarFrom +
                    Constant.DBTableNames.Detections +
                    Constant.Sqlite.InnerJoin + Constant.DatabaseTable.FileData + Constant.Sqlite.On +
                    Constant.DatabaseTable.FileData + "." + Constant.DatabaseColumn.ID + Constant.Sqlite.Equal +
                    Constant.DBTableNames.Detections + "." + Constant.DetectionColumns.ImageID;
            }
            else
            {
                query = Constant.Sqlite.SelectCountStarFrom + Constant.DatabaseTable.FileData;
            }

            if ((GlobalReferences.DetectionsExists && this.CustomSelection.ShowMissingDetections == false) || skipWhere == false)
            {
                string where = this.GetFilesConditionalExpression(fileSelection);
                if (!String.IsNullOrEmpty(where))
                {
                    query += where;
                }
                if (fileSelection == FileSelectionEnum.Custom && this.CustomSelection.DetectionSelections.Enabled == true)
                {
                    query += Constant.Sqlite.CloseParenthesis;
                }
            }
            // System.Diagnostics.Debug.Print("Count: " + query);
            return this.Database.GetCountFromSelect(query);
        }
        #endregion

        // Insert one or more rows into a table
        private void InsertRows(string table, List<List<ColumnTuple>> insertionStatements)
        {
            this.CreateBackupIfNeeded();
            this.Database.Insert(table, insertionStatements);
        }

        // Return the selected folder (if any)
        public string GetSelectedFolder()
        {
            if (this.CustomSelection == null)
            {
                return String.Empty;
            }
            return this.CustomSelection.GetRelativePathFolder();
        }
        private string GetFilesConditionalExpression(FileSelectionEnum selection)
        {
            // System.Diagnostics.Debug.Print(selection.ToString());
            switch (selection)
            {
                case FileSelectionEnum.All:
                    return String.Empty;
                case FileSelectionEnum.Unknown:
                case FileSelectionEnum.Dark:
                case FileSelectionEnum.Light:
                    return Constant.Sqlite.Where + this.DataLabelFromStandardControlType[Constant.DatabaseColumn.ImageQuality] + "=" + Utilities.QuoteForSql(selection.ToString());
                case FileSelectionEnum.MarkedForDeletion:
                    return Constant.Sqlite.Where + this.DataLabelFromStandardControlType[Constant.DatabaseColumn.DeleteFlag] + "=" + Utilities.QuoteForSql(Constant.BooleanValue.True);
                case FileSelectionEnum.Custom:
                case FileSelectionEnum.Folders:
                    return this.CustomSelection.GetFilesWhere();
                default:
                    throw new NotSupportedException(String.Format("Unhandled quality selection {0}.  For custom selections call CustomSelection.GetImagesWhere().", selection));
            }
        }

        public void IndexCreateForDetectionsAndClassifications()
        {
            this.Database.CreateIndex(Constant.DatabaseValues.IndexID, Constant.DBTableNames.Detections, Constant.DatabaseColumn.ID);
            this.Database.CreateIndex(Constant.DatabaseValues.IndexDetectionID, Constant.DBTableNames.Classifications, Constant.DetectionColumns.DetectionID);
        }

        public void IndexCreateForFileAndRelativePath()
        {
            this.Database.CreateIndex(Constant.DatabaseValues.IndexRelativePath, Constant.DatabaseTable.FileData, Constant.DatabaseColumn.RelativePath);
            this.Database.CreateIndex(Constant.DatabaseValues.IndexFile, Constant.DatabaseTable.FileData, Constant.DatabaseColumn.File);
        }

        public void IndexDropForFileAndRelativePath()
        {
            this.Database.DropIndex(Constant.DatabaseValues.IndexRelativePath);
            this.Database.DropIndex(Constant.DatabaseValues.IndexFile);
        }



        #region Update Files
        /// <summary>
        /// Update a column value (identified by its key) in an existing row (identified by its ID) 
        /// By default, if the table parameter is not included, we use the TABLEDATA table
        /// </summary>
        public void UpdateFile(long fileID, string dataLabel, string value)
        {
            // update the data table
            ImageRow image = this.FileTable.Find(fileID);
            image.SetValueFromDatabaseString(dataLabel, value);

            // update the row in the database
            this.CreateBackupIfNeeded();

            ColumnTuplesWithWhere columnToUpdate = new ColumnTuplesWithWhere();
            columnToUpdate.Columns.Add(new ColumnTuple(dataLabel, value)); // Populate the data 
            columnToUpdate.SetWhere(fileID);
            this.Database.Update(Constant.DatabaseTable.FileData, columnToUpdate);
        }

        // Set one property on all rows in the selected view to a given value
        public void UpdateFiles(ImageRow valueSource, string dataLabel)
        {
            this.UpdateFiles(valueSource, dataLabel, 0, this.CurrentlySelectedFileCount - 1);
        }

        // Given a list of column/value pairs (the string,object) and the FILE name indicating a row, update it
        public void UpdateFiles(List<ColumnTuplesWithWhere> filesToUpdate)
        {
            this.CreateBackupIfNeeded();
            this.Database.Update(Constant.DatabaseTable.FileData, filesToUpdate);
        }

        public void UpdateFiles(ColumnTuplesWithWhere filesToUpdate)
        {
            List<ColumnTuplesWithWhere> imagesToUpdateList = new List<ColumnTuplesWithWhere>
            {
                filesToUpdate
            };
            this.Database.Update(Constant.DatabaseTable.FileData, imagesToUpdateList);
        }

        public void UpdateFiles(ColumnTuple columnToUpdate)
        {
            this.Database.Update(Constant.DatabaseTable.FileData, columnToUpdate);
        }

        // Given a range of selected files, update the field identifed by dataLabel with the value in valueSource
        // Updates are applied to both the datatable (so the user sees the updates immediately) and the database
        public void UpdateFiles(ImageRow valueSource, string dataLabel, int fromIndex, int toIndex)
        {
            if (fromIndex < 0)
            {
                throw new ArgumentOutOfRangeException("fromIndex");
            }
            if (toIndex < fromIndex || toIndex > this.CurrentlySelectedFileCount - 1)
            {
                throw new ArgumentOutOfRangeException("toIndex");
            }

            string value = valueSource.GetValueDatabaseString(dataLabel);
            List<ColumnTuplesWithWhere> imagesToUpdate = new List<ColumnTuplesWithWhere>();
            for (int index = fromIndex; index <= toIndex; index++)
            {
                // update data table
                ImageRow image = this.FileTable[index];
                image.SetValueFromDatabaseString(dataLabel, value);

                // update database
                List<ColumnTuple> columnToUpdate = new List<ColumnTuple>() { new ColumnTuple(dataLabel, value) };
                ColumnTuplesWithWhere imageUpdate = new ColumnTuplesWithWhere(columnToUpdate, image.ID);
                imagesToUpdate.Add(imageUpdate);
            }
            this.CreateBackupIfNeeded();
            this.Database.Update(Constant.DatabaseTable.FileData, imagesToUpdate);
        }

        // Similar to above
        // Given a list of selected files, update the field identifed by dataLabel with the value in valueSource
        // Updates are applied to both the datatable (so the user sees the updates immediately) and the database
        public void UpdateFiles(List<int> fileIndexes, string dataLabel, string value)
        {
            if (fileIndexes.Count == 0)
            {
                return;
            }

            // string value = valueSource.GetValueDatabaseString(dataLabel);
            List<ColumnTuplesWithWhere> imagesToUpdate = new List<ColumnTuplesWithWhere>();
            foreach (int fileIndex in fileIndexes)
            {
                // update data table
                ImageRow image = this.FileTable[fileIndex];
                image.SetValueFromDatabaseString(dataLabel, value);

                // update database
                List<ColumnTuple> columnToUpdate = new List<ColumnTuple>() { new ColumnTuple(dataLabel, value) };
                ColumnTuplesWithWhere imageUpdate = new ColumnTuplesWithWhere(columnToUpdate, image.ID);
                imagesToUpdate.Add(imageUpdate);
            }
            this.CreateBackupIfNeeded();
            this.Database.Update(Constant.DatabaseTable.FileData, imagesToUpdate);
        }
        #endregion

        #region AdjustFileTimes
        public void AdjustFileTimes(TimeSpan adjustment)
        {
            this.AdjustFileTimes(adjustment, 0, this.CurrentlySelectedFileCount - 1);
        }

        public void AdjustFileTimes(TimeSpan adjustment, int startRow, int endRow)
        {
            if (adjustment.Milliseconds != 0)
            {
                throw new ArgumentOutOfRangeException("adjustment", "The current format of the time column does not support milliseconds.");
            }
            this.AdjustFileTimes((DateTimeOffset imageTime) => { return imageTime + adjustment; }, startRow, endRow);
        }

        // Given a time difference in ticks, update all the date/time field in the database
        // Note that it does NOT update the dataTable - this has to be done outside of this routine by regenerating the datatables with whatever selection is being used..
        public void AdjustFileTimes(Func<DateTimeOffset, DateTimeOffset> adjustment, int startRow, int endRow)
        {
            if (this.IsFileRowInRange(startRow) == false)
            {
                throw new ArgumentOutOfRangeException("startRow");
            }
            if (this.IsFileRowInRange(endRow) == false)
            {
                throw new ArgumentOutOfRangeException("endRow");
            }
            if (endRow < startRow)
            {
                throw new ArgumentOutOfRangeException("endRow", "endRow must be greater than or equal to startRow.");
            }
            if (this.CurrentlySelectedFileCount == 0)
            {
                return;
            }

            // We now have an unselected temporary data table
            // Get the original value of each, and update each date by the corrected amount if possible
            List<ImageRow> filesToAdjust = new List<ImageRow>();
            TimeSpan mostRecentAdjustment = TimeSpan.Zero;
            for (int row = startRow; row <= endRow; ++row)
            {
                ImageRow image = this.FileTable[row];
                DateTimeOffset currentImageDateTime = image.DateTimeIncorporatingOffset;

                // adjust the date/time
                DateTimeOffset newImageDateTime = adjustment.Invoke(currentImageDateTime);
                if (newImageDateTime == currentImageDateTime)
                {
                    continue;
                }
                mostRecentAdjustment = newImageDateTime - currentImageDateTime;
                image.SetDateTimeOffset(newImageDateTime);
                filesToAdjust.Add(image);
            }

            // update the database with the new date/time values
            List<ColumnTuplesWithWhere> imagesToUpdate = new List<ColumnTuplesWithWhere>();
            foreach (ImageRow image in filesToAdjust)
            {
                imagesToUpdate.Add(image.GetDateTimeColumnTuples());
            }

            if (imagesToUpdate.Count > 0)
            {
                this.CreateBackupIfNeeded();
                this.Database.Update(Constant.DatabaseTable.FileData, imagesToUpdate);

                // Add an entry into the log detailing what we just did
                StringBuilder log = new StringBuilder(Environment.NewLine);
                log.AppendFormat("System entry: Adjusted dates and times of {0} selected files.{1}", filesToAdjust.Count, Environment.NewLine);
                log.AppendFormat("The first file adjusted was '{0}', the last '{1}', and the last file was adjusted by {2}.{3}", filesToAdjust[0].File, filesToAdjust[filesToAdjust.Count - 1].File, mostRecentAdjustment, Environment.NewLine);
                this.AppendToImageSetLog(log);
            }
        }
        #endregion

        #region ExchangeDayAndMonthInFileDates
        // Update all the date fields by swapping the days and months.
        // This should ONLY be called if such swapping across all dates (excepting corrupt ones) is possible
        // as otherwise it will only swap those dates it can
        // It also assumes that the data table is showing All images
        public void ExchangeDayAndMonthInFileDates()
        {
            this.ExchangeDayAndMonthInFileDates(0, this.CurrentlySelectedFileCount - 1);
        }

        // Update all the date fields between the start and end index by swapping the days and months.
        public void ExchangeDayAndMonthInFileDates(int startRow, int endRow)
        {
            if (this.IsFileRowInRange(startRow) == false)
            {
                throw new ArgumentOutOfRangeException("startRow");
            }
            if (this.IsFileRowInRange(endRow) == false)
            {
                throw new ArgumentOutOfRangeException("endRow");
            }
            if (endRow < startRow)
            {
                throw new ArgumentOutOfRangeException("endRow", "endRow must be greater than or equal to startRow.");
            }
            if (this.CurrentlySelectedFileCount == 0)
            {
                return;
            }

            // Get the original date value of each. If we can swap the date order, do so. 
            List<ColumnTuplesWithWhere> imagesToUpdate = new List<ColumnTuplesWithWhere>();
            ImageRow firstImage = this.FileTable[startRow];
            ImageRow lastImage = null;
            TimeZoneInfo imageSetTimeZone = this.ImageSet.GetSystemTimeZone();
            DateTimeOffset mostRecentOriginalDateTime = DateTime.MinValue;
            DateTimeOffset mostRecentReversedDateTime = DateTime.MinValue;
            for (int row = startRow; row <= endRow; row++)
            {
                ImageRow image = this.FileTable[row];
                DateTimeOffset originalDateTime = image.DateTimeIncorporatingOffset;

                if (DateTimeHandler.TrySwapDayMonth(originalDateTime, out DateTimeOffset reversedDateTime) == false)
                {
                    continue;
                }

                // Now update the actual database with the new date/time values stored in the temporary table
                image.SetDateTimeOffset(reversedDateTime);
                imagesToUpdate.Add(image.GetDateTimeColumnTuples());
                lastImage = image;
                mostRecentOriginalDateTime = originalDateTime;
                mostRecentReversedDateTime = reversedDateTime;
            }

            if (imagesToUpdate.Count > 0)
            {
                this.CreateBackupIfNeeded();
                this.Database.Update(Constant.DatabaseTable.FileData, imagesToUpdate);

                StringBuilder log = new StringBuilder(Environment.NewLine);
                log.AppendFormat("System entry: Swapped days and months for {0} files.{1}", imagesToUpdate.Count, Environment.NewLine);
                log.AppendFormat("The first file adjusted was '{0}' and the last '{1}'.{2}", firstImage.File, lastImage.File, Environment.NewLine);
                log.AppendFormat("The last file's date was changed from '{0}' to '{1}'.{2}", DateTimeHandler.ToDisplayDateString(mostRecentOriginalDateTime), DateTimeHandler.ToDisplayDateString(mostRecentReversedDateTime), Environment.NewLine);
                this.AppendToImageSetLog(log);
            }
        }
        #endregion

        // Delete the data (including markers associated with the images identified by the list of IDs.
        public void DeleteFilesAndMarkers(List<long> fileIDs)
        {
            if (fileIDs.Count < 1)
            {
                // nothing to do
                return;
            }

            List<string> idClauses = new List<string>();
            foreach (long fileID in fileIDs)
            {
                idClauses.Add(Constant.DatabaseColumn.ID + " = " + fileID.ToString());
            }
            // Delete the data and markers associated with that image
            this.CreateBackupIfNeeded();
            this.Database.Delete(Constant.DatabaseTable.FileData, idClauses);
            this.Database.Delete(Constant.DatabaseTable.Markers, idClauses);
        }

        // Convenience routine for checking to see if the image in the given row is displayable (i.e., not corrupted or missing)
        public bool IsFileDisplayable(int rowIndex)
        {
            if (this.IsFileRowInRange(rowIndex) == false)
            {
                return false;
            }
            return this.FileTable[rowIndex].IsDisplayable(this.FolderPath);
        }

        // Find the next displayable file at or after the provided row in the current image set.
        // If there is no next displayable file, then find the closest previous file before the provided row that is displayable.
        public int GetCurrentOrNextDisplayableFile(int startIndex)
        {
            for (int index = startIndex; index < this.CurrentlySelectedFileCount; index++)
            {
                if (this.IsFileDisplayable(index))
                {
                    return index;
                }
            }
            for (int index = startIndex - 1; index >= 0; index--)
            {
                if (this.IsFileDisplayable(index))
                {
                    return index;
                }
            }
            return -1;
        }

        public bool IsFileRowInRange(int imageRowIndex)
        {
            return (imageRowIndex >= 0) && (imageRowIndex < this.CurrentlySelectedFileCount) ? true : false;
        }

        // Find the next displayable image after the provided row in the current image set
        // If there is no next displayable image, then find the first previous image before the provided row that is dispay
        public int FindFirstDisplayableImage(int firstRowInSearch)
        {
            for (int row = firstRowInSearch; row < this.CurrentlySelectedFileCount; row++)
            {
                if (this.IsFileDisplayable(row))
                {
                    return row;
                }
            }
            for (int row = firstRowInSearch - 1; row >= 0; row--)
            {
                if (this.IsFileDisplayable(row))
                {
                    return row;
                }
            }
            return -1;
        }

        public bool TableExists(string dataTable)
        {
            return this.Database.TableExists(dataTable);
        }

        #region FindByFilename variations
        // Find by file name, forwards and backwards with wrapping
        public int FindByFileName(int currentRow, bool isForward, string filename)
        {
            int rowIndex;

            if (isForward)
            {
                // Find forwards with wrapping
                rowIndex = this.FindByFileNameForwards(currentRow + 1, this.CurrentlySelectedFileCount, filename);
                return rowIndex == -1 ? this.FindByFileNameForwards(0, currentRow - 1, filename) : rowIndex;
            }
            else
            {
                // Find backwards  with wrapping
                rowIndex = this.FindByFileNameBackwards(currentRow - 1, 0, filename);
                return rowIndex == -1 ? this.FindByFileNameBackwards(this.CurrentlySelectedFileCount, currentRow + 1, filename) : rowIndex;
            }
        }

        // Helper for FindByFileName
        private int FindByFileNameForwards(int from, int to, string filename)
        {
            for (int rowIndex = from; rowIndex <= to; rowIndex++)
            {
                if (this.FileRowContainsFileName(rowIndex, filename) >= 0)
                {
                    return rowIndex;
                }
            }
            return -1;
        }

        // Helper for FindByFileName
        private int FindByFileNameBackwards(int from, int downto, string filename)
        {
            for (int rowIndex = from; rowIndex >= downto; rowIndex--)
            {
                if (this.FileRowContainsFileName(rowIndex, filename) >= 0)
                {
                    return rowIndex;
                }
            }
            return -1;
        }

        // Helper for FindByFileName
        private int FileRowContainsFileName(int rowIndex, string filename)
        {
            CultureInfo culture = new CultureInfo("en");
            if (this.IsFileRowInRange(rowIndex) == false)
            {
                return -1;
            }
            return culture.CompareInfo.IndexOf(this.FileTable[rowIndex].File, filename, CompareOptions.IgnoreCase);
        }
        #endregion

        // Find the image whose ID is closest to the provided ID  in the current image set
        // If the ID does not exist, then return the image row whose ID is just greater than the provided one. 
        // However, if there is no greater ID (i.e., we are at the end) return the last row. 
        public int FindClosestImageRow(long fileID)
        {
            for (int rowIndex = 0; rowIndex < this.CurrentlySelectedFileCount; ++rowIndex)
            {
                if (this.FileTable[rowIndex].ID >= fileID)
                {
                    return rowIndex;
                }
            }
            return this.CurrentlySelectedFileCount - 1;
        }

        public string GetControlDefaultValue(string dataLabel)
        {
            long id = this.GetControlIDFromTemplateTable(dataLabel);
            ControlRow control = this.Controls.Find(id);
            return control.DefaultValue;
        }

        // Find the file whose ID is closest to the provided ID in the current image set
        // If the ID does not exist, then return the file whose ID is just greater than the provided one. 
        // However, if there is no greater ID (i.e., we are at the end) return the last row. 
        public int GetFileOrNextFileIndex(long fileID)
        {
            // try primary key lookup first as typically the requested ID will be present in the data table
            // (ideally the caller could use the ImageRow found directly, but this doesn't compose with index based navigation)
            ImageRow file = this.FileTable.Find(fileID);
            if (file != null)
            {
                return this.FileTable.IndexOf(file);
            }

            // when sorted by ID ascending so an inexact binary search works
            // Sorting by datetime is usually identical to ID sorting in single camera image sets 
            // But no datetime seed is available if direct ID lookup fails.  Thw API can be reworked to provide a datetime hint
            // if this proves too troublesome.
            int firstIndex = 0;
            int lastIndex = this.CurrentlySelectedFileCount - 1;
            while (firstIndex <= lastIndex)
            {
                int midpointIndex = (firstIndex + lastIndex) / 2;
                file = this.FileTable[midpointIndex];
                long midpointID = file.ID;

                if (fileID > midpointID)
                {
                    // look at higher index partition next
                    firstIndex = midpointIndex + 1;
                }
                else if (fileID < midpointID)
                {
                    // look at lower index partition next
                    lastIndex = midpointIndex - 1;
                }
                else
                {
                    // found the ID closest to fileID
                    return midpointIndex;
                }
            }

            // all IDs in the selection are smaller than fileID
            if (firstIndex >= this.CurrentlySelectedFileCount)
            {
                return this.CurrentlySelectedFileCount - 1;
            }

            // all IDs in the selection are larger than fileID
            return firstIndex;
        }

        // Return all distinct values from a column in the file database
        // We used to use this for autocomplete, but its now depracated as 
        // we scan the file table instead. However, this is a bit of a limitation, as it means autocomplete
        // only works on the Selected File row vs. every entry.
        public List<string> GetDistinctValuesInFileDataBaseTableColumn(string dataLabel)
        {
            List<string> distinctValues = new List<string>();
            foreach (object value in this.Database.GetDistinctValuesInColumn(Constant.DatabaseTable.FileData, dataLabel))
            {
                distinctValues.Add(value.ToString());
            }
            return distinctValues;
        }

        // Return all distinct values from a column in the file table
        // Note that this returns distinct values only in the SELECTED files
        // See comments above in GetDistinctValuesInFileDataBaseTableColumn
        // Perhaps - GetDistinctValuesInSelectedFileTableColumn 
        // - maybe check substrings before adding, to avoid having too many entries?
        // - or, only store the longest version of a string. But this would involve more work when adding entries, so likely not worth it.
        public Dictionary<string, string> GetDistinctValuesInSelectedFileTableColumn(string dataLabel, int minimumNumberOfRequiredCharacters)
        {
            Dictionary<string, string> distinctValues = new Dictionary<string, string>();
            foreach (ImageRow row in this.FileTable)
            {
                string value = row.GetValueDatabaseString(dataLabel);
                if (value.Length < minimumNumberOfRequiredCharacters)
                {
                    continue;
                }
                if (distinctValues.ContainsKey(value) == false)
                {
                    distinctValues.Add(value, String.Empty);
                }
            }
            return distinctValues;
        }

        private void GetImageSet()
        {
            string imageSetQuery = Constant.Sqlite.SelectStarFrom + Constant.DatabaseTable.ImageSet + Constant.Sqlite.Where + Constant.DatabaseColumn.ID + " = " + Constant.DatabaseValues.ImageSetRowID.ToString();
            DataTable imageSetTable = this.Database.GetDataTableFromSelect(imageSetQuery);
            this.ImageSet = new ImageSetRow(imageSetTable.Rows[0]);
        }

        /// <summary>
        /// Get all markers for the specified file.
        /// This is done by getting the marker list associated with all counters representing the current row
        /// It will have a MarkerCounter for each control, even if there may be no metatags in it
        /// </summary>
        public List<MarkersForCounter> GetMarkersForCurrentFile(long fileID)
        {
            List<MarkersForCounter> markersForAllCounters = new List<MarkersForCounter>();

            // Get the current row number of the id in the marker table
            MarkerRow markersForImage = this.Markers.Find(fileID);
            if (markersForImage == null)
            {
                return markersForAllCounters;
            }

            // Iterate through the columns, where we create a MarkersForCounter for each control and add it to the MarkersForCounter list
            foreach (string dataLabel in markersForImage.DataLabels)
            {
                // create a marker for each point and add it to the counter 
                MarkersForCounter markersForCounter = new MarkersForCounter(dataLabel);
                string pointList;
                try
                {
                    pointList = markersForImage[dataLabel];
                }
                catch (Exception exception)
                {
                    TraceDebug.PrintMessage(String.Format("Read of marker failed for dataLabel '{0}'. {1}", dataLabel, exception.ToString()));
                    pointList = String.Empty;
                }
                markersForCounter.Parse(pointList);
                markersForAllCounters.Add(markersForCounter);
            }
            return markersForAllCounters;
        }

        private void GetMarkers()
        {
            string markersQuery = Constant.Sqlite.SelectStarFrom + Constant.DatabaseTable.Markers;
            this.Markers = new DataTableBackedList<MarkerRow>(this.Database.GetDataTableFromSelect(markersQuery), (DataRow row) => { return new MarkerRow(row); });
        }

        /// <summary>
        /// Set the list of marker points on the current row in the marker table. 
        /// </summary>
        public void SetMarkerPositions(long imageID, MarkersForCounter markersForCounter)
        {
            // Find the current row number
            MarkerRow marker = this.Markers.Find(imageID);
            if (marker == null)
            {
                TraceDebug.PrintMessage(String.Format("Image ID {0} missing in markers table.", imageID));
                return;
            }

            // Update the database and datatable
            marker[markersForCounter.DataLabel] = markersForCounter.GetPointList();
            this.SyncMarkerToDatabase(marker);
        }

        public void SyncImageSetToDatabase()
        {
            // don't trigger backups on image set updates as none of the properties in the image set table is particularly important
            // For example, this avoids creating a backup when a custom selection is reverted to all when Timelapse exits.
            this.Database.Update(Constant.DatabaseTable.ImageSet, this.ImageSet.GetColumnTuples());
        }

        public void SyncMarkerToDatabase(MarkerRow marker)
        {
            this.CreateBackupIfNeeded();
            this.Database.Update(Constant.DatabaseTable.Markers, marker.GetColumnTuples());
        }

        // The id is the row to update, the datalabels are the labels of each control to updata, 
        // and the markers are the respective point lists for each of those labels
        public void UpdateMarkers(List<ColumnTuplesWithWhere> markersToUpdate)
        {
            // update markers in database
            this.CreateBackupIfNeeded();
            this.Database.Update(Constant.DatabaseTable.Markers, markersToUpdate);

            // update markers in marker data table
            this.GetMarkers();
        }

        protected override void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }

            if (disposing)
            {
                if (this.FileTable != null)
                {
                    this.FileTable.Dispose();
                }
                if (this.Markers != null)
                {
                    this.Markers.Dispose();
                }
            }

            base.Dispose(disposing);
            this.disposed = true;
        }

        private static ColumnDefinition CreateFileDataColumnDefinition(ControlRow control)
        {
            if (control.DataLabel == Constant.DatabaseColumn.DateTime)
            {
                return new ColumnDefinition(control.DataLabel, "DATETIME", DateTimeHandler.ToDatabaseDateTimeString(Constant.ControlDefault.DateTimeValue));
            }
            if (control.DataLabel == Constant.DatabaseColumn.UtcOffset)
            {
                // UTC offsets are typically represented as TimeSpans but the least awkward way to store them in SQLite is as a real column containing the offset in
                // hours.  This is because SQLite
                // - handles TIME columns as DateTime rather than TimeSpan, requiring the associated DataTable column also be of type DateTime
                // - doesn't support negative values in time formats, requiring offsets for time zones west of Greenwich be represented as positive values
                // - imposes an upper bound of 24 hours on time formats, meaning the 26 hour range of UTC offsets (UTC-12 to UTC+14) cannot be accomodated
                // - lacks support for DateTimeOffset, so whilst offset information can be written to the database it cannot be read from the database as .NET
                //   supports only DateTimes whose offset matches the current system time zone
                // Storing offsets as ticks, milliseconds, seconds, minutes, or days offers equivalent functionality.  Potential for rounding error in roundtrip 
                // calculations on offsets is similar to hours for all formats other than an INTEGER (long) column containing ticks.  Ticks are a common 
                // implementation choice but testing shows no roundoff errors at single tick precision (100 nanoseconds) when using hours.  Even with TimeSpans 
                // near the upper bound of 256M hours, well beyond the plausible range of time zone calculations.  So there does not appear to be any reason to 
                // avoid using hours for readability when working with the database directly.
                return new ColumnDefinition(control.DataLabel, "REAL", DateTimeHandler.ToDatabaseUtcOffsetString(Constant.ControlDefault.DateTimeValue.Offset));
            }
            if (String.IsNullOrWhiteSpace(control.DefaultValue))
            {
                return new ColumnDefinition(control.DataLabel, Constant.Sqlite.Text, String.Empty);
            }
            return new ColumnDefinition(control.DataLabel, Constant.Sqlite.Text, control.DefaultValue);
        }

        #region DETECTION INTEGRATION
        public bool PopulateDetectionTables(string path)
        {
            DetectionDatabases.CreateOrRecreateTablesAndColumns(this.Database);
            if (File.Exists(path) == false)
            {
                return false;
            }
            try
            {
                Stopwatch sw = new Stopwatch();
                sw.Start();

                using (Detector detector = JsonConvert.DeserializeObject<Detector>(File.ReadAllText(path)))
                {
                    sw.Stop();
                    System.Diagnostics.Debug.Print(("Json Time: " + sw.ElapsedMilliseconds / 1000).ToString());
                    sw.Reset();
                    // Its possible that the folder where the .tdb file is located is either:
                    // - the same as the root folder where the detections were done 
                    // - a subfolder somewhere beneath the root folder.
                    // If a subfolder, then the file paths for the detections will not be valid.
                    // The code below examines one of the existing images and compares it to every folder path in the detection file, where
                    // it tries to find the portion of the path that is 'above' the current folder. Because this could be ambiguous, it creates a list
                    // of possible folders where the sub-folder could have originally been located in, and asks the user to disambiguate between them
                    // For example, consider detections done on these folders:
                    //  A/B/C/X/Y
                    //  A/B/D/Y/Z
                    //  A/B/S/T
                    // If the Y/Z folder from A/B/C was moved to (say) myFolder and the .tdb located there, 
                    // the two possible locations would be A/B/C/Y/Z and A/B/D/Y/Z
                    // The code below would present those two to the user and ask the user to choose which one is the correct one.
                    // Later, any detecton image not matching A/B/C will be ignored, and for the ones matching A/B/C will be trimmed off the path
                    string pathPrefixToTruncateFromPaths = String.Empty;
                    string folderpath;
                    if (detector.images.Count <= 0)
                    {
                        // No point continuing as there are no detector images
                        return false;
                    }

                    // Get an example file for comparing its path against the detector paths
                    int fileindex = this.GetCurrentOrNextDisplayableFile(0);
                    if (fileindex < 0)
                    {
                        // No point continuing as there are no files to process
                        return false;
                    }

                    // First pass. Get all the unique folder paths from the detector images
                    sw.Start();
                    string imageFilePath = this.FileTable[fileindex].RelativePath;
                    string imageFileName = this.FileTable[fileindex].File;
                    SortedSet<string> folders = new SortedSet<string>();
                    foreach (image image in detector.images)
                    {
                        folderpath = Path.GetDirectoryName(image.file);
                        if (folderpath != String.Empty)
                        {
                            folderpath += "\\";
                        }
                        if (folders.Contains(folderpath) == false)
                        {
                            folders.Add(folderpath);
                        }
                    }

                    // Add a closing slash onto the imageFilePath to terminate any matches
                    // e.g., A/B  would also match A/Buzz, which we don't want. But A/B/ won't match that.
                    if (imageFilePath != String.Empty)
                    {
                        imageFilePath += "\\";
                    }

                    // Second Pass. For all folder paths in the detections, find the minimum prefix that matches the sampleimage file path 
                    // and create a 
                    int shortestIndex = int.MaxValue;
                    int currentIndex;
                    string highestFoldermatch = String.Empty;
                    foreach (string folder in folders)
                    {
                        currentIndex = folder.IndexOf(imageFilePath);
                        if ((currentIndex < shortestIndex) && (currentIndex >= 0))
                        {
                            shortestIndex = folder.IndexOf(imageFilePath);
                            highestFoldermatch = folder;
                        }
                    }
                    currentIndex = highestFoldermatch.IndexOf(imageFilePath);
                    pathPrefixToTruncateFromPaths = highestFoldermatch.Substring(0, currentIndex);
                    List<string> candidateFolders = new List<string>();
                    foreach (string folder in folders)
                    {
                        if (folder.IndexOf(imageFilePath) != -1)
                        {
                            candidateFolders.Add(folder);
                        }
                    }

                    // Third step. If there is more than one candidate folder that may have contained the image set, 
                    // ask the user to disambiguate which one it is.
                    if (candidateFolders.Count > 1)
                    {
                        ChooseDetectorFilePath chooseDetectorFilePath = new ChooseDetectorFilePath(candidateFolders, pathPrefixToTruncateFromPaths, Path.Combine(imageFilePath, imageFileName), GlobalReferences.MainWindow);
                        if (chooseDetectorFilePath.ShowDialog() == true)
                        {
                            currentIndex = chooseDetectorFilePath.SelectedFolder.IndexOf(imageFilePath);
                            pathPrefixToTruncateFromPaths = chooseDetectorFilePath.SelectedFolder.Substring(0, currentIndex).Replace('\\', '/');
                        }
                        else
                        {
                            // The user has aborted detections, likely because they didn't know which folder to choose
                            return false;
                        }
                    }
                    else
                    {
                        // There is only one or 0 candidate folders, so we know what the path Prefix should be.
                        pathPrefixToTruncateFromPaths = (candidateFolders.Count == 1) ? candidateFolders[0] : String.Empty;
                    }
                    sw.Stop();
                    System.Diagnostics.Debug.Print("Dealing with truncations: " + sw.ElapsedMilliseconds.ToString());
                    // END TEST STUFF FOR FINDING CORRECT SUBFOLDER

                    // If detection population was previously done in this session, resetting these tables to null 
                    // will force reading the new values into them
                    this.detectionDataTable = null; // to force repopulating the data structure if it already exists.
                    this.detectionCategoriesDictionary = null;
                    this.classificationCategoriesDictionary = null;
                    this.classificationsDataTable = null;

                    sw.Reset();
                    sw.Start();
                    DetectionDatabases.PopulateTables(detector, this, this.Database, pathPrefixToTruncateFromPaths);
                    sw.Stop();
                    System.Diagnostics.Debug.Print(("Populate Time: " + sw.ElapsedMilliseconds / 1000).ToString());
                    return true;
                }
            }
            catch
            {
                System.Diagnostics.Debug.Print("Could not populate detection data");
                return false;
            }
        }

        // Get the detections associated with the current file, if any
        // As part of the, create a DetectionTable in memory that mirrors the database table
        public DataRow[] GetDetectionsFromFileID(long fileID)
        {
            if (this.detectionDataTable == null)
            {
                Stopwatch sw = new Stopwatch();
                sw.Start();
                this.detectionDataTable = this.Database.GetDataTableFromSelect(Constant.Sqlite.SelectStarFrom + Constant.DBTableNames.Detections);
                sw.Stop();
                System.Diagnostics.Debug.Print(String.Format("creating detections datatable took {0}", sw.ElapsedMilliseconds / 1000));
                // this.DetectionDataTable.PrimaryKey = new DataColumn[] { this.DetectionDataTable.Columns[Constant.DatabaseColumn.ID] };
            }
            // Note that because IDs are in the database as a string, we convert it
            return this.detectionDataTable.Select(Constant.DatabaseColumn.ID + Constant.Sqlite.Equal + fileID.ToString());
        }

        // Get the detections associated with the current file, if any
        public DataRow[] GetClassificationsFromDetectionID(long detectionID)
        {
            if (this.classificationsDataTable == null)
            {
                Stopwatch sw = new Stopwatch();
                sw.Start();
                this.classificationsDataTable = this.Database.GetDataTableFromSelect(Constant.Sqlite.SelectStarFrom + Constant.DBTableNames.Classifications);
                sw.Stop();
                System.Diagnostics.Debug.Print(String.Format("creating classifications datatable took {0}", sw.ElapsedMilliseconds / 1000));
                // this.ClassificationsDataTable.PrimaryKey = new DataColumn[] { this.ClassificationsDataTable.Columns[Constant.ClassificationColumns.DetectionID]};
            }
            // Note that because IDs are in the database as a string, we convert it
            return this.classificationsDataTable.Select(Constant.ClassificationColumns.DetectionID + Constant.Sqlite.Equal + detectionID.ToString());
        }

        // Return the label that matches the detection category 
        public string GetDetectionLabelFromCategory(string category)
        {
            try
            {
                // Null means we have never tried to create the dictionary. Try to do so.
                if (this.detectionCategoriesDictionary == null)
                {
                    this.detectionCategoriesDictionary = new Dictionary<string, string>();
                    DataTable dataTable = this.Database.GetDataTableFromSelect(Constant.Sqlite.SelectStarFrom + Constant.DBTableNames.DetectionCategories);
                    for (int i = 0; i < dataTable.Rows.Count; i++)
                    {
                        DataRow row = dataTable.Rows[i];
                        this.detectionCategoriesDictionary.Add((string)row[Constant.DetectionCategoriesColumns.Category], (string)row[Constant.DetectionCategoriesColumns.Label]);
                    }
                }

                // At this point, a lookup dictionary already exists, so just return the category value.
                return detectionCategoriesDictionary.TryGetValue(category, out string value) ? value : String.Empty;
            }
            catch
            {
                // Should never really get here, but just in case.
                return String.Empty;
            }
        }

        public void CreateDetectionCategoriesDictionaryIfNeeded()
        {
            // Null means we have never tried to create the dictionary. Try to do so.
            if (this.detectionCategoriesDictionary == null)
            {
                this.detectionCategoriesDictionary = new Dictionary<string, string>();

                DataTable dataTable = this.Database.GetDataTableFromSelect(Constant.Sqlite.SelectStarFrom + Constant.DBTableNames.DetectionCategories);
                for (int i = 0; i < dataTable.Rows.Count; i++)
                {
                    DataRow row = dataTable.Rows[i];
                    this.detectionCategoriesDictionary.Add((string)row[Constant.DetectionCategoriesColumns.Category], (string)row[Constant.DetectionCategoriesColumns.Label]);
                }
            }
        }

        // Create the detection category dictionary to mirror the detection table
        public string GetDetectionCategoryFromLabel(string label)
        {
            try
            {
                CreateDetectionCategoriesDictionaryIfNeeded();
                // At this point, a lookup dictionary already exists, so just return the category value.
                string myKey = detectionCategoriesDictionary.FirstOrDefault(x => x.Value == label).Key;
                return myKey ?? String.Empty;
            }
            catch
            {
                // Should never really get here, but just in case.
                return String.Empty;
            }
        }

        public List<string> GetDetectionLabels()
        {
            List<string> labels = new List<string>();
            CreateDetectionCategoriesDictionaryIfNeeded();
            foreach (KeyValuePair<string, string> entry in this.detectionCategoriesDictionary)
            {
                labels.Add(entry.Value);
            }
            return labels;
        }

        // Create the classification category dictionary to mirror the detection table
        public void CreateClassificationCategoriesDictionaryIfNeeded()
        {
            // Null means we have never tried to create the dictionary. Try to do so.
            if (this.classificationCategoriesDictionary == null)
            {
                this.classificationCategoriesDictionary = new Dictionary<string, string>();
                DataTable dataTable = this.Database.GetDataTableFromSelect(Constant.Sqlite.SelectStarFrom + Constant.DBTableNames.ClassificationCategories);
                for (int i = 0; i < dataTable.Rows.Count; i++)
                {
                    DataRow row = dataTable.Rows[i];
                    this.classificationCategoriesDictionary.Add((string)row[Constant.ClassificationCategoriesColumns.Category], (string)row[Constant.ClassificationCategoriesColumns.Label]);
                }
            }
        }

        // return the label that matches the detection category 
        public string GetClassificationLabelFromCategory(string category)
        {
            try
            {
                CreateClassificationCategoriesDictionaryIfNeeded();

                // At this point, a lookup dictionary already exists, so just return the category value.
                return classificationCategoriesDictionary.TryGetValue(category, out string value) ? value : String.Empty;
            }
            catch
            {
                // Should never really get here, but just in case.
                return String.Empty;
            }
        }
        public bool DetectionsExists()
        {
            return this.Database.TableExistsAndNotEmpty(Constant.DBTableNames.Detections);
        }
        #endregion
    }
}
