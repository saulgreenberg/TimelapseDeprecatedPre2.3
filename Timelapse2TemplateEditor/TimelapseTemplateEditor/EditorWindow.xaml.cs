using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions; // For debugging
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Timelapse.Database;
using Timelapse.Editor.Dialog;
using Timelapse.Editor.Util;
using Timelapse.Util;
using MessageBox = Timelapse.Dialog.MessageBox;

namespace Timelapse.Editor
{
    public partial class EditorWindow : Window
    {
        // state tracking
        private EditorControls controls;
        private bool dataGridBeingUpdatedByCode;
        private EditorUserRegistrySettings userSettings;

        // These variables support the drag/drop of controls
        private UIElement dummyMouseDragSource;
        private bool isMouseDown;
        private bool isMouseDragging;
        private Point mouseDownStartPosition;
        private UIElement realMouseDragSource;

        // database where the template is stored
        private TemplateDatabase templateDatabase;

        #region Initialization, Window Loading and Closing
        /// <summary>
        /// Starts the UI.
        /// </summary>
        public EditorWindow()
        {
            AppDomain.CurrentDomain.UnhandledException += this.OnUnhandledException;
            this.InitializeComponent();
            this.Title = EditorConstant.MainWindowBaseTitle;
            Utilities.TryFitWindowInWorkingArea(this);

            // Abort if some of the required dependencies are missing
            if (Dependencies.AreRequiredBinariesPresent(EditorConstant.ApplicationName, Assembly.GetExecutingAssembly()) == false)
            {
                Dependencies.ShowMissingBinariesDialog(EditorConstant.ApplicationName);
                Application.Current.Shutdown();
            }

            this.controls = new EditorControls();
            this.dummyMouseDragSource = new UIElement();
            this.dataGridBeingUpdatedByCode = false;

            this.MenuViewShowAllColumns_Click(this.MenuViewShowAllColumns, null);

            // Recall state from prior sessions
            this.userSettings = new EditorUserRegistrySettings();
            this.MenuViewShowUTCDateTimeSettingsMenuItem.IsChecked = this.userSettings.ShowUtcOffset;

            // populate the most recent databases list
            this.MenuFileRecentTemplates_Refresh();
        }
        
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (DateTime.UtcNow - this.userSettings.MostRecentCheckForUpdates > Constant.CheckForUpdateInterval)
            {
                VersionClient updater = new VersionClient(this, Constant.ApplicationName, Constant.LatestVersionFilenameXML);
                updater.TryGetAndParseVersion(false);
                this.userSettings.MostRecentCheckForUpdates = DateTime.UtcNow;
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            // apply any pending edits
            this.TemplateDataGrid.CommitEdit();
            // persist state to registry
            this.userSettings.WriteToRegistry();
        }

        // If we get an exception that wasn't handled, show a dialog asking the user to send the bug report to us.
        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Utilities.ShowExceptionReportingDialog("The template editor needs to close.", e, this);
        }
        #endregion

        #region File Menu Callbacks
        /// <summary>
        /// Creates a new database file of a user chosen name in a user chosen location.
        /// </summary>
        private void MenuFileNewTemplate_Click(object sender, RoutedEventArgs e)
        {
            this.TemplateDataGrid.CommitEdit(); // to apply edits that the enter key was not pressed

            // Configure save file dialog box
            SaveFileDialog newTemplateFilePathDialog = new SaveFileDialog();
            newTemplateFilePathDialog.FileName = Path.GetFileNameWithoutExtension(Constant.File.DefaultTemplateDatabaseFileName); // Default file name without the extension
            newTemplateFilePathDialog.DefaultExt = Constant.File.TemplateDatabaseFileExtension; // Default file extension
            newTemplateFilePathDialog.Filter = "Database Files (" + Constant.File.TemplateDatabaseFileExtension + ")|*" + Constant.File.TemplateDatabaseFileExtension; // Filter files by extension 
            newTemplateFilePathDialog.Title = "Select Location to Save New Template File";

            // Show save file dialog box
            Nullable<bool> result = newTemplateFilePathDialog.ShowDialog();

            // Process save file dialog box results 
            if (result == true)
            {
                // Overwrite the file if it exists
                if (File.Exists(newTemplateFilePathDialog.FileName))
                {
                    FileBackup.TryCreateBackup(newTemplateFilePathDialog.FileName);
                    File.Delete(newTemplateFilePathDialog.FileName);
                }

                // Open document 
                this.InitializeDataGrid(newTemplateFilePathDialog.FileName);
                this.HelpMessageInitial.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Opens an existing database file.
        /// </summary>
        private void MenuFileOpenTemplate_Click(object sender, RoutedEventArgs e)
        {
            this.TemplateDataGrid.CommitEdit(); // to save any edits that the enter key was not pressed

            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.FileName = Path.GetFileNameWithoutExtension(Constant.File.DefaultTemplateDatabaseFileName); // Default file name without the extension
            openFileDialog.DefaultExt = Constant.File.TemplateDatabaseFileExtension; // Default file extension
            openFileDialog.Filter = "Database Files (" + Constant.File.TemplateDatabaseFileExtension + ")|*" + Constant.File.TemplateDatabaseFileExtension; // Filter files by extension 
            openFileDialog.Title = "Select an Existing Template File to Open";

            // Show open file dialog box
            Nullable<bool> result = openFileDialog.ShowDialog();

            // Process open file dialog box results 
            if (result == true)
            {
                // Open document 
                this.InitializeDataGrid(openFileDialog.FileName);
                this.HelpMessageInitial.Visibility = Visibility.Collapsed;
            }
        }

        // Open a rencently used template
        private void MenuItemRecentTemplate_Click(object sender, RoutedEventArgs e)
        {
            string recentTemplatePath = (string)((MenuItem)sender).ToolTip;
            this.InitializeDataGrid(recentTemplatePath);
            this.HelpMessageInitial.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Convert an old style xml template to the new style template
        /// </summary>
        private void MenuFileConvertTemplate_Click(object sender, RoutedEventArgs e)
        {
            string codeTemplateFileName = String.Empty;  // The code template file name

            this.TemplateDataGrid.CommitEdit(); // to save any edits that the enter key was not pressed

            // Get the name of the Code Template file to open
            OpenFileDialog codeTemplateFile = new OpenFileDialog();
            codeTemplateFile.FileName = Path.GetFileName(Constant.File.XmlDataFileName); // Default file name
            string xmlDataFileExtension = Path.GetExtension(Constant.File.XmlDataFileName);
            codeTemplateFile.DefaultExt = xmlDataFileExtension; // Default file extension
            codeTemplateFile.Filter = "Code Template Files (" + xmlDataFileExtension + ")|*" + xmlDataFileExtension; // Filter files by extension 
            codeTemplateFile.Title = "Select Code Template File to convert...";

            Nullable<bool> result = codeTemplateFile.ShowDialog(); // Show the open file dialog box
            if (result == true)
            {
                codeTemplateFileName = codeTemplateFile.FileName;  // Process open file dialog box results 
            }
            else
            {
                return;
            }

            // Get the name of the new database template file to create (over-writes it if it exists)
            SaveFileDialog templateDatabaseFile = new SaveFileDialog();
            templateDatabaseFile.Title = "Select Location to Save the Converted Template File";
            templateDatabaseFile.FileName = Path.GetFileNameWithoutExtension(Constant.File.DefaultTemplateDatabaseFileName); // Default file name
            templateDatabaseFile.DefaultExt = Constant.File.TemplateDatabaseFileExtension; // Default file extension
            templateDatabaseFile.Filter = "Database Files (" + Constant.File.TemplateDatabaseFileExtension + ")|*" + Constant.File.TemplateDatabaseFileExtension; // Filter files by extension 
            result = templateDatabaseFile.ShowDialog(); // Show open file dialog box

            // Process open file dialog box results 
            if (result == true)
            {
                // Overwrite the file if it exists
                if (File.Exists(templateDatabaseFile.FileName))
                {
                    File.Delete(templateDatabaseFile.FileName);
                }
            }
            else
            {
                return;
            }

            // Begin the conversion by creating a default data template
            this.InitializeDataGrid(templateDatabaseFile.FileName);
            this.HelpMessageInitial.Visibility = Visibility.Collapsed;

            // Now convert the code template file into a Data Template, overwriting values and adding rows as required
            Mouse.OverrideCursor = Cursors.Wait;
            this.dataGridBeingUpdatedByCode = true;

            List<string> conversionErrors;
            CodeTemplateImporter importer = new CodeTemplateImporter();
            importer.Import(codeTemplateFileName, this.templateDatabase, out conversionErrors);

            // Now that we have new contents of the datatable, update the user interface to match that
            this.controls.Generate(this, this.ControlsPanel, this.templateDatabase.Controls);
            this.GenerateSpreadsheet();

            this.dataGridBeingUpdatedByCode = false;
            Mouse.OverrideCursor = null;

            // Provide feedback to the user explaining any conversion errors and how they were repaired
            if (conversionErrors.Count > 0)
            {
                MessageBox messageBox = new MessageBox("One or more data labels were problematic", this);
                messageBox.Message.Icon = MessageBoxImage.Warning;
                messageBox.Message.Problem = conversionErrors.Count.ToString() + " of your Data Labels were problematic." + Environment.NewLine + Environment.NewLine +
                              "Data Labels:" + Environment.NewLine +
                              "\u2022 must be unique," + Environment.NewLine +
                              "\u2022 can only contain alphanumeric characters and '_'," + Environment.NewLine +
                              "\u2022 cannot match particular reserved words.";
                messageBox.Message.Result = "We will automatically repair these Data Labels:";
                foreach (string s in conversionErrors)
                {
                    messageBox.Message.Solution += Environment.NewLine + "\u2022 " + s;
                }
                messageBox.Message.Hint = "Check if these are the names you want. You can also rename these corrected Data Labels if you want";
                messageBox.ShowDialog();
            }
        }

        /// <summary>
        /// Exits the application.
        /// </summary>
        private void MenuFileExit_Click(object sender, RoutedEventArgs e)
        {
            this.TemplateDataGrid.CommitEdit(); // to save any edits that the enter key was not pressed
            Application.Current.Shutdown();
        }
        #endregion

        #region View Menu Callbacks

        // Before opening the view menu, check to see if the Utc offset is visible. 
        // If so, disable the ShowUTCDateTimeSettingsMenuItem  as we don't want the user to hide the Utc offset.
        private void ViewMenu_SubmenuOpened(object sender, RoutedEventArgs e)
        {
            // Find the UtcOffset Control and check its visibility
            List<ControlRow> controlsInControlOrder = this.templateDatabase.Controls.OrderBy(control => control.ControlOrder).ToList();
            foreach (ControlRow control in controlsInControlOrder)
            {
                if (control.Type == Constant.DatabaseColumn.UtcOffset)
                {
                    this.MenuViewShowUTCDateTimeSettingsMenuItem.IsEnabled = control.Visible ? false : true;
                    break;
                }
            }
        }

        /// <summary>
        /// Depending on the menu's checkbox state, show all columns or hide selected columns
        /// </summary>
        private void MenuViewShowAllColumns_Click(object sender, RoutedEventArgs e)
        {
            MenuItem mi = sender as MenuItem;
            if (mi == null)
            {
                return;
            }

            Visibility visibility = mi.IsChecked ? Visibility.Visible : Visibility.Collapsed;
            foreach (DataGridColumn column in this.TemplateDataGrid.Columns)
            {
                if (column.Header.Equals(EditorConstant.ColumnHeader.ID) ||
                    column.Header.Equals(EditorConstant.ColumnHeader.ControlOrder) ||
                    column.Header.Equals(EditorConstant.ColumnHeader.SpreadsheetOrder))
                {
                    column.Visibility = visibility;
                }
            }
        }

        /// <summary>
        /// Show or hide the UTC offset-related items. Note that this presents a dialog box explaining what UTC offset does.
        /// </summary>
        private void MenuItemUseUTCDateTimeSettings_Click(object sender, RoutedEventArgs e)
        {
            MenuItem mi = sender as MenuItem;
            if (mi.IsChecked == true)
            {
                MessageBox messageBox = new MessageBox("Confirm Showing UTC Date/Time Settings", this, MessageBoxButton.OKCancel);
                messageBox.Message.Icon = MessageBoxImage.Information;

                messageBox.Message.What = "Timelapse normally presents Dates and Times in a format understandable to typical users." + Environment.NewLine + Environment.NewLine;
                messageBox.Message.What += "In rare cases, users may need to see or manipulate Time Zone information. Timelapse can do this using the Coordinated Universal Time (UTC) standard, which includes a time zone offset (corrected for daylight saving times if needed) as part of the Date/Time.";
                messageBox.Message.Result += "Timelapse normally presents a single control indicating the date / time, and exports the date / time as two columns to a spreadsheet." + Environment.NewLine + Environment.NewLine;
                messageBox.Message.Result += "If you choose to display the UTC Date/Time settings:" + Environment.NewLine +
                              "\u2022 a row describing the UtcOffset control will be displayed in the editor, which you can then make visible" + Environment.NewLine +
                              "\u2022 if UtcOffset is visible, Timelapse will:" + Environment.NewLine +
                              "    \u2022 include a menu option to manipulate the time zone." + Environment.NewLine +
                              "    \u2022 export two extra columns to the spreadsheet: the absolute DateTime, and the UTC offset.";
                messageBox.Message.Hint = "Avoid showing UTC Date/Time unless you really need it, as your users may find it confusing." + Environment.NewLine;
                bool? result = messageBox.ShowDialog();
                if (result != true)
                {
                    mi.IsChecked = false;
                }
            }
            this.userSettings.ShowUtcOffset = mi.IsChecked ? true : false;
            this.controls.Generate(this, this.ControlsPanel, this.templateDatabase.Controls);
            this.GenerateSpreadsheet();
        }

        /// <summary>
        /// Show the dialog that allows a user to inspect image metadata
        /// </summary>
        private void MenuItemInspectImageMetadata_Click(object sender, RoutedEventArgs e)
        {
            InspectMetadata inspectMetadata = new InspectMetadata(this);
            inspectMetadata.ShowDialog();
        }
        #endregion

        #region Help Menu Callbacks
        /// <summary>Display the Timelapse home page </summary> 
        private void MenuHelpTimelapseWebPage_Click(object sender, RoutedEventArgs e)
        {
            Uri uri = new Uri("http://saul.cpsc.ucalgary.ca/timelapse/");
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri));
        }

        /// <summary>Display the manual in a web browser </summary> 
        private void MenuHelpTutorialManual_Click(object sender, RoutedEventArgs e)
        {
            Uri uri = new Uri("http://saul.cpsc.ucalgary.ca/timelapse/uploads/Installs/Timelapse2/Timelapse2Manual.pdf");
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri));
        }

        /// <summary>Display the page in the web browser that lets you join the Timelapse mailing list</summary>
        private void MenuHelpJoinTimelapseMailingList_Click(object sender, RoutedEventArgs e)
        {
            Uri uri = new Uri("http://mailman.ucalgary.ca/mailman/listinfo/timelapse-l");
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri));
        }

        /// <summary>Download the sample images from a web browser</summary>
        private void MenuHelpDownloadSampleImages_Click(object sender, RoutedEventArgs e)
        {
            Uri uri = new Uri("http://saul.cpsc.ucalgary.ca/timelapse/uploads/Main/TutorialImageSet.zip");
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri));
        }

        /// <summary>Send mail to the timelapse mailing list</summary> 
        private void MenuHelpMailToTimelapseMailingList_Click(object sender, RoutedEventArgs e)
        {
            Uri uri = new Uri("mailto:timelapse-l@mailman.ucalgary.ca");
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri));
        }

        private void MenuHelpAbout_Click(object sender, RoutedEventArgs e)
        {
            Timelapse.Dialog.AboutTimelapse about = new Timelapse.Dialog.AboutTimelapse(this);
            if ((about.ShowDialog() == true) && about.MostRecentCheckForUpdate.HasValue)
            {
                this.userSettings.MostRecentCheckForUpdates = about.MostRecentCheckForUpdate.Value;
            }
        }
        #endregion

        #region DataGrid and New Database Initialization
        /// <summary>
        /// Given a database file path,create a new DB file if one does not exist, or load a DB file if there is one.
        /// After a DB file is loaded, the table is extracted and loaded a DataTable for binding to the DataGrid.
        /// Some listeners are added to the DataTable, and the DataTable is bound. The add row buttons are enabled.
        /// </summary>
        /// <param name="templateDatabaseFilePath">The path of the DB file created or loaded</param>
        private void InitializeDataGrid(string templateDatabaseFilePath)
        {
            // Create a new DB file if one does not exist, or load a DB file if there is one.
            this.templateDatabase = TemplateDatabase.CreateOrOpen(templateDatabaseFilePath);

            // Map the data table to the data grid, and create a callback executed whenever the datatable row changes
            this.templateDatabase.BindToEditorDataGrid(this.TemplateDataGrid, this.TemplateDataTable_RowChanged);

            // Update the user interface specified by the contents of the table
            this.controls.Generate(this, this.ControlsPanel, this.templateDatabase.Controls);
            this.GenerateSpreadsheet();

            // Update UI to reflect that a .tdb is now loaded
            // First, enable all the buttons that allow rows to be added
            this.AddCounterButton.IsEnabled = true;
            this.AddFixedChoiceButton.IsEnabled = true;
            this.AddNoteButton.IsEnabled = true;
            this.AddFlagButton.IsEnabled = true;

            // Second, enable/disable the various menus as needed. This includes updating the recent templates list. 
            this.MenuFileNewTemplate.IsEnabled = false;
            this.MenuFileOpenTemplate.IsEnabled = false;
            this.MenuFileConvertTemplate.IsEnabled = false;
            this.MenuView.IsEnabled = true;
            this.userSettings.MostRecentTemplates.SetMostRecent(templateDatabaseFilePath);
            this.MenuFileRecentTemplates.IsEnabled = false;

            // Third, include the database file name in the window title
            this.Title = EditorConstant.MainWindowBaseTitle + " (" + Path.GetFileName(this.templateDatabase.FilePath) + ")";

            // Switch to the Template Pane tab
            this.TemplatePane.IsActive = true;
        }
        #endregion DataGrid and New Database Initialization

        #region Data Changed Listeners and Methods
        /// <summary>
        /// Updates a given control in the database with the current state of the DataGrid. 
        /// </summary>
        private void SyncControlToDatabase(ControlRow control)
        {
            this.dataGridBeingUpdatedByCode = true;

            this.templateDatabase.SyncControlToDatabase(control);
            this.controls.Generate(this, this.ControlsPanel, this.templateDatabase.Controls);
            this.GenerateSpreadsheet();

            this.dataGridBeingUpdatedByCode = false;
        }

        /// <summary>
        /// Whenever a row changes, save that row to the database, which also updates the grid colors.
        /// </summary>
        private void TemplateDataTable_RowChanged(object sender, DataRowChangeEventArgs e)
        {
            Debug.Print("InRowChanged");
            if (!this.dataGridBeingUpdatedByCode)
            {
                this.SyncControlToDatabase(new ControlRow(e.Row));
                Debug.Print("---Syncing");
            }
        }
        #endregion Data Changed Listeners and Methods

        #region Datagrid Row Modifiers listeners and methods
        /// <summary>
        /// Logic to enable/disable editing buttons depending on there being a row selection
        /// Also sets the text for the remove row button.
        /// </summary>
        private void TemplateDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            DataRowView selectedRowView = this.TemplateDataGrid.SelectedItem as DataRowView;
            if (selectedRowView == null)
            {
                this.RemoveControlButton.IsEnabled = false;
                return;
            }

            ControlRow control = new ControlRow(selectedRowView.Row);
            this.RemoveControlButton.IsEnabled = !Constant.Control.StandardTypes.Contains(control.Type);
        }

        /// <summary>
        /// Adds a row to the table. The row type is decided by the button tags.
        /// Default values are set for the added row, differing depending on type.
        /// </summary>
        private void AddControlButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            string controlType = button.Tag.ToString();

            this.dataGridBeingUpdatedByCode = true;

            this.templateDatabase.AddUserDefinedControl(controlType);
            this.TemplateDataGrid.DataContext = this.templateDatabase.Controls;
            this.TemplateDataGrid.ScrollIntoView(this.TemplateDataGrid.Items[this.TemplateDataGrid.Items.Count - 1]);

            this.controls.Generate(this, this.ControlsPanel, this.templateDatabase.Controls);
            this.GenerateSpreadsheet();
            this.OnControlOrderChanged();

            this.dataGridBeingUpdatedByCode = true;
        }

        /// <summary>
        /// Removes a row from the table and shifts up the ids on the remaining rows.
        /// The required rows are unable to be deleted.
        /// </summary>
        private void RemoveControlButton_Click(object sender, RoutedEventArgs e)
        {
            DataRowView selectedRowView = this.TemplateDataGrid.SelectedItem as DataRowView;
            if (selectedRowView == null || selectedRowView.Row == null)
            {
                // nothing to do
                return;
            }

            ControlRow control = new ControlRow(selectedRowView.Row);
            if (EditorControls.IsStandardControlType(control.Type))
            {
                // standard controls cannot be removed
                return;
            }

            this.dataGridBeingUpdatedByCode = true;
            // remove the control, then update the view so it reflects the current values in the database
            this.templateDatabase.RemoveUserDefinedControl(new ControlRow(selectedRowView.Row));
            this.controls.Generate(this, this.ControlsPanel, this.templateDatabase.Controls);
            this.GenerateSpreadsheet();
            this.dataGridBeingUpdatedByCode = false;
        }
        #endregion Datagrid Row Modifyiers listeners and methods

        #region Choice List Box Handlers
        // When the  choice list button is clicked, raise a dialog box that lets the user edit the list of choices
        private void ChoiceListButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;

            // The button tag holds the Control Order of the row the button is in, not the ID.
            // So we have to search through the rows to find the one with the correct control order
            // and retrieve / set the ItemList menu in that row.
            ControlRow choiceControl = this.templateDatabase.Controls.FirstOrDefault(control => control.ControlOrder.ToString().Equals(button.Tag.ToString()));
            if (choiceControl == null)
            {
                Utilities.PrintFailure(String.Format("Control named {0} not found.", button.Tag));
                return;
            }

            bool includesEmptyChoice;
            List<string> choiceList = choiceControl.GetChoices(out includesEmptyChoice);
            Dialog.EditChoiceList choiceListDialog = new Dialog.EditChoiceList(button, choiceList, includesEmptyChoice, this);
            bool? result = choiceListDialog.ShowDialog();
            if (result == true)
            {
                choiceControl.SetChoices(choiceListDialog.Choices);
                this.SyncControlToDatabase(choiceControl);
            }
        }
        #endregion

        #region Cell Editing / Coloring Listeners and Methods
        // Cell editing: Preview character by character entry to disallow spaces in particular fields (DataLabels, Width, Counters
        private void TemplateDataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            DataGridCell currentCell;
            DataGridRow currentRow;
            if ((this.TryGetCurrentCell(out currentCell, out currentRow) == false) || currentCell.Background.Equals(EditorConstant.NotEditableCellColor))
            {
                e.Handled = true;
                return;
            }

            switch ((string)this.TemplateDataGrid.CurrentColumn.Header)
            {
                case EditorConstant.ColumnHeader.DataLabel:
                    // Datalabels should not accept spaces - display a warning if needed
                    if (e.Key == Key.Space)
                    {
                        this.ShowMessageBox_DataLabelRequirements();
                        e.Handled = true;
                    }
                    // If its a tab, commit the edit before going to the next cell
                    if (e.Key == Key.Tab)
                    {
                        TemplateDataGrid.CommitEdit();
                    }
                    break;
                case EditorConstant.ColumnHeader.Width:
                    // Width should  not accept spaces 
                    e.Handled = e.Key == Key.Space;
                    break;
                case EditorConstant.ColumnHeader.DefaultValue:
                    // Default value for Counters should not accept spaces 
                    ControlRow control = new ControlRow((currentRow.Item as DataRowView).Row);
                    if (control.Type == Constant.Control.Counter)
                    {
                        e.Handled = e.Key == Key.Space;
                    }
                    break;
                default:
                    break;
            }
        }

        // Cell editing: Preview final string entry
        // Accept only numbers in counters and widths, t and f in flags (translated to true and false), and alphanumeric or _ in datalabels
        private void TemplateDataGrid_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            DataGridCell currentCell;
            DataGridRow currentRow;
            if ((this.TryGetCurrentCell(out currentCell, out currentRow) == false) || currentCell.Background.Equals(EditorConstant.NotEditableCellColor))
            {
                e.Handled = true;
                return;
            }

            switch ((string)this.TemplateDataGrid.CurrentColumn.Header)
            {
                // EditorConstant.Control.ControlOrder is not editable
                case EditorConstant.ColumnHeader.DataLabel:
                    // Only allow alphanumeric and '_' in data labels
                    if ((!Utilities.IsLetterOrDigit(e.Text)) && !e.Text.Equals("_"))
                    {
                        this.ShowMessageBox_DataLabelRequirements();
                        e.Handled = true;
                    }
                    break;
                case EditorConstant.ColumnHeader.DefaultValue:
                    // Restrict certain default values for counters, flags (and perhaps fixed choices in the future)
                    ControlRow control = new ControlRow((currentRow.Item as DataRowView).Row);
                    switch (control.Type)
                    {
                        case Constant.Control.Counter:
                            // Only allow numbers in counters 
                            e.Handled = !Utilities.IsDigits(e.Text);
                            break;
                        case Constant.Control.Flag:
                            // Only allow t/f and translate to true/false
                            if (e.Text == "t" || e.Text == "T")
                            {
                                control.DefaultValue = Constant.Boolean.True;
                                this.SyncControlToDatabase(control);
                            }
                            else if (e.Text == "f" || e.Text == "F")
                            {
                                control.DefaultValue = Constant.Boolean.False;
                                this.SyncControlToDatabase(control);
                            }
                            e.Handled = true;
                            break;
                        case Constant.Control.FixedChoice:
                            // The default value should be constrained to one of the choices, but that introduces a chicken and egg problem
                            // So lets just ignore it for now.
                            break;
                        case Constant.Control.Note:
                        default:
                            // no restrictions on Notes 
                            break;
                    }
                    break;
                case EditorConstant.ColumnHeader.Width:
                    // Only allow digits in widths as they must be parseable as integers
                    e.Handled = !Utilities.IsDigits(e.Text);
                    break;
                default:
                    // no restrictions on any of the other editable coumns
                    break;  
            }
        }

        /// <summary>
        /// Before cell editing begins on a cell click, the cell is disabled if it is grey (meaning cannot be edited).
        /// Another method re-enables the cell immediately afterwards.
        /// The reason for this implementation is because disabled cells cannot be single clicked, which is needed for row actions.
        /// </summary>
        private void TemplateDataGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            DataGridCell currentCell;
            DataGridRow currentRow;
            if (this.TryGetCurrentCell(out currentCell, out currentRow) == false)
            {
                return;
            }

            if (currentCell.Background.Equals(EditorConstant.NotEditableCellColor))
            {
                currentCell.IsEnabled = false;
                this.TemplateDataGrid.CancelEdit();
            }
        }

        /// <summary>
        /// After cell editing ends (prematurely or no), re-enable disabled cells.
        /// See TemplateDataGrid_BeginningEdit for full explanation.
        /// </summary>
        private void TemplateDataGrid_CurrentCellChanged(object sender, EventArgs e)
        {
            DataGridCell currentCell;
            DataGridRow currentRow;
            if (this.TryGetCurrentCell(out currentCell, out currentRow) == false)
            {
                return;
            }
            if (currentCell.Background.Equals(EditorConstant.NotEditableCellColor))
            {
                currentCell.IsEnabled = true;
            }
        }

        private bool manualCommitEdit = false;
        // After editing is complete, validate the data labels, default values, and widths as needed.
        // Also commit the edit, which will raise the RowChanged event
        private void TemplateDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            // Stop re-entering, which can occur after we manually perform a CommitEdit (see below) 
            if (this.manualCommitEdit == true)
            {
                this.manualCommitEdit = false;
                return;
            }
            DataGridCell currentCell;
            DataGridRow currentRow;
            if (this.TryGetCurrentCell(out currentCell, out currentRow) == false)
            {
                return;
            }

            switch ((string)this.TemplateDataGrid.CurrentColumn.Header)
            {
                case EditorConstant.ColumnHeader.DataLabel:
                    this.ValidateDataLabel(e);
                    break;
                case EditorConstant.ColumnHeader.DefaultValue:
                    this.ValidateDefaults(e, currentRow);
                    break;
                case EditorConstant.ColumnHeader.Width:
                    this.ValidateWidths(e, currentRow);
                    break;
                default:
                    // no restrictions on any of the other editable columns
                    break;
            }

            // While hitting return after editing (say) a note will raise a RowChanged event, 
            // clicking out of that cell does not raise a RowChangedEvent, even though the cell has been edited. 
            // Thus we manuallly commit the edit. 
            this.manualCommitEdit = true;
            this.TemplateDataGrid.CommitEdit(DataGridEditingUnit.Row, true);
        }

        /// <summary>
        /// Updates colors when rows are added, moved, or deleted.
        /// </summary>
        private void TemplateDataGrid_LayoutUpdated(object sender, EventArgs e)
        {
            // Greys out cells and updates the visibility of particular rows as defined by logic. 
            // This is to  show the user uneditable cells. Color is also used by code to check whether a cell can be edited.
            // This method should be called after row are added/moved/deleted to update the colors. 
            // This also disables checkboxes that cannot be edited. Disabling checkboxes does not effect row interactions.
            // Finally, it collapses or shows various date-associated rows.
            for (int rowIndex = 0; rowIndex < this.TemplateDataGrid.Items.Count; rowIndex++)
            {
                // In order for ItemContainerGenerator to work, we need to set the TemplateGrid in the XAML to VirtualizingStackPanel.IsVirtualizing="False"
                // Alternately, we could just do the following, which may be more efficient for large grids (which we normally don't have)
                // this.TemplateDataGrid.UpdateLayout();
                // this.TemplateDataGrid.ScrollIntoView(rowIndex + 1);
                DataGridRow row = (DataGridRow)this.TemplateDataGrid.ItemContainerGenerator.ContainerFromIndex(rowIndex);
                if (row == null)
                {
                    return;
                }

                // grid cells are editable by default
                // disable cells which should not be editable
                DataGridCellsPresenter presenter = GetVisualChild<DataGridCellsPresenter>(row);
                for (int column = 0; column < this.TemplateDataGrid.Columns.Count; column++)
                {
                    DataGridCell cell = (DataGridCell)presenter.ItemContainerGenerator.ContainerFromIndex(column);
                    if (cell == null)
                    {
                        // cell will be null for columns with Visibility = Hidden
                        continue;
                    }

                    ControlRow control = new ControlRow(((DataRowView)this.TemplateDataGrid.Items[rowIndex]).Row);
                    string controlType = control.Type;

                    // These columns should always be editable
                    string columnHeader = (string)this.TemplateDataGrid.Columns[column].Header;
                    if ((columnHeader == Constant.Control.Label) ||
                        (columnHeader == Constant.Control.Tooltip) ||
                        (columnHeader == Constant.Control.Visible) ||
                        (columnHeader == EditorConstant.ColumnHeader.Width))
                    {
                        cell.SetValue(DataGridCell.IsTabStopProperty, true); // Allow tabbing in non-editable fields
                        continue;
                    }

                    // Update the visibility of various date-related rows, where its visibility depends upon whether the UseUTCDateTime advanced option is set.
                    this.UpdateRowVisibility(controlType, row);

                    // The following attributes should NOT be editable.
                    ContentPresenter cellContent = cell.Content as ContentPresenter;
                    string sortMemberPath = this.TemplateDataGrid.Columns[column].SortMemberPath;
                    if (String.Equals(sortMemberPath, Constant.DatabaseColumn.ID, StringComparison.OrdinalIgnoreCase) ||
                        String.Equals(sortMemberPath, Constant.Control.ControlOrder, StringComparison.OrdinalIgnoreCase) ||
                        String.Equals(sortMemberPath, Constant.Control.SpreadsheetOrder, StringComparison.OrdinalIgnoreCase) ||
                        String.Equals(sortMemberPath, Constant.Control.Type, StringComparison.OrdinalIgnoreCase) ||
                        (controlType == Constant.DatabaseColumn.Date) ||
                        (controlType == Constant.DatabaseColumn.DateTime) ||
                        (controlType == Constant.DatabaseColumn.DeleteFlag) ||
                        (controlType == Constant.DatabaseColumn.File) ||
                        (controlType == Constant.DatabaseColumn.Folder) ||
                        ((controlType == Constant.DatabaseColumn.ImageQuality) && (columnHeader == Constant.Control.Copyable)) ||
                        ((controlType == Constant.DatabaseColumn.ImageQuality) && (columnHeader == EditorConstant.ColumnHeader.DataLabel)) ||
                        ((controlType == Constant.DatabaseColumn.ImageQuality) && (columnHeader == Constant.Control.List)) ||
                        ((controlType == Constant.DatabaseColumn.ImageQuality) && (sortMemberPath == Constant.Control.DefaultValue)) ||
                        (controlType == Constant.DatabaseColumn.RelativePath) ||
                        (controlType == Constant.DatabaseColumn.Time) ||
                        (controlType == Constant.DatabaseColumn.UtcOffset) ||
                        ((controlType == Constant.Control.Counter) && (columnHeader == Constant.Control.List)) ||
                        ((controlType == Constant.Control.Flag) && (columnHeader == Constant.Control.List)) ||
                        ((controlType == Constant.Control.Note) && (columnHeader == Constant.Control.List)))
                    {
                        cell.Background = EditorConstant.NotEditableCellColor;
                        cell.Foreground = Brushes.Gray;
                        cell.SetValue(DataGridCell.IsTabStopProperty, false);  // Disallow tabbing in non-editable fields

                        // if cell has a checkbox, also disable it.
                        if (cellContent != null)
                        {
                            CheckBox checkbox = cellContent.ContentTemplate.FindName("CheckBox", cellContent) as CheckBox;
                            if (checkbox != null)
                            {
                                checkbox.IsEnabled = false;
                            }
                            else if ((controlType == Constant.DatabaseColumn.ImageQuality) && TemplateDataGrid.Columns[column].Header.Equals("List"))
                            {
                                cell.IsEnabled = false; // Don't let users edit the ImageQuality menu
                            }
                        }
                    }
                    else
                    {
                        cell.ClearValue(DataGridCell.BackgroundProperty); // otherwise when scrolling cells offscreen get colored randomly
                        cell.SetValue(DataGridCell.IsTabStopProperty, true);
                        // if cell has a checkbox, enable it.
                        if (cellContent != null)
                        {
                            CheckBox checkbox = cellContent.ContentTemplate.FindName("CheckBox", cellContent) as CheckBox;
                            if (checkbox != null)
                            {
                                checkbox.IsEnabled = true;
                            }
                        }
                    }
                }
            }
        }

        // Update the visibility of various Date-related rows 
        private void UpdateRowVisibility(string controlType, DataGridRow row)
        {
            // Find the UtcOffset Control and check its visibility
            bool utcIsVisibile = false;
            List<ControlRow> controlsInControlOrder = this.templateDatabase.Controls.OrderBy(control => control.ControlOrder).ToList();
            foreach (ControlRow control in controlsInControlOrder)
            {
                if (control.Type == Constant.DatabaseColumn.UtcOffset)
                {
                    utcIsVisibile = control.Visible;
                    break;
                }
            }

            // Now adjust the row's visibility. 
            if (controlType == Constant.DatabaseColumn.UtcOffset)
            {
                if (utcIsVisibile == true)
                {
                    // If the Utc control is set to visible, we should always show the row.
                    row.Visibility = Visibility.Visible;
                }
                else
                {
                    // Otherwise, the visibility depends on the user settings
                    row.Visibility = (this.userSettings.ShowUtcOffset == true) ? Visibility.Visible : Visibility.Collapsed;
                }
            }
            // Always hide the Date/Time rows, as they are now internal to Timelapse.
            if (controlType == Constant.DatabaseColumn.Date || controlType == Constant.DatabaseColumn.Time)
            {
                row.Visibility = Visibility.Collapsed;
            }
        }
        #endregion Cell Editing / Coloring Listeners and Methods

        #region Retrieving cells from the datagrid
        // If we can, return the curentCell and the current Row
        private bool TryGetCurrentCell(out DataGridCell currentCell, out DataGridRow currentRow)
        {
            if ((this.TemplateDataGrid.SelectedIndex == -1) || (this.TemplateDataGrid.CurrentColumn == null))
            {
                currentCell = null;
                currentRow = null;
                return false;
            }

            currentRow = (DataGridRow)this.TemplateDataGrid.ItemContainerGenerator.ContainerFromIndex(this.TemplateDataGrid.SelectedIndex);
            DataGridCellsPresenter presenter = EditorWindow.GetVisualChild<DataGridCellsPresenter>(currentRow);
            currentCell = (DataGridCell)presenter.ItemContainerGenerator.ContainerFromIndex(this.TemplateDataGrid.CurrentColumn.DisplayIndex);
            return currentCell != null;
        }

        /// <summary>
        /// Used in this code to get the child of a DataGridRows, DataGridCellsPresenter. This can be used to get the DataGridCell.
        /// WPF does not make it easy to get to the actual cells.
        /// </summary>
        // Code from: http://techiethings.blogspot.com/2010/05/get-wpf-datagrid-row-and-cell.html
        private static T GetVisualChild<T>(Visual parent) where T : Visual
        {
            T child = default(T);
            int numVisuals = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < numVisuals; i++)
            {
                Visual v = (Visual)VisualTreeHelper.GetChild(parent, i);
                child = v as T;
                if (child == null)
                {
                    child = GetVisualChild<T>(v);
                }
                if (child != null)
                {
                    break;
                }
            }
            return child;
        }
        #endregion

        #region Validation of Cell contents
        // Validate the data label to correct for empty, duplicate, or non-legal naming
        private void ValidateDataLabel(DataGridCellEditEndingEventArgs e)
        {
            // Check to see if the data label entered is a reserved word or if its a non-unique label
            TextBox textBox = e.EditingElement as TextBox;
            string dataLabel = textBox.Text;

            // Check to see if the data label is empty. If it is, generate a unique data label and warn the user
            if (String.IsNullOrWhiteSpace(dataLabel))
            {
                this.ShowMessageBox_DataLabelsCannotBeEmpty();
                textBox.Text = this.templateDatabase.GetNextUniqueDataLabel("DataLabel");
            }

            // Check to see if the data label is unique. If not, generate a unique data label and warn the user
            for (int row = 0; row < this.templateDatabase.Controls.RowCount; row++)
            {
                ControlRow control = this.templateDatabase.Controls[row];
                if (dataLabel.Equals(control.DataLabel))
                {
                    if (this.TemplateDataGrid.SelectedIndex == row)
                    {
                        continue; // Its the same row, so its the same key, so skip it
                    }
                    this.ShowMessageBox_DataLabelsMustBeUnique(textBox.Text);
                    textBox.Text = this.templateDatabase.GetNextUniqueDataLabel(dataLabel);
                    break;
                }
            }

            // Check to see if the label (if its not empty, which it shouldn't be) has any illegal characters.
            // Note that most of this is redundant, as we have already checked for illegal characters as they are typed. However,
            // we have not checked to see if the first letter is alphabetic.
            if (dataLabel.Length > 0)
            {
                Regex alphanumdash = new Regex("^[a-zA-Z0-9_]*$");
                Regex alpha = new Regex("^[a-zA-Z]*$");

                string firstCharacter = dataLabel[0].ToString();

                if (!(alpha.IsMatch(firstCharacter) && alphanumdash.IsMatch(dataLabel)))
                {
                    string replacementDataLabel = dataLabel;

                    if (!alpha.IsMatch(firstCharacter))
                    {
                        replacementDataLabel = "X" + replacementDataLabel.Substring(1);
                    }
                    replacementDataLabel = Regex.Replace(replacementDataLabel, @"[^A-Za-z0-9_]+", "X");

                    this.ShowMessageBox_DataLabelIsInvalid(textBox.Text, replacementDataLabel);
                    textBox.Text = replacementDataLabel;
                }
            }

            // Check to see if its a reserved word
            foreach (string sqlKeyword in EditorConstant.ReservedSqlKeywords)
            {
                if (String.Equals(sqlKeyword, dataLabel, StringComparison.OrdinalIgnoreCase))
                {
                    this.ShowMessageBox_DataLabelIsAReservedWord(textBox.Text);
                    textBox.Text += "_";
                    break;
                }
            }
        }

        // Validation of Defaults: Particular defaults (Flags) cannot be empty 
        // There is no need to check other validations here (e.g., unallowed characters) as these will have been caught previously 
        private void ValidateDefaults(DataGridCellEditEndingEventArgs e, DataGridRow currentRow)
        {
            ControlRow control = new ControlRow((currentRow.Item as DataRowView).Row);
            TextBox textBox = e.EditingElement as TextBox;
            switch (control.Type)
            {
                case Constant.Control.Flag:
                    if (String.IsNullOrWhiteSpace(textBox.Text))
                    {
                        textBox.Text = Constant.ControlDefault.FlagValue;
                    }
                    break;
                case Constant.Control.FixedChoice:
                case Constant.Control.Counter:
                case Constant.Control.Note:
                default:
                    // empty fields are allowed in these control types
                    break;
            }
        }

        // Validation of Widths: if a control's width is empty, reset it to its corresponding default width
        private void ValidateWidths(DataGridCellEditEndingEventArgs e, DataGridRow currentRow)
        {
            TextBox textBox = e.EditingElement as TextBox;
            if (!String.IsNullOrWhiteSpace(textBox.Text))
            {
                return;
            }

            ControlRow control = new ControlRow((currentRow.Item as DataRowView).Row);
            switch (control.Type)
            {
                case Constant.DatabaseColumn.File:
                    textBox.Text = Constant.ControlDefault.FileWidth.ToString();
                    break;
                case Constant.DatabaseColumn.Folder:
                    textBox.Text = Constant.ControlDefault.FolderWidth.ToString();
                    break;
                case Constant.DatabaseColumn.DateTime:
                    textBox.Text = Constant.ControlDefault.DateTimeWidth.ToString();
                    break;
                case Constant.DatabaseColumn.ImageQuality:
                    textBox.Text = Constant.ControlDefault.ImageQualityWidth.ToString();
                    break;
                case Constant.DatabaseColumn.UtcOffset:
                    textBox.Text = Constant.ControlDefault.UtcOffsetWidth.ToString();
                    break;
                case Constant.DatabaseColumn.RelativePath:
                    textBox.Text = Constant.ControlDefault.RelativePathWidth.ToString();
                    break;
                case Constant.DatabaseColumn.DeleteFlag:
                case Constant.Control.Flag:
                    textBox.Text = Constant.ControlDefault.FlagWidth.ToString();
                    break;
                case Constant.Control.FixedChoice:
                    textBox.Text = Constant.ControlDefault.FixedChoiceWidth.ToString();
                    break;
                case Constant.Control.Counter:
                    textBox.Text = Constant.ControlDefault.CounterWidth.ToString();
                    break;
                case Constant.Control.Note:
                    textBox.Text = Constant.ControlDefault.NoteWidth.ToString();
                    break;
                default:
                    Constant.ControlDefault.NoteWidth.ToString();
                    break;
            }
        }
        #endregion

        #region Other menu related items
        /// <summary>
        /// Update the list of recent databases displayed under File -> Recent Databases.
        /// </summary>
        private void MenuFileRecentTemplates_Refresh()
        {
            this.MenuFileRecentTemplates.IsEnabled = this.userSettings.MostRecentTemplates.Count > 0;
            this.MenuFileRecentTemplates.Items.Clear();

            int index = 1;
            foreach (string recentTemplatePath in this.userSettings.MostRecentTemplates)
            {
                MenuItem recentImageSetItem = new MenuItem();
                recentImageSetItem.Click += this.MenuItemRecentTemplate_Click;
                recentImageSetItem.Header = String.Format("_{0} {1}", index, recentTemplatePath);
                recentImageSetItem.ToolTip = recentTemplatePath;
                this.MenuFileRecentTemplates.Items.Add(recentImageSetItem);
                ++index;
            }
        }

        #endregion

        #region ShowMessageBox_DataLabel
        private void ShowMessageBox_DataLabelIsAReservedWord(string data_label)
        {
            MessageBox messageBox = new MessageBox("'" + data_label + "' is not a valid data label.", this);
            messageBox.Message.Icon = MessageBoxImage.Warning;
            messageBox.Message.Problem = "Data labels cannot match the reserved words.";
            messageBox.Message.Result = "We will add an '_' suffix to this Data Label to make it differ from the reserved word";
            messageBox.Message.Hint = "Avoid the reserved words listed below. Start your label with a letter. Then use any combination of letters, numbers, and '_'." + Environment.NewLine;
            foreach (string keyword in EditorConstant.ReservedSqlKeywords)
            {
                messageBox.Message.Hint += keyword + " ";
            }
            messageBox.ShowDialog();
        }

        private void ShowMessageBox_DataLabelsCannotBeEmpty()
        {
            MessageBox messageBox = new MessageBox("Data Labels cannot be empty", this);
            messageBox.Message.Icon = MessageBoxImage.Warning;
            messageBox.Message.Problem = "Data Labels cannot be empty. They must begin with a letter, followed only by letters, numbers, and '_'.";
            messageBox.Message.Result = "We will automatically create a uniquely named Data Label for you.";
            messageBox.Message.Hint = "You can create your own name for this Data Label. Start your label with a letter. Then use any combination of letters, numbers, and '_'.";
            messageBox.ShowDialog();
        }

        private void ShowMessageBox_DataLabelIsInvalid(string old_data_label, string new_data_label)
        {
            MessageBox messageBox = new MessageBox("'" + old_data_label + "' is not a valid data label.", this);
            messageBox.Message.Icon = MessageBoxImage.Warning;
            messageBox.Message.Problem = "Data labels must begin with a letter, followed only by letters, numbers, and '_'.";
            messageBox.Message.Result = "We replaced all dissallowed characters with an 'X': " + new_data_label;
            messageBox.Message.Hint = "Start your label with a letter. Then use any combination of letters, numbers, and '_'.";
            messageBox.ShowDialog();
        }

        private void ShowMessageBox_DataLabelsMustBeUnique(string data_label)
        {
            MessageBox messageBox = new MessageBox("Data Labels must be unique.", this);
            messageBox.Message.Icon = MessageBoxImage.Warning;
            messageBox.Message.Problem = "'" + data_label + "' is not a valid Data Label, as you have already used it in another row.";
            messageBox.Message.Result = "We will automatically create a unique Data Label for you by adding a number to its end.";
            messageBox.Message.Hint = "You can create your own unique name for this Data Label. Start your label with a letter. Then use any combination of letters, numbers, and '_'.";
            messageBox.ShowDialog();
        }

        private void ShowMessageBox_DataLabelRequirements()
        {
            MessageBox messageBox = new MessageBox("Data Labels can only contain letters, numbers and '_'.", this);
            messageBox.Message.Icon = MessageBoxImage.Warning;
            messageBox.Message.Problem = "Data labels must begin with a letter, followed only by letters, numbers, and '_'.";
            messageBox.Message.Result = "We will automatically ignore other characters, including spaces";
            messageBox.Message.Hint = "Start your label with a letter. Then use any combination of letters, numbers, and '_'.";
            messageBox.ShowDialog();
        }
        #endregion

        #region SpreadsheetAppearance
        // Generate the spreadsheet, adjusting the DateTime and UTCOffset visibility as needed
        private void GenerateSpreadsheet()
        {
            List<ControlRow> controlsInSpreadsheetOrder = this.templateDatabase.Controls.OrderBy(control => control.SpreadsheetOrder).ToList();
            this.dgSpreadsheet.Columns.Clear();

            // Find the DateTime Control, and the UtcOffset Control 
            ControlRow utcOffsetControl = null;
            ControlRow dateTimeControl = null;
            foreach (ControlRow control in controlsInSpreadsheetOrder)
            {
                if (control.Type == Constant.DatabaseColumn.DateTime)
                {
                    dateTimeControl = control;
                } 
                else if (control.Type == Constant.DatabaseColumn.UtcOffset)
                {
                    utcOffsetControl = control;
                }
                if (dateTimeControl != null && utcOffsetControl != null)
                {
                    break;
                }
            }

            // Now generate the spreadsheet columns as needed. We don't show the DateTime and UTCOffset column if both:
            // - the user is not using the UseUTCDateTimeSettings options
            // - the UtcOffset visibility is false
            foreach (ControlRow control in controlsInSpreadsheetOrder)
            {
                if (control.Type == Constant.DatabaseColumn.DateTime || control.Type == Constant.DatabaseColumn.UtcOffset)
                {
                    if (this.userSettings.ShowUtcOffset == false) 
                    {
                        continue;
                    }
                }
                DataGridTextColumn column = new DataGridTextColumn();
                string dataLabel = control.DataLabel;
                if (String.IsNullOrEmpty(dataLabel))
                {
                    Utilities.PrintFailure("GenerateSpreadsheet: Database constructors should guarantee data labels are not null.");
                }
                else
                {
                    column.Header = dataLabel;
                    this.dgSpreadsheet.Columns.Add(column);
                }
            }
        }

        // When the spreadsheet order is changd, update the database
        private void OnSpreadsheetOrderChanged(object sender, DataGridColumnEventArgs e)
        {
            DataGrid dataGrid = (DataGrid)sender;
            Dictionary<string, long> spreadsheetOrderByDataLabel = new Dictionary<string, long>();
            for (int control = 0; control < dataGrid.Columns.Count; control++)
            {
                string dataLabelFromColumnHeader = dataGrid.Columns[control].Header.ToString();
                long newSpreadsheetOrder = dataGrid.Columns[control].DisplayIndex + 1;
                spreadsheetOrderByDataLabel.Add(dataLabelFromColumnHeader, newSpreadsheetOrder);
            }
            this.templateDatabase.UpdateDisplayOrder(Constant.Control.SpreadsheetOrder, spreadsheetOrderByDataLabel);
        }
        #endregion

        #region Dragging and Dropping of Controls to Reorder them
        private void ControlsPanel_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.Source != this.ControlsPanel)
            {
                this.isMouseDown = true;
                this.mouseDownStartPosition = e.GetPosition(this.ControlsPanel);
            }
        }

        private void ControlsPanel_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                this.isMouseDown = false;
                this.isMouseDragging = false;
                if (!(this.realMouseDragSource == null))
                {
                    this.realMouseDragSource.ReleaseMouseCapture();
                }
            }
            catch
            {
            }
        }

        private void ControlsPanel_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (this.isMouseDown)
            {
                Point currentMousePosition = e.GetPosition(this.ControlsPanel);
                if ((this.isMouseDragging == false) &&
                    ((Math.Abs(currentMousePosition.X - this.mouseDownStartPosition.X) > SystemParameters.MinimumHorizontalDragDistance) ||
                     (Math.Abs(currentMousePosition.Y - this.mouseDownStartPosition.Y) > SystemParameters.MinimumVerticalDragDistance)))
                {
                    this.isMouseDragging = true;
                    this.realMouseDragSource = e.Source as UIElement;
                    this.realMouseDragSource.CaptureMouse();
                    DragDrop.DoDragDrop(this.dummyMouseDragSource, new DataObject("UIElement", e.Source, true), DragDropEffects.Move);
                }
            }
        }

        private void ControlsPanel_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("UIElement"))
            {
                e.Effects = DragDropEffects.Move;
            }
        }

        private void ControlsPanel_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("UIElement"))
            {
                UIElement dropTarget = e.Source as UIElement;
                int control = 0;
                int dropTargetIndex = -1;
                foreach (UIElement element in this.ControlsPanel.Children)
                {
                    if (element.Equals(dropTarget))
                    {
                        dropTargetIndex = control;
                        break;
                    }
                    else
                    {
                        // Check if its a stack panel, and if so check to see if its children are the drop target
                        StackPanel stackPanel = element as StackPanel;
                        if (stackPanel != null)
                        {
                            // Check the children...
                            foreach (UIElement subelement in stackPanel.Children)
                            {
                                if (subelement.Equals(dropTarget))
                                {
                                    dropTargetIndex = control;
                                    break;
                                }
                            }
                        }
                    }
                    control++;
                }
                if (dropTargetIndex != -1)
                {
                    StackPanel tsp = this.realMouseDragSource as StackPanel;
                    if (tsp == null)
                    {
                        StackPanel parent = FindVisualParent<StackPanel>(this.realMouseDragSource);
                        this.realMouseDragSource = parent;
                    }
                    this.ControlsPanel.Children.Remove(this.realMouseDragSource);
                    this.ControlsPanel.Children.Insert(dropTargetIndex, this.realMouseDragSource);
                    this.OnControlOrderChanged();
                }

                this.isMouseDown = false;
                this.isMouseDragging = false;
                this.realMouseDragSource.ReleaseMouseCapture();
            }
        }

        private static T FindVisualParent<T>(UIElement element) where T : UIElement
        {
            UIElement parent = element;
            while (parent != null)
            {
                T correctlyTyped = parent as T;
                if (correctlyTyped != null)
                {
                    return correctlyTyped;
                }
                parent = VisualTreeHelper.GetParent(parent) as UIElement;
            }
            return null;
        }

        private void OnControlOrderChanged()
        {
            Dictionary<string, long> newControlOrderByDataLabel = new Dictionary<string, long>();
            long controlOrder = 1;
            foreach (UIElement element in this.ControlsPanel.Children)
            {
                StackPanel stackPanel = element as StackPanel;
                if (stackPanel == null)
                {
                    continue;
                }
                newControlOrderByDataLabel.Add((string)stackPanel.Tag, controlOrder);
                controlOrder++;
            }

            this.templateDatabase.UpdateDisplayOrder(Constant.Control.ControlOrder, newControlOrderByDataLabel);
            this.controls.Generate(this, this.ControlsPanel, this.templateDatabase.Controls); // A contorted to make sure the controls panel updates itself
        }
        #endregion

        #region Template Drag and Drop
        // Dragging and dropping a .tdb file on the help window will open that file 
        // SAULXXX Seems esoteric - maybe delete this function. Likely not useful after Avalon dock added.
        private void HelpDocument_Drop(object sender, DragEventArgs dropEvent)
        {
            string templateDatabaseFilePath;
            if (Utilities.IsSingleTemplateFileDrag(dropEvent, out templateDatabaseFilePath))
            {
                this.InitializeDataGrid(templateDatabaseFilePath);
            }
        }

        private void HelpDocument_PreviewDrag(object sender, DragEventArgs dragEvent)
        {
            Utilities.OnHelpDocumentPreviewDrag(dragEvent);
        }
        #endregion
    }
}