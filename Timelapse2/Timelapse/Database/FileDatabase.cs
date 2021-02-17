using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Controls;
using Timelapse.Detection;
using Timelapse.Enums;
using Timelapse.Images;
using Timelapse.Util;

namespace Timelapse.Database
{
    // FileDatabase manages the Timelapse data held in datatables and the .ddb files.
    // It also acts as a go-between with the database, where it forms Timelapse-specific SQL requests to the SQL wrapper
    public class FileDatabase : TemplateDatabase
    {
        #region Private variables
        private DataGrid boundGrid;
        private bool disposed;
        private DataRowChangeEventHandler onFileDataTableRowChanged;

        // These two dictionaries mirror the contents of the detectionCategory and classificationCategory database table
        // for faster access
        private Dictionary<string, string> detectionCategoriesDictionary;
        private Dictionary<string, string> classificationCategoriesDictionary;
        private DataTable detectionDataTable; // Mirrors the database detection table
        private DataTable classificationsDataTable; // Mirrors the database classification table
        #endregion

        #region Properties 
        public CustomSelection CustomSelection { get; private set; }

        /// <summary>Gets the file name of the database on disk.</summary>
        public string FileName { get; private set; }

        /// <summary>Get the complete path to the folder containing the database.</summary>
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

        public static async Task<FileDatabase> CreateEmptyDatabase(string filePath, TemplateDatabase templateDatabase)
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            // initialize the database if it's newly created
            FileDatabase fileDatabase = new FileDatabase(filePath);
            await fileDatabase.OnDatabaseCreatedAsync(templateDatabase).ConfigureAwait(true);
            return fileDatabase;
        }


        public static async Task<FileDatabase> CreateOrOpenAsync(string filePath, TemplateDatabase templateDatabase, CustomSelectionOperatorEnum customSelectionTermCombiningOperator, TemplateSyncResults templateSyncResults)
        {
            // check for an existing database before instantiating the database as SQL wrapper instantiation creates the database file
            bool populateDatabase = !File.Exists(filePath);

            FileDatabase fileDatabase = new FileDatabase(filePath);

            if (populateDatabase)
            {
                // initialize the database if it's newly created
                await fileDatabase.OnDatabaseCreatedAsync(templateDatabase).ConfigureAwait(true);
            }
            else
            {
                // if it's an existing database check if it needs updating to current structure and load data tables
                await fileDatabase.OnExistingDatabaseOpenedAsync(templateDatabase, templateSyncResults).ConfigureAwait(true);
            }

            // ensure all tables have been loaded from the database
            if (fileDatabase.ImageSet == null)
            {
                fileDatabase.ImageSetLoadFromDatabase();
            }
            if (fileDatabase.Markers == null)
            {
                fileDatabase.MarkersLoadRowsFromDatabase();
            }
            fileDatabase.CustomSelection = new CustomSelection(fileDatabase.Controls, customSelectionTermCombiningOperator);
            fileDatabase.PopulateDataLabelMaps();
            return fileDatabase;
        }

        /// <summary>
        /// Make an empty Data Table based on the information in the Template Table.
        /// Assumes that the database has already been opened and that the Template Table is loaded, where the DataLabel always has a valid value.
        /// Then create both the ImageSet table and the Markers table
        /// </summary>
        protected async override Task OnDatabaseCreatedAsync(TemplateDatabase templateDatabase)
        {
            // copy the template's TemplateTable
            await base.OnDatabaseCreatedAsync(templateDatabase).ConfigureAwait(true);

            // Create the DataTable from the template
            // First, define the creation string based on the contents of the template. 
            List<SchemaColumnDefinition> schemaColumnDefinitions = new List<SchemaColumnDefinition>
            {
                new SchemaColumnDefinition(Constant.DatabaseColumn.ID, Sql.CreationStringPrimaryKey)  // It begins with the ID integer primary key
            };
            foreach (ControlRow control in this.Controls)
            {
                schemaColumnDefinitions.Add(CreateFileDataColumnDefinition(control));
            }
            this.Database.CreateTable(Constant.DBTables.FileData, schemaColumnDefinitions);

            // Create the ImageSetTable and initialize a single row in it
            schemaColumnDefinitions.Clear();
            schemaColumnDefinitions.Add(new SchemaColumnDefinition(Constant.DatabaseColumn.ID, Sql.CreationStringPrimaryKey));  // It begins with the ID integer primary key
            schemaColumnDefinitions.Add(new SchemaColumnDefinition(Constant.DatabaseColumn.Log, Sql.Text, Constant.DatabaseValues.ImageSetDefaultLog));
            schemaColumnDefinitions.Add(new SchemaColumnDefinition(Constant.DatabaseColumn.MagnifyingGlass, Sql.Text, Constant.BooleanValue.True));
            schemaColumnDefinitions.Add(new SchemaColumnDefinition(Constant.DatabaseColumn.MostRecentFileID, Sql.Text));
            int allImages = (int)FileSelectionEnum.All;
            schemaColumnDefinitions.Add(new SchemaColumnDefinition(Constant.DatabaseColumn.Selection, Sql.Text, allImages));
            schemaColumnDefinitions.Add(new SchemaColumnDefinition(Constant.DatabaseColumn.WhiteSpaceTrimmed, Sql.Text));
            schemaColumnDefinitions.Add(new SchemaColumnDefinition(Constant.DatabaseColumn.TimeZone, Sql.Text));
            schemaColumnDefinitions.Add(new SchemaColumnDefinition(Constant.DatabaseColumn.VersionCompatabily, Sql.Text));  // Records the highest Timelapse version number ever used to open this database
            schemaColumnDefinitions.Add(new SchemaColumnDefinition(Constant.DatabaseColumn.SortTerms, Sql.Text));        // A comma-separated list of 4 sort terms
            schemaColumnDefinitions.Add(new SchemaColumnDefinition(Constant.DatabaseColumn.SelectedFolder, Sql.Text));
            schemaColumnDefinitions.Add(new SchemaColumnDefinition(Constant.DatabaseColumn.QuickPasteXML, Sql.Text));        // A comma-separated list of 4 sort terms

            this.Database.CreateTable(Constant.DBTables.ImageSet, schemaColumnDefinitions);

            // Populate the data for the image set with defaults
            // VersionCompatabily
            Version timelapseCurrentVersionNumber = VersionChecks.GetTimelapseCurrentVersionNumber();
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
            this.Database.Insert(Constant.DBTables.ImageSet, insertionStatements);

            this.ImageSetLoadFromDatabase();

            // create the Files table
            // This is necessary as files can't be added unless the Files Column is available.  Thus SelectFiles() has to be called after the ImageSetTable is created
            // so that the selection can be persisted.
            await this.SelectFilesAsync(FileSelectionEnum.All).ConfigureAwait(true);

            this.BindToDataGrid();

            // Create the MarkersTable and initialize it from the template table
            schemaColumnDefinitions.Clear();
            schemaColumnDefinitions.Add(new SchemaColumnDefinition(Constant.DatabaseColumn.ID, Sql.CreationStringPrimaryKey));  // It begins with the ID integer primary key
            foreach (ControlRow control in this.Controls)
            {
                if (control.Type.Equals(Constant.Control.Counter))
                {
                    schemaColumnDefinitions.Add(new SchemaColumnDefinition(control.DataLabel, Sql.Text, String.Empty));
                }
            }
            this.Database.CreateTable(Constant.DBTables.Markers, schemaColumnDefinitions);
        }

        protected override async Task OnExistingDatabaseOpenedAsync(TemplateDatabase templateDatabase, TemplateSyncResults templateSyncResults)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(templateDatabase, nameof(templateDatabase));
            ThrowIf.IsNullArgument(templateSyncResults, nameof(templateSyncResults));

            // Perform TemplateTable initializations.
            await base.OnExistingDatabaseOpenedAsync(templateDatabase, null).ConfigureAwait(true);

            // If directed to use the template found in the template database, 
            // check and repair differences between the .tdb and .ddb template tables due to  missing or added controls 
            if (templateSyncResults.UseTemplateDBTemplate)
            {
                // Check for differences between the TemplateTable in the .tdb and .ddb database.
                if (templateSyncResults.SyncRequiredAsDataLabelsDiffer || templateSyncResults.SyncRequiredAsChoiceMenusDiffer)
                {
                    // The TemplateTable in the .tdb and .ddb database differ. 
                    // Update the .ddb Template table by dropping the .ddb template table and replacing it with the .tdb table. 
                    base.Database.DropTable(Constant.DBTables.Controls);
                    await base.OnDatabaseCreatedAsync(templateDatabase).ConfigureAwait(true);
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
                    SchemaColumnDefinition columnDefinition = CreateFileDataColumnDefinition(control);
                    this.Database.SchemaAddColumnToEndOfTable(Constant.DBTables.FileData, columnDefinition);

                    if (control.Type == Constant.Control.Counter)
                    {
                        SchemaColumnDefinition markerColumnDefinition = new SchemaColumnDefinition(dataLabel, Sql.Text);
                        this.Database.SchemaAddColumnToEndOfTable(Constant.DBTables.Markers, markerColumnDefinition);
                    }
                }

                // Condition 2: The image template table had contained one or more controls not found in the template table.
                // That is, the .ddb DataTable contains data columns that now have no corresponding control 
                // Action: Delete those data columns
                // Redundant check for null, as for some reason the CA1062 warning was still showing up
                ThrowIf.IsNullArgument(templateSyncResults, nameof(templateSyncResults));
                foreach (string dataLabel in templateSyncResults.DataLabelsToDelete)
                {
                    this.Database.SchemaDeleteColumn(Constant.DBTables.FileData, dataLabel);

                    // Delete the markers column associated with this data label (if it exists) from the Markers table
                    // Note that we do this for all column types, even though only counters have an associated entry in the Markers table.
                    // This is because we can't get the type of the data label as it no longer exists in the Template.
                    if (this.Database.SchemaIsColumnInTable(Constant.DBTables.Markers, dataLabel))
                    {
                        this.Database.SchemaDeleteColumn(Constant.DBTables.Markers, dataLabel);
                    }
                }

                // Condition 3: The user indicated that the following controls (add/delete) are actually renamed controls
                // Action: Rename those data columns
                foreach (KeyValuePair<string, string> dataLabelToRename in templateSyncResults.DataLabelsToRename)
                {
                    // Rename the column associated with that data label from the FileData table
                    this.Database.SchemaRenameColumn(Constant.DBTables.FileData, dataLabelToRename.Key, dataLabelToRename.Value);

                    // Rename the markers column associated with this data label (if it exists) from the Markers table
                    // Note that we do this for all column types, even though only counters have an associated entry in the Markers table.
                    // This is because its easiest to code, as the function handles attempts to delete a column that isn't there (which also returns false).
                    if (this.Database.SchemaIsColumnInTable(Constant.DBTables.Markers, dataLabelToRename.Key))
                    {
                        this.Database.SchemaRenameColumn(Constant.DBTables.Markers, dataLabelToRename.Key, dataLabelToRename.Value);
                    }
                }

                // Refetch the data labels if needed, as they will have changed due to the repair
                List<string> dataLabels = this.GetDataLabelsExceptIDInSpreadsheetOrder();

                // Condition 4: There are non-critical updates in the template's row (e.g., that only change the UI). 
                // Synchronize the image database's TemplateTable with the template database's TemplateTable 
                // Redundant check for null, as for some reason the CA1062 warning was still showing up
                ThrowIf.IsNullArgument(templateDatabase, nameof(templateDatabase));
                if (templateSyncResults.SyncRequiredAsNonCriticalFieldsDiffer)
                {
                    foreach (string dataLabel in dataLabels)
                    {
                        ControlRow imageDatabaseControl = this.GetControlFromTemplateTable(dataLabel);
                        ControlRow templateControl = templateDatabase.GetControlFromTemplateTable(dataLabel);

                        if (imageDatabaseControl.TryUpdateThisControlRowToMatch(templateControl))
                        {
                            // The control row was updated, so synchronize it to the database
                            this.SyncControlToDatabase(imageDatabaseControl);
                        }
                    }
                }
            }
        }

        private static SchemaColumnDefinition CreateFileDataColumnDefinition(ControlRow control)
        {
            if (control.DataLabel == Constant.DatabaseColumn.DateTime)
            {
                return new SchemaColumnDefinition(control.DataLabel, "DATETIME", DateTimeHandler.ToStringDatabaseDateTime(Constant.ControlDefault.DateTimeValue));
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
                return new SchemaColumnDefinition(control.DataLabel, "REAL", DateTimeHandler.ToStringDatabaseUtcOffset(Constant.ControlDefault.DateTimeValue.Offset));
            }
            if (String.IsNullOrWhiteSpace(control.DefaultValue))
            {
                return new SchemaColumnDefinition(control.DataLabel, Sql.Text, String.Empty);
            }
            return new SchemaColumnDefinition(control.DataLabel, Sql.Text, control.DefaultValue);
        }

        /// <summary>
        /// Create lookup tables that allow us to retrieve a key from a type and vice versa
        /// </summary>
        private void PopulateDataLabelMaps()
        {
            foreach (ControlRow control in this.Controls)
            {
                FileTableColumn column = FileTableColumn.CreateColumnMatchingControlRowsType(control);
                this.FileTableColumnsByDataLabel.Add(column.DataLabel, column);

                // don't type map user defined controls as if there are multiple ones the key would not be unique
                if (Constant.Control.StandardTypes.Contains(column.ControlType))
                {
                    this.DataLabelFromStandardControlType.Add(column.ControlType, column.DataLabel);
                }
            }
        }
        #endregion

        #region Upgrade Databases and Templates
        public async static Task<FileDatabase> UpgradeDatabasesAndCompareTemplates(string filePath, TemplateDatabase templateDatabase, TemplateSyncResults templateSyncResults)
        {
            // If the file doesn't exist, then no immediate action is needed
            if (!File.Exists(filePath))
            {
                return null;
            }
            FileDatabase fileDatabase = new FileDatabase(filePath);
            if (fileDatabase.Database.PragmaGetQuickCheck() == false || fileDatabase.TableExists(Constant.DBTables.FileData) == false)
            {
                // The database file is likely corrupt, possibly due to missing a key table, is an empty file, or is otherwise unreadable
                if (fileDatabase != null)
                {
                    fileDatabase.Dispose();
                }
                return null;
            }
            await fileDatabase.UpgradeDatabasesAndCompareTemplatesAsync(templateDatabase, templateSyncResults).ConfigureAwait(true);
            return fileDatabase;
        }

        protected async override Task UpgradeDatabasesAndCompareTemplatesAsync(TemplateDatabase templateDatabase, TemplateSyncResults templateSyncResults)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(templateDatabase, nameof(templateDatabase));
            ThrowIf.IsNullArgument(templateSyncResults, nameof(templateSyncResults));

            // perform TemplateTable initializations and migrations, then check for synchronization issues
            await base.UpgradeDatabasesAndCompareTemplatesAsync(templateDatabase, null).ConfigureAwait(true);

            // Upgrade the database from older to newer formats to preserve backwards compatability
            await this.UpgradeDatabasesForBackwardsCompatabilityAsync().ConfigureAwait(true);

            // Get the datalabels in the various templates 
            Dictionary<string, string> templateDataLabels = templateDatabase.GetTypedDataLabelsExceptIDInSpreadsheetOrder();
            Dictionary<string, string> imageDataLabels = this.GetTypedDataLabelsExceptIDInSpreadsheetOrder();
            templateSyncResults.DataLabelsInTemplateButNotImageDatabase = Compare.Dictionary1ExceptDictionary2(templateDataLabels, imageDataLabels);
            templateSyncResults.DataLabelsInImageButNotTemplateDatabase = Compare.Dictionary1ExceptDictionary2(imageDataLabels, templateDataLabels);

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
            List<SchemaColumnDefinition> columnDefinitions = new List<SchemaColumnDefinition>
            {
                new SchemaColumnDefinition(Constant.DatabaseColumn.ID, Sql.CreationStringPrimaryKey)  // It begins with the ID integer primary key
            };

            // Add the schema for the columns from the FileDB table
            foreach (ControlRow control in this.Controls)
            {
                columnDefinitions.Add(CreateFileDataColumnDefinition(control));
            }

            // Replace the schema in the FildDB table with the schema defined by the column definitions.
            this.Database.SchemaAlterTableWithNewColumnDefinitions(Constant.DBTables.FileData, columnDefinitions);
        }

        // Upgrade the database as needed from older to newer formats to preserve backwards compatability 
        private async Task UpgradeDatabasesForBackwardsCompatabilityAsync()
        {
            // Note that we avoid Selecting * from the DataTable, as that could be an expensive operation
            // Instead, we operate directly on the database. There is only one exception (updating DateTime),
            // as we have to regenerate all the column's values

            // Get the image set. We will be checking some of its values as we go along
            this.ImageSetLoadFromDatabase();

            // Some comparisons are triggered by comparing the version number stored in the DB with 
            // particular version numbers where known changes occured 
            // Note: if we can't retrieve the version number from the image set, then set it to a very low version number to guarantee all checks will be made
            string lowestVersionNumber = "1.0.0.0";
            bool versionCompatabilityColumnExists = this.Database.SchemaIsColumnInTable(Constant.DBTables.ImageSet, Constant.DatabaseColumn.VersionCompatabily);
            string imageSetVersionNumber = versionCompatabilityColumnExists ? this.ImageSet.VersionCompatability
                : lowestVersionNumber;
            string timelapseVersionNumberAsString = VersionChecks.GetTimelapseCurrentVersionNumber().ToString();

            // Step 1. Check the FileTable for missing columns
            // RelativePath column (if missing) needs to be added 
            if (this.Database.SchemaIsColumnInTable(Constant.DBTables.FileData, Constant.DatabaseColumn.RelativePath) == false)
            {
                long relativePathID = this.GetControlIDFromTemplateTable(Constant.DatabaseColumn.RelativePath);
                ControlRow relativePathControl = this.Controls.Find(relativePathID);
                SchemaColumnDefinition columnDefinition = CreateFileDataColumnDefinition(relativePathControl);
                this.Database.SchemaAddColumnToTable(Constant.DBTables.FileData, Constant.DatabaseValues.RelativePathPosition, columnDefinition);
            }

            // DateTime column (if missing) needs to be added 
            if (this.Database.SchemaIsColumnInTable(Constant.DBTables.FileData, Constant.DatabaseColumn.DateTime) == false)
            {
                long dateTimeID = this.GetControlIDFromTemplateTable(Constant.DatabaseColumn.DateTime);
                ControlRow dateTimeControl = this.Controls.Find(dateTimeID);
                SchemaColumnDefinition columnDefinition = CreateFileDataColumnDefinition(dateTimeControl);
                this.Database.SchemaAddColumnToTable(Constant.DBTables.FileData, Constant.DatabaseValues.DateTimePosition, columnDefinition);
            }

            // UTCOffset column (if missing) needs to be added 
            if (this.Database.SchemaIsColumnInTable(Constant.DBTables.FileData, Constant.DatabaseColumn.UtcOffset) == false)
            {
                long utcOffsetID = this.GetControlIDFromTemplateTable(Constant.DatabaseColumn.UtcOffset);
                ControlRow utcOffsetControl = this.Controls.Find(utcOffsetID);
                SchemaColumnDefinition columnDefinition = CreateFileDataColumnDefinition(utcOffsetControl);
                this.Database.SchemaAddColumnToTable(Constant.DBTables.FileData, Constant.DatabaseValues.UtcOffsetPosition, columnDefinition);
            }

            // Remove MarkForDeletion column and add DeleteFlag column(if needed)
            bool hasMarkForDeletion = this.Database.SchemaIsColumnInTable(Constant.DBTables.FileData, Constant.ControlsDeprecated.MarkForDeletion);
            bool hasDeleteFlag = this.Database.SchemaIsColumnInTable(Constant.DBTables.FileData, Constant.DatabaseColumn.DeleteFlag);
            if (hasMarkForDeletion && (hasDeleteFlag == false))
            {
                // migrate any existing MarkForDeletion column to DeleteFlag
                // this is likely the most typical case
                this.Database.SchemaRenameColumn(Constant.DBTables.FileData, Constant.ControlsDeprecated.MarkForDeletion, Constant.DatabaseColumn.DeleteFlag);
            }
            else if (hasMarkForDeletion && hasDeleteFlag)
            {
                // if both MarkForDeletion and DeleteFlag are present drop MarkForDeletion
                // this is not expected to occur
                this.Database.SchemaDeleteColumn(Constant.DBTables.FileData, Constant.ControlsDeprecated.MarkForDeletion);
            }
            else if (hasDeleteFlag == false)
            {
                // if there's neither a MarkForDeletion or DeleteFlag column add DeleteFlag
                long id = this.GetControlIDFromTemplateTable(Constant.DatabaseColumn.DeleteFlag);
                ControlRow control = this.Controls.Find(id);
                SchemaColumnDefinition columnDefinition = CreateFileDataColumnDefinition(control);
                this.Database.SchemaAddColumnToEndOfTable(Constant.DBTables.FileData, columnDefinition);
            }

            // STEP 2. Check the ImageTable for missing columns
            // Make sure that all the string data in the datatable has white space trimmed from its beginning and end
            // This is needed as the custom selection doesn't work well in testing comparisons if there is leading or trailing white space in it
            // Newer versions of Timelapse  trim the data as it is entered, but older versions did not, so this is to make it backwards-compatable.
            // The WhiteSpaceExists column in the ImageSet Table did not exist before this version, so we add it to the table if needed. If it exists, then 
            // we know the data has been trimmed and we don't have to do it again as the newer versions take care of trimmingon the fly.
            bool whiteSpaceColumnExists = this.Database.SchemaIsColumnInTable(Constant.DBTables.ImageSet, Constant.DatabaseColumn.WhiteSpaceTrimmed);
            if (!whiteSpaceColumnExists)
            {
                // create the whitespace column
                this.Database.SchemaAddColumnToEndOfTable(Constant.DBTables.ImageSet, new SchemaColumnDefinition(Constant.DatabaseColumn.WhiteSpaceTrimmed, Sql.Text, Constant.BooleanValue.False));

                // trim whitespace from the data table
                this.Database.TrimWhitespace(Constant.DBTables.FileData, this.GetDataLabelsExceptIDInSpreadsheetOrder());

                // mark image set as whitespace trimmed
                // This still has to be synchronized, which will occur after we prepare all missing columns
                this.ImageSetLoadFromDatabase();
                this.ImageSet.WhitespaceTrimmed = true;
            }

            // Null test check against the version number
            // Versions prior to 2.2.2.4 may have set nulls as default values, which don't interact well with some aspects of Timelapse. 
            // Repair by turning all nulls in FileTable, if any, into empty strings
            // SAULXX Note that we could likely remove the WhiteSpaceTrimmed column and use the version number instead but we need to check if that is backwards compatable before doing so.
            string firstVersionWithNullCheck = "2.2.2.4";
            if (VersionChecks.IsVersion1GreaterThanVersion2(firstVersionWithNullCheck, imageSetVersionNumber))
            {
                this.Database.ChangeNullToEmptyString(Constant.DBTables.FileData, this.GetDataLabelsExceptIDInSpreadsheetOrder());
            }

            // Updates the UTCOffset format. The issue is that the offset could have been written in the form +3,00 instead of +3.00 (i.e. with a comma)
            // depending on the computer's culture. 

            string firstVersionWithUTCOffsetCheck = "2.2.3.8";
            if (VersionChecks.IsVersion1GreaterThanVersion2(firstVersionWithUTCOffsetCheck, imageSetVersionNumber))
            {
                string utcColumnName = Constant.DatabaseColumn.UtcOffset;
                // FORM:  UPDATE DataTable SET UtcOffset =  REPLACE  ( UtcOffset, ',', '.' )  WHERE  INSTR  ( UtcOffset, ',' )  > 0
                this.Database.ExecuteNonQuery(Sql.Update + Constant.DBTables.FileData + Sql.Set + utcColumnName + Sql.Equal +
                    Sql.Replace + Sql.OpenParenthesis + utcColumnName + Sql.Comma + Sql.Quote(",") + Sql.Comma + Sql.Quote(".") + Sql.CloseParenthesis +
                    Sql.Where + Sql.Instr + Sql.OpenParenthesis + utcColumnName + Sql.Comma + Sql.Quote(",") + Sql.CloseParenthesis + Sql.GreaterThan + "0");

            }

            // STEP 3. Check both templates and update if needed (including values)

            // Version Compatabillity Column: If the imageSetVersion is set to the lowest version number, then the column containing the VersionCompatabily does not exist in the image set table. 
            // Add it and update the entry to contain the version of Timelapse currently being used to open this database
            // Note that we do this after the version compatability tests as otherwise we would just get the current version number
            if (versionCompatabilityColumnExists == false)
            {
                // Create the versioncompatability column and update the image set. Syncronization happens later
                this.Database.SchemaAddColumnToEndOfTable(Constant.DBTables.ImageSet, new SchemaColumnDefinition(Constant.DatabaseColumn.VersionCompatabily, Sql.Text, timelapseVersionNumberAsString));
            }

            // Sort Criteria Column: Make sure that the column containing the SortCriteria exists in the image set table. 
            // If not, add it and set it to the default
            bool sortCriteriaColumnExists = this.Database.SchemaIsColumnInTable(Constant.DBTables.ImageSet, Constant.DatabaseColumn.SortTerms);
            if (!sortCriteriaColumnExists)
            {
                // create the sortCriteria column and update the image set. Syncronization happens later
                this.Database.SchemaAddColumnToEndOfTable(Constant.DBTables.ImageSet, new SchemaColumnDefinition(Constant.DatabaseColumn.SortTerms, Sql.Text, Constant.DatabaseValues.DefaultSortTerms));
            }

            // SelectedFolder Column: Make sure that the column containing the SelectedFolder exists in the image set table. 
            // If not, add it and set it to the default
            string firstVersionWithSelectedFilesColumns = "2.2.2.6";
            if (VersionChecks.IsVersion1GreaterOrEqualToVersion2(firstVersionWithSelectedFilesColumns, imageSetVersionNumber))
            {
                // Because we may be running this several times on the same version, we should still check to see if the column exists before adding it
                bool selectedFolderColumnExists = this.Database.SchemaIsColumnInTable(Constant.DBTables.ImageSet, Constant.DatabaseColumn.SelectedFolder);
                if (!selectedFolderColumnExists)
                {
                    // create the sortCriteria column and update the image set. Syncronization happens later
                    this.Database.SchemaAddColumnToEndOfTable(Constant.DBTables.ImageSet, new SchemaColumnDefinition(Constant.DatabaseColumn.SelectedFolder, Sql.Text, String.Empty));
                    this.ImageSetLoadFromDatabase();
                }
            }
            // Make sure that the column containing the QuickPasteXML exists in the image set table. 
            // If not, add it and set it to the default
            bool quickPasteXMLColumnExists = this.Database.SchemaIsColumnInTable(Constant.DBTables.ImageSet, Constant.DatabaseColumn.QuickPasteXML);
            if (!quickPasteXMLColumnExists)
            {
                // create the QuickPaste column and update the image set. Syncronization happens later
                this.Database.SchemaAddColumnToEndOfTable(Constant.DBTables.ImageSet, new SchemaColumnDefinition(Constant.DatabaseColumn.QuickPasteXML, Sql.Text, Constant.DatabaseValues.DefaultQuickPasteXML));
            }

            // Timezone column (if missing) needs to be added to the Imageset Table
            bool timeZoneColumnExists = this.Database.SchemaIsColumnInTable(Constant.DBTables.ImageSet, Constant.DatabaseColumn.TimeZone);
            bool timeZoneColumnIsNotPopulated = timeZoneColumnExists;
            if (!timeZoneColumnExists)
            {
                // create default time zone entry and refresh the image set.
                this.Database.SchemaAddColumnToEndOfTable(Constant.DBTables.ImageSet, new SchemaColumnDefinition(Constant.DatabaseColumn.TimeZone, Sql.Text));
                this.Database.SetColumnToACommonValue(Constant.DBTables.ImageSet, Constant.DatabaseColumn.TimeZone, TimeZoneInfo.Local.Id);
                this.ImageSetLoadFromDatabase();
            }

            // Populate DateTime column if the column has just been added
            if (!timeZoneColumnIsNotPopulated)
            {
                TimeZoneInfo imageSetTimeZone = this.ImageSet.GetSystemTimeZone();
                List<ColumnTuplesWithWhere> updateQuery = new List<ColumnTuplesWithWhere>();
                // PERFORMANCE, BUT RARE: We invoke this to update various date/time values on all rows based on existing values. However, its rarely called
                // PROGRESSBAR - Add to all calls to SelectFiles, perhaps after a .5 second delay
                // we  have to select all rows. However, this operation would only ever happen once, and only on legacy .ddb files
                await this.SelectFilesAsync(FileSelectionEnum.All).ConfigureAwait(true);
                this.BindToDataGrid();
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
                this.Database.Update(Constant.DBTables.FileData, updateQuery);
                // Note that the FileTable is now stale as we have updated the database directly
            }


        }
        #endregion

        #region Add Files to the Database
        // Add file rows to the database. This generates an SQLite command in the form of:
        // INSERT INTO DataTable (columnnames) (imageRow1Values) (imageRow2Values)... for example,
        // INSERT INTO DataTable ( File, RelativePath, Folder, ... ) VALUES   
        // ( 'IMG_1.JPG', 'relpath', 'folderfoo', ...) ,  
        // ( 'IMG_2.JPG', 'relpath', 'folderfoo', ...)
        // ...
        public void AddFiles(List<ImageRow> files, Action<ImageRow, int> onFileAdded)
        {
            if (files == null)
            {
                // Nothing to do
                return;
            }
            int rowNumber = 0;
            StringBuilder queryColumns = new StringBuilder(Sql.InsertInto + Constant.DBTables.FileData + Sql.OpenParenthesis); // INSERT INTO DataTable (

            Dictionary<string, string> defaultValueLookup = this.GetDefaultControlValueLookup();

            // Create a comma-separated lists of column names
            // e.g., ... File, RelativePath, Folder, DateTime, ..., 
            foreach (string columnName in this.FileTable.ColumnNames)
            {
                if (columnName == Constant.DatabaseColumn.ID)
                {
                    // skip the ID column as it's not associated with a data label and doesn't need to be set as it's autoincrement
                    continue;
                }
                queryColumns.Append(columnName);
                queryColumns.Append(Sql.Comma);
            }

            queryColumns.Remove(queryColumns.Length - 2, 2); // Remove trailing ", "
            queryColumns.Append(Sql.CloseParenthesis + Sql.Values);

            // We should now have a partial SQL expression in the form of: INSERT INTO DataTable ( File, RelativePath, Folder, DateTime, ... )  VALUES 
            // Create a dataline from each of the image properties, add it to a list of data lines, then do a multiple insert of the list of datalines to the database
            // We limit the datalines to RowsPerInsert
            int fileCount = (files == null) ? 0 : files.Count;
            for (int image = 0; image < fileCount; image += Constant.DatabaseValues.RowsPerInsert)
            {
                // PERFORMANCE: Reimplement Markers as a foreign key, as many rows will be empty. However, this will break backwards/forwards compatability
                List<List<ColumnTuple>> markerRows = new List<List<ColumnTuple>>();

                string command;

                StringBuilder queryValues = new StringBuilder();

                // This loop creates a dataline containing this image's property values, e.g., ( 'IMG_1.JPG', 'relpath', 'folderfoo', ...) ,  
                for (int insertIndex = image; (insertIndex < (image + Constant.DatabaseValues.RowsPerInsert)) && (insertIndex < fileCount); insertIndex++)
                {
                    queryValues.Append(Sql.OpenParenthesis);

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
                            case Constant.DatabaseColumn.File:
                                queryValues.Append($"{Sql.Quote(imageProperties.File)}{Sql.Comma}");
                                break;

                            case Constant.DatabaseColumn.RelativePath:
                                queryValues.Append($"{Sql.Quote(imageProperties.RelativePath)}{Sql.Comma}");
                                break;

                            case Constant.DatabaseColumn.Folder:
                                queryValues.Append($"{Sql.Quote(imageProperties.Folder)}{Sql.Comma}");
                                break;

                            case Constant.DatabaseColumn.Date:
                                queryValues.Append($"{Sql.Quote(imageProperties.Date)}{Sql.Comma}");
                                break;

                            case Constant.DatabaseColumn.DateTime:
                                queryValues.Append($"{Sql.Quote(DateTimeHandler.ToStringDatabaseDateTime(imageProperties.DateTime))}{Sql.Comma}");
                                break;

                            case Constant.DatabaseColumn.UtcOffset:
                                queryValues.Append($"{Sql.Quote(DateTimeHandler.ToStringDatabaseUtcOffset(imageProperties.UtcOffset))}{Sql.Comma}");
                                break;

                            case Constant.DatabaseColumn.Time:
                                queryValues.Append($"{Sql.Quote(imageProperties.Time)}{Sql.Comma}");
                                break;

                            case Constant.DatabaseColumn.ImageQuality:
                                queryValues.Append($"{Sql.Quote(imageProperties.ImageQuality.ToString())}{Sql.Comma}");
                                break;

                            case Constant.DatabaseColumn.DeleteFlag:
                                string dataLabel = this.DataLabelFromStandardControlType[Constant.DatabaseColumn.DeleteFlag];

                                // Default as specified in the template file, which should be "false"
                                queryValues.Append($"{Sql.Quote(defaultValueLookup[dataLabel])}{Sql.Comma}");
                                break;

                            // Find and then add the customizable types, populating it with their default values.
                            case Constant.Control.Note:
                            case Constant.Control.FixedChoice:
                            case Constant.Control.Flag:
                                // Now initialize notes, flags, and fixed choices to the defaults
                                queryValues.Append($"{Sql.Quote(defaultValueLookup[columnName])}{Sql.Comma}");
                                break;

                            case Constant.Control.Counter:
                                queryValues.Append($"{Sql.Quote(defaultValueLookup[columnName])}{Sql.Comma}");
                                markerRow.Add(new ColumnTuple(columnName, String.Empty));
                                break;

                            default:
                                TracePrint.PrintMessage(String.Format("Unhandled control type '{0}' in AddImages.", controlType));
                                break;
                        }
                    }

                    // Remove trailing commam then add " ) ,"
                    queryValues.Remove(queryValues.Length - 2, 2); // Remove ", "
                    queryValues.Append(Sql.CloseParenthesis + Sql.Comma);

                    // The dataline should now be added to the string list of data lines, so go to the next image
                    ++rowNumber;

                    if (markerRow.Count > 0)
                    {
                        markerRows.Add(markerRow);
                    }
                }

                // Remove trailing comma.
                queryValues.Remove(queryValues.Length - 2, 2); // Remove ", "

                // Create the entire SQL command (limited to RowsPerInsert datalines)
                command = queryColumns.ToString() + queryValues.ToString();

                this.CreateBackupIfNeeded();
                //this.Database.ExecuteOneNonQueryCommand(command);
                this.Database.ExecuteNonQuery(command);
                this.InsertRows(Constant.DBTables.Markers, markerRows);

                if (onFileAdded != null)
                {
                    int lastImageInserted = Math.Min(fileCount - 1, image + Constant.DatabaseValues.RowsPerInsert);
                    onFileAdded.Invoke(files[lastImageInserted], lastImageInserted);
                }
            }

            // Load / refresh the marker table from the database to keep it in sync - Doing so here will make sure that there is one row for each image.
            this.MarkersLoadRowsFromDatabase();
        }

        /// <summary>
        /// Returns a dictionary populated with control default values based on the control data label.
        /// </summary>
        private Dictionary<string, string> GetDefaultControlValueLookup()
        {
            Dictionary<string, string> results = new Dictionary<string, string>();
            foreach (ControlRow control in this.Controls)
            {
                if (!results.ContainsKey(control.DataLabel))
                {
                    results.Add(control.DataLabel, control.DefaultValue);
                }
            }

            return results;
        }
        #endregion

        #region Exists (all return true or false)
        /// <summary>
        /// Return true/false if the relativePath and filename exist in the Database DataTable  
        /// </summary>
        /// <param name="relativePath"></param>
        /// <param name="filename"></param>
        /// <returns></returns>
        public bool ExistsRelativePathAndFileInDataTable(string relativePath, string filename)
        {
            // Form: Select Exists(Select 1 from DataTable where RelativePath='cameras\Camera1' AND File='IMG_001.JPG')
            string query = Sql.SelectExists + Sql.OpenParenthesis + Sql.SelectOne + Sql.From + Constant.DBTables.FileData;
            query += Sql.Where + Constant.DatabaseColumn.RelativePath + Sql.Equal + Sql.Quote(relativePath);
            query += Sql.And + Constant.DatabaseColumn.File + Sql.Equal + Sql.Quote(filename) + Sql.CloseParenthesis;
            return this.Database.ScalarBoolFromOneOrZero(query);
        }
        #endregion

        #region Select Files in the file table
        /// <summary> 
        /// Rebuild the file table with all files in the database table which match the specified selection.
        /// CODECLEANUP:  should probably merge all 'special cases' of selection (e.g., detections, etc.) into a single class so they are treated the same way, 
        /// eg., to simplify CountAllFilesMatchingSelectionCondition vs SelectFilesAsync
        /// PERFORMANCE can be a slow query on very large databases. Could check for better SQL expressions or database design, but need an SQL expert for that
        /// </summary>
        public async Task SelectFilesAsync(FileSelectionEnum selection)
        {
            string query = String.Empty;

            // Random selection - Add prefix
            //if (this.CustomSelection.RandomSample > 0)
            //{
            //    query += "Select * from DataTable WHERE id IN (SELECT id FROM(";
            //}

            if (this.CustomSelection == null)
            {
                // If no custom selections are configure, then just use a standard query
                query += Sql.SelectStarFrom + Constant.DBTables.FileData;

                // Random selection - Add suffix
                //if (this.CustomSelection.RandomSample > 0)
                //{
                //    query += ") ORDER BY RANDOM() LIMIT 10";
                //}
            }
            else
            {
                if (this.CustomSelection.RandomSample > 0)
                {
                    query += " Select * from DataTable WHERE id IN (SELECT id FROM ( ";
                }

                // If its a pre-configured selection type, set the search terms to match that selection type
                this.CustomSelection.SetSearchTermsFromSelection(selection, this.GetSelectedFolder());


                if (GlobalReferences.DetectionsExists && this.CustomSelection.ShowMissingDetections)
                {
                    // MISSING DETECTIONS 
                    // Create a partial query that returns all missing detections
                    // Form: SELECT DataTable.* FROM DataTable LEFT JOIN Detections ON DataTable.ID = Detections.Id WHERE Detections.Id IS NULL
                    query += SqlPhrase.SelectMissingDetections(SelectTypesEnum.Star);
                }
                else if (GlobalReferences.DetectionsExists && this.CustomSelection.DetectionSelections.Enabled == true && this.CustomSelection.DetectionSelections.RecognitionType == RecognitionType.Detection)
                {
                    // DETECTIONS
                    // Create a partial query that returns detections matching some conditions
                    // Form: SELECT DataTable.* FROM Detections INNER JOIN DataTable ON DataTable.Id = Detections.Id
                    query += SqlPhrase.SelectDetections(SelectTypesEnum.Star);
                }
                else if (GlobalReferences.DetectionsExists && this.CustomSelection.DetectionSelections.Enabled == true && this.CustomSelection.DetectionSelections.RecognitionType == RecognitionType.Classification)
                {
                    // CLASSIFICATIONS 
                    // Create a partial query that returns classifications matching some conditions
                    // Form: SELECT DataTable.* FROM Classifications INNER JOIN DataTable ON DataTable.Id = Detections.Id INNER JOIN Detections ON Detections.detectionID = Classifications.detectionID 
                    query += SqlPhrase.SelectClassifications(SelectTypesEnum.Star);
                }
                else
                {
                    // Standard query (ie., no detections, no missing detections, no classifications 
                    query += Sql.SelectStarFrom + Constant.DBTables.FileData;
                }
            }

            if (this.CustomSelection != null && (GlobalReferences.DetectionsExists == false || this.CustomSelection.ShowMissingDetections == false))
            {
                string conditionalExpression = this.CustomSelection.GetFilesWhere(); //this.GetFilesConditionalExpression(selection);
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
                if (this.CustomSelection != null && this.CustomSelection.DetectionSelections.UseRecognition && this.CustomSelection.DetectionSelections.RecognitionType == RecognitionType.Classification && this.CustomSelection.DetectionSelections.RankByConfidence)
                {
                    // Classifications: Override any sorting as we have asked to rank the results by confidence values
                    term[0] = Constant.DatabaseColumn.RelativePath;
                    term[1] = Constant.DBTables.Classifications + "." + Constant.ClassificationColumns.Conf;
                    term[1] += Sql.Descending;
                }
                else if (this.CustomSelection != null && this.CustomSelection.DetectionSelections.UseRecognition && this.CustomSelection.DetectionSelections.RecognitionType == RecognitionType.Detection && this.CustomSelection.DetectionSelections.RankByConfidence)
                {
                    // Detections: Override any sorting as we have asked to rank the results by confidence values
                    term[0] = Constant.DatabaseColumn.RelativePath;
                    term[1] = Constant.DBTables.Detections + "." + Constant.DetectionColumns.Conf;
                    term[1] += Sql.Descending;
                }
                else
                {
                    // Given the format of the corrected DateTime
                    for (int i = 0; i <= 1; i++)
                    {
                        sortTerm[i] = this.ImageSet.GetSortTerm(i);

                        // If we see an empty data label, we don't have to construct any more terms as there will be nothing more to sort
                        if (string.IsNullOrEmpty(sortTerm[i].DataLabel))
                        {
                            break;
                        }
                        else if (sortTerm[i].DataLabel == Constant.DatabaseColumn.DateTime)
                        {
                            // First Check for special cases, where we want to modify how sorting is done
                            // DateTime:the modified query adds the UTC Offset to it
                            // NOTE SAULXXX I Had A BUG WHERE IN SOME CULTURES UTC IS RECORDED AS EG +3,00 rather than +3.00 WHICH BLOWS UP THE DATE SORT
                            // I now repaired the UTC OFFSET ROW IN  ALL THESE INSTANCES ON DATABASE OPEN, BUT IF THIS CONTINUES TO BE A PROBLEM USED
                            // THE COMMENTED OUT VERSION WHICH IGNORES UTCOFFSET
                            term[i] = String.Format("datetime({0}, {1} || ' hours')", Constant.DatabaseColumn.DateTime, Constant.DatabaseColumn.UtcOffset);
                            //term[i] = String.Format("datetime({0})", Constant.DatabaseColumn.DateTime);
                        }
                        else if (sortTerm[i].DataLabel == Constant.DatabaseColumn.File)
                        {
                            // File: the modified term creates a file path by concatonating relative path and file
                            term[i] = String.Format("{0}{1}{2}", Constant.DatabaseColumn.RelativePath, Sql.Comma, Constant.DatabaseColumn.File);
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
                            term[i] += Sql.Descending;
                        }
                    }
                }

                // Random selection - Add suffix
                if (this.CustomSelection != null && this.CustomSelection.RandomSample > 0)
                {
                    query += String.Format(" ) ORDER BY RANDOM() LIMIT {0} )", this.CustomSelection.RandomSample);
                }

                if (!String.IsNullOrEmpty(term[0]))
                {
                    query += Sql.OrderBy + term[0];

                    // If there is a second sort key, add it here
                    if (!String.IsNullOrEmpty(term[1]))
                    {
                        query += Sql.Comma + term[1];
                    }
                    //query += Sql.Semicolon;
                }
            }


            DataTable filesTable = await Task.Run(() =>
            {
                // System.Diagnostics.Debug.Print("Select Query: " + query);
                // PERFORMANCE  This seems to be the main performance bottleneck. Running a query on a large database that returns
                // a large datatable (e.g., all files) is very slow. There is likely a better way to do this, but I am not sure what
                // as I am not that savvy in database optimizations.
                // System.Diagnostics.Debug.Print(query);
                return this.Database.GetDataTableFromSelect(query);
            }).ConfigureAwait(true);
            this.FileTable = new FileTable(filesTable);
        }

        // Select all files in the file table
        public FileTable SelectAllFiles()
        {
            string query = Sql.SelectStarFrom + Constant.DBTables.FileData;
            DataTable filesTable = this.Database.GetDataTableFromSelect(query);
            return new FileTable(filesTable);
        }

        // Check for the existence of missing files in the current selection, and return a list of IDs of those that are missing
        // PERFORMANCE this can be slow if there are many files
        public bool SelectMissingFilesFromCurrentlySelectedFiles()
        {
            if (this.FileTable == null)
            {
                return false;
            }
            string filepath = String.Empty;
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
            if (string.IsNullOrEmpty(commaSeparatedListOfIDs))
            {
                // No missing files
                return false;
            }
            this.FileTable = this.SelectFilesInDataTableByCommaSeparatedIds(commaSeparatedListOfIDs);
            this.FileTable.BindDataGrid(this.boundGrid, this.onFileDataTableRowChanged);
            return true;
        }

        public List<string> SelectFileNamesWithRelativePathFromDatabase(string relativePath)
        {
            List<string> files = new List<string>();
            // Form: Select * From DataTable Where RelativePath = '<relativePath>'
            string query = Sql.Select + Constant.DatabaseColumn.File + Sql.From + Constant.DBTables.FileData + Sql.Where + Constant.DatabaseColumn.RelativePath + Sql.Equal + Sql.Quote(relativePath);
            DataTable images = this.Database.GetDataTableFromSelect(query);
            int count = images.Rows.Count;
            for (int i = 0; i < count; i++)
            {
                files.Add((string)images.Rows[i].ItemArray[0]);
            }
            images.Dispose();
            return files;
        }

        // Select only those files that are marked for deletion i.e. DeleteFlag = true
        public FileTable SelectFilesMarkedForDeletion()
        {
            string where = this.DataLabelFromStandardControlType[Constant.DatabaseColumn.DeleteFlag] + "=" + Sql.Quote(Constant.BooleanValue.True); // = value
            string query = Sql.SelectStarFrom + Constant.DBTables.FileData + Sql.Where + where;
            DataTable filesTable = this.Database.GetDataTableFromSelect(query);
            return new FileTable(filesTable);
        }

        // Select files with matching IDs where IDs are a comma-separated string i.e.,
        // Select * From DataTable Where  Id IN(1,2,4 )
        public FileTable SelectFilesInDataTableByCommaSeparatedIds(string listOfIds)
        {
            string query = Sql.SelectStarFrom + Constant.DBTables.FileData + Sql.WhereIDIn + Sql.OpenParenthesis + listOfIds + Sql.CloseParenthesis;
            DataTable filesTable = this.Database.GetDataTableFromSelect(query);
            return new FileTable(filesTable);
        }

        public FileTable SelectFileInDataTableById(string id)
        {
            string query = Sql.SelectStarFrom + Constant.DBTables.FileData + Sql.WhereIDEquals + Sql.Quote(id) + Sql.LimitOne;
            DataTable filesTable = this.Database.GetDataTableFromSelect(query);
            return new FileTable(filesTable);
        }

        // This is only used for converting old XML data files to the new ones
        // Normally, we would need to use the relative path for this to work reliably,
        // but the old XML files don't have a relative path.
        public long GetIDFromDataTableByFileName(string fileName)
        {
            string query = Sql.Select + Constant.DatabaseColumn.ID + Sql.From + Constant.DBTables.FileData;
            query += Sql.Where + Constant.DatabaseColumn.File + Sql.Equal + Sql.Quote(fileName) + Sql.LimitOne;
            DataTable images = this.Database.GetDataTableFromSelect(query);
            long id = (images.Rows.Count == 1) ? (long)images.Rows[0][0] : -1;
            images.Dispose();
            return id;
        }

        // A specialized call: Given a relative path and two dates (in database DateTime format without the offset)
        // return a table containing ID, DateTime that matches the relative path and is inbetween the two datetime intervals
        public DataTable GetIDandDateWithRelativePathAndBetweenDates(string relativePath, string lowerDateTime, string uppderDateTime)
        {
            // datetimes are in database format e.g., 2017-06-14T18:36:52.000Z 
            // Form: Select ID,DateTime from DataTable where RelativePath='relativePath' and DateTime BETWEEN 'lowerDateTime' AND 'uppderDateTime' ORDER BY DateTime ORDER BY DateTime  
            string query = Sql.Select + Constant.DatabaseColumn.ID + Sql.Comma + Constant.DatabaseColumn.DateTime + Sql.From + Constant.DBTables.FileData;
            query += Sql.Where + Constant.DatabaseColumn.RelativePath + Sql.Equal + Sql.Quote(relativePath);
            query += Sql.And + Constant.DatabaseColumn.DateTime + Sql.Between + Sql.Quote(lowerDateTime) + Sql.And + Sql.Quote(uppderDateTime);
            query += Sql.OrderBy + Constant.DatabaseColumn.DateTime;
            return (this.Database.GetDataTableFromSelect(query));
        }
        #endregion

        // Return a new sorted list containing the distinct relative paths in the database,
        // and the (unique) parents of each relative path entry.
        // For example, if the relative paths were a/b, a/b/c, a/b/d and d/c it would return
        // a | a/b | a/b/c, a/b/d | d | d/c
        public List<string> GetFoldersFromRelativePaths()
        {
            // Get all the relative paths
            List<object> relativePathList = GetDistinctValuesInColumn(Constant.DBTables.FileData, Constant.DatabaseColumn.RelativePath);
            List<string> allPaths = new List<string>();
            foreach (string relativePath in relativePathList)
            {
                allPaths.Add(relativePath);
                string parent = String.IsNullOrEmpty(relativePath) ? String.Empty : System.IO.Path.GetDirectoryName(relativePath);
                while (!String.IsNullOrWhiteSpace(parent))
                {
                    if (!allPaths.Contains(parent))
                    {
                        allPaths.Add(parent);
                    }
                    parent = System.IO.Path.GetDirectoryName(parent);
                }
            }
            allPaths.Sort();
            return allPaths;
        }
        #region Get Distinct Values
        public List<object> GetDistinctValuesInColumn(string table, string columnName)
        {
            return this.Database.GetDistinctValuesInColumn(table, columnName);
        }

        // Return all distinct values from a column in the file table, used for autocompletion
        // Note that this returns distinct values only in the SELECTED files
        // PERFORMANCE - the issue here is that there may be too many distinct entries, which slows down autocompletion. This should thus restrict entries, perhaps by:
        // - check matching substrings before adding, to avoid having too many entries?
        // - only store the longest version of a string. But this would involve more work when adding entries, so likely not worth it.
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
        #endregion

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
            this.Database.Update(Constant.DBTables.FileData, columnToUpdate);
        }

        // Set one property on all rows in the selected view to a given value
        public void UpdateFiles(ImageRow valueSource, string dataLabel)
        {
            this.UpdateFiles(valueSource, dataLabel, 0, this.CountAllCurrentlySelectedFiles - 1);
        }

        // Given a list of column/value pairs (the string,object) and the FILE name indicating a row, update it
        public void UpdateFiles(List<ColumnTuplesWithWhere> filesToUpdate)
        {
            this.CreateBackupIfNeeded();
            this.Database.Update(Constant.DBTables.FileData, filesToUpdate);
        }

        public void UpdateFiles(ColumnTuplesWithWhere filesToUpdate)
        {
            List<ColumnTuplesWithWhere> imagesToUpdateList = new List<ColumnTuplesWithWhere>
            {
                filesToUpdate
            };
            this.Database.Update(Constant.DBTables.FileData, imagesToUpdateList);
        }

        public void UpdateFiles(ColumnTuple columnToUpdate)
        {
            this.Database.Update(Constant.DBTables.FileData, columnToUpdate);
        }

        // Given a range of selected files, update the field identifed by dataLabel with the value in valueSource
        // Updates are applied to both the datatable (so the user sees the updates immediately) and the database
        public void UpdateFiles(ImageRow valueSource, string dataLabel, int fromIndex, int toIndex)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(valueSource, nameof(valueSource));

            if (fromIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(fromIndex));
            }
            if (toIndex < fromIndex || toIndex > this.CountAllCurrentlySelectedFiles - 1)
            {
                throw new ArgumentOutOfRangeException(nameof(toIndex));
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
            this.Database.Update(Constant.DBTables.FileData, imagesToUpdate);
        }

        // Similar to above
        // Given a list of selected files, update the field identifed by dataLabel with the value in valueSource
        // Updates are applied to both the datatable (so the user sees the updates immediately) and the database
        public void UpdateFiles(List<int> fileIndexes, string dataLabel, string value)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(fileIndexes, nameof(fileIndexes));

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
            this.Database.Update(Constant.DBTables.FileData, imagesToUpdate);
        }
        #endregion

        #region Update Syncing 
        public void UpdateSyncImageSetToDatabase()
        {
            // don't trigger backups on image set updates as none of the properties in the image set table is particularly important
            // For example, this avoids creating a backup when a custom selection is reverted to all when Timelapse exits.
            this.Database.Update(Constant.DBTables.ImageSet, this.ImageSet.CreateColumnTuplesWithWhereByID());
        }

        public void UpdateSyncMarkerToDatabase(MarkerRow marker)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(marker, nameof(marker));

            this.CreateBackupIfNeeded();
            this.Database.Update(Constant.DBTables.Markers, marker.CreateColumnTuplesWithWhereByID());
        }
        #endregion

        #region Update Markers
        // The id is the row to update, the datalabels are the labels of each control to updata, 
        // and the markers are the respective point lists for each of those labels
        public void UpdateMarkers(List<ColumnTuplesWithWhere> markersToUpdate)
        {
            // update markers in database
            this.CreateBackupIfNeeded();
            this.Database.Update(Constant.DBTables.Markers, markersToUpdate);

            // update markers in marker data table
            this.MarkersLoadRowsFromDatabase();
        }
        #endregion

        #region Update File Dates and Times
        // Update all selected files with the given time adjustment
        public void UpdateAdjustedFileTimes(TimeSpan adjustment)
        {
            this.UpdateAdjustedFileTimes(adjustment, 0, this.CountAllCurrentlySelectedFiles - 1);
        }

        // Update all selected files between the start and end row with the given time adjustment
        public void UpdateAdjustedFileTimes(TimeSpan adjustment, int startRow, int endRow)
        {
            if (adjustment.Milliseconds != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(adjustment), "The current format of the time column does not support milliseconds.");
            }
            this.UpdateAdjustedFileTimes((string fileName, int fileIndex, int count, DateTimeOffset imageTime) => { return imageTime + adjustment; }, startRow, endRow, CancellationToken.None);
        }

        // Given a time difference in ticks, update all the date/time field in the database
        // Note that it does NOT update the dataTable - this has to be done outside of this routine by regenerating the datatables with whatever selection is being used..
        public void UpdateAdjustedFileTimes(Func<string, int, int, DateTimeOffset, DateTimeOffset> adjustment, int startRow, int endRow, CancellationToken token)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(adjustment, nameof(adjustment));

            if (this.IsFileRowInRange(startRow) == false)
            {
                throw new ArgumentOutOfRangeException(nameof(startRow));
            }
            if (this.IsFileRowInRange(endRow) == false)
            {
                throw new ArgumentOutOfRangeException(nameof(endRow));
            }
            if (endRow < startRow)
            {
                throw new ArgumentOutOfRangeException(nameof(endRow), "endRow must be greater than or equal to startRow.");
            }
            if (this.CountAllCurrentlySelectedFiles == 0)
            {
                return;
            }

            // We now have an unselected temporary data table
            // Get the original value of each, and update each date by the corrected amount if possible
            List<ImageRow> filesToAdjust = new List<ImageRow>();
            TimeSpan mostRecentAdjustment;
            int count = endRow - startRow + 1;
            int fileIndex = 0;
            for (int row = startRow; row <= endRow; ++row)
            {
                if (token.IsCancellationRequested)
                {
                    // A cancel was requested. Clear all pending changes and abort
                    return;
                }
                ImageRow image = this.FileTable[row];
                DateTimeOffset currentImageDateTime = image.DateTimeIncorporatingOffset;

                // adjust the date/time
                fileIndex++;
                DateTimeOffset newImageDateTime = adjustment.Invoke(image.File, fileIndex, count, currentImageDateTime);
                mostRecentAdjustment = newImageDateTime - currentImageDateTime;
                if (mostRecentAdjustment.Duration() < TimeSpan.FromSeconds(1))
                {
                    // Ignore changes if it results in less than a 1 second change, 
                    continue;
                }
                image.SetDateTimeOffset(newImageDateTime);
                filesToAdjust.Add(image);
            }

            if (token.IsCancellationRequested)
            {
                // Don't update the database, as a cancellation was requested.
                return;
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
                this.Database.Update(Constant.DBTables.FileData, imagesToUpdate);
            }
        }

        // Update all the date fields by swapping the days and months.
        // This should ONLY be called if such swapping across all dates (excepting corrupt ones) is possible
        // as otherwise it will only swap those dates it can
        // It also assumes that the data table is showing All images
        public void UpdateExchangeDayAndMonthInFileDates()
        {
            this.UpdateExchangeDayAndMonthInFileDates(0, this.CountAllCurrentlySelectedFiles - 1);
        }

        // Update all the date fields between the start and end index by swapping the days and months.
        public void UpdateExchangeDayAndMonthInFileDates(int startRow, int endRow)
        {
            if (this.IsFileRowInRange(startRow) == false)
            {
                throw new ArgumentOutOfRangeException(nameof(startRow));
            }
            if (this.IsFileRowInRange(endRow) == false)
            {
                throw new ArgumentOutOfRangeException(nameof(endRow));
            }
            if (endRow < startRow)
            {
                throw new ArgumentOutOfRangeException(nameof(endRow), "endRow must be greater than or equal to startRow.");
            }
            if (this.CountAllCurrentlySelectedFiles == 0)
            {
                return;
            }

            // Get the original date value of each. If we can swap the date order, do so. 
            List<ColumnTuplesWithWhere> imagesToUpdate = new List<ColumnTuplesWithWhere>();
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
            }

            if (imagesToUpdate.Count > 0)
            {
                this.CreateBackupIfNeeded();
                this.Database.Update(Constant.DBTables.FileData, imagesToUpdate);
            }
        }
        #endregion

        #region Deletions
        // Delete the data (including markers associated with the images identified by the list of IDs.
        public void DeleteFilesAndMarkers(List<long> fileIDs)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(fileIDs, nameof(fileIDs));

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
            this.Database.Delete(Constant.DBTables.FileData, idClauses);
            this.Database.Delete(Constant.DBTables.Markers, idClauses);
        }
        #endregion

        #region Schema retrieval
        public Dictionary<string, string> SchemaGetColumnsAndDefaultValues(string tableName)
        {
            return this.Database.SchemaGetColumnsAndDefaultValues(tableName);
        }

        public List<string> SchemaGetColumns(string tableName)
        {
            return this.Database.SchemaGetColumns(tableName);
        }
        #endregion

        #region Counts or Exists 1 of matching files
        // Return a total count of the currently selected files in the file table.
        public int CountAllCurrentlySelectedFiles
        {
            get { return (this.FileTable == null) ? 0 : this.FileTable.RowCount; }
        }

        // Return the count of the files matching the fileSelection condition in the entire database
        // Form examples
        // - Select Count(*) FROM DataTable WHERE ImageQuality='Light'
        // - Select Count(*) FROM (Select * From Detections INNER JOIN DataTable ON DataTable.Id = Detections.Id WHERE DataTable.ImageQuality='Light' GROUP BY Detections.Id HAVING  MAX  ( Detections.conf )  <= 0.9)
        // - Select Count(*) FROM (Select * From Classifications INNER JOIN DataTable ON DataTable.Id = Detections.Id  INER JOIN Detections ON Detections.detectionID = Classifications.detectionID WHERE DataTable.Person<>'true' 
        // AND Classifications.category = 6 GROUP BY Classifications.classificationID HAVING  MAX  (Classifications.conf ) BETWEEN 0.8 AND 1 
        public int CountAllFilesMatchingSelectionCondition(FileSelectionEnum fileSelection)
        {
            string query;
            bool skipWhere = false;

            // PART 1 of Query
            if (fileSelection == FileSelectionEnum.Custom && GlobalReferences.DetectionsExists && this.CustomSelection.ShowMissingDetections)
            {
                // MISSING DETECTIONS
                // Create a query that returns a count of missing detections
                // Form: SELECT COUNT ( DataTable.Id ) FROM DataTable LEFT JOIN Detections ON DataTable.ID = Detections.Id WHERE Detections.Id IS NULL 
                query = SqlPhrase.SelectMissingDetections(SelectTypesEnum.Count);
                skipWhere = true;
            }
            else if (fileSelection == FileSelectionEnum.Custom && GlobalReferences.DetectionsExists && this.CustomSelection.DetectionSelections.Enabled == true && this.CustomSelection.DetectionSelections.RecognitionType == RecognitionType.Detection)
            {
                // DETECTIONS
                // Create a query that returns a count of detections matching some conditions
                // Form: SELECT COUNT  ( * )  FROM  (  SELECT * FROM Detections INNER JOIN DataTable ON DataTable.Id = Detections.Id
                query = SqlPhrase.SelectDetections(SelectTypesEnum.Count);
            }
            else if (fileSelection == FileSelectionEnum.Custom && GlobalReferences.DetectionsExists && this.CustomSelection.DetectionSelections.Enabled == true && this.CustomSelection.DetectionSelections.RecognitionType == RecognitionType.Classification)
            {
                // CLASSIFICATIONS
                // Create a partial query that returns a count of classifications matching some conditions
                // Form: Select COUNT  ( * )  FROM  (SELECT DISTINCT DataTable.* FROM Classifications INNER JOIN DataTable ON DataTable.Id = Detections.Id INNER JOIN Detections ON Detections.detectionID = Classifications.detectionID 
                query = SqlPhrase.SelectClassifications(SelectTypesEnum.Count);
            }
            else
            {
                // STANDARD (NO DETECTIONS/CLASSIFICATIONS)
                // Create a query that returns a count that does not consider detections
                query = Sql.SelectCountStarFrom + Constant.DBTables.FileData;
            }

            // PART 2 of Query
            // Now add the Where conditions to the query
            if ((GlobalReferences.DetectionsExists && this.CustomSelection.ShowMissingDetections == false) || skipWhere == false)
            {
                string where = this.CustomSelection.GetFilesWhere(); //this.GetFilesConditionalExpression(fileSelection);
                if (!String.IsNullOrEmpty(where))
                {
                    query += where;
                }
                if (fileSelection == FileSelectionEnum.Custom && Util.GlobalReferences.TimelapseState.UseDetections == true && this.CustomSelection.DetectionSelections.Enabled == true)
                {
                    // Add a close parenthesis if we are querying for detections
                    query += Sql.CloseParenthesis;
                }
            }
            // Uncommment this to see the actual complete query
            //    System.Diagnostics.Debug.Print("File Counts: " + query);
            return this.Database.ScalarGetCountFromSelect(query);
        }

        // Return true if even one file matches the fileSelection condition in the entire database
        // NOTE: Currently only used by 1 method to check if deleteflags exists. Check how well this works if other methods start using it.
        // NOTE: This method is somewhat similar to CountAllFilesMatchingSelectionCondition. They could be combined, but its easier for now to keep them separate
        // Form examples
        // -  No detections:  SELECT EXISTS (  SELECT 1  FROM DataTable WHERE  ( DeleteFlag='true' )  )  //
        // -  detectopms:     SELECT EXISTS (  SELECT 1  FROM Detections INNER JOIN DataTable ON DataTable.Id = Detections.Id WHERE  ( DataTable.DeleteFlag='true' )  GROUP BY Detections.Id HAVING  MAX  ( Detections.conf )  BETWEEN 0.8 AND 1 )
        // -  recognitions:   SELECT EXISTS (  SELECT 1  FROM  (  SELECT DISTINCT DataTable.* FROM Classifications INNER JOIN DataTable ON DataTable.Id = Detections.Id INNER JOIN Detections ON Detections.detectionID = Classifications.detectionID 
        //                    WHERE  ( DataTable.DeleteFlag='true' )  AND Classifications.category = 1 GROUP BY Classifications.classificationID HAVING  MAX  ( Classifications.conf )  BETWEEN 0.8 AND 1 )  ) :1
        public bool ExistsFilesMatchingSelectionCondition(FileSelectionEnum fileSelection)
        {
            string query;
            bool skipWhere = false;
            query = " SELECT EXISTS ( ";

            // PART 1 of Query
            if (fileSelection == FileSelectionEnum.Custom && GlobalReferences.DetectionsExists && this.CustomSelection.ShowMissingDetections)
            {
                // MISSING DETECTIONS
                // Create a query that returns a count of missing detections
                // Form: SELECT COUNT ( DataTable.Id ) FROM DataTable LEFT JOIN Detections ON DataTable.ID = Detections.Id WHERE Detections.Id IS NULL 
                query += SqlPhrase.SelectMissingDetections(SelectTypesEnum.One);
                skipWhere = true;
            }
            else if (fileSelection == FileSelectionEnum.Custom && GlobalReferences.DetectionsExists && this.CustomSelection.DetectionSelections.Enabled == true && this.CustomSelection.DetectionSelections.RecognitionType == RecognitionType.Detection)
            {
                // DETECTIONS
                // Create a query that returns a count of detections matching some conditions
                // Form: SELECT COUNT  ( * )  FROM  (  SELECT * FROM Detections INNER JOIN DataTable ON DataTable.Id = Detections.Id
                query += SqlPhrase.SelectDetections(SelectTypesEnum.One);
            }
            else if (fileSelection == FileSelectionEnum.Custom && GlobalReferences.DetectionsExists && this.CustomSelection.DetectionSelections.Enabled == true && this.CustomSelection.DetectionSelections.RecognitionType == RecognitionType.Classification)
            {
                // CLASSIFICATIONS
                // Create a partial query that returns a count of classifications matching some conditions
                // Form: Select COUNT  ( * )  FROM  (SELECT DISTINCT DataTable.* FROM Classifications INNER JOIN DataTable ON DataTable.Id = Detections.Id INNER JOIN Detections ON Detections.detectionID = Classifications.detectionID 
                query += SqlPhrase.SelectClassifications(SelectTypesEnum.One);
            }
            else
            {
                // STANDARD (NO DETECTIONS/CLASSIFICATIONS)
                // Create a query that returns a count that does not consider detections
                query += Sql.SelectOne + Sql.From + Constant.DBTables.FileData;
            }

            // PART 2 of Query
            // Now add the Where conditions to the query
            if ((GlobalReferences.DetectionsExists && this.CustomSelection.ShowMissingDetections == false) || skipWhere == false)
            {
                string where = this.CustomSelection.GetFilesWhere(); //this.GetFilesConditionalExpression(fileSelection);
                if (!String.IsNullOrEmpty(where))
                {
                    query += where;
                }
                if (fileSelection == FileSelectionEnum.Custom && Util.GlobalReferences.TimelapseState.UseDetections == true && this.CustomSelection.DetectionSelections.Enabled == true && this.CustomSelection.DetectionSelections.RecognitionType == RecognitionType.Classification)
                {
                    // Add a close parenthesis if we are querying for detections. Not sure where the unbalanced parenthesis is coming from! Needs some checking.
                    query += Sql.CloseParenthesis;
                }
            }
            query += Sql.CloseParenthesis;

            // Uncommment this to see the actual complete query
            //System.Diagnostics.Debug.Print("File Exists: " + query + ":" + this.Database.ScalarGetCountFromSelect(query).ToString() );
            return this.Database.ScalarGetCountFromSelect(query) != 0;
        }

        #endregion

        #region Exists matching files  
        // Return true if there is at least one file matching the fileSelection condition in the entire database
        // Form examples
        // - Select EXISTS  ( SELECT 1   FROM DataTable WHERE DeleteFlag='true')
        // -     SELECT EXISTS  ( SELECT 1  FROM DataTable WHERE  (RelativePath= 'Station1' OR RelativePath GLOB 'Station1\*') AND DeleteFlag = 'TRUE' COllate nocase)
        // -XXXX SELECT EXISTS  ( SELECT 1  FROM DataTable WHERE  (  ( RelativePath='Station1\\Deployment2' OR RelativePath GLOB 'Station1\\Deployment2\\*' )  AND DeleteFlag='true' )) 
        // The performance of this query depends upon how many rows in the table has to be searched
        // before the first exists appears. If there are no matching rows, the performance is more or
        // less equivalent to COUNT as it has to go through every row. 
        public bool ExistsRowThatMatchesSelectionForAllFilesOrConstrainedRelativePathFiles(FileSelectionEnum fileSelection)
        {
            // Create a term that will be used, if needed, to account for a constrained relative path
            // Term form is: ( RelativePath='relpathValue' OR DataTable.RelativePath GLOB 'relpathValue\*' )
            string constrainToRelativePathTerm = GlobalReferences.MainWindow.Arguments.ConstrainToRelativePath
                    ? CustomSelection.RelativePathGlobToIncludeSubfolders(Constant.DatabaseColumn.RelativePath, GlobalReferences.MainWindow.Arguments.RelativePath)
                    : String.Empty;
            string selectionTerm;
            // Common query prefix: SELECT EXISTS  ( SELECT 1  FROM DataTable WHERE 
            string query = Sql.SelectExists + Sql.OpenParenthesis + Sql.SelectOne + Sql.From + Constant.DBTables.FileData + Sql.Where;


            // Count the number of deleteFlags
            if (fileSelection == FileSelectionEnum.MarkedForDeletion)
            {
                // Term form is: DeleteFlag = 'TRUE' COllate nocase
                selectionTerm = Constant.DatabaseColumn.DeleteFlag + Sql.Equal + Sql.Quote("true") + Sql.CollateNocase;
            }
            else if (fileSelection == FileSelectionEnum.Ok)
            {
                // Term form is: ImageQuality = 'TRUE' COllate nocase
                selectionTerm = Constant.DatabaseColumn.ImageQuality + Sql.Equal + Sql.Quote(Constant.ImageQuality.Ok);
            }
            else if (fileSelection == FileSelectionEnum.Dark)
            {
                // Term form is: ImageQuality = 'TRUE' COllate nocase
                selectionTerm = Constant.DatabaseColumn.ImageQuality + Sql.Equal + Sql.Quote(Constant.ImageQuality.Dark);
            }
            else
            {
                // Shouldn't get here, as this should only be used with MarkedForDeletion, Ok, or Dark
                // so essentially a noop
                return false;
            }
            if (String.IsNullOrWhiteSpace(constrainToRelativePathTerm))
            {
                // Form after this:  SELECT EXISTS  (  SELECT 1  FROM DataTable WHERE   DeleteFlag = 'TRUE' COllate nocase )
                query += selectionTerm + Sql.CloseParenthesis;
            }
            else
            {
                // Form after this: SELECT EXISTS  ( SELECT 1  FROM DataTable WHERE  (RelativePath= 'Station1' OR RelativePath GLOB 'Station1\*') AND DeleteFlag = 'TRUE' COllate nocase)
                query += constrainToRelativePathTerm + Sql.And + selectionTerm + Sql.CloseParenthesis;
            }

            // System.Diagnostics.Debug.Print("ExistsRowThatMatchesExactSelection: " + query);
            return this.Database.ScalarBoolFromOneOrZero(query);
        }
        #endregion

        #region Find: By Filename 
        // Find by file name, forwards and backwards with wrapping
        public int FindByFileName(int currentRow, bool isForward, string filename)
        {
            int rowIndex;

            if (isForward)
            {
                // Find forwards with wrapping
                rowIndex = this.FindByFileNameForwards(currentRow + 1, this.CountAllCurrentlySelectedFiles, filename);
                return rowIndex == -1 ? this.FindByFileNameForwards(0, currentRow - 1, filename) : rowIndex;
            }
            else
            {
                // Find backwards  with wrapping
                rowIndex = this.FindByFileNameBackwards(currentRow - 1, 0, filename);
                return rowIndex == -1 ? this.FindByFileNameBackwards(this.CountAllCurrentlySelectedFiles, currentRow + 1, filename) : rowIndex;
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

        #region Find: Displayable
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
            int countAllCurrentlySelectedFiles = this.CountAllCurrentlySelectedFiles;
            for (int index = startIndex; index < countAllCurrentlySelectedFiles; index++)
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
        #endregion

        #region Find: By Row Index
        // Check if index is within the file row range
        public bool IsFileRowInRange(int imageRowIndex)
        {
            return (imageRowIndex >= 0) && (imageRowIndex < this.CountAllCurrentlySelectedFiles);
        }

        // Find the image whose ID is closest to the provided ID  in the current image set
        // If the ID does not exist, then return the image row whose ID is just greater than the provided one. 
        // However, if there is no greater ID (i.e., we are at the end) return the last row. 
        public int FindClosestImageRow(long fileID)
        {
            int countAllCurrentlySelectedFiles = this.CountAllCurrentlySelectedFiles;
            for (int rowIndex = 0, maxCount = countAllCurrentlySelectedFiles; rowIndex < maxCount; ++rowIndex)
            {
                if (this.FileTable[rowIndex].ID >= fileID)
                {
                    return rowIndex;
                }
            }
            return countAllCurrentlySelectedFiles - 1;
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
            int lastIndex = this.CountAllCurrentlySelectedFiles - 1;
            int countAllCurrentlySelectedFiles = this.CountAllCurrentlySelectedFiles;
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
            if (firstIndex >= countAllCurrentlySelectedFiles)
            {
                return countAllCurrentlySelectedFiles - 1;
            }

            // all IDs in the selection are larger than fileID
            return firstIndex;
        }
        #endregion

        #region Binding the data grid
        // Bind the data grid to an event, using boundGrid and the onFileDataTableRowChanged event 

        // Convenience form that knows which datagrid to use
        public void BindToDataGrid()
        {
            if (this.FileTable == null)
            {
                return;
            }
            this.FileTable.BindDataGrid(this.boundGrid, this.onFileDataTableRowChanged);
        }

        // Generalized form of the above
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
        #endregion

        #region Index creation and dropping
        public void IndexCreateForDetectionsAndClassifications()
        {
            this.Database.IndexCreate(Constant.DatabaseValues.IndexID, Constant.DBTables.Detections, Constant.DatabaseColumn.ID);
            this.Database.IndexCreate(Constant.DatabaseValues.IndexDetectionID, Constant.DBTables.Classifications, Constant.DetectionColumns.DetectionID);
        }

        public void IndexCreateForFileAndRelativePath()
        {
            this.Database.IndexCreate(Constant.DatabaseValues.IndexRelativePath, Constant.DBTables.FileData, Constant.DatabaseColumn.RelativePath);
            this.Database.IndexCreate(Constant.DatabaseValues.IndexFile, Constant.DBTables.FileData, Constant.DatabaseColumn.File);
        }

        public void IndexDropForFileAndRelativePath()
        {
            this.Database.IndexDrop(Constant.DatabaseValues.IndexRelativePath);
            this.Database.IndexDrop(Constant.DatabaseValues.IndexFile);
        }
        #endregion

        #region File retrieval and manipulation
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

        // NO LONGER USED AS THESE HAVE ALL BEEN CENTRALIZED TO USE THE SEARCHTERM DATA STRUCTURE
        //private string GetFilesConditionalExpression(FileSelectionEnum selection)
        //{
        //    // System.Diagnostics.Debug.Print(selection.ToString());
        //    switch (selection)
        //    {
        //        case FileSelectionEnum.All:
        //        case FileSelectionEnum.Corrupted:
        //        case FileSelectionEnum.Missing:
        //            // SAULXXX: Corrupted and Missing should no longer be accessible: these cases could be deleted.
        //            return String.Empty;
        //        case FileSelectionEnum.Dark:
        //        case FileSelectionEnum.Ok:
        //            return Sql.Where + this.DataLabelFromStandardControlType[Constant.DatabaseColumn.ImageQuality] + "=" + Sql.Quote(selection.ToString());
        //        case FileSelectionEnum.MarkedForDeletion:
        //            return Sql.Where + this.DataLabelFromStandardControlType[Constant.DatabaseColumn.DeleteFlag] + "=" + Sql.Quote(Constant.BooleanValue.True);
        //        case FileSelectionEnum.Custom:
        //        case FileSelectionEnum.Folders:
        //            //this.CustomSelection.GetRelativePathFolder();
        //            string whereClause = this.CustomSelection.GetFilesWhere();
        //            System.Diagnostics.Debug.Print(whereClause);
        //            //return this.CustomSelection.GetFilesWhere();
        //            return whereClause;
        //        default:
        //            throw new NotSupportedException(String.Format("Unhandled quality selection {0}.  For custom selections call CustomSelection.GetImagesWhere().", selection));
        //    }
        //}
        #endregion

        #region Markers
        /// <summary>
        /// Get all markers for the specified file.
        /// This is done by getting the marker list associated with all counters representing the current row
        /// It will have a MarkerCounter for each control, even if there may be no metatags in it
        /// </summary>
        public List<MarkersForCounter> MarkersGetMarkersForCurrentFile(long fileID)
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
                    TracePrint.PrintMessage(String.Format("Read of marker failed for dataLabel '{0}'. {1}", dataLabel, exception.ToString()));
                    pointList = String.Empty;
                }
                markersForCounter.ParsePontList(pointList);
                markersForAllCounters.Add(markersForCounter);
            }
            return markersForAllCounters;
        }

        // Get all markers from the Markers table and load it into the data table
        private void MarkersLoadRowsFromDatabase()
        {
            string markersQuery = Sql.SelectStarFrom + Constant.DBTables.Markers;
            this.Markers = new DataTableBackedList<MarkerRow>(this.Database.GetDataTableFromSelect(markersQuery), (DataRow row) => { return new MarkerRow(row); });
        }

        // Add an empty new row to the marker list if it isnt there. Return true if we added it, otherwise false 
        // Columns will be automatically set to NULL
        public bool MarkersTryInsertNewMarkerRow(long imageID)
        {
            if (this.Markers.Find(imageID) != null)
            {
                // There should already be a row for this, so don't creat one
                return false;
            }
            List<ColumnTuple> columns = new List<ColumnTuple>()
            {
                new ColumnTuple(Constant.DatabaseColumn.ID, imageID.ToString())
            };
            List<List<ColumnTuple>> insertionStatements = new List<List<ColumnTuple>>()
            {
                columns
            };
            this.Database.Insert(Constant.DBTables.Markers, insertionStatements);
            this.MarkersLoadRowsFromDatabase(); // Update the markers list to include this new row
            return true;
        }

        /// <summary>
        /// Set the list of marker points on the current row in the marker table. 
        /// </summary>
        public void MarkersUpdateMarkerRow(long imageID, MarkersForCounter markersForCounter)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(markersForCounter, nameof(markersForCounter));

            // Find the current row number
            MarkerRow marker = this.Markers.Find(imageID);
            if (marker == null)
            {
                TracePrint.PrintMessage(String.Format("Image ID {0} missing in markers table.", imageID));
                return;
            }

            // Update the database and datatable
            // Note that I repeated the null check here, as for some reason it was still coming up as a CA1062 warning
            ThrowIf.IsNullArgument(markersForCounter, nameof(markersForCounter));
            marker[markersForCounter.DataLabel] = markersForCounter.GetPointList();
            this.UpdateSyncMarkerToDatabase(marker);
        }
        #endregion

        #region ImageSet manipulation
        private void ImageSetLoadFromDatabase()
        {
            string imageSetQuery = Sql.SelectStarFrom + Constant.DBTables.ImageSet + Sql.Where + Constant.DatabaseColumn.ID + " = " + Constant.DatabaseValues.ImageSetRowID.ToString();
            DataTable imageSetTable = this.Database.GetDataTableFromSelect(imageSetQuery);
            this.ImageSet = new ImageSetRow(imageSetTable.Rows[0]);
            if (imageSetTable != null)
            {
                imageSetTable.Dispose();
            }
        }
        #endregion

        #region DETECTION - Populate the Database (with progress bar)
        // To help determine periodic updates to the progress bar 
        private DateTime lastRefreshDateTime = DateTime.Now;
        protected bool ReadyToRefresh()
        {
            TimeSpan intervalFromLastRefresh = DateTime.Now - this.lastRefreshDateTime;
            if (intervalFromLastRefresh > Constant.ThrottleValues.ProgressBarRefreshInterval)
            {
                this.lastRefreshDateTime = DateTime.Now;
                return true;
            }
            return false;
        }

        private void PStream_BytesRead(object sender, ProgressStreamReportEventArgs args)
        {
            Progress<ProgressBarArguments> progressHandler = new Progress<ProgressBarArguments>(value =>
            {
                FileDatabase.UpdateProgressBar(GlobalReferences.BusyCancelIndicator, value.PercentDone, value.Message, value.IsCancelEnabled, value.IsIndeterminate);
            });
            IProgress<ProgressBarArguments> progress = progressHandler;

            long current = args.StreamPosition;
            long total = args.StreamLength;
            double p = current / ((double)total);
            if (this.ReadyToRefresh())
            {
                // SaulXXX This should really be cancellable, but not sure how to do it from here.
                // Update the progress bar
                progress.Report(new ProgressBarArguments((int)(100 * p), "Reading detection files, please wait", false, false));
                Thread.Sleep(Constant.ThrottleValues.RenderingBackoffTime);  // Allows the UI thread to update every now and then
            }
        }

        static private void UpdateProgressBar(BusyCancelIndicator busyCancelIndicator, int percent, string message, bool isCancelEnabled, bool isIndeterminate)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // Code to run on the GUI thread.
                // Check the arguments for null 
                ThrowIf.IsNullArgument(busyCancelIndicator, nameof(busyCancelIndicator));

                // Set it as a progressive or indeterminate bar
                busyCancelIndicator.IsIndeterminate = isIndeterminate;

                // Set the progress bar position (only visible if determinate)
                busyCancelIndicator.Percent = percent;

                // Update the text message
                busyCancelIndicator.Message = message;

                // Update the cancel button to reflect the cancelEnabled argument
                busyCancelIndicator.CancelButtonIsEnabled = isCancelEnabled;
                busyCancelIndicator.CancelButtonText = isCancelEnabled ? "Cancel" : "Processing detections...";
            });
        }

        public async Task<bool> PopulateDetectionTablesAsync(string path, List<string> foldersInDBListButNotInJSon, List<string> foldersInJsonButNotInDB, List<string> foldersInBoth)
        {
            // Set up a progress handler that will update the progress bar
            Progress<ProgressBarArguments> progressHandler = new Progress<ProgressBarArguments>(value =>
            {
                // Update the progress bar
                FileDatabase.UpdateProgressBar(GlobalReferences.BusyCancelIndicator, value.PercentDone, value.Message, value.IsCancelEnabled, value.IsIndeterminate);
            });
            IProgress<ProgressBarArguments> progress = progressHandler;

            // Check the arguments for null 
            ThrowIf.IsNullArgument(foldersInDBListButNotInJSon, nameof(foldersInDBListButNotInJSon));
            ThrowIf.IsNullArgument(foldersInJsonButNotInDB, nameof(foldersInJsonButNotInDB));
            ThrowIf.IsNullArgument(foldersInBoth, nameof(foldersInBoth));

            if (File.Exists(path) == false)
            {
                return false;
            }

            bool result;
            using (ProgressStream ps = new ProgressStream(System.IO.File.OpenRead(path)))
            {
                ps.BytesRead += new ProgressStreamReportDelegate(PStream_BytesRead);

                using (TextReader sr = new StreamReader(ps))
                {
                    result = await Task.Run(() =>
                    {
                        try
                        {
                            using (JsonReader reader = new JsonTextReader(sr))
                            {
                                JsonSerializer serializer = new JsonSerializer();
                                Detector detector = serializer.Deserialize<Detector>(reader);

                                // If detection population was previously done in this session, resetting these tables to null 
                                // will force reading the new values into them
                                this.detectionDataTable = null; // to force repopulating the data structure if it already exists.
                                this.detectionCategoriesDictionary = null;
                                this.classificationCategoriesDictionary = null;
                                this.classificationsDataTable = null;

                                // Prepare the detection tables. If they already exist, clear them
                                DetectionDatabases.CreateOrRecreateTablesAndColumns(this.Database);

                                // PERFORMANCE This check is likely somewhat slow. Check it on large detection files / dbs 
                                if (this.CompareDetectorAndDBFolders(detector, foldersInDBListButNotInJSon, foldersInJsonButNotInDB, foldersInBoth) == false)
                                {
                                    // No folders in the detections match folders in the databases. Abort without doing anything.
                                    return false;
                                }
                                // PERFORMANCE This method does two things:
                                // - it walks through the detector data structure to construct sql insertion statements
                                // - it invokes the actual insertion in the database.
                                // Both steps are very slow with a very large JSON of detections that matches folders of images.
                                // (e.g., 225 seconds for 2,000,000 images and their detections). Note that I batch insert 50,000 statements at a time. 

                                // Update the progress bar and populate the detection tables
                                progress.Report(new ProgressBarArguments(0, "Updating database with detections. Please wait", false, true));
                                Thread.Sleep(Constant.ThrottleValues.RenderingBackoffTime);  // Allows the UI thread to update every now and then
                                DetectionDatabases.PopulateTables(detector, this, this.Database, String.Empty);

                                // DetectionExists needs to be primed if it is to save its DetectionExists state
                                this.DetectionsExists(true);
                            }
                            return true;
                        }
                        catch (Exception e)
                        {
                            System.Diagnostics.Debug.Print(e.Message + "Could not populate detection data");
                            return false;
                        }
                    }).ConfigureAwait(true);
                }
            }
            return result;
        }
        #endregion

        #region Detections
        // Return true if there is at least one match between a detector folder and a DB folder
        // Return a list of folder paths missing in the DB but present in the detector file
        private bool CompareDetectorAndDBFolders(Detector detector, List<string> foldersInDBListButNotInJSon, List<string> foldersInJsonButNotInDB, List<string> foldersInBoth)
        {
            string folderpath;

            if (detector.images.Count <= 0)
            {
                // No point continuing if there are no detector entries
                return false;
            }

            // Get all distinct folders in the database
            // This operation could b somewhat slow, but ...
            List<string> FoldersInDBList = this.GetDistinctValuesInColumn(Constant.DBTables.FileData, Constant.DatabaseColumn.RelativePath).Select(i => i.ToString()).ToList();
            if (FoldersInDBList.Count == 0)
            {
                // No point continuing if there are no folders in the database (i.e., no images)
                return false;
            }

            // Get all distinct folders in the Detector 
            // We add a closing slash onto the imageFilePath to terminate any matches
            // e.g., A/B  would also match A/Buzz, which we don't want. But A/B/ won't match that.
            SortedSet<string> foldersInDetectorList = new SortedSet<string>();
            foreach (image image in detector.images)
            {
                folderpath = Path.GetDirectoryName(image.file);
                if (!string.IsNullOrEmpty(folderpath))
                {
                    folderpath += "\\";
                }
                if (foldersInDetectorList.Contains(folderpath) == false)
                {
                    foldersInDetectorList.Add(folderpath);
                }
            }

            // Compare each folder in the DB against the folders in the detector );
            foreach (string originalFolderDB in FoldersInDBList)
            {
                // Add a closing slash to the folderDB for the same reasons described above
                string modifiedFolderDB = String.Empty;
                if (!string.IsNullOrEmpty(originalFolderDB))
                {
                    modifiedFolderDB = originalFolderDB + "\\";
                }

                if (foldersInDetectorList.Contains(modifiedFolderDB))
                {
                    // this folder path is in both the detector file and the image set
                    foldersInBoth.Add(modifiedFolderDB);
                }
                else
                {
                    if (string.IsNullOrEmpty(originalFolderDB))
                    {
                        // An empty strng is the root folder, so make sure we add it
                        foldersInDBListButNotInJSon.Add("<root folder>");
                    }
                    else
                    {
                        // This folder is in the image set but NOT in the detector
                        foldersInDBListButNotInJSon.Add(originalFolderDB);
                    }
                }
            }
            List<string> tempList = foldersInDetectorList.Except(foldersInBoth).ToList();
            foreach (string s in tempList)
            {
                foldersInJsonButNotInDB.Add(s);
            }
            // if there is at least one folder in both, it means that we have some recognition data that we can import.
            return foldersInBoth.Count > 0;
        }

        // Get the detections associated with the current file, if any
        // As part of the, create a DetectionTable in memory that mirrors the database table
        public DataRow[] GetDetectionsFromFileID(long fileID)
        {
            if (this.detectionDataTable == null)
            {
                // PERFORMANCE 0 or more detections can be associated with every image. THus we should expect the number of detections could easily be two or three times the 
                // number of images. With very large databases, retrieving the datatable of detections can be very slow (and can consume significant memory). 
                // While this operation is only done once per image set session, it is still expensive. I suppose I could get it from the database on the fly, but 
                // its important to show detection data (including bounding boxes) as rapidly as possible, such as when a user is quickly scrolling through images.
                // So I am not clear on how to optimize this (although I suspect a thread running in the background when Timelapse is loaded could perhaps do this)
                this.detectionDataTable = this.Database.GetDataTableFromSelect(Sql.SelectStarFrom + Constant.DBTables.Detections);
            }
            // Retrieve the detection from the in-memory datatable.
            // Note that because IDs are in the database as a string, we convert it
            // PERFORMANCE: This takes a bit of time, not much... but could be improved. Not sure if there is an index automatically built on it. If not, do so.
            return this.detectionDataTable.Select(Constant.DatabaseColumn.ID + Sql.Equal + fileID.ToString());
        }

        // Get the detections associated with the current file, if any
        public DataRow[] GetClassificationsFromDetectionID(long detectionID)
        {
            if (this.classificationsDataTable == null)
            {
                this.classificationsDataTable = this.Database.GetDataTableFromSelect(Sql.SelectStarFrom + Constant.DBTables.Classifications);
            }
            // Note that because IDs are in the database as a string, we convert it
            return this.classificationsDataTable.Select(Constant.ClassificationColumns.DetectionID + Sql.Equal + detectionID.ToString());
        }

        // Return the label that matches the detection category 
        public string GetDetectionLabelFromCategory(string category)
        {
            this.CreateDetectionCategoriesDictionaryIfNeeded();
            return this.detectionCategoriesDictionary.TryGetValue(category, out string value) ? value : String.Empty;

        }

        public void CreateDetectionCategoriesDictionaryIfNeeded()
        {
            // Null means we have never tried to create the dictionary. Try to do so.
            if (this.detectionCategoriesDictionary == null)
            {
                this.detectionCategoriesDictionary = new Dictionary<string, string>();
                try
                {
                    using (DataTable dataTable = this.Database.GetDataTableFromSelect(Sql.SelectStarFrom + Constant.DBTables.DetectionCategories))
                    {
                        int dataTableRowCount = dataTable.Rows.Count;
                        for (int i = 0; i < dataTableRowCount; i++)
                        {
                            DataRow row = dataTable.Rows[i];
                            this.detectionCategoriesDictionary.Add((string)row[Constant.DetectionCategoriesColumns.Category], (string)row[Constant.DetectionCategoriesColumns.Label]);
                        }
                    }
                }
                catch
                {
                    // Should never really get here, but just in case.
                }
            }
        }

        // Create the detection category dictionary to mirror the detection table
        public string GetDetectionCategoryFromLabel(string label)
        {
            try
            {
                this.CreateDetectionCategoriesDictionaryIfNeeded();
                // A lookup dictionary should now exists, so just return the category value.
                string myKey = this.detectionCategoriesDictionary.FirstOrDefault(x => x.Value == label).Key;
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
            this.CreateDetectionCategoriesDictionaryIfNeeded();
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
                try
                {
                    using (DataTable dataTable = this.Database.GetDataTableFromSelect(Sql.SelectStarFrom + Constant.DBTables.ClassificationCategories))
                    {
                        int dataTableRowCount = dataTable.Rows.Count;
                        for (int i = 0; i < dataTableRowCount; i++)
                        {
                            DataRow row = dataTable.Rows[i];
                            this.classificationCategoriesDictionary.Add((string)row[Constant.ClassificationCategoriesColumns.Category], (string)row[Constant.ClassificationCategoriesColumns.Label]);
                        }
                    }
                }
                catch
                {
                    // Should never really get here, but just in case.
                }
            }
        }

        public List<string> GetClassificationLabels()
        {
            List<string> labels = new List<string>();
            this.CreateClassificationCategoriesDictionaryIfNeeded();
            foreach (KeyValuePair<string, string> entry in this.classificationCategoriesDictionary)
            {
                labels.Add(entry.Value);
            }
            labels = labels.OrderBy(q => q).ToList();
            return labels;
        }

        // return the label that matches the detection category 
        public string GetClassificationLabelFromCategory(string category)
        {
            try
            {
                this.CreateClassificationCategoriesDictionaryIfNeeded();
                // A lookup dictionary should now exists, so just return the category value.
                return this.classificationCategoriesDictionary.TryGetValue(category, out string value) ? value : String.Empty;
            }
            catch
            {
                // Should never really get here, but just in case.
                return String.Empty;
            }
        }

        public string GetClassificationCategoryFromLabel(string label)
        {
            try
            {
                this.CreateClassificationCategoriesDictionaryIfNeeded();
                // At this point, a lookup dictionary already exists, so just return the category number.
                string myKey = this.classificationCategoriesDictionary.FirstOrDefault(x => x.Value == label).Key;
                return myKey ?? String.Empty;
            }
            catch
            {
                // Should never really get here, but just in case.
                return String.Empty;
            }
        }
        // See if detections exist in this instance. We test once, and then save the state (unless forceQuery is true)
        private bool? detectionExists;
        /// <summary>
        /// Return if a non-empty detections table exists. If forceQuery is true, then we always do this via an SQL query vs. refering to previous checks
        /// </summary>
        /// <returns></returns>
        public bool DetectionsExists()
        {
            return this.DetectionsExists(false);
        }
        public bool DetectionsExists(bool forceQuery)
        {
            if (forceQuery == true || this.detectionExists == null)
            {
                this.detectionExists = this.Database.TableExistsAndNotEmpty(Constant.DBTables.Detections);
            }
            return this.detectionExists == true;
        }
        #endregion

        #region Quickpaste retrieval
        public static string TryGetQuickPasteXMLFromDatabase(string filePath)
        {
            // Open the database if it exists
            SQLiteWrapper sqliteWrapper = new SQLiteWrapper(filePath);
            if (sqliteWrapper.SchemaIsColumnInTable(Constant.DBTables.ImageSet, Constant.DatabaseColumn.QuickPasteXML) == false)
            {
                // The column isn't in the table, so give up
                return String.Empty;
            }

            List<object> listOfObjects = sqliteWrapper.GetDistinctValuesInColumn(Constant.DBTables.ImageSet, Constant.DatabaseColumn.QuickPasteXML);
            if (listOfObjects.Count == 1)
            {
                return (string)listOfObjects[0];
            }
            return String.Empty;
        }
        #endregion

        #region Disposing
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
                if (this.detectionDataTable != null)
                {
                    detectionDataTable.Dispose();
                }
                if (this.classificationsDataTable != null)
                {
                    classificationsDataTable.Dispose();
                }
            }

            base.Dispose(disposing);
            this.disposed = true;
        }
        #endregion
    }
}