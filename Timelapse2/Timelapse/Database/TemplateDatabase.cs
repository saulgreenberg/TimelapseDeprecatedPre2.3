using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using Timelapse.Util;

namespace Timelapse.Database
{
    /// <summary>
    /// Timelapse Template Database.
    /// </summary>
    public class TemplateDatabase : IDisposable
    {
        #region Public / Protected Properties
        public DataTableBackedList<ControlRow> Controls { get; private set; }

        /// <summary>Gets the file name of the image database on disk.</summary>
        public string FilePath { get; private set; }

        protected SQLiteWrapper Database { get; set; }
        #endregion

        #region Private Variables
        private bool disposed;
        private DataGrid editorDataGrid;
        private DateTime mostRecentBackup;
        private DataRowChangeEventHandler onTemplateTableRowChanged;
        #endregion

        #region Constructors
        protected TemplateDatabase(string filePath)
        {
            this.disposed = false;
            this.mostRecentBackup = FileBackup.GetMostRecentBackup(filePath);

            // open or create database
            this.Database = new SQLiteWrapper(filePath);
            this.FilePath = filePath;
        }
        #endregion

        #region Public / Protected Async Tasks - TryCreateOrOpen, OnDatabaseCreated, UpgradeDatabasesAndCompareTemplates, OnExistingDatabaseOpened
        public async static Task<TemplateDatabase> CreateOrOpenAsync(string filePath)
        {
            // check for an existing database before instantiating the databse as SQL wrapper instantiation creates the database file
            bool populateDatabase = !File.Exists(filePath);

            TemplateDatabase templateDatabase = new TemplateDatabase(filePath);
            if (populateDatabase)
            {
                // initialize the database if it's newly created
                await templateDatabase.OnDatabaseCreatedAsync(null).ConfigureAwait(true);
            }
            else
            {
                // The database file exists. However, we still need to check if its valid. 
                // We do this by checking the database integrity (which may raise an internal exception) and if that is ok, by checking if it has a TemplateTable. 
                if (templateDatabase.Database.PragmaGetQuickCheck() == false || templateDatabase.TableExists(Constant.DBTables.Controls) == false)
                {
                    if (templateDatabase != null)
                    {
                        templateDatabase.Dispose();
                    }
                    return null;
                }
                // if it's an existing database check if it needs updating to current structure and load data tables
                await templateDatabase.OnExistingDatabaseOpenedAsync(null, null).ConfigureAwait(true);
            }
            return templateDatabase;
        }

        public async static Task<Tuple<bool, TemplateDatabase>> TryCreateOrOpenAsync(string filePath)
        {
            // Follow the MSDN design pattern for returning an IDisposable: https://www.codeproject.com/Questions/385273/Returning-a-Disposable-Object-from-a-Method
            TemplateDatabase disposableTemplateDB = null;
            try
            {
                disposableTemplateDB = await CreateOrOpenAsync(filePath).ConfigureAwait(true);
                TemplateDatabase returnableTemplateDB = disposableTemplateDB;
                // the returnableTemplateDB will be null if its not a valid template, e.g., if no TemplateTable exists in it
                bool successOrFail = returnableTemplateDB != null;
                return new Tuple<bool, TemplateDatabase>(successOrFail, returnableTemplateDB);
            }
            catch (Exception exception)
            {
                TracePrint.PrintMessage(String.Format("Failure in TryCreateOpen. {0}", exception.ToString()));
                return new Tuple<bool, TemplateDatabase>(false, null);
            }
            finally
            {
                if (disposableTemplateDB != null)
                {
                    disposableTemplateDB.Dispose();
                }
            }
        }

        protected async virtual Task OnDatabaseCreatedAsync(TemplateDatabase other)
        {
            // create the template table
            await Task.Run(() =>
            {
                List<SchemaColumnDefinition> templateTableColumns = new List<SchemaColumnDefinition>
                {
                    new SchemaColumnDefinition(Constant.DatabaseColumn.ID, Sql.CreationStringPrimaryKey),
                    new SchemaColumnDefinition(Constant.Control.ControlOrder, Sql.IntegerType),
                    new SchemaColumnDefinition(Constant.Control.SpreadsheetOrder, Sql.IntegerType),
                    new SchemaColumnDefinition(Constant.Control.Type, Sql.Text),
                    new SchemaColumnDefinition(Constant.Control.DefaultValue, Sql.Text),
                    new SchemaColumnDefinition(Constant.Control.Label, Sql.Text),
                    new SchemaColumnDefinition(Constant.Control.DataLabel, Sql.Text),
                    new SchemaColumnDefinition(Constant.Control.Tooltip, Sql.Text),
                    new SchemaColumnDefinition(Constant.Control.TextBoxWidth, Sql.Text),
                    new SchemaColumnDefinition(Constant.Control.Copyable, Sql.Text),
                    new SchemaColumnDefinition(Constant.Control.Visible, Sql.Text),
                    new SchemaColumnDefinition(Constant.Control.List, Sql.Text)
                };
                this.Database.CreateTable(Constant.DBTables.Controls, templateTableColumns);

                // if an existing table was passed, clone its contents into this database
                if (other != null)
                {
                    this.SyncTemplateTableToDatabase(other.Controls);
                    return;
                }

                // no existing table to clone, so add standard controls to template table
                List<List<ColumnTuple>> standardControls = new List<List<ColumnTuple>>();
                long controlOrder = 0; // The control order, a one based count incremented for every new entry
                long spreadsheetOrder = 0; // The spreadsheet order, a one based count incremented for every new entry

                // file
                List<ColumnTuple> file = new List<ColumnTuple>
            {
                new ColumnTuple(Constant.Control.ControlOrder, ++controlOrder),
                new ColumnTuple(Constant.Control.SpreadsheetOrder, ++spreadsheetOrder),
                new ColumnTuple(Constant.Control.Type, Constant.DatabaseColumn.File),
                new ColumnTuple(Constant.Control.DefaultValue, Constant.ControlDefault.Value),
                new ColumnTuple(Constant.Control.Label, Constant.DatabaseColumn.File),
                new ColumnTuple(Constant.Control.DataLabel, Constant.DatabaseColumn.File),
                new ColumnTuple(Constant.Control.Tooltip, Constant.ControlDefault.FileTooltip),
                new ColumnTuple(Constant.Control.TextBoxWidth, Constant.ControlDefault.FileWidth),
                new ColumnTuple(Constant.Control.Copyable, false),
                new ColumnTuple(Constant.Control.Visible, true),
                new ColumnTuple(Constant.Control.List, Constant.ControlDefault.Value)
            };
                standardControls.Add(file);

                // relative path
                standardControls.Add(GetRelativePathTuples(++controlOrder, ++spreadsheetOrder, true));

                // folder
                List<ColumnTuple> folder = new List<ColumnTuple>
            {
                new ColumnTuple(Constant.Control.ControlOrder, ++controlOrder),
                new ColumnTuple(Constant.Control.SpreadsheetOrder, ++spreadsheetOrder),
                new ColumnTuple(Constant.Control.Type, Constant.DatabaseColumn.Folder),
                new ColumnTuple(Constant.Control.DefaultValue, Constant.ControlDefault.Value),
                new ColumnTuple(Constant.Control.Label, Constant.DatabaseColumn.Folder),
                new ColumnTuple(Constant.Control.DataLabel, Constant.DatabaseColumn.Folder),
                new ColumnTuple(Constant.Control.Tooltip, Constant.ControlDefault.FolderTooltip),
                new ColumnTuple(Constant.Control.TextBoxWidth, Constant.ControlDefault.FolderWidth),
                new ColumnTuple(Constant.Control.Copyable, false),
                new ColumnTuple(Constant.Control.Visible, true),
                new ColumnTuple(Constant.Control.List, Constant.ControlDefault.Value)
            };
                standardControls.Add(folder);

                // datetime
                standardControls.Add(GetDateTimeTuples(++controlOrder, ++spreadsheetOrder, true));

                // utcOffset
                standardControls.Add(GetUtcOffsetTuples(++controlOrder, ++spreadsheetOrder, false));

                // date
                List<ColumnTuple> date = new List<ColumnTuple>
            {
                new ColumnTuple(Constant.Control.ControlOrder, ++controlOrder),
                new ColumnTuple(Constant.Control.SpreadsheetOrder, ++spreadsheetOrder),
                new ColumnTuple(Constant.Control.Type, Constant.DatabaseColumn.Date),
                new ColumnTuple(Constant.Control.DefaultValue, DateTimeHandler.ToStringDisplayDate(Constant.ControlDefault.DateTimeValue)),
                new ColumnTuple(Constant.Control.Label, Constant.DatabaseColumn.Date),
                new ColumnTuple(Constant.Control.DataLabel, Constant.DatabaseColumn.Date),
                new ColumnTuple(Constant.Control.Tooltip, Constant.ControlDefault.DateTooltip),
                new ColumnTuple(Constant.Control.TextBoxWidth, Constant.ControlDefault.DateWidth),
                new ColumnTuple(Constant.Control.Copyable, false),
                new ColumnTuple(Constant.Control.Visible, false),
                new ColumnTuple(Constant.Control.List, Constant.ControlDefault.Value)
            };
                standardControls.Add(date);

                // time
                List<ColumnTuple> time = new List<ColumnTuple>
            {
                new ColumnTuple(Constant.Control.ControlOrder, ++controlOrder),
                new ColumnTuple(Constant.Control.SpreadsheetOrder, ++spreadsheetOrder),
                new ColumnTuple(Constant.Control.Type, Constant.DatabaseColumn.Time),
                new ColumnTuple(Constant.Control.DefaultValue, DateTimeHandler.ToStringDisplayTime(Constant.ControlDefault.DateTimeValue)),
                new ColumnTuple(Constant.Control.Label, Constant.DatabaseColumn.Time),
                new ColumnTuple(Constant.Control.DataLabel, Constant.DatabaseColumn.Time),
                new ColumnTuple(Constant.Control.Tooltip, Constant.ControlDefault.TimeTooltip),
                new ColumnTuple(Constant.Control.TextBoxWidth, Constant.ControlDefault.TimeWidth),
                new ColumnTuple(Constant.Control.Copyable, false),
                new ColumnTuple(Constant.Control.Visible, false),
                new ColumnTuple(Constant.Control.List, Constant.ControlDefault.Value)
            };
                standardControls.Add(time);

                // image quality
                List<ColumnTuple> imageQuality = new List<ColumnTuple>
            {
                new ColumnTuple(Constant.Control.ControlOrder, ++controlOrder),
                new ColumnTuple(Constant.Control.SpreadsheetOrder, ++spreadsheetOrder),
                new ColumnTuple(Constant.Control.Type, Constant.DatabaseColumn.ImageQuality),
                new ColumnTuple(Constant.Control.DefaultValue, Constant.ImageQuality.Ok),
                new ColumnTuple(Constant.Control.Label, Constant.DatabaseColumn.ImageQuality),
                new ColumnTuple(Constant.Control.DataLabel, Constant.DatabaseColumn.ImageQuality),
                new ColumnTuple(Constant.Control.Tooltip, Constant.ControlDefault.ImageQualityTooltip),
                new ColumnTuple(Constant.Control.TextBoxWidth, Constant.ControlDefault.ImageQualityWidth),
                new ColumnTuple(Constant.Control.Copyable, false),
                new ColumnTuple(Constant.Control.Visible, true),
                new ColumnTuple(Constant.Control.List, Constant.ImageQuality.ListOfValues)
            };
                standardControls.Add(imageQuality);

                // delete flag
                standardControls.Add(GetDeleteFlagTuples(++controlOrder, ++spreadsheetOrder, true));

                // insert standard controls into the template table
                this.Database.Insert(Constant.DBTables.Controls, standardControls);

                // populate the in memory version of the template table
                this.GetControlsSortedByControlOrder();
            }).ConfigureAwait(true);
        }

        protected async virtual Task UpgradeDatabasesAndCompareTemplatesAsync(TemplateDatabase other, TemplateSyncResults templateSyncResults)
        {
            await Task.Run(() =>
            {
                this.GetControlsSortedByControlOrder();
                this.EnsureDataLabelsAndLabelsNotEmpty();
                this.EnsureCurrentSchema();
            }).ConfigureAwait(true);
        }

        protected async virtual Task OnExistingDatabaseOpenedAsync(TemplateDatabase other, TemplateSyncResults templateSyncResults)
        {
            await Task.Run(() =>
            {
                this.GetControlsSortedByControlOrder();
                this.EnsureDataLabelsAndLabelsNotEmpty();
                this.EnsureCurrentSchema();
            }).ConfigureAwait(true);
        }
        #endregion

        #region Public Methods - Boolean tests - Exists tables, Is database valid
        public bool TableExists(string dataTable)
        {
            return this.Database.TableExists(dataTable);
        }

        // Check if the database table specified in the path has a detections table
        public static bool TableExists(string dataTable, string dbPath)
        {
            // Note that no error checking is done - I assume, perhaps unwisely, that the file is a valid database
            // On tedting, it does return 'false' on an invalid ddb file, so I suppose that's ok.
            SQLiteWrapper db = new SQLiteWrapper(dbPath);
            return db.TableExists(dataTable);
        }

        // Check if the database is valid. 
        public bool IsDatabaseFileValid(string filePath, string tableNameToCheck)
        {
            // check if a database file exists, and if so that it is not corrupt
            if (!File.Exists(filePath))
            {
                return false;
            }

            // The database file exists. However, we still need to check if its valid. 
            using (TemplateDatabase database = new TemplateDatabase(filePath))
            {
                if (database?.Database == null)
                {
                    return false;
                }

                // We do this by checking the database integrity (which may raise an internal exception) and if that is ok, by checking if it has a TemplateTable. 
                if (this.Database.PragmaGetQuickCheck() == false || this.TableExists(tableNameToCheck) == false)
                {
                    return false;
                }
                return true;
            }
        }

        public bool IsControlCopyable(string dataLabel)
        {
            long id = this.GetControlIDFromTemplateTable(dataLabel);
            ControlRow control = this.Controls.Find(id);
            return control.Copyable;
        }
        #endregion

        #region Public methods - Add user defined control
        public ControlRow AddUserDefinedControl(string controlType)
        {
            this.CreateBackupIfNeeded();

            // create the row for the new control in the data table
            ControlRow newControl = this.Controls.NewRow();
            string dataLabelPrefix;
            switch (controlType)
            {
                case Constant.Control.Counter:
                    dataLabelPrefix = Constant.Control.Counter;
                    newControl.DefaultValue = Constant.ControlDefault.CounterValue;
                    newControl.Type = Constant.Control.Counter;
                    newControl.Width = Constant.ControlDefault.CounterWidth;
                    newControl.Copyable = false;
                    newControl.Visible = true;
                    newControl.Tooltip = Constant.ControlDefault.CounterTooltip;
                    newControl.DataLabel = this.GetNextUniqueDataLabel(dataLabelPrefix);
                    newControl.Label = newControl.DataLabel;
                    break;
                case Constant.Control.Note:
                    dataLabelPrefix = Constant.Control.Note;
                    newControl.DefaultValue = Constant.ControlDefault.Value;
                    newControl.Type = Constant.Control.Note;
                    newControl.Width = Constant.ControlDefault.NoteWidth;
                    newControl.Copyable = true;
                    newControl.Visible = true;
                    newControl.Tooltip = Constant.ControlDefault.NoteTooltip;
                    newControl.DataLabel = this.GetNextUniqueDataLabel(dataLabelPrefix);
                    newControl.Label = newControl.DataLabel;
                    break;
                case Constant.Control.FixedChoice:
                    dataLabelPrefix = Constant.Control.Choice;
                    newControl.DefaultValue = Constant.ControlDefault.Value;
                    newControl.Type = Constant.Control.FixedChoice;
                    newControl.Width = Constant.ControlDefault.FixedChoiceWidth;
                    newControl.Copyable = true;
                    newControl.Visible = true;
                    newControl.Tooltip = Constant.ControlDefault.FixedChoiceTooltip;
                    newControl.DataLabel = this.GetNextUniqueDataLabel(dataLabelPrefix);
                    newControl.Label = newControl.DataLabel;
                    break;
                case Constant.Control.Flag:
                    dataLabelPrefix = Constant.Control.Flag;
                    newControl.DefaultValue = Constant.ControlDefault.FlagValue;
                    newControl.Type = Constant.Control.Flag;
                    newControl.Width = Constant.ControlDefault.FlagWidth;
                    newControl.Copyable = true;
                    newControl.Visible = true;
                    newControl.Tooltip = Constant.ControlDefault.FlagTooltip;
                    newControl.DataLabel = this.GetNextUniqueDataLabel(dataLabelPrefix);
                    newControl.Label = newControl.DataLabel;
                    break;
                default:
                    throw new NotSupportedException(String.Format("Unhandled control type {0}.", controlType));
            }
            newControl.ControlOrder = this.GetOrderForNewControl();
            newControl.List = Constant.ControlDefault.Value;
            newControl.SpreadsheetOrder = newControl.ControlOrder;

            // add the new control to the database
            List<List<ColumnTuple>> controlInsertWrapper = new List<List<ColumnTuple>>() { newControl.CreateColumnTuplesWithWhereByID().Columns };
            this.Database.Insert(Constant.DBTables.Controls, controlInsertWrapper);

            // update the in memory table to reflect current database content
            // could just add the new row to the table but this is done in case a bug results in the insert lacking perfect fidelity
            this.GetControlsSortedByControlOrder();
            return this.Controls[this.Controls.RowCount - 1];
        }
        #endregion

        #region Public Methods - Get Controls, DtaaLables, TypedDataLable
        public List<string> GetDataLabelsExceptIDInSpreadsheetOrder()
        {
            // Utilities.PrintMethodName();
            List<string> dataLabels = new List<string>();
            IEnumerable<ControlRow> controlsInSpreadsheetOrder = this.Controls.OrderBy(control => control.SpreadsheetOrder);
            foreach (ControlRow control in controlsInSpreadsheetOrder)
            {
                string dataLabel = control.DataLabel;
                if (String.IsNullOrEmpty(dataLabel))
                {
                    dataLabel = control.DataLabel;
                }
                Debug.Assert(String.IsNullOrWhiteSpace(dataLabel) == false, String.Format("Encountered empty data label and label at ID {0} in template table.", control.ID));

                // get a list of datalabels so we can add columns in the order that matches the current template table order
                if (Constant.DatabaseColumn.ID != dataLabel)
                {
                    dataLabels.Add(dataLabel);
                }
            }
            return dataLabels;
        }

        public Dictionary<string, string> GetTypedDataLabelsExceptIDInSpreadsheetOrder()
        {
            // Utilities.PrintMethodName();
            Dictionary<string, string> typedDataLabels = new Dictionary<string, string>();
            IEnumerable<ControlRow> controlsInSpreadsheetOrder = this.Controls.OrderBy(control => control.SpreadsheetOrder);
            foreach (ControlRow control in controlsInSpreadsheetOrder)
            {
                string dataLabel = control.DataLabel;
                if (String.IsNullOrEmpty(dataLabel))
                {
                    dataLabel = control.Label;
                }
                Debug.Assert(String.IsNullOrWhiteSpace(dataLabel) == false, String.Format("Encountered empty data label and label at ID {0} in template table.", control.ID));

                // get a list of datalabels so we can add columns in the order that matches the current template table order
                if (Constant.DatabaseColumn.ID != dataLabel)
                {
                    typedDataLabels.Add(dataLabel, control.Type);
                }
            }
            return typedDataLabels;
        }
        #endregion

        #region Public Methods - RemoveuserDefinedCongtrol
        public void RemoveUserDefinedControl(ControlRow controlToRemove)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(controlToRemove, nameof(controlToRemove));

            this.CreateBackupIfNeeded();

            string controlType;
            // For backwards compatability: MarkForDeletion DataLabel is of the type DeleteFlag,
            // which is a standard control. So we coerce it into thinking its a different type.
            if (controlToRemove.DataLabel == Constant.ControlsDeprecated.MarkForDeletion)
            {
                controlType = Constant.ControlsDeprecated.MarkForDeletion;
            }
            else
            {
                controlType = controlToRemove.Type;
            }
            if (Constant.Control.StandardTypes.Contains(controlType))
            {
                throw new NotSupportedException(String.Format("Standard control of type {0} cannot be removed.", controlType));
            }

            // capture state
            long removedControlOrder = controlToRemove.ControlOrder;
            long removedSpreadsheetOrder = controlToRemove.SpreadsheetOrder;

            // drop the control from the database and data table
            string where = Constant.DatabaseColumn.ID + " = " + controlToRemove.ID;
            this.Database.DeleteRows(Constant.DBTables.Controls, where);
            this.GetControlsSortedByControlOrder();

            // regenerate counter and spreadsheet orders; if they're greater than the one removed, decrement
            List<ColumnTuplesWithWhere> controlUpdates = new List<ColumnTuplesWithWhere>();
            foreach (ControlRow control in this.Controls)
            {
                long controlOrder = control.ControlOrder;
                long spreadsheetOrder = control.SpreadsheetOrder;

                if (controlOrder > removedControlOrder)
                {
                    List<ColumnTuple> controlUpdate = new List<ColumnTuple>
                    {
                        new ColumnTuple(Constant.Control.ControlOrder, controlOrder - 1)
                    };
                    control.ControlOrder = controlOrder - 1;
                    controlUpdates.Add(new ColumnTuplesWithWhere(controlUpdate, control.ID));
                }

                if (spreadsheetOrder > removedSpreadsheetOrder)
                {
                    List<ColumnTuple> controlUpdate = new List<ColumnTuple>
                    {
                        new ColumnTuple(Constant.Control.SpreadsheetOrder, spreadsheetOrder - 1)
                    };
                    control.SpreadsheetOrder = spreadsheetOrder - 1;
                    controlUpdates.Add(new ColumnTuplesWithWhere(controlUpdate, control.ID));
                }
            }
            this.Database.Update(Constant.DBTables.Controls, controlUpdates);

            // update the in memory table to reflect current database content
            // should not be necessary but this is done to mitigate divergence in case a bug results in the delete lacking perfect fidelity
            this.GetControlsSortedByControlOrder();
        }
        #endregion

        #region Public /Private Methods Sync - ControlToDatabase, TemplateTableCOntrolAndSpreadsheetOrderToDatabase
        public void SyncControlToDatabase(ControlRow control)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(control, nameof(control));

            this.CreateBackupIfNeeded();
            this.Database.Update(Constant.DBTables.Controls, control.CreateColumnTuplesWithWhereByID());

            // it's possible the passed data row isn't attached to TemplateTable, so refresh the table just in case
            this.GetControlsSortedByControlOrder();
        }

        // Update all ControlOrder and SpreadsheetOrder column entries in the template database to match their in-memory counterparts
        public void SyncTemplateTableControlAndSpreadsheetOrderToDatabase()
        {
            // Utilities.PrintMethodName();
            List<ColumnTuplesWithWhere> columnsTuplesWithWhereList = new List<ColumnTuplesWithWhere>();    // holds columns which have changed for the current control
            foreach (ControlRow control in this.Controls)
            {
                // Update each row's Control and Spreadsheet order values
                List<ColumnTuple> columnTupleList = new List<ColumnTuple>();
                ColumnTuplesWithWhere columnTupleWithWhere = new ColumnTuplesWithWhere(columnTupleList, control.ID);
                columnTupleList.Add(new ColumnTuple(Constant.Control.ControlOrder, control.ControlOrder));
                columnTupleList.Add(new ColumnTuple(Constant.Control.SpreadsheetOrder, control.SpreadsheetOrder));
                columnsTuplesWithWhereList.Add(columnTupleWithWhere);
            }
            this.Database.Update(Constant.DBTables.Controls, columnsTuplesWithWhereList);
            // update the in memory table to reflect current database content
            // could just use the new table but this is done in case a bug results in the insert lacking perfect fidelity
            this.GetControlsSortedByControlOrder();
        }

        // Update the entire template database to match the in-memory template
        // Note that this version does this by recreating the entire table: 
        // We could likely be far more efficient by only updateding those entries that differ from the current entries.
        private void SyncTemplateTableToDatabase(DataTableBackedList<ControlRow> newTable)
        {
            // Utilities.PrintMethodName("Called with arguments");
            // clear the existing table in the database 
            this.Database.DeleteRows(Constant.DBTables.Controls, null);

            // Create new rows in the database to match the in-memory verson
            List<List<ColumnTuple>> newTableTuples = new List<List<ColumnTuple>>();
            foreach (ControlRow control in newTable)
            {
                newTableTuples.Add(control.CreateColumnTuplesWithWhereByID().Columns);
            }
            this.Database.Insert(Constant.DBTables.Controls, newTableTuples);

            // update the in memory table to reflect current database content
            // could just use the new table but this is done in case a bug results in the insert lacking perfect fidelity
            this.GetControlsSortedByControlOrder();
        }
        #endregion

        #region Public Methods - Misc: BindToEditorDataGrid, CreateBackupIfNeeded, Update DisplayOrder

        public void BindToEditorDataGrid(DataGrid dataGrid, DataRowChangeEventHandler onRowChanged)
        {
            this.editorDataGrid = dataGrid;
            this.onTemplateTableRowChanged = onRowChanged;
            this.GetControlsSortedByControlOrder();
        }

        protected void CreateBackupIfNeeded()
        {
            if (DateTime.UtcNow - this.mostRecentBackup < Constant.File.BackupInterval)
            {
                // not due for a new backup yet
                return;
            }

            FileBackup.TryCreateBackup(this.FilePath);
            this.mostRecentBackup = DateTime.UtcNow;
        }

        public void UpdateDisplayOrder(string orderColumnName, Dictionary<string, long> newOrderByDataLabel)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(newOrderByDataLabel, nameof(newOrderByDataLabel));

            // Utilities.PrintMethodName();

            // argument validation. Only ControlOrder and SpreadsheetOrder are orderable columns
            if (orderColumnName != Constant.Control.ControlOrder && orderColumnName != Constant.Control.SpreadsheetOrder)
            {
                throw new ArgumentOutOfRangeException(nameof(orderColumnName), String.Format("column '{0}' is not a control order column.  Only '{1}' and '{2}' are order columns.", orderColumnName, Constant.Control.ControlOrder, Constant.Control.SpreadsheetOrder));
            }

            List<long> uniqueOrderValues = newOrderByDataLabel.Values.Distinct().ToList();
            if (uniqueOrderValues.Count != newOrderByDataLabel.Count)
            {
                throw new ArgumentException(String.Format("newOrderByDataLabel: Each control must have a unique value for its order.  {0} duplicate values were passed for '{1}'.", newOrderByDataLabel.Count - uniqueOrderValues.Count, orderColumnName), nameof(newOrderByDataLabel));
            }

            uniqueOrderValues.Sort();
            int uniqueOrderValuesCount = uniqueOrderValues.Count;
            for (int control = 0; control < uniqueOrderValuesCount; ++control)
            {
                int expectedOrder = control + 1;
                if (uniqueOrderValues[control] != expectedOrder)
                {
                    throw new ArgumentOutOfRangeException(nameof(newOrderByDataLabel), String.Format("Control order must be a ones based count.  An order of {0} was passed instead of the expected order {1} for '{2}'.", uniqueOrderValues[0], expectedOrder, orderColumnName));
                }
            }

            long lastItem = this.Controls.Count();

            // update in memory table with new order
            foreach (ControlRow control in this.Controls)
            {
                // Redundant check for null, as for some reason the CA1062 warning was still showing up
                ThrowIf.IsNullArgument(newOrderByDataLabel, nameof(newOrderByDataLabel));

                string dataLabel = control.DataLabel;
                // Because we don't show all controls, we skip the ones that are missing.
                if (newOrderByDataLabel.ContainsKey(dataLabel) == false)
                {
                    control.SpreadsheetOrder = lastItem--;
                    continue;
                }
                long newOrder = newOrderByDataLabel[dataLabel];
                switch (orderColumnName)
                {
                    case Constant.Control.ControlOrder:
                        control.ControlOrder = newOrder;
                        break;
                    case Constant.Control.SpreadsheetOrder:
                        control.SpreadsheetOrder = newOrder;
                        break;
                    default:
                        // Ignore unhandled columns, as these are the ones that are not visible   
                        break;
                }
            }
            // sync new order to database
            this.SyncTemplateTableControlAndSpreadsheetOrderToDatabase();
        }
        #endregion

        #region Public Methods - Get ControlFromTemplate, NextUniqueDataLabel
        /// <summary>Given a data label, get the corresponding data entry control</summary>
        public ControlRow GetControlFromTemplateTable(string dataLabel)
        {
            foreach (ControlRow control in this.Controls)
            {
                if (dataLabel.Equals(control.DataLabel))
                {
                    return control;
                }
            }
            return null;
        }


        public string GetNextUniqueDataLabel(string dataLabelPrefix)
        {
            // get all existing data labels, as we have to ensure that a new data label doesn't have the same name as an existing one
            List<string> dataLabels = new List<string>();
            foreach (ControlRow control in this.Controls)
            {
                dataLabels.Add(control.DataLabel);
            }

            // If the data label name exists, keep incrementing the count that is appended to the end
            // of the field type until it forms a unique data label name
            int dataLabelUniqueIdentifier = 0;
            string nextDataLabel = dataLabelPrefix + dataLabelUniqueIdentifier.ToString();
            while (dataLabels.Contains(nextDataLabel))
            {
                ++dataLabelUniqueIdentifier;
                nextDataLabel = dataLabelPrefix + dataLabelUniqueIdentifier.ToString();
            }

            return nextDataLabel;
        }
        #endregion

        #region Private Methods - Ensure: Current Schema,  DataLabelsAndLabelsNotEmpty
        // Do various checks and corrections to the Template DB to maintain backwards compatability. 
        private void EnsureCurrentSchema()
        {
            // Add a RelativePath control to pre v2.1 databases if one hasn't already been inserted
            long relativePathID = this.GetControlIDFromTemplateTable(Constant.DatabaseColumn.RelativePath);
            if (relativePathID == -1)
            {
                // insert a relative path control, where its ID will be created as the next highest ID
                long order = this.GetOrderForNewControl();
                List<ColumnTuple> relativePathControl = GetRelativePathTuples(order, order, true);
                this.Database.Insert(Constant.DBTables.Controls, new List<List<ColumnTuple>>() { relativePathControl });

                // move the relative path control to ID and order 2 for consistency with newly created templates
                this.SetControlID(Constant.DatabaseColumn.RelativePath, Constant.DatabaseValues.RelativePathPosition);
                this.SetControlOrders(Constant.DatabaseColumn.RelativePath, Constant.DatabaseValues.RelativePathPosition);
            }

            // add DateTime and UtcOffset controls to pre v2.1.0.5 databases if they haven't already been inserted
            long dateTimeID = this.GetControlIDFromTemplateTable(Constant.DatabaseColumn.DateTime);
            if (dateTimeID == -1)
            {
                ControlRow date = this.GetControlFromTemplateTable(Constant.DatabaseColumn.Date);
                ControlRow time = this.GetControlFromTemplateTable(Constant.DatabaseColumn.Time);

                // insert a date time control, where its ID will be created as the next highest ID
                // if either the date or time was visible make the date time visible
                bool dateTimeVisible = date.Visible || time.Visible;
                long order = this.GetOrderForNewControl();
                List<ColumnTuple> dateTimeControl = GetDateTimeTuples(order, order, dateTimeVisible);
                this.Database.Insert(Constant.DBTables.Controls, new List<List<ColumnTuple>>() { dateTimeControl });

                // make date and time controls invisible as they're replaced by the date time control
                if (date.Visible)
                {
                    date.Visible = false;
                    this.SyncControlToDatabase(date);
                }
                if (time.Visible)
                {
                    time.Visible = false;
                    this.SyncControlToDatabase(time);
                }

                // move the date time control to ID and order 2 for consistency with newly created templates
                this.SetControlID(Constant.DatabaseColumn.DateTime, Constant.DatabaseValues.DateTimePosition);
                this.SetControlOrders(Constant.DatabaseColumn.DateTime, Constant.DatabaseValues.DateTimePosition);
            }

            long utcOffsetID = this.GetControlIDFromTemplateTable(Constant.DatabaseColumn.UtcOffset);
            if (utcOffsetID == -1)
            {
                // insert a relative path control, where its ID will be created as the next highest ID
                long order = this.GetOrderForNewControl();
                List<ColumnTuple> utcOffsetControl = GetUtcOffsetTuples(order, order, false);
                this.Database.Insert(Constant.DBTables.Controls, new List<List<ColumnTuple>>() { utcOffsetControl });

                // move the relative path control to ID and order 2 for consistency with newly created templates
                this.SetControlID(Constant.DatabaseColumn.UtcOffset, Constant.DatabaseValues.UtcOffsetPosition);
                this.SetControlOrders(Constant.DatabaseColumn.UtcOffset, Constant.DatabaseValues.UtcOffsetPosition);
            }

            // Bug fix: 
            // Check to ensure that the image quality choice list in the templae match the expected default value,
            // A previously introduced bug had added spaces before several items in the list. This fixes that.
            // Note that this updates the template table in both the .tdb and .ddb file
            ControlRow imageQualityControlRow = this.GetControlFromTemplateTable(Constant.DatabaseColumn.ImageQuality);
            if (imageQualityControlRow != null && imageQualityControlRow.List != Constant.ImageQuality.ListOfValues)
            {
                imageQualityControlRow.List = Constant.ImageQuality.ListOfValues;
                this.SyncControlToDatabase(imageQualityControlRow);
            }

            // Backwards compatability: ensure a DeleteFlag control exists, replacing the MarkForDeletion data label used in pre 2.1.0.4 templates if necessary
            ControlRow markForDeletion = this.GetControlFromTemplateTable(Constant.ControlsDeprecated.MarkForDeletion);
            if (markForDeletion != null)
            {
                List<ColumnTuple> deleteFlagControl = GetDeleteFlagTuples(markForDeletion.ControlOrder, markForDeletion.SpreadsheetOrder, markForDeletion.Visible);
                this.Database.Update(Constant.DBTables.Controls, new ColumnTuplesWithWhere(deleteFlagControl, markForDeletion.ID));
                this.GetControlsSortedByControlOrder();
            }
            else if (this.GetControlIDFromTemplateTable(Constant.DatabaseColumn.DeleteFlag) < 0)
            {
                // insert a DeleteFlag control, where its ID will be created as the next highest ID
                long order = this.GetOrderForNewControl();
                List<ColumnTuple> deleteFlagControl = GetDeleteFlagTuples(order, order, true);
                this.Database.Insert(Constant.DBTables.Controls, new List<List<ColumnTuple>>() { deleteFlagControl });
                this.GetControlsSortedByControlOrder();
            }
        }

        /// <summary>
        /// Supply default values for any empty labels or data labels are non-empty, updating both TemplateTable and the database as needed
        /// </summary>
        private void EnsureDataLabelsAndLabelsNotEmpty()
        {
            // All the code below goes through the template table to see if there are any non-empty labels / data labels,
            // and if so, updates them to a reasonable value. If both are empty, it keeps track of its type and creates
            // a label called (say) Counter3 for the third counter that has no label. If there is no DataLabel value, it
            // makes it the same as the label. Ultimately, it guarantees that there will always be a (hopefully unique)
            // data label and label name. 
            // As well, the contents of the template table are loaded into memory.
            try
            {
                foreach (ControlRow control in this.Controls)
                {
                    // Check if various values are empty, and if so update the row and fill the dataline with appropriate defaults
                    ColumnTuplesWithWhere columnsToUpdate = new ColumnTuplesWithWhere();    // holds columns which have changed for the current control
                    bool noDataLabel = String.IsNullOrWhiteSpace(control.DataLabel);
                    if (noDataLabel && String.IsNullOrWhiteSpace(control.Label))
                    {
                        string dataLabel = this.GetNextUniqueDataLabel(control.Type);
                        columnsToUpdate.Columns.Add(new ColumnTuple(Constant.Control.Label, dataLabel));
                        columnsToUpdate.Columns.Add(new ColumnTuple(Constant.Control.DataLabel, dataLabel));
                        control.Label = dataLabel;
                        control.DataLabel = dataLabel;
                    }
                    else if (noDataLabel)
                    {
                        // No data label but a label, so use the label's value as the data label
                        columnsToUpdate.Columns.Add(new ColumnTuple(Constant.Control.DataLabel, control.Label));
                        control.DataLabel = control.Label;
                    }

                    // Now add the new values to the database
                    if (columnsToUpdate.Columns.Count > 0)
                    {
                        columnsToUpdate.SetWhere(control.ID);
                        this.Database.Update(Constant.DBTables.Controls, columnsToUpdate);
                    }
                }
            }
            catch
            {
                // Throw a custom exception so we can give a more informative fatal error message.
                // While this method does not normally fail, one user did report it crashing here due to his Citrix system
                // limiting how the template file is manipulated. The actual failure happens before this, but this
                // is where it is caught.
                Exception custom_e = new Exception(Constant.ExceptionTypes.TemplateReadWriteException, null);
                throw custom_e;
            }
        }
        #endregion

        #region Private Methods - Set ControlOrder, ControlID
        /// <summary>
        /// Set the order of the specified control to the specified value, shifting other controls' orders as needed.
        /// </summary>
        private void SetControlOrders(string dataLabel, int order)
        {
            if ((order < 1) || (order > this.Controls.RowCount))
            {
                throw new ArgumentOutOfRangeException(nameof(order), "Control and spreadsheet orders must be contiguous ones based values.");
            }

            Dictionary<string, long> newControlOrderByDataLabel = new Dictionary<string, long>();
            Dictionary<string, long> newSpreadsheetOrderByDataLabel = new Dictionary<string, long>();
            foreach (ControlRow control in this.Controls)
            {
                if (control.DataLabel == dataLabel)
                {
                    newControlOrderByDataLabel.Add(dataLabel, order);
                    newSpreadsheetOrderByDataLabel.Add(dataLabel, order);
                }
                else
                {
                    long currentControlOrder = control.ControlOrder;
                    if (currentControlOrder >= order)
                    {
                        ++currentControlOrder;
                    }
                    newControlOrderByDataLabel.Add(control.DataLabel, currentControlOrder);

                    long currentSpreadsheetOrder = control.SpreadsheetOrder;
                    if (currentSpreadsheetOrder >= order)
                    {
                        ++currentSpreadsheetOrder;
                    }
                    newSpreadsheetOrderByDataLabel.Add(control.DataLabel, currentSpreadsheetOrder);
                }
            }

            this.UpdateDisplayOrder(Constant.Control.ControlOrder, newControlOrderByDataLabel);
            this.UpdateDisplayOrder(Constant.Control.SpreadsheetOrder, newSpreadsheetOrderByDataLabel);
            this.GetControlsSortedByControlOrder();
        }

        /// <summary>
        /// Set the ID of the specified control to the specified value, shifting other controls' IDs as needed.
        /// </summary>
        private void SetControlID(string dataLabel, int newID)
        {
            // Utilities.PrintMethodName();
            // nothing to do
            long currentID = this.GetControlIDFromTemplateTable(dataLabel);
            if (currentID == newID)
            {
                return;
            }

            // move other controls out of the way if the requested ID is in use
            ControlRow conflictingControl = this.Controls.Find(newID);
            List<string> queries = new List<string>();
            if (conflictingControl != null)
            {
                // First update: because any changed IDs have to be unique, first move them beyond the current ID range
                long maximumID = 0;
                foreach (ControlRow control in this.Controls)
                {
                    if (maximumID < control.ID)
                    {
                        maximumID = control.ID;
                    }
                }
                Debug.Assert((maximumID > 0) && (maximumID <= Int64.MaxValue), String.Format("Maximum ID found is {0}, which is out of range.", maximumID));
                string jumpAmount = maximumID.ToString();

                string increaseIDs = Sql.Update + Constant.DBTables.Controls;
                increaseIDs += Sql.Set + Constant.DatabaseColumn.ID + " = " + Constant.DatabaseColumn.ID + " + 1 + " + jumpAmount;
                increaseIDs += Sql.Where + Constant.DatabaseColumn.ID + " >= " + newID;
                queries.Add(increaseIDs);

                // Second update: decrease IDs above newID to be one more than their original value
                // This leaves everything in sequence except for an open spot at newID.
                string reduceIDs = Sql.Update + Constant.DBTables.Controls;
                reduceIDs += Sql.Set + Constant.DatabaseColumn.ID + " = " + Constant.DatabaseColumn.ID + " - " + jumpAmount;
                reduceIDs += Sql.Where + Constant.DatabaseColumn.ID + " >= " + newID;
                queries.Add(reduceIDs);
            }

            // 3rd update: change the target ID to the desired ID
            this.CreateBackupIfNeeded();

            string setControlID = Sql.Update + Constant.DBTables.Controls;
            setControlID += Sql.Set + Constant.DatabaseColumn.ID + " = " + newID;
            setControlID += Sql.Where + Constant.Control.DataLabel + " = '" + dataLabel + "'";
            queries.Add(setControlID);
            this.Database.ExecuteNonQueryWrappedInBeginEnd(queries);

            this.GetControlsSortedByControlOrder();
        }
        #endregion

        #region Private Methods - Get OrderForNewControl, ControlID, DatTime RelativePath, DeleteFlag, UTCOffset, Controls in sorted order
        private long GetOrderForNewControl()
        {
            return this.Controls.RowCount + 1;
        }

        /// <summary>Given a data label, get the id of the corresponding data entry control</summary>
        protected long GetControlIDFromTemplateTable(string dataLabel)
        {
            ControlRow control = this.GetControlFromTemplateTable(dataLabel);
            if (control == null)
            {
                return -1;
            }
            return control.ID;
        }

        private static List<ColumnTuple> GetDateTimeTuples(long controlOrder, long spreadsheetOrder, bool visible)
        {
            List<ColumnTuple> dateTime = new List<ColumnTuple>
            {
                new ColumnTuple(Constant.Control.ControlOrder, controlOrder),
                new ColumnTuple(Constant.Control.SpreadsheetOrder, spreadsheetOrder),
                new ColumnTuple(Constant.Control.Type, Constant.DatabaseColumn.DateTime),
                new ColumnTuple(Constant.Control.DefaultValue, Constant.ControlDefault.DateTimeValue.UtcDateTime),
                new ColumnTuple(Constant.Control.Label, Constant.DatabaseColumn.DateTime),
                new ColumnTuple(Constant.Control.DataLabel, Constant.DatabaseColumn.DateTime),
                new ColumnTuple(Constant.Control.Tooltip, Constant.ControlDefault.DateTimeTooltip),
                new ColumnTuple(Constant.Control.TextBoxWidth, Constant.ControlDefault.DateTimeWidth),
                new ColumnTuple(Constant.Control.Copyable, false),
                new ColumnTuple(Constant.Control.Visible, visible),
                new ColumnTuple(Constant.Control.List, Constant.ControlDefault.Value)
            };
            return dateTime;
        }

        // Defines a RelativePath control. The definition is used by its caller to insert a RelativePath control into the template for backwards compatability. 
        private static List<ColumnTuple> GetRelativePathTuples(long controlOrder, long spreadsheetOrder, bool visible)
        {
            List<ColumnTuple> relativePath = new List<ColumnTuple>
            {
                new ColumnTuple(Constant.Control.ControlOrder, controlOrder),
                new ColumnTuple(Constant.Control.SpreadsheetOrder, spreadsheetOrder),
                new ColumnTuple(Constant.Control.Type, Constant.DatabaseColumn.RelativePath),
                new ColumnTuple(Constant.Control.DefaultValue, Constant.ControlDefault.Value),
                new ColumnTuple(Constant.Control.Label, Constant.DatabaseColumn.RelativePath),
                new ColumnTuple(Constant.Control.DataLabel, Constant.DatabaseColumn.RelativePath),
                new ColumnTuple(Constant.Control.Tooltip, Constant.ControlDefault.RelativePathTooltip),
                new ColumnTuple(Constant.Control.TextBoxWidth, Constant.ControlDefault.RelativePathWidth),
                new ColumnTuple(Constant.Control.Copyable, false),
                new ColumnTuple(Constant.Control.Visible, visible),
                new ColumnTuple(Constant.Control.List, Constant.ControlDefault.Value)
            };
            return relativePath;
        }

        // Defines a DeleteFlag control. The definition is used by its caller to insert a DeleteFlag control into the template for backwards compatability. 
        private static List<ColumnTuple> GetDeleteFlagTuples(long controlOrder, long spreadsheetOrder, bool visible)
        {
            List<ColumnTuple> deleteFlag = new List<ColumnTuple>
            {
                new ColumnTuple(Constant.Control.ControlOrder, controlOrder),
                new ColumnTuple(Constant.Control.SpreadsheetOrder, spreadsheetOrder),
                new ColumnTuple(Constant.Control.Type, Constant.DatabaseColumn.DeleteFlag),
                new ColumnTuple(Constant.Control.DefaultValue, Constant.ControlDefault.FlagValue),
                new ColumnTuple(Constant.Control.Label, Constant.ControlDefault.DeleteFlagLabel),
                new ColumnTuple(Constant.Control.DataLabel, Constant.DatabaseColumn.DeleteFlag),
                new ColumnTuple(Constant.Control.Tooltip, Constant.ControlDefault.DeleteFlagTooltip),
                new ColumnTuple(Constant.Control.TextBoxWidth, Constant.ControlDefault.FlagWidth),
                new ColumnTuple(Constant.Control.Copyable, false),
                new ColumnTuple(Constant.Control.Visible, visible),
                new ColumnTuple(Constant.Control.List, Constant.ControlDefault.Value)
            };
            return deleteFlag;
        }

        private static List<ColumnTuple> GetUtcOffsetTuples(long controlOrder, long spreadsheetOrder, bool visible)
        {
            List<ColumnTuple> utcOffset = new List<ColumnTuple>
            {
                new ColumnTuple(Constant.Control.ControlOrder, controlOrder),
                new ColumnTuple(Constant.Control.SpreadsheetOrder, spreadsheetOrder),
                new ColumnTuple(Constant.Control.Type, Constant.DatabaseColumn.UtcOffset),
                new ColumnTuple(Constant.Control.DefaultValue, Constant.ControlDefault.DateTimeValue.Offset),
                new ColumnTuple(Constant.Control.Label, Constant.DatabaseColumn.UtcOffset),
                new ColumnTuple(Constant.Control.DataLabel, Constant.DatabaseColumn.UtcOffset),
                new ColumnTuple(Constant.Control.Tooltip, Constant.ControlDefault.UtcOffsetTooltip),
                new ColumnTuple(Constant.Control.TextBoxWidth, Constant.ControlDefault.UtcOffsetWidth),
                new ColumnTuple(Constant.Control.Copyable, false),
                new ColumnTuple(Constant.Control.Visible, visible),
                new ColumnTuple(Constant.Control.List, Constant.ControlDefault.Value)
            };
            return utcOffset;
        }

        private void GetControlsSortedByControlOrder()
        {
            // Utilities.PrintMethodName();
            DataTable templateTable = this.Database.GetDataTableFromSelect(Sql.SelectStarFrom + Constant.DBTables.Controls + Sql.OrderBy + Constant.Control.ControlOrder);
            this.Controls = new DataTableBackedList<ControlRow>(templateTable, (DataRow row) => { return new ControlRow(row); });
            this.Controls.BindDataGrid(this.editorDataGrid, this.onTemplateTableRowChanged);
        }
        #endregion

        #region Disposing
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }

            if (disposing)
            {
                if (this.Controls != null)
                {
                    this.Controls.Dispose();
                }
            }

            this.disposed = true;
        }
        #endregion
    }
}
