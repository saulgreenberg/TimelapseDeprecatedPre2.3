using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Dialog;
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
        public FileTable Files { get; private set; }

        public ImageSetRow ImageSet { get; private set; }

        // contains the markers
        public DataTableBackedList<MarkerRow> Markers { get; private set; }

        // flags
        public bool OrderFilesByDateTime { get; set; }

        #endregion

        private FileDatabase(string filePath)
            : base(filePath)
        {
            this.DataLabelFromStandardControlType = new Dictionary<string, string>();
            this.disposed = false;
            this.FolderPath = Path.GetDirectoryName(filePath);
            this.FileName = Path.GetFileName(filePath);
            this.FileTableColumnsByDataLabel = new Dictionary<string, FileTableColumn>();
            this.OrderFilesByDateTime = false;
        }

        public static FileDatabase CreateOrOpen(string filePath, TemplateDatabase templateDatabase, bool orderFilesByDate, CustomSelectionOperator customSelectionTermCombiningOperator, TemplateSyncResults templateSyncResults)
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
            fileDatabase.OrderFilesByDateTime = orderFilesByDate;
            fileDatabase.PopulateDataLabelMaps();
            return fileDatabase;
        }

        public List<object> GetDistinctValuesInColumn(string table, string columnName)
        {
            return this.Database.GetDistinctValuesInColumn(table, columnName);
        }

        /// <summary>Gets the number of files currently in the file table.</summary>
        public int CurrentlySelectedFileCount
        {
            get { return this.Files.RowCount; }
        }

        public void AddFiles(List<ImageRow> files, Action<ImageRow, int> onFileAdded)
        {
            // We need to get a list of which columns are counters vs notes or fixed coices, 
            // as we will shortly have to initialize them to some defaults
            List<string> counterList = new List<string>();
            List<string> notesAndFixedChoicesList = new List<string>();
            List<string> flagsList = new List<string>();
            foreach (string columnName in this.Files.ColumnNames)
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
            for (int image = 0; image < files.Count; image += Constant.Database.RowsPerInsert)
            {
                // Create a dataline from the image properties, add it to a list of data lines,
                // then do a multiple insert of the list of datalines to the database 
                List<List<ColumnTuple>> fileDataRows = new List<List<ColumnTuple>>();
                List<List<ColumnTuple>> markerRows = new List<List<ColumnTuple>>();
                for (int insertIndex = image; (insertIndex < (image + Constant.Database.RowsPerInsert)) && (insertIndex < files.Count); insertIndex++)
                {
                    List<ColumnTuple> imageRow = new List<ColumnTuple>();
                    List<ColumnTuple> markerRow = new List<ColumnTuple>();
                    foreach (string columnName in this.Files.ColumnNames)
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
                                imageRow.Add(new ColumnTuple(dataLabel, imageProperties.FileName));
                                break;
                            case Constant.DatabaseColumn.RelativePath: // Add the relative path name
                                dataLabel = this.DataLabelFromStandardControlType[Constant.DatabaseColumn.RelativePath];
                                imageRow.Add(new ColumnTuple(dataLabel, imageProperties.RelativePath));
                                break;
                            case Constant.DatabaseColumn.Folder: // Add The Folder name
                                dataLabel = this.DataLabelFromStandardControlType[Constant.DatabaseColumn.Folder];
                                imageRow.Add(new ColumnTuple(dataLabel, imageProperties.InitialRootFolderName));
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
                                Utilities.PrintFailure(String.Format("Unhandled control type '{0}' in AddImages.", controlType));
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
                    int lastImageInserted = Math.Min(files.Count - 1, image + Constant.Database.RowsPerInsert);
                    onFileAdded.Invoke(files[lastImageInserted], lastImageInserted);
                }
            }

            // Load / refresh the marker table from the database to keep it in sync - Doing so here will make sure that there is one row for each image.
            this.GetMarkers();
        }

        public void AppendToImageSetLog(StringBuilder logEntry)
        {
            this.ImageSet.Log += logEntry;
            this.SyncImageSetToDatabase();
        }

        public void BindToDataGrid(DataGrid dataGrid, DataRowChangeEventHandler onRowChanged)
        {
            this.boundGrid = dataGrid;
            this.onFileDataTableRowChanged = onRowChanged;
            this.Files.BindDataGrid(dataGrid, onRowChanged);
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
                new ColumnDefinition(Constant.DatabaseColumn.ID, Constant.Sql.CreationStringPrimaryKey)  // It begins with the ID integer primary key
            };
            foreach (ControlRow control in this.Controls)
            {
                columnDefinitions.Add(this.CreateFileDataColumnDefinition(control));
            }
            this.Database.CreateTable(Constant.DatabaseTable.FileData, columnDefinitions);

            // SAULXX THIS IS IN TODDs - Don't uncomment it until we figure out how he uses it. He creates it but I am unsure if he actually uses it
            // Index the DateTime column
            // this.Database.ExecuteNonQuery("CREATE INDEX 'FileDateTimeIndex' ON 'FileData' ('DateTime')");

            // Create the ImageSetTable and initialize a single row in it
            columnDefinitions.Clear();
            columnDefinitions.Add(new ColumnDefinition(Constant.DatabaseColumn.ID, Constant.Sql.CreationStringPrimaryKey));  // It begins with the ID integer primary key
            columnDefinitions.Add(new ColumnDefinition(Constant.DatabaseColumn.Log, Constant.Sql.Text, Constant.Database.ImageSetDefaultLog));
            columnDefinitions.Add(new ColumnDefinition(Constant.DatabaseColumn.MagnifyingGlass, Constant.Sql.Text, Constant.BooleanValue.True));       
            columnDefinitions.Add(new ColumnDefinition(Constant.DatabaseColumn.MostRecentFileID, Constant.Sql.Text));
            int allImages = (int)FileSelection.All;
            columnDefinitions.Add(new ColumnDefinition(Constant.DatabaseColumn.Selection, Constant.Sql.Text, allImages));
            columnDefinitions.Add(new ColumnDefinition(Constant.DatabaseColumn.WhiteSpaceTrimmed, Constant.Sql.Text));
            columnDefinitions.Add(new ColumnDefinition(Constant.DatabaseColumn.TimeZone, Constant.Sql.Text));
            this.Database.CreateTable(Constant.DatabaseTable.ImageSet, columnDefinitions);

            // Populate the data for the image set with defaults
            List<ColumnTuple> columnsToUpdate = new List<ColumnTuple>
            {
                new ColumnTuple(Constant.DatabaseColumn.Log, Constant.Database.ImageSetDefaultLog),
                new ColumnTuple(Constant.DatabaseColumn.MagnifyingGlass, Constant.BooleanValue.True),
                new ColumnTuple(Constant.DatabaseColumn.MostRecentFileID, Constant.Database.InvalidID),
                new ColumnTuple(Constant.DatabaseColumn.Selection, allImages.ToString()),
                new ColumnTuple(Constant.DatabaseColumn.WhiteSpaceTrimmed, Constant.BooleanValue.True),
                new ColumnTuple(Constant.DatabaseColumn.TimeZone, TimeZoneInfo.Local.Id)
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
            this.SelectFiles(FileSelection.All);

            // Create the MarkersTable and initialize it from the template table
            columnDefinitions.Clear();
            columnDefinitions.Add(new ColumnDefinition(Constant.DatabaseColumn.ID, Constant.Sql.CreationStringPrimaryKey));  // It begins with the ID integer primary key
            string type = String.Empty;
            foreach (ControlRow control in this.Controls)
            {
                if (control.Type.Equals(Constant.Control.Counter))
                {
                    columnDefinitions.Add(new ColumnDefinition(control.DataLabel, Constant.Sql.Text, String.Empty));
                }
            }
            this.Database.CreateTable(Constant.DatabaseTable.Markers, columnDefinitions);
        }

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

        protected override void UpgradeDatabasesAndCompareTemplates(TemplateDatabase templateDatabase, TemplateSyncResults templateSyncResults)
        {
            // perform TemplateTable initializations and migrations, then check for synchronization issues
            base.UpgradeDatabasesAndCompareTemplates(templateDatabase, null);

            // Upgrade the database from older to newer formats to preserve backwards compatability
            this.UpgradeDatabasesForBackwardsCompatability();

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

        // Upgrade the database as needed from older to newer formats to preserve backwards compatability 
        private void UpgradeDatabasesForBackwardsCompatability()
        {
            this.SelectFiles(FileSelection.All);
            bool refreshImageDataTable = false;

            // RelativePath column (if missing) needs to be added 
            if (this.Files.ColumnNames.Contains(Constant.DatabaseColumn.RelativePath) == false)
            {
                long relativePathID = this.GetControlIDFromTemplateTable(Constant.DatabaseColumn.RelativePath);
                ControlRow relativePathControl = this.Controls.Find(relativePathID);
                ColumnDefinition columnDefinition = this.CreateFileDataColumnDefinition(relativePathControl);
                this.Database.AddColumnToTable(Constant.DatabaseTable.FileData, Constant.Database.RelativePathPosition, columnDefinition);
                refreshImageDataTable = true;
            }

            // DateTime column (if missing) needs to be added 
            if (this.Files.ColumnNames.Contains(Constant.DatabaseColumn.DateTime) == false)
            {
                long dateTimeID = this.GetControlIDFromTemplateTable(Constant.DatabaseColumn.DateTime);
                ControlRow dateTimeControl = this.Controls.Find(dateTimeID);
                ColumnDefinition columnDefinition = this.CreateFileDataColumnDefinition(dateTimeControl);
                this.Database.AddColumnToTable(Constant.DatabaseTable.FileData, Constant.Database.DateTimePosition, columnDefinition);
                refreshImageDataTable = true;
            }

            // UTCOffset column (if missing) needs to be added 
            if (this.Files.ColumnNames.Contains(Constant.DatabaseColumn.UtcOffset) == false)
            {
                long utcOffsetID = this.GetControlIDFromTemplateTable(Constant.DatabaseColumn.UtcOffset);
                ControlRow utcOffsetControl = this.Controls.Find(utcOffsetID);
                ColumnDefinition columnDefinition = this.CreateFileDataColumnDefinition(utcOffsetControl);
                this.Database.AddColumnToTable(Constant.DatabaseTable.FileData, Constant.Database.UtcOffsetPosition, columnDefinition);
                refreshImageDataTable = true;
            }

            // Remove MarkForDeletion column and add DeleteFlag column(if needed)
            bool hasMarkForDeletion = this.Database.IsColumnInTable(Constant.DatabaseTable.FileData, Constant.ControlsDeprecated.MarkForDeletion);
            bool hasDeleteFlag = this.Database.IsColumnInTable(Constant.DatabaseTable.FileData, Constant.DatabaseColumn.DeleteFlag);
            if (hasMarkForDeletion && (hasDeleteFlag == false))
            {
                // migrate any existing MarkForDeletion column to DeleteFlag
                // this is likely the most typical case
                this.Database.RenameColumn(Constant.DatabaseTable.FileData, Constant.ControlsDeprecated.MarkForDeletion, Constant.DatabaseColumn.DeleteFlag);
                refreshImageDataTable = true;
            }
            else if (hasMarkForDeletion && hasDeleteFlag)
            {
                // if both MarkForDeletion and DeleteFlag are present drop MarkForDeletion
                // this is not expected to occur
                this.Database.DeleteColumn(Constant.DatabaseTable.FileData, Constant.ControlsDeprecated.MarkForDeletion);
                refreshImageDataTable = true;
            }
            else if (hasDeleteFlag == false)
            {
                // if there's neither a MarkForDeletion or DeleteFlag column add DeleteFlag
                long id = this.GetControlIDFromTemplateTable(Constant.DatabaseColumn.DeleteFlag);
                ControlRow control = this.Controls.Find(id);
                ColumnDefinition columnDefinition = this.CreateFileDataColumnDefinition(control);
                this.Database.AddColumnToEndOfTable(Constant.DatabaseTable.FileData, columnDefinition);
                refreshImageDataTable = true;
            }

            if (refreshImageDataTable)
            {
                // update image data table to current schema
                this.SelectFiles(FileSelection.All);
            }

            // Make sure that all the string data in the datatable has white space trimmed from its beginning and end
            // This is needed as the custom selection doesn't work well in testing comparisons if there is leading or trailing white space in it
            // Newer versions of Timelapse  trim the data as it is entered, but older versions did not, so this is to make it backwards-compatable.
            // The WhiteSpaceExists column in the ImageSet Table did not exist before this version, so we add it to the table if needed. If it exists, then 
            // we know the data has been trimmed and we don't have to do it again as the newer versions take care of trimmingon the fly.
            bool whiteSpaceColumnExists = this.Database.IsColumnInTable(Constant.DatabaseTable.ImageSet, Constant.DatabaseColumn.WhiteSpaceTrimmed);
            if (!whiteSpaceColumnExists)
            {
                // create the whitespace column
                this.Database.AddColumnToEndOfTable(Constant.DatabaseTable.ImageSet, new ColumnDefinition(Constant.DatabaseColumn.WhiteSpaceTrimmed, Constant.Sql.Text, Constant.BooleanValue.False));

                // trim whitespace from the data table
                this.Database.TrimWhitespace(Constant.DatabaseTable.FileData, this.GetDataLabelsExceptIDInSpreadsheetOrder());

                // mark image set as whitespace trimmed
                this.GetImageSet();
                this.ImageSet.WhitespaceTrimmed = true;
                // This still has to be synchronized, which will occur after we prepare all missing columns
            }

            // Timezone column (if missing) needs to be added to the Imageset Table
            bool timeZoneColumnExists = this.Database.IsColumnInTable(Constant.DatabaseTable.ImageSet, Constant.DatabaseColumn.TimeZone);
            bool timeZoneColumnIsNotPopulated = timeZoneColumnExists;
            if (!timeZoneColumnExists)
            {
                // create default time zone entry
                this.Database.AddColumnToEndOfTable(Constant.DatabaseTable.ImageSet, new ColumnDefinition(Constant.DatabaseColumn.TimeZone, Constant.Sql.Text));
                this.GetImageSet();
                this.ImageSet.TimeZone = TimeZoneInfo.Local.Id;
                // This still has to be synchronized, which will occur until after we prepare all missing columns
            }

            // Check to see if synchronization is needed i.e., if any of the columns were missing. If so, synchronziation will add those columns.
            if (!timeZoneColumnExists || (!whiteSpaceColumnExists))
            {
                this.SyncImageSetToDatabase();
            }

            // Populate DateTime column if the column has just been added
            if (!timeZoneColumnIsNotPopulated)
            {
                TimeZoneInfo imageSetTimeZone = this.ImageSet.GetTimeZone();
                List<ColumnTuplesWithWhere> updateQuery = new List<ColumnTuplesWithWhere>();

                foreach (ImageRow image in this.Files)
                {
                    // NEED TO GET Legacy DATE TIME  (i.e., FROM DATE AND TIME fields) as the new DateTime did not exist in this old database. 
                    bool result = DateTimeHandler.TryParseLegacyDateTime(image.Date, image.Time, imageSetTimeZone, out DateTimeOffset imageDateTime);
                    if (!result)
                    {
                        // If we can't get the legacy date time, try getting the date time this way
                        imageDateTime = image.GetDateTime();
                    }
                    image.SetDateTimeOffset(imageDateTime);
                    updateQuery.Add(image.GetDateTimeColumnTuples());
                }
                this.Database.Update(Constant.DatabaseTable.FileData, updateQuery);
            }
        }

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
                    ColumnDefinition columnDefinition = this.CreateFileDataColumnDefinition(control);
                    this.Database.AddColumnToEndOfTable(Constant.DatabaseTable.FileData, columnDefinition);

                    if (control.Type == Constant.Control.Counter)
                    {
                        ColumnDefinition markerColumnDefinition = new ColumnDefinition(dataLabel, Constant.Sql.Text);
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
                    if (this.Database.ColumnExists(Constant.DatabaseTable.Markers, dataLabel))
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
                    if (this.Database.ColumnExists(Constant.DatabaseTable.Markers, dataLabelToRename.Key))
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
            this.SelectFiles(FileSelection.All);
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

            string query = Constant.Sql.SelectStarFrom + Constant.DatabaseTable.FileData + Constant.Sql.Where + where;
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
        public void SelectFiles(FileSelection selection)
        {
            string query = Constant.Sql.SelectStarFrom + Constant.DatabaseTable.FileData;
            string where = this.GetFilesWhere(selection);
            if (String.IsNullOrEmpty(where) == false)
            {
                query += Constant.Sql.Where + where;
            }
            if (this.OrderFilesByDateTime)
            {
                query += Constant.Sql.OrderBy + Constant.DatabaseColumn.DateTime;
            }

            DataTable images = this.Database.GetDataTableFromSelect(query);
            this.Files = new FileTable(images);
            this.Files.BindDataGrid(this.boundGrid, this.onFileDataTableRowChanged);
        }

        public FileTable GetFilesMarkedForDeletion()
        {
            string where = this.DataLabelFromStandardControlType[Constant.DatabaseColumn.DeleteFlag] + "=" + Utilities.QuoteForSql(Constant.BooleanValue.True); // = value
            string query = Constant.Sql.SelectStarFrom + Constant.DatabaseTable.FileData + Constant.Sql.Where + where;
            DataTable images = this.Database.GetDataTableFromSelect(query);
            return new FileTable(images);
        }

        // SAULXXX: TEMPORARY - TO FIX DUPLICATE BUG. TO BE REMOVED IN FUTURE VERSIONS
        // Get a table containing a subset of rows that have duplicate File and RelativePaths 
        public FileTable GetDuplicateFiles()
        {
            string query = Constant.Sql.Select + " RelativePath, File, COUNT(*) " + Constant.Sql.From + Constant.DatabaseTable.FileData;
            query += Constant.Sql.GroupBy + " RelativePath, File HAVING COUNT(*) > 1";
            DataTable images = this.Database.GetDataTableFromSelect(query);
            return new FileTable(images);
        }

        // SAULXXX: TEMPORARY - TO FIX DUPLICATE BUG. TO BE REMOVED IN FUTURE VERSIONS
        // Delete duplicate rows from the database, identified by identical File and RelativePath contents.
        public void DeleteDuplicateFiles()
        {
            string query = Constant.Sql.DeleteFrom + Constant.DatabaseTable.FileData + Constant.Sql.WhereIDNotIn;
            query += Constant.Sql.OpenParenthesis + Constant.Sql.Select + " MIN(Id) " + Constant.Sql.From + Constant.DatabaseTable.FileData + Constant.Sql.GroupBy + "RelativePath, File)";
            DataTable images = this.Database.GetDataTableFromSelect(query);
        }

        /// <summary>
        /// Get the row matching the specified image or create a new image.  The caller is responsible for adding newly created images the database and data table.
        /// </summary>
        /// <returns>true if the image is already in the database</returns>
        public bool GetOrCreateFile(FileInfo fileInfo, TimeZoneInfo imageSetTimeZone, out ImageRow file)
        {
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
                file = this.Files.NewRow(fileInfo);
                file.InitialRootFolderName = initialRootFolderName;
                file.RelativePath = relativePath;
                file.SetDateTimeOffsetFromFileInfo(this.FolderPath, imageSetTimeZone);
                return false;
            }
        }

        public Dictionary<FileSelection, int> GetFileCountsBySelection()
        {
            Dictionary<FileSelection, int> counts = new Dictionary<FileSelection, int>
            {
                [FileSelection.Dark] = this.GetFileCount(FileSelection.Dark),
                [FileSelection.Corrupted] = this.GetFileCount(FileSelection.Corrupted),
                [FileSelection.Missing] = this.GetFileCount(FileSelection.Missing),
                [FileSelection.Ok] = this.GetFileCount(FileSelection.Ok)
            };
            return counts;
        }

        public int GetFileCount(FileSelection fileSelection)
        {
            string query = Constant.Sql.SelectCountStarFrom + Constant.DatabaseTable.FileData;
            string where = this.GetFilesWhere(fileSelection);
            if (String.IsNullOrEmpty(where))
            {
                if (fileSelection == FileSelection.Custom)
                {
                    // if no search terms are active the image count is undefined as no filtering is in operation
                    return -1;
                }
                // otherwise, the query is for all images as no where clause is present
            }
            else
            {
                query += Constant.Sql.Where + where;
            }
            return this.Database.GetCountFromSelect(query);
        }

        // Insert one or more rows into a table
        private void InsertRows(string table, List<List<ColumnTuple>> insertionStatements)
        {
            this.CreateBackupIfNeeded();
            this.Database.Insert(table, insertionStatements);
        }

        private string GetFilesWhere(FileSelection selection)
        {
            switch (selection)
            {
                case FileSelection.All:
                    return String.Empty;
                case FileSelection.Corrupted:
                case FileSelection.Dark:
                case FileSelection.Missing:
                case FileSelection.Ok:
                    return this.DataLabelFromStandardControlType[Constant.DatabaseColumn.ImageQuality] + "=" + Utilities.QuoteForSql(selection.ToString());
                case FileSelection.MarkedForDeletion:
                    return this.DataLabelFromStandardControlType[Constant.DatabaseColumn.DeleteFlag] + "=" + Utilities.QuoteForSql(Constant.BooleanValue.True); 
                case FileSelection.Custom:
                    return this.CustomSelection.GetFilesWhere();
                default:
                    throw new NotSupportedException(String.Format("Unhandled quality selection {0}.  For custom selections call CustomSelection.GetImagesWhere().", selection));
            }
        }

        /// <summary>
        /// Update a column value (identified by its key) in an existing row (identified by its ID) 
        /// By default, if the table parameter is not included, we use the TABLEDATA table
        /// </summary>
        public void UpdateFile(long fileID, string dataLabel, string value)
        {
            // update the data table
            ImageRow image = this.Files.Find(fileID);
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
                ImageRow image = this.Files[index];
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
                ImageRow image = this.Files[fileIndex];
                image.SetValueFromDatabaseString(dataLabel, value);

                // update database
                List<ColumnTuple> columnToUpdate = new List<ColumnTuple>() { new ColumnTuple(dataLabel, value) };
                ColumnTuplesWithWhere imageUpdate = new ColumnTuplesWithWhere(columnToUpdate, image.ID);
                imagesToUpdate.Add(imageUpdate);
            }
            this.CreateBackupIfNeeded();
            this.Database.Update(Constant.DatabaseTable.FileData, imagesToUpdate);
        }

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
                ImageRow image = this.Files[row];
                DateTimeOffset currentImageDateTime = image.GetDateTime();

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
                log.AppendFormat("The first file adjusted was '{0}', the last '{1}', and the last file was adjusted by {2}.{3}", filesToAdjust[0].FileName, filesToAdjust[filesToAdjust.Count - 1].FileName, mostRecentAdjustment, Environment.NewLine);
                this.AppendToImageSetLog(log);
            }
        }

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
            ImageRow firstImage = this.Files[startRow];
            ImageRow lastImage = null;
            TimeZoneInfo imageSetTimeZone = this.ImageSet.GetTimeZone();
            DateTimeOffset mostRecentOriginalDateTime = DateTime.MinValue;
            DateTimeOffset mostRecentReversedDateTime = DateTime.MinValue;
            for (int row = startRow; row <= endRow; row++)
            {
                ImageRow image = this.Files[row];
                DateTimeOffset originalDateTime = image.GetDateTime();

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
                log.AppendFormat("The first file adjusted was '{0}' and the last '{1}'.{2}", firstImage.FileName, lastImage.FileName, Environment.NewLine);
                log.AppendFormat("The last file's date was changed from '{0}' to '{1}'.{2}", DateTimeHandler.ToDisplayDateString(mostRecentOriginalDateTime), DateTimeHandler.ToDisplayDateString(mostRecentReversedDateTime), Environment.NewLine);
                this.AppendToImageSetLog(log);
            }
        }

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

        /// <summary>A convenience routine for checking to see if the image in the given row is displayable (i.e., not corrupted or missing)</summary>
        public bool IsFileDisplayable(int rowIndex)
        {
            if (this.IsFileRowInRange(rowIndex) == false)
            {
                return false;
            }

            return this.Files[rowIndex].IsDisplayable();
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
            CultureInfo culture = new CultureInfo("en");
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
            CultureInfo culture = new CultureInfo("en");
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
            return culture.CompareInfo.IndexOf(this.Files[rowIndex].FileName, filename, CompareOptions.IgnoreCase);
        }

        // Find the image whose ID is closest to the provided ID  in the current image set
        // If the ID does not exist, then return the image row whose ID is just greater than the provided one. 
        // However, if there is no greater ID (i.e., we are at the end) return the last row. 
        public int FindClosestImageRow(long fileID)
        {
            for (int rowIndex = 0; rowIndex < this.CurrentlySelectedFileCount; ++rowIndex)
            {
                if (this.Files[rowIndex].ID >= fileID)
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
            ImageRow file = this.Files.Find(fileID);
            if (file != null)
            {
                return this.Files.IndexOf(file);
            }

            // when sorted by ID ascending so an inexact binary search works
            // Sorting by datetime is usually identical to ID sorting in single camera image sets, so ignoring this.OrderFilesByDateTime has no effect in 
            // simple cases.  In complex, multi-camera image sets it will be wrong but typically still tend to select a plausibly reasonable file rather
            // than a ridiculous one.  But no datetime seed is available if direct ID lookup fails.  Thw API can be reworked to provide a datetime hint
            // if this proves too troublesome.
            int firstIndex = 0;
            int lastIndex = this.CurrentlySelectedFileCount - 1;
            while (firstIndex <= lastIndex)
            {
                int midpointIndex = (firstIndex + lastIndex) / 2;
                file = this.Files[midpointIndex];
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

        public List<string> GetDistinctValuesInFileDataColumn(string dataLabel)
        {
            List<string> distinctValues = new List<string>();
            foreach (object value in this.Database.GetDistinctValuesInColumn(Constant.DatabaseTable.FileData, dataLabel))
            {
                distinctValues.Add(value.ToString());
            }
            return distinctValues;
        }

        private void GetImageSet()
        {
            string imageSetQuery = "Select * From " + Constant.DatabaseTable.ImageSet + " WHERE " + Constant.DatabaseColumn.ID + " = " + Constant.Database.ImageSetRowID.ToString();
            DataTable imageSetTable = this.Database.GetDataTableFromSelect(imageSetQuery);
            this.ImageSet = new ImageSetRow(imageSetTable.Rows[0]);
        }

        /// <summary>
        /// Get all markers for the specified file.
        /// This is done by getting the marker list associated with all counters representing the current row
        /// It will have a MarkerCounter for each control, even if there may be no metatags in it
        /// </summary>
        public List<MarkersForCounter> GetMarkersOnFile(long fileID)
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
                    Utilities.PrintFailure(String.Format("Read of marker failed for dataLabel '{0}'. {1}", dataLabel, exception.ToString()));
                    pointList = String.Empty;
                }
                markersForCounter.Parse(pointList);
                markersForAllCounters.Add(markersForCounter);
            }
            return markersForAllCounters;
        }

        private void GetMarkers()
        {
            string markersQuery = "Select * FROM " + Constant.DatabaseTable.Markers;
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
                Utilities.PrintFailure(String.Format("Image ID {0} missing in markers table.", imageID));
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
                if (this.Files != null)
                {
                    this.Files.Dispose();
                }
                if (this.Markers != null)
                {
                    this.Markers.Dispose();
                }
            }

            base.Dispose(disposing);
            this.disposed = true;
        }

        private ColumnDefinition CreateFileDataColumnDefinition(ControlRow control)
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
                 return new ColumnDefinition(control.DataLabel, Constant.Sql.Text);
            }
            return new ColumnDefinition(control.DataLabel, Constant.Sql.Text, control.DefaultValue);
        }
    }
}
