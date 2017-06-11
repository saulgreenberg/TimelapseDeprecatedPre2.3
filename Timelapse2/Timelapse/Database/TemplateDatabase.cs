using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Controls;
using Timelapse.Util;

namespace Timelapse.Database
{
    /// <summary>
    /// Timelapse Template Database.
    /// </summary>
    public class TemplateDatabase : IDisposable
    {
        private bool disposed;
        private DataGrid editorDataGrid;
        private DateTime mostRecentBackup;
        private DataRowChangeEventHandler onTemplateTableRowChanged;

        public DataTableBackedList<ControlRow> Controls { get; private set; }

        protected SQLiteWrapper Database { get; set; }

        /// <summary>Gets the file name of the image database on disk.</summary>
        public string FilePath { get; private set; }

        protected TemplateDatabase(string filePath)
        {
            this.disposed = false;
            this.mostRecentBackup = FileBackup.GetMostRecentBackup(filePath);

            // open or create database
            this.Database = new SQLiteWrapper(filePath);
            this.FilePath = filePath;
        }

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

        public static TemplateDatabase CreateOrOpen(string filePath)
        {
            // check for an existing database before instantiating the databse as SQL wrapper instantiation creates the database file
            bool populateDatabase = !File.Exists(filePath);

            TemplateDatabase templateDatabase = new TemplateDatabase(filePath);
            if (populateDatabase)
            {
                // initialize the database if it's newly created
                templateDatabase.OnDatabaseCreated(null);
            }
            else
            {
                // if it's an existing database check if it needs updating to current structure and load data tables
                templateDatabase.OnExistingDatabaseOpened(null, null);
            }
            return templateDatabase;
        }

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
            List<List<ColumnTuple>> controlInsertWrapper = new List<List<ColumnTuple>>() { newControl.GetColumnTuples().Columns };
            this.Database.Insert(Constant.DatabaseTable.Controls, controlInsertWrapper);

            // update the in memory table to reflect current database content
            // could just add the new row to the table but this is done in case a bug results in the insert lacking perfect fidelity
            this.GetControlsSortedByControlOrder();
            return this.Controls[this.Controls.RowCount - 1];
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void GetControlsSortedByControlOrder()
        {
            DataTable templateTable = this.Database.GetDataTableFromSelect(Constant.Sql.SelectStarFrom + Constant.DatabaseTable.Controls + Constant.Sql.OrderBy + Constant.Control.ControlOrder);
            this.Controls = new DataTableBackedList<ControlRow>(templateTable, (DataRow row) => { return new ControlRow(row); });
            this.Controls.BindDataGrid(this.editorDataGrid, this.onTemplateTableRowChanged);
        }

        public List<string> GetDataLabelsExceptIDInSpreadsheetOrder()
        {
            List<string> dataLabels = new List<string>();
            IEnumerable<ControlRow> controlsInSpreadsheetOrder = this.Controls.OrderBy(control => control.SpreadsheetOrder);
            foreach (ControlRow control in controlsInSpreadsheetOrder)
            {
                string dataLabel = control.DataLabel;
                if (dataLabel == String.Empty)
                {
                    dataLabel = control.Label;
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
            Dictionary<string, string> typedDataLabels = new Dictionary<string, string>();
            IEnumerable<ControlRow> controlsInSpreadsheetOrder = this.Controls.OrderBy(control => control.SpreadsheetOrder);
            foreach (ControlRow control in controlsInSpreadsheetOrder)
            {
                string dataLabel = control.DataLabel;
                if (dataLabel == String.Empty)
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

        public bool IsControlCopyable(string dataLabel)
        {
            long id = this.GetControlIDFromTemplateTable(dataLabel);
            ControlRow control = this.Controls.Find(id);
            return control.Copyable;
        }

        public void RemoveUserDefinedControl(ControlRow controlToRemove)
        {
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
            this.Database.DeleteRows(Constant.DatabaseTable.Controls, where);
            this.GetControlsSortedByControlOrder();

            // regenerate counter and spreadsheet orders; if they're greater than the one removed, decrement
            List<ColumnTuplesWithWhere> controlUpdates = new List<ColumnTuplesWithWhere>();
            foreach (ControlRow control in this.Controls)
            {
                long controlOrder = control.ControlOrder;
                long spreadsheetOrder = control.SpreadsheetOrder;

                if (controlOrder > removedControlOrder)
                {
                    List<ColumnTuple> controlUpdate = new List<ColumnTuple>();
                    controlUpdate.Add(new ColumnTuple(Constant.Control.ControlOrder, controlOrder - 1));
                    control.ControlOrder = controlOrder - 1;
                    controlUpdates.Add(new ColumnTuplesWithWhere(controlUpdate, control.ID));
                }

                if (spreadsheetOrder > removedSpreadsheetOrder)
                {
                    List<ColumnTuple> controlUpdate = new List<ColumnTuple>();
                    controlUpdate.Add(new ColumnTuple(Constant.Control.SpreadsheetOrder, spreadsheetOrder - 1));
                    control.SpreadsheetOrder = spreadsheetOrder - 1;
                    controlUpdates.Add(new ColumnTuplesWithWhere(controlUpdate, control.ID));
                }
            }
            this.Database.Update(Constant.DatabaseTable.Controls, controlUpdates);

            // update the in memory table to reflect current database content
            // should not be necessary but this is done to mitigate divergence in case a bug results in the delete lacking perfect fidelity
            this.GetControlsSortedByControlOrder();
        }

        public void SyncControlToDatabase(ControlRow control)
        {
            this.CreateBackupIfNeeded();
            this.Database.Update(Constant.DatabaseTable.Controls, control.GetColumnTuples());

            // it's possible the passed data row isn't attached to TemplateTable, so refresh the table just in case
            this.GetControlsSortedByControlOrder();
        }

        private void SyncTemplateTableToDatabase()
        {
            this.CreateBackupIfNeeded();
            this.SyncTemplateTableToDatabase(this.Controls);
        }

        private void SyncTemplateTableToDatabase(DataTableBackedList<ControlRow> newTable)
        {
            // clear the existing table in the database and add the new values
            this.Database.DeleteRows(Constant.DatabaseTable.Controls, null);

            List<List<ColumnTuple>> newTableTuples = new List<List<ColumnTuple>>();
            foreach (ControlRow control in newTable)
            {
                newTableTuples.Add(control.GetColumnTuples().Columns);
            }
            this.Database.Insert(Constant.DatabaseTable.Controls, newTableTuples);

            // update the in memory table to reflect current database content
            // could just use the new table but this is done in case a bug results in the insert lacking perfect fidelity
            this.GetControlsSortedByControlOrder();
        }

        public static bool TryCreateOrOpen(string filePath, out TemplateDatabase database)
        {
            try
            {
                database = TemplateDatabase.CreateOrOpen(filePath);
                return true;
            }
            catch (Exception exception)
            {
                Utilities.PrintFailure(String.Format("Failure in TryCreateOpen. {0}", exception.ToString()));
                database = null;
                return false;
            }
        }

        public void UpdateDisplayOrder(string orderColumnName, Dictionary<string, long> newOrderByDataLabel)
        {
            // argument validation
            if (orderColumnName != Constant.Control.ControlOrder && orderColumnName != Constant.Control.SpreadsheetOrder)
            {
                throw new ArgumentOutOfRangeException("column", String.Format("'{0}' is not a control order column.  Only '{1}' and '{2}' are order columns.", orderColumnName, Constant.Control.ControlOrder, Constant.Control.SpreadsheetOrder));
            }

            // Commented out, as we now don't have some controls showing up due to optional Utc 
            // Saul TODO: Replace by a better test?
            //            if (newOrderByDataLabel.Count != this.Controls.RowCount)
            //            {
            //                throw new NotSupportedException(String.Format("Partial order updates are not supported.  New ordering for {0} controls was passed but {1} controls are present for '{2}'.", newOrderByDataLabel.Count, this.Controls.RowCount, orderColumnName));
            //            }

            List<long> uniqueOrderValues = newOrderByDataLabel.Values.Distinct().ToList();
            if (uniqueOrderValues.Count != newOrderByDataLabel.Count)
            {
                throw new ArgumentException("newOrderByDataLabel", String.Format("Each control must have a unique value for its order.  {0} duplicate values were passed for '{1}'.", newOrderByDataLabel.Count - uniqueOrderValues.Count, orderColumnName));
            }

            uniqueOrderValues.Sort();
            for (int control = 0; control < uniqueOrderValues.Count; ++control)
            {
                int expectedOrder = control + 1;
                if (uniqueOrderValues[control] != expectedOrder)
                {
                    throw new ArgumentOutOfRangeException("newOrderByDataLabel", String.Format("Control order must be a ones based count.  An order of {0} was passed instead of the expected order {1} for '{2}'.", uniqueOrderValues[0], expectedOrder, orderColumnName));
                }
            }

            // update in memory table with new order
            foreach (ControlRow control in this.Controls)
            {
                string dataLabel = control.DataLabel;

                // Because we don't show all controls, we skip the ones that are missing.
                if (newOrderByDataLabel.ContainsKey(dataLabel) == false)
                {
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
            this.SyncTemplateTableToDatabase();
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

        protected virtual void OnDatabaseCreated(TemplateDatabase other)
        {
            // create the template table
            List<ColumnDefinition> templateTableColumns = new List<ColumnDefinition>();
            templateTableColumns.Add(new ColumnDefinition(Constant.DatabaseColumn.ID, Constant.Sql.CreationStringPrimaryKey));
            templateTableColumns.Add(new ColumnDefinition(Constant.Control.ControlOrder, Constant.Sql.Integer));
            templateTableColumns.Add(new ColumnDefinition(Constant.Control.SpreadsheetOrder, Constant.Sql.Integer));
            templateTableColumns.Add(new ColumnDefinition(Constant.Control.Type, Constant.Sql.Text));
            templateTableColumns.Add(new ColumnDefinition(Constant.Control.DefaultValue, Constant.Sql.Text));
            templateTableColumns.Add(new ColumnDefinition(Constant.Control.Label, Constant.Sql.Text));
            templateTableColumns.Add(new ColumnDefinition(Constant.Control.DataLabel, Constant.Sql.Text));
            templateTableColumns.Add(new ColumnDefinition(Constant.Control.Tooltip, Constant.Sql.Text));
            templateTableColumns.Add(new ColumnDefinition(Constant.Control.TextBoxWidth, Constant.Sql.Text));
            templateTableColumns.Add(new ColumnDefinition(Constant.Control.Copyable, Constant.Sql.Text));
            templateTableColumns.Add(new ColumnDefinition(Constant.Control.Visible, Constant.Sql.Text));
            templateTableColumns.Add(new ColumnDefinition(Constant.Control.List, Constant.Sql.Text));
            this.Database.CreateTable(Constant.DatabaseTable.Controls, templateTableColumns);

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
            List<ColumnTuple> file = new List<ColumnTuple>();
            file.Add(new ColumnTuple(Constant.Control.ControlOrder, ++controlOrder));
            file.Add(new ColumnTuple(Constant.Control.SpreadsheetOrder, ++spreadsheetOrder));
            file.Add(new ColumnTuple(Constant.Control.Type, Constant.DatabaseColumn.File));
            file.Add(new ColumnTuple(Constant.Control.DefaultValue, Constant.ControlDefault.Value));
            file.Add(new ColumnTuple(Constant.Control.Label, Constant.DatabaseColumn.File));
            file.Add(new ColumnTuple(Constant.Control.DataLabel, Constant.DatabaseColumn.File));
            file.Add(new ColumnTuple(Constant.Control.Tooltip, Constant.ControlDefault.FileTooltip));
            file.Add(new ColumnTuple(Constant.Control.TextBoxWidth, Constant.ControlDefault.FileWidth));
            file.Add(new ColumnTuple(Constant.Control.Copyable, false));
            file.Add(new ColumnTuple(Constant.Control.Visible, true));
            file.Add(new ColumnTuple(Constant.Control.List, Constant.ControlDefault.Value));
            standardControls.Add(file);

            // relative path
            standardControls.Add(this.GetRelativePathTuples(++controlOrder, ++spreadsheetOrder, true));

            // folder
            List<ColumnTuple> folder = new List<ColumnTuple>();
            folder.Add(new ColumnTuple(Constant.Control.ControlOrder, ++controlOrder));
            folder.Add(new ColumnTuple(Constant.Control.SpreadsheetOrder, ++spreadsheetOrder));
            folder.Add(new ColumnTuple(Constant.Control.Type, Constant.DatabaseColumn.Folder));
            folder.Add(new ColumnTuple(Constant.Control.DefaultValue, Constant.ControlDefault.Value));
            folder.Add(new ColumnTuple(Constant.Control.Label, Constant.DatabaseColumn.Folder));
            folder.Add(new ColumnTuple(Constant.Control.DataLabel, Constant.DatabaseColumn.Folder));
            folder.Add(new ColumnTuple(Constant.Control.Tooltip, Constant.ControlDefault.FolderTooltip));
            folder.Add(new ColumnTuple(Constant.Control.TextBoxWidth, Constant.ControlDefault.FolderWidth));
            folder.Add(new ColumnTuple(Constant.Control.Copyable, false));
            folder.Add(new ColumnTuple(Constant.Control.Visible, true));
            folder.Add(new ColumnTuple(Constant.Control.List, Constant.ControlDefault.Value));
            standardControls.Add(folder);

            // datetime
            standardControls.Add(this.GetDateTimeTuples(++controlOrder, ++spreadsheetOrder, true));

            // utcOffset
            standardControls.Add(this.GetUtcOffsetTuples(++controlOrder, ++spreadsheetOrder, false));

            // date
            List<ColumnTuple> date = new List<ColumnTuple>();
            date.Add(new ColumnTuple(Constant.Control.ControlOrder, ++controlOrder));
            date.Add(new ColumnTuple(Constant.Control.SpreadsheetOrder, ++spreadsheetOrder));
            date.Add(new ColumnTuple(Constant.Control.Type, Constant.DatabaseColumn.Date));
            date.Add(new ColumnTuple(Constant.Control.DefaultValue, DateTimeHandler.ToDisplayDateString(Constant.ControlDefault.DateTimeValue)));
            date.Add(new ColumnTuple(Constant.Control.Label, Constant.DatabaseColumn.Date));
            date.Add(new ColumnTuple(Constant.Control.DataLabel, Constant.DatabaseColumn.Date));
            date.Add(new ColumnTuple(Constant.Control.Tooltip, Constant.ControlDefault.DateTooltip));
            date.Add(new ColumnTuple(Constant.Control.TextBoxWidth, Constant.ControlDefault.DateWidth));
            date.Add(new ColumnTuple(Constant.Control.Copyable, false));
            date.Add(new ColumnTuple(Constant.Control.Visible, false));
            date.Add(new ColumnTuple(Constant.Control.List, Constant.ControlDefault.Value));
            standardControls.Add(date);

            // time
            List<ColumnTuple> time = new List<ColumnTuple>();
            time.Add(new ColumnTuple(Constant.Control.ControlOrder, ++controlOrder));
            time.Add(new ColumnTuple(Constant.Control.SpreadsheetOrder, ++spreadsheetOrder));
            time.Add(new ColumnTuple(Constant.Control.Type, Constant.DatabaseColumn.Time));
            time.Add(new ColumnTuple(Constant.Control.DefaultValue, DateTimeHandler.ToDisplayTimeString(Constant.ControlDefault.DateTimeValue)));
            time.Add(new ColumnTuple(Constant.Control.Label, Constant.DatabaseColumn.Time));
            time.Add(new ColumnTuple(Constant.Control.DataLabel, Constant.DatabaseColumn.Time));
            time.Add(new ColumnTuple(Constant.Control.Tooltip, Constant.ControlDefault.TimeTooltip));
            time.Add(new ColumnTuple(Constant.Control.TextBoxWidth, Constant.ControlDefault.TimeWidth));
            time.Add(new ColumnTuple(Constant.Control.Copyable, false));
            time.Add(new ColumnTuple(Constant.Control.Visible, false));
            time.Add(new ColumnTuple(Constant.Control.List, Constant.ControlDefault.Value));
            standardControls.Add(time);

            // image quality
            List<ColumnTuple> imageQuality = new List<ColumnTuple>();
            imageQuality.Add(new ColumnTuple(Constant.Control.ControlOrder, ++controlOrder));
            imageQuality.Add(new ColumnTuple(Constant.Control.SpreadsheetOrder, ++spreadsheetOrder));
            imageQuality.Add(new ColumnTuple(Constant.Control.Type, Constant.DatabaseColumn.ImageQuality));
            imageQuality.Add(new ColumnTuple(Constant.Control.DefaultValue, Constant.ControlDefault.Value));
            imageQuality.Add(new ColumnTuple(Constant.Control.Label, Constant.DatabaseColumn.ImageQuality));
            imageQuality.Add(new ColumnTuple(Constant.Control.DataLabel, Constant.DatabaseColumn.ImageQuality));
            imageQuality.Add(new ColumnTuple(Constant.Control.Tooltip, Constant.ControlDefault.ImageQualityTooltip));
            imageQuality.Add(new ColumnTuple(Constant.Control.TextBoxWidth, Constant.ControlDefault.ImageQualityWidth));
            imageQuality.Add(new ColumnTuple(Constant.Control.Copyable, false));
            imageQuality.Add(new ColumnTuple(Constant.Control.Visible, true));
            imageQuality.Add(new ColumnTuple(Constant.Control.List, Constant.ImageQuality.ListOfValues));
            standardControls.Add(imageQuality);

            // delete flag
            standardControls.Add(this.GetDeleteFlagTuples(++controlOrder, ++spreadsheetOrder, true));

            // insert standard controls into the template table
            this.Database.Insert(Constant.DatabaseTable.Controls, standardControls);

            // populate the in memory version of the template table
            this.GetControlsSortedByControlOrder();
        }

        protected virtual void UpgradeDatabasesAndCompareTemplates(TemplateDatabase other, TemplateSyncResults filetableDifferences)
        {
            this.GetControlsSortedByControlOrder();
            this.EnsureDataLabelsAndLabelsNotEmpty();
            this.EnsureCurrentSchema();
        }

        protected virtual void OnExistingDatabaseOpened(TemplateDatabase other, TemplateSyncResults filetableDifferences)
        {
            this.GetControlsSortedByControlOrder();
            this.EnsureDataLabelsAndLabelsNotEmpty();
            this.EnsureCurrentSchema();
        }

        // Do various checks and corrections to the Template DB to maintain backwards compatability. 
        private void EnsureCurrentSchema()
        {
            // Add a RelativePath control to pre v2.1 databases if one hasn't already been inserted
            long relativePathID = this.GetControlIDFromTemplateTable(Constant.DatabaseColumn.RelativePath);
            if (relativePathID == -1)
            {
                // insert a relative path control, where its ID will be created as the next highest ID
                long order = this.GetOrderForNewControl();
                List<ColumnTuple> relativePathControl = this.GetRelativePathTuples(order, order, true);
                this.Database.Insert(Constant.DatabaseTable.Controls, new List<List<ColumnTuple>>() { relativePathControl });

                // move the relative path control to ID and order 2 for consistency with newly created templates
                this.SetControlID(Constant.DatabaseColumn.RelativePath, Constant.Database.RelativePathPosition);
                this.SetControlOrders(Constant.DatabaseColumn.RelativePath, Constant.Database.RelativePathPosition);
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
                List<ColumnTuple> dateTimeControl = this.GetDateTimeTuples(order, order, dateTimeVisible);
                this.Database.Insert(Constant.DatabaseTable.Controls, new List<List<ColumnTuple>>() { dateTimeControl });

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
                this.SetControlID(Constant.DatabaseColumn.DateTime, Constant.Database.DateTimePosition);
                this.SetControlOrders(Constant.DatabaseColumn.DateTime, Constant.Database.DateTimePosition);
            }

            long utcOffsetID = this.GetControlIDFromTemplateTable(Constant.DatabaseColumn.UtcOffset);
            if (utcOffsetID == -1)
            {
                // insert a relative path control, where its ID will be created as the next highest ID
                long order = this.GetOrderForNewControl();
                List<ColumnTuple> utcOffsetControl = this.GetUtcOffsetTuples(order, order, false);
                this.Database.Insert(Constant.DatabaseTable.Controls, new List<List<ColumnTuple>>() { utcOffsetControl });

                // move the relative path control to ID and order 2 for consistency with newly created templates
                this.SetControlID(Constant.DatabaseColumn.UtcOffset, Constant.Database.UtcOffsetPosition);
                this.SetControlOrders(Constant.DatabaseColumn.UtcOffset, Constant.Database.UtcOffsetPosition);
            }

            // Backwards compatability: ensure a DeleteFlag control exists, replacing the MarkForDeletion data label used in pre 2.1.0.4 templates if necessary
            ControlRow markForDeletion = this.GetControlFromTemplateTable(Constant.ControlsDeprecated.MarkForDeletion);
            if (markForDeletion != null)
            {
                List<ColumnTuple> deleteFlagControl = this.GetDeleteFlagTuples(markForDeletion.ControlOrder, markForDeletion.SpreadsheetOrder, markForDeletion.Visible);
                this.Database.Update(Constant.DatabaseTable.Controls, new ColumnTuplesWithWhere(deleteFlagControl, markForDeletion.ID));
                this.GetControlsSortedByControlOrder();
            }
            else if (this.GetControlIDFromTemplateTable(Constant.DatabaseColumn.DeleteFlag) < 0)
            {
                // insert a DeleteFlag control, where its ID will be created as the next highest ID
                long order = this.GetOrderForNewControl();
                List<ColumnTuple> deleteFlagControl = this.GetDeleteFlagTuples(order, order, true);
                this.Database.Insert(Constant.DatabaseTable.Controls, new List<List<ColumnTuple>>() { deleteFlagControl });
                this.GetControlsSortedByControlOrder();
            }
        }

        /// <summary>
        /// Set the order of the specified control to the specified value, shifting other controls' orders as needed.
        /// </summary>
        private void SetControlOrders(string dataLabel, int order)
        {
            if ((order < 1) || (order > this.Controls.RowCount))
            {
                throw new ArgumentOutOfRangeException("order", "Control and spreadsheet orders must be contiguous ones based values.");
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

                string increaseIDs = Constant.Sql.Update + Constant.DatabaseTable.Controls;
                increaseIDs += Constant.Sql.Set + Constant.DatabaseColumn.ID + " = " + Constant.DatabaseColumn.ID + " + 1 + " + jumpAmount;
                increaseIDs += Constant.Sql.Where + Constant.DatabaseColumn.ID + " >= " + newID;
                queries.Add(increaseIDs);

                // Second update: decrease IDs above newID to be one more than their original value
                // This leaves everything in sequence except for an open spot at newID.
                string reduceIDs = Constant.Sql.Update + Constant.DatabaseTable.Controls;
                reduceIDs += Constant.Sql.Set + Constant.DatabaseColumn.ID + " = " + Constant.DatabaseColumn.ID + " - " + jumpAmount;
                reduceIDs += Constant.Sql.Where + Constant.DatabaseColumn.ID + " >= " + newID;
                queries.Add(reduceIDs);
            }

            // 3rd update: change the target ID to the desired ID
            this.CreateBackupIfNeeded();

            string setControlID = Constant.Sql.Update + Constant.DatabaseTable.Controls;
            setControlID += Constant.Sql.Set + Constant.DatabaseColumn.ID + " = " + newID;
            setControlID += Constant.Sql.Where + Constant.Control.DataLabel + " = '" + dataLabel + "'";
            queries.Add(setControlID);
            this.Database.ExecuteNonQueryWrappedInBeginEnd(queries);

            this.GetControlsSortedByControlOrder();
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

        private long GetOrderForNewControl()
        {
            return this.Controls.RowCount + 1;
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
                    this.Database.Update(Constant.DatabaseTable.Controls, columnsToUpdate);
                }
            }
        }

        private List<ColumnTuple> GetDateTimeTuples(long controlOrder, long spreadsheetOrder, bool visible)
        {
            List<ColumnTuple> dateTime = new List<ColumnTuple>();
            dateTime.Add(new ColumnTuple(Constant.Control.ControlOrder, controlOrder));
            dateTime.Add(new ColumnTuple(Constant.Control.SpreadsheetOrder, spreadsheetOrder));
            dateTime.Add(new ColumnTuple(Constant.Control.Type, Constant.DatabaseColumn.DateTime));
            dateTime.Add(new ColumnTuple(Constant.Control.DefaultValue, Constant.ControlDefault.DateTimeValue.UtcDateTime));
            dateTime.Add(new ColumnTuple(Constant.Control.Label, Constant.DatabaseColumn.DateTime));
            dateTime.Add(new ColumnTuple(Constant.Control.DataLabel, Constant.DatabaseColumn.DateTime));
            dateTime.Add(new ColumnTuple(Constant.Control.Tooltip, Constant.ControlDefault.DateTimeTooltip));
            dateTime.Add(new ColumnTuple(Constant.Control.TextBoxWidth, Constant.ControlDefault.DateTimeWidth));
            dateTime.Add(new ColumnTuple(Constant.Control.Copyable, false));
            dateTime.Add(new ColumnTuple(Constant.Control.Visible, visible));
            dateTime.Add(new ColumnTuple(Constant.Control.List, Constant.ControlDefault.Value));
            return dateTime;
        }

        // Defines a RelativePath control. The definition is used by its caller to insert a RelativePath control into the template for backwards compatability. 
        private List<ColumnTuple> GetRelativePathTuples(long controlOrder, long spreadsheetOrder, bool visible)
        {
            List<ColumnTuple> relativePath = new List<ColumnTuple>();
            relativePath.Add(new ColumnTuple(Constant.Control.ControlOrder, controlOrder));
            relativePath.Add(new ColumnTuple(Constant.Control.SpreadsheetOrder, spreadsheetOrder));
            relativePath.Add(new ColumnTuple(Constant.Control.Type, Constant.DatabaseColumn.RelativePath));
            relativePath.Add(new ColumnTuple(Constant.Control.DefaultValue, Constant.ControlDefault.Value));
            relativePath.Add(new ColumnTuple(Constant.Control.Label, Constant.DatabaseColumn.RelativePath));
            relativePath.Add(new ColumnTuple(Constant.Control.DataLabel, Constant.DatabaseColumn.RelativePath));
            relativePath.Add(new ColumnTuple(Constant.Control.Tooltip, Constant.ControlDefault.RelativePathTooltip));
            relativePath.Add(new ColumnTuple(Constant.Control.TextBoxWidth, Constant.ControlDefault.RelativePathWidth));
            relativePath.Add(new ColumnTuple(Constant.Control.Copyable, false));
            relativePath.Add(new ColumnTuple(Constant.Control.Visible, visible));
            relativePath.Add(new ColumnTuple(Constant.Control.List, Constant.ControlDefault.Value));
            return relativePath;
        }

        // Defines a DeleteFlag control. The definition is used by its caller to insert a DeleteFlag control into the template for backwards compatability. 
        private List<ColumnTuple> GetDeleteFlagTuples(long controlOrder, long spreadsheetOrder, bool visible)
        {
            List<ColumnTuple> deleteFlag = new List<ColumnTuple>();
            deleteFlag.Add(new ColumnTuple(Constant.Control.ControlOrder, controlOrder));
            deleteFlag.Add(new ColumnTuple(Constant.Control.SpreadsheetOrder, spreadsheetOrder));
            deleteFlag.Add(new ColumnTuple(Constant.Control.Type, Constant.DatabaseColumn.DeleteFlag));
            deleteFlag.Add(new ColumnTuple(Constant.Control.DefaultValue, Constant.ControlDefault.FlagValue));
            deleteFlag.Add(new ColumnTuple(Constant.Control.Label, Constant.ControlDefault.DeleteFlagLabel));
            deleteFlag.Add(new ColumnTuple(Constant.Control.DataLabel, Constant.DatabaseColumn.DeleteFlag));
            deleteFlag.Add(new ColumnTuple(Constant.Control.Tooltip, Constant.ControlDefault.DeleteFlagTooltip));
            deleteFlag.Add(new ColumnTuple(Constant.Control.TextBoxWidth, Constant.ControlDefault.FlagWidth));
            deleteFlag.Add(new ColumnTuple(Constant.Control.Copyable, false));
            deleteFlag.Add(new ColumnTuple(Constant.Control.Visible, visible));
            deleteFlag.Add(new ColumnTuple(Constant.Control.List, Constant.ControlDefault.Value));
            return deleteFlag;
        }

        private List<ColumnTuple> GetUtcOffsetTuples(long controlOrder, long spreadsheetOrder, bool visible)
        {
            List<ColumnTuple> utcOffset = new List<ColumnTuple>();
            utcOffset.Add(new ColumnTuple(Constant.Control.ControlOrder, controlOrder));
            utcOffset.Add(new ColumnTuple(Constant.Control.SpreadsheetOrder, spreadsheetOrder));
            utcOffset.Add(new ColumnTuple(Constant.Control.Type, Constant.DatabaseColumn.UtcOffset));
            utcOffset.Add(new ColumnTuple(Constant.Control.DefaultValue, Constant.ControlDefault.DateTimeValue.Offset));
            utcOffset.Add(new ColumnTuple(Constant.Control.Label, Constant.DatabaseColumn.UtcOffset));
            utcOffset.Add(new ColumnTuple(Constant.Control.DataLabel, Constant.DatabaseColumn.UtcOffset));
            utcOffset.Add(new ColumnTuple(Constant.Control.Tooltip, Constant.ControlDefault.UtcOffsetTooltip));
            utcOffset.Add(new ColumnTuple(Constant.Control.TextBoxWidth, Constant.ControlDefault.UtcOffsetWidth));
            utcOffset.Add(new ColumnTuple(Constant.Control.Copyable, false));
            utcOffset.Add(new ColumnTuple(Constant.Control.Visible, visible));
            utcOffset.Add(new ColumnTuple(Constant.Control.List, Constant.ControlDefault.Value));
            return utcOffset;
        }
    }
}
