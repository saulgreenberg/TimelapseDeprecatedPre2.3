using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Timelapse.Database;
using Timelapse.Dialog;
using Timelapse.Images;
using Timelapse.Util;
using Xceed.Wpf.Toolkit;

namespace Timelapse.Controls
{
    /// <summary>
    /// The code in here propagates values of a control across the various images in various ways.
    /// Note that this is control-type specific, which means this code would have to be modified to handle new control types
    /// Pay attention to the hacks described by SAULXXX DateTimePicker Workaround as these may not be needed if future versions of the DateTimePicker work as they are supposed to.
    /// </summary>
    public class DataEntryHandler : IDisposable
    {
        // Index location of these menu items in the context menu
        private const int PropagateFromLastValueIndex = 0;
        private const int CopyForwardIndex = 1;
        private const int CopyToAllIndex = 2;

        private bool disposed;

        public FileDatabase FileDatabase { get; private set; }
        public ImageCache ImageCache { get; private set; }
        public bool IsProgrammaticControlUpdate { get; set; }

        // We need to get selected files from the clickableimages grid, so we need this reference
        public ClickableImagesGrid ClickableImagesGrid { get; set; }
        public MarkableCanvas MarkableCanvas { get; set; }
        #region Loading, Disposing
        public DataEntryHandler(FileDatabase fileDatabase)
        {
            this.disposed = false;
            this.ImageCache = new ImageCache(fileDatabase);
            this.FileDatabase = fileDatabase;  // We need a reference to the database if we are going to update it.
            this.IsProgrammaticControlUpdate = false;
        }

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
                if (this.FileDatabase != null)
                {
                    this.FileDatabase.Dispose();
                }
            }
            this.disposed = true;
        }
        #endregion

        #region Configuration, including Callback Configuration
        public static void Configure(DateTimePicker dateTimePicker, Nullable<DateTime> defaultValue)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(dateTimePicker, nameof(dateTimePicker));

            dateTimePicker.AutoCloseCalendar = true;
            dateTimePicker.Format = DateTimeFormat.Custom;
            dateTimePicker.FormatString = Constant.Time.DateTimeDisplayFormat;
            dateTimePicker.TimeFormat = DateTimeFormat.Custom;
            dateTimePicker.TimeFormatString = Constant.Time.TimeFormat;
            dateTimePicker.CultureInfo = System.Globalization.CultureInfo.CreateSpecificCulture("en-US");
            dateTimePicker.Value = defaultValue;
        }

        /// <summary>
        /// Add data event handler callbacks for (possibly invisible) controls
        /// </summary>
        public void SetDataEntryCallbacks(Dictionary<string, DataEntryControl> controlsByDataLabel)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(controlsByDataLabel, nameof(controlsByDataLabel));

            // Add data entry callbacks to all editable controls. When the user changes a file's attribute using a particular control,
            // the callback updates the matching field for that file in the database.
            foreach (KeyValuePair<string, DataEntryControl> pair in controlsByDataLabel)
            {


                if (pair.Value.ContentReadOnly)
                {
                    continue;
                }

                string controlType = this.FileDatabase.FileTableColumnsByDataLabel[pair.Key].ControlType;
                switch (controlType)
                {
                    case Constant.Control.Note:
                    case Constant.DatabaseColumn.Date:
                    case Constant.DatabaseColumn.File:
                    case Constant.DatabaseColumn.Folder:
                    case Constant.DatabaseColumn.RelativePath:
                    case Constant.DatabaseColumn.Time:
                        DataEntryNote note = (DataEntryNote)pair.Value;
                        note.ContentControl.TextAutocompleted += this.NoteControl_TextAutocompleted;
                        if (controlType == Constant.Control.Note)
                        {
                            this.SetContextMenuCallbacks(note);
                        }
                        break;
                    case Constant.DatabaseColumn.DateTime:
                        // SAULXXX There are several issues with the XCEED DateTimePicker. In particular, the date in the 
                        // text date area is not well coordinated with the date in the calendar, i.e., the two aren't necessarily in
                        // sync. As well, changing a date on the calendar doesnt' appear to trigger the DateTimeContro_ValueChanged event
                        // Various workarounds are implemented as commented below with SAULXXX DateTimePicker Workaround.
                        // If the toolkit is updated to fix them, then those workarounds can be deleted (but test them first).
                        DataEntryDateTime dateTime = (DataEntryDateTime)pair.Value;
                        dateTime.ContentControl.ValueChanged += this.DateTimeControl_ValueChanged;

                        // SAULXXX DateTimePicker Workaround. 
                        // We need to access the calendar part of the DateTImePicker, but 
                        // we can't do that until the control is loaded.
                        dateTime.ContentControl.Loaded += this.DateTimePicker_Loaded;

                        // SAULXXX This was an old workaround to a DateTimePicker control issue, which I think is no longer needed due to updating WPFToolkit
                        // dateTime.ContentControl.MouseLeave += this.DateTime_MouseLeave; 
                        break;
                    case Constant.DatabaseColumn.UtcOffset:
                        DataEntryUtcOffset utcOffset = (DataEntryUtcOffset)pair.Value;
                        utcOffset.ContentControl.ValueChanged += this.UtcOffsetControl_ValueChanged;
                        break;
                    case Constant.DatabaseColumn.DeleteFlag:
                    case Constant.Control.Flag:
                        DataEntryFlag flag = (DataEntryFlag)pair.Value;
                        flag.ContentControl.Checked += this.FlagControl_CheckedChanged;
                        flag.ContentControl.Unchecked += this.FlagControl_CheckedChanged;
                        this.SetContextMenuCallbacks(flag);
                        break;
                    case Constant.DatabaseColumn.ImageQuality:
                    case Constant.Control.FixedChoice:
                        DataEntryChoice choice = (DataEntryChoice)pair.Value;
                        choice.ContentControl.SelectionChanged += this.ChoiceControl_SelectionChanged;
                        this.SetContextMenuCallbacks(choice);
                        break;
                    case Constant.Control.Counter:
                        DataEntryCounter counter = (DataEntryCounter)pair.Value;
                        counter.ContentControl.ValueChanged += this.CounterControl_ValueChanged;
                        this.SetContextMenuCallbacks(counter);
                        break;
                    default:
                        break;
                }
            }
        }

        // SAULXXX DateTimePicker Workaround. 
        // Access the calendar part of the datetimepicker, and
        // add an event to it that is triggered whenever the user changes the calendar.
        // For convenience, we use the calendar's tag to store the DateTimePicker control so we can retrieve it from the event.
        private void DateTimePicker_Loaded(object sender, RoutedEventArgs e)
        {
            DateTimePicker dateTimePicker = sender as DateTimePicker;
            if (dateTimePicker.Template.FindName("PART_Calendar", dateTimePicker) is Calendar calendar)
            {
                // System.Diagnostics.Debug.Print("DateTimePicker_Loaded: Adding calendar event ");
                calendar.Tag = dateTimePicker;
                calendar.IsTodayHighlighted = false; // Don't highlight today's date, as it could be confusing given what this control is used for.
                calendar.SelectedDatesChanged += this.Calendar_SelectedDatesChanged;
            }
            // else
            // {
            //    System.Diagnostics.Debug.Print("DateTimePicker_Loaded: Couldnt add calendar event ");
            // }
        }

        private void SetContextMenuCallbacks(DataEntryControl control)
        {
            MenuItem menuItemPropagateFromLastValue = new MenuItem()
            {
                IsCheckable = false,
                Tag = control,
                Header = "Propagate from the last non-empty value to here"
            };
            if (control is DataEntryCounter)
            {
                menuItemPropagateFromLastValue.Header = "Propagate from the last non-zero value to here";
            }
            menuItemPropagateFromLastValue.Click += this.MenuItemPropagateFromLastValue_Click;

            MenuItem menuItemCopyForward = new MenuItem()
            {
                IsCheckable = false,
                Header = "Copy forward to end",
                ToolTip = "The value of this field will be copied forward from this file to the last file in this set",
                Tag = control
            };
            menuItemCopyForward.Click += this.MenuItemPropagateForward_Click;
            MenuItem menuItemCopyCurrentValue = new MenuItem()
            {
                IsCheckable = false,
                Header = "Copy to all",
                Tag = control
            };
            menuItemCopyCurrentValue.Click += this.MenuItemCopyCurrentValueToAll_Click;

            // DataEntrHandler.PropagateFromLastValueIndex and CopyForwardIndex must be kept in sync with the add order here
            ContextMenu menu = new ContextMenu();
            menu.Items.Add(menuItemPropagateFromLastValue);
            menu.Items.Add(menuItemCopyForward);
            menu.Items.Add(menuItemCopyCurrentValue);

            control.Container.ContextMenu = menu;
            control.Container.PreviewMouseRightButtonDown += this.Container_PreviewMouseRightButtonDown;

            if (control is DataEntryCounter counter)
            {
                counter.ContentControl.ContextMenu = menu;
            }
            else if (control is DataEntryNote note)
            {
                note.ContentControl.ContextMenu = menu;
            }
            else if (control is DataEntryChoice choice)
            {
                choice.ContentControl.ContextMenu = menu;
            }
            else if (control is DataEntryFlag flag)
            {
                flag.ContentControl.ContextMenu = menu;
            }
            else
            {
                throw new NotSupportedException(String.Format("Unhandled control type {0}.", control.GetType().Name));
            }
        }
        #endregion

        #region Context menu event handlers
        // Menu selections for propagating or copying the current value of this control to all images

        // Copy the last non-empty value in this control preceding this file up to the current image
        protected virtual void MenuItemPropagateFromLastValue_Click(object sender, RoutedEventArgs e)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(sender, nameof(sender));

            // Get the chosen data entry control
            DataEntryControl control = (DataEntryControl)((MenuItem)sender).Tag;
            if (control == null)
            {
                return;
            }

            bool checkForZero = control is DataEntryCounter;
            bool isFlag = control is DataEntryFlag;
            int indexToCopyFrom = -1;
            ImageRow valueSource = null;
            string valueToCopy = checkForZero ? "0" : String.Empty;

            // Search for the row with some value in it, starting from the previous row
            int currentRowIndex = (this.ClickableImagesGrid.IsVisible == false) ? this.ImageCache.CurrentRow : this.ClickableImagesGrid.GetSelected()[0];
            for (int previousIndex = currentRowIndex - 1; previousIndex >= 0; previousIndex--)
            {
                ImageRow file = this.FileDatabase.FileTable[previousIndex];
                valueToCopy = file.GetValueDatabaseString(control.DataLabel);
                if (valueToCopy == null)
                {
                    continue;
                }
                valueToCopy = valueToCopy.Trim();
                if (valueToCopy.Length > 0)
                {
                    if ((checkForZero && !valueToCopy.Equals("0")) ||             // Skip over non-zero values for counters
                        (isFlag && !valueToCopy.Equals(Constant.BooleanValue.False, StringComparison.OrdinalIgnoreCase)) || // Skip over false values for flags
                        (!checkForZero && !isFlag))
                    {
                        indexToCopyFrom = previousIndex;    // We found a non-empty value
                        valueSource = file;
                        break;
                    }
                }
            }

            string newContent = valueToCopy;
            if (indexToCopyFrom < 0)
            {
                // Display a dialog box saying there is nothing to propagate. 
                // Note that this should never be displayed, as the menu shouldn't be highlit if there is nothing to propagate
                // But just in case...
                Dialogs.DataEntryNothingToPropagateDialog(Application.Current.MainWindow);
                newContent = this.FileDatabase.FileTable[this.ImageCache.CurrentRow].GetValueDisplayString(control.DataLabel); // No change, so return the current value
            }

            // Display the appropriate dialog box that explains what will happen. Arguments indicate what is to be propagated and how many files will be affected
            int filesAffected = currentRowIndex - indexToCopyFrom;
            if (Dialogs.DataEntryConfirmPropagateFromLastValueDialog(Application.Current.MainWindow, valueToCopy, filesAffected) != true)
            {
                newContent = this.FileDatabase.FileTable[currentRowIndex].GetValueDisplayString(control.DataLabel); // No change, so return the current value
            }

            // Update the affected files. Note that we start on the row after the one with a value in it to the current row.
            Mouse.OverrideCursor = Cursors.Wait;
            this.FileDatabase.UpdateFiles(valueSource, control.DataLabel, indexToCopyFrom + 1, currentRowIndex);
            control.SetContentAndTooltip(newContent);
            Mouse.OverrideCursor = null;
        }

        // Copy the current value of this control to all images
        protected virtual void MenuItemCopyCurrentValueToAll_Click(object sender, RoutedEventArgs e)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(sender, nameof(sender));

            // Get the chosen data entry control
            DataEntryControl control = (DataEntryControl)((MenuItem)sender).Tag;
            if (control == null)
            {
                return;
            }

            // Display a dialog box that explains what will happen. Arguments indicate how many files will be affected, and is tuned to the type of control 
            bool checkForZero = control is DataEntryCounter;
            int filesAffected = this.FileDatabase.CountAllCurrentlySelectedFiles;
            if (Dialogs.DataEntryConfirmCopyCurrentValueToAllDialog(Application.Current.MainWindow, control.Content, filesAffected, checkForZero) != true)
            {
                return;
            }

            // Update all files to match the value of the control (identified by the data label) in the currently selected image row.
            Mouse.OverrideCursor = Cursors.Wait;
            ImageRow imageRow = (this.ClickableImagesGrid.IsVisible == false) ? this.ImageCache.Current : this.FileDatabase.FileTable[this.ClickableImagesGrid.GetSelected()[0]];
            this.FileDatabase.UpdateFiles(imageRow, control.DataLabel);
            Mouse.OverrideCursor = null;
        }

        // Propagate the current value of this control forward from this point across the current set of selected images
        protected virtual void MenuItemPropagateForward_Click(object sender, RoutedEventArgs e)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(sender, nameof(sender));

            // Get the chosen data entry control
            DataEntryControl control = (DataEntryControl)((MenuItem)sender).Tag;
            if (control == null)
            {
                return;
            }

            int currentRowIndex = (this.ClickableImagesGrid.IsVisible == false) ? this.ImageCache.CurrentRow : this.ClickableImagesGrid.GetSelected()[0];
            int imagesAffected = this.FileDatabase.CountAllCurrentlySelectedFiles - currentRowIndex - 1;
            if (imagesAffected == 0)
            {
                // Display a dialog box saying there is nothing to copy forward. 
                // Note that this should never be displayed, as the menu shouldn't be highlit if we are on the last image
                // But just in case...
                Dialogs.DataEntryNothingToCopyForwardDialog(Application.Current.MainWindow);
                return;
            }

            // Display the appropriate dialog box that explains what will happen. Arguments indicate how many files will be affected, and is tuned to the type of control 
            ImageRow imageRow = (this.ClickableImagesGrid.IsVisible == false) ? this.ImageCache.Current : this.FileDatabase.FileTable[this.ClickableImagesGrid.GetSelected()[0]];
            string valueToCopy = imageRow.GetValueDisplayString(control.DataLabel);
            bool checkForZero = control is DataEntryCounter;
            if (Dialogs.DataEntryConfirmCopyForwardDialog(Application.Current.MainWindow, valueToCopy, imagesAffected, checkForZero) != true)
            {
                return;
            }

            // Update the files from the next row (as we are copying from the current row) to the end.
            Mouse.OverrideCursor = Cursors.Wait;
            int nextRowIndex = (this.ClickableImagesGrid.IsVisible == false) ? this.ImageCache.CurrentRow + 1 : this.ClickableImagesGrid.GetSelected()[0] + 1;
            this.FileDatabase.UpdateFiles(imageRow, control.DataLabel, nextRowIndex, this.FileDatabase.CountAllCurrentlySelectedFiles - 1);
            Mouse.OverrideCursor = null;
        }

        // Enable or disable particular context menu items
        protected virtual void Container_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(sender, nameof(sender));

            StackPanel stackPanel = (StackPanel)sender;
            DataEntryControl control = (DataEntryControl)stackPanel.Tag;

            MenuItem menuItemCopyToAll = (MenuItem)stackPanel.ContextMenu.Items[DataEntryHandler.CopyToAllIndex];
            MenuItem menuItemCopyForward = (MenuItem)stackPanel.ContextMenu.Items[DataEntryHandler.CopyForwardIndex];
            MenuItem menuItemPropagateFromLastValue = (MenuItem)stackPanel.ContextMenu.Items[DataEntryHandler.PropagateFromLastValueIndex];

            // Behaviour: 
            // - if the clickable image is visible, disable Copy to all / Copy forward / Propagate if a single item isn't selected
            // - otherwise enable the menut item only if the resulting action is coherent
            bool enabledIsPossible = this.ClickableImagesGrid.IsVisible == false || this.ClickableImagesGrid.SelectedCount() == 1;
            menuItemCopyToAll.IsEnabled = enabledIsPossible;
            menuItemCopyForward.IsEnabled = enabledIsPossible ? menuItemCopyForward.IsEnabled = this.IsCopyForwardPossible() : false;
            menuItemPropagateFromLastValue.IsEnabled = enabledIsPossible ? this.IsCopyFromLastNonEmptyValuePossible(control) : false;
        }
        #endregion

        #region Helpers for Copy Forward/Backwards etc.
        public bool IsCopyForwardPossible()
        {
            if (this.ImageCache.Current == null)
            {
                return false;
            }

            int filesAffected = this.FileDatabase.CountAllCurrentlySelectedFiles - this.ImageCache.CurrentRow - 1;
            return (filesAffected > 0) ? true : false;
        }

        // Return true if there is a non-empty value available
        public bool IsCopyFromLastNonEmptyValuePossible(DataEntryControl control)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(control, nameof(control));

            bool checkForZero = control is DataEntryCounter;
            int nearestRowWithCopyableValue = -1;
            for (int fileIndex = this.ImageCache.CurrentRow - 1; fileIndex >= 0; fileIndex--)
            {
                // Search for the row with some value in it, starting from the previous row
                string valueToCopy = this.FileDatabase.FileTable[fileIndex].GetValueDatabaseString(control.DataLabel);
                if (String.IsNullOrWhiteSpace(valueToCopy) == false)
                {
                    if ((checkForZero && !valueToCopy.Equals("0")) || !checkForZero)
                    {
                        nearestRowWithCopyableValue = fileIndex;    // We found a non-empty value
                        break;
                    }
                }
            }
            return (nearestRowWithCopyableValue >= 0) ? true : false;
        }
        #endregion

        #region Event handlers - Content Selections and Changes

        // SAULXXX This was an old workaround to a DateTimePicker control issue, which I think is no longer needed due to updating WPFToolkit
        // SAULXXX The original issue was reported in  https://github.com/xceedsoftware/wpftoolkit/issues/1206 
        // SAULXXX Delete this in future Timelapse versions
        // The DateTimePicker has a 'bug' where it does not trigger the value update event unless a return has been pressed (or similar)
        // As this does not always happen, this means some text changes don't actually get remembered. 
        // This workaround checks to see if the mouse has left the DateTimePicker. If so, it checks for changes to the date/time and updates
        // the values correctly. 
        private void DateTime_MouseLeave(object sender, MouseEventArgs e)
        {
            // System.Diagnostics.Debug.Print("DateTimeControl_MouseLeave triggered");
            DateTimePicker dateTimePicker = sender as DateTimePicker;
            if (dateTimePicker.Value == null)
            {
                return;
            }
            DateTime oldDateTime = dateTimePicker.Value.Value;

            // SAULXXX: Try to parse the new datetime. If we cannot, then don't do anything.
            // This is not the best solution, as it means some changes are ignored. But we don't really have much choice here.
            // Otherwise, if the dates differe, update using the new date.
            if (Util.DateTimeHandler.TryParseDisplayDateTimeString(dateTimePicker.Text, out DateTime newDateTime) &&
                (oldDateTime != newDateTime))
            {
                this.DateTimeUpdate(dateTimePicker, newDateTime);
            }
        }

        private void DateTimeControl_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            // System.Diagnostics.Debug.Print("DateTimeControl_ValueChanged triggered");
            if (this.IsProgrammaticControlUpdate)
            {
                return;
            }

            DateTimePicker dateTimePicker = (DateTimePicker)sender;
            if (dateTimePicker.Value.HasValue == false)
            {
                return;
            }
            // Update file data table and write the new DateTime, Date, and Time to the database
            this.DateTimeUpdate(dateTimePicker, dateTimePicker.Value.Value);

            // SAULXXX DateTimePicker Workaround. 
            // There is a bug (?) in the dateTimePicker where it doesn't update the calendar to the
            // changed date. This means that if you open the calendar it shows the
            // original date, and when you close it (even without selecting a date) it reverts to the old date.
            // The fix below updates the calendar to the current date.
            if (dateTimePicker.Template.FindName("PART_Calendar", dateTimePicker) is Calendar calendar)
            {
                this.IsProgrammaticControlUpdate = true;
                // System.Diagnostics.Debug.Print("Got it " + calendar.ToString());
                calendar.DisplayDate = dateTimePicker.Value.Value;
                calendar.SelectedDate = dateTimePicker.Value.Value;
                if (calendar.Template.FindName("PART_TimePicker", calendar) is TimePicker timepicker)
                {
                    timepicker.Value = dateTimePicker.Value.Value;
                    // System.Diagnostics.Debug.Print("Setting Time pickker");
                }
                this.IsProgrammaticControlUpdate = false;
            }
            // else
            // {
            //    System.Diagnostics.Debug.Print("Not a calendar");
            // }
        }

        // SAULXXX DateTimePicker Workaround. 
        // Sync changes from the datetimepicker's calendar back to the datetimepicker text 
        // and updates the database
        private void Calendar_SelectedDatesChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.IsProgrammaticControlUpdate)
            {
                return;
            }
            Calendar calendar = sender as Calendar;
            DateTimePicker dateTimePicker = (DateTimePicker)calendar.Tag;
            TimeSpan timespan = dateTimePicker.Value.Value.TimeOfDay;
            this.IsProgrammaticControlUpdate = true;
            dateTimePicker.Value = calendar.SelectedDate + timespan; // + dateTimePicker.Value.Value.TimeOfDay;
            // Update file data table and write the new DateTime, Date, and Time to the database
            this.DateTimeUpdate(dateTimePicker, (DateTime)dateTimePicker.Value);

            // System.Diagnostics.Debug.Print("Got calendar event " + calendar.SelectedDate.ToString());
            this.IsProgrammaticControlUpdate = false;
        }

        // Helper method for above DateTime changes.
        private void DateTimeUpdate(DateTimePicker dateTimePicker, DateTime dateTime)
        {
            // update file data table and write the new DateTime, Date, and Time to the database
            this.ImageCache.Current.SetDateTimeOffset(dateTime);
            dateTimePicker.ToolTip = DateTimeHandler.ToDisplayDateTimeString(dateTime);

            List<ColumnTuplesWithWhere> imageToUpdate = new List<ColumnTuplesWithWhere>() { this.ImageCache.Current.GetDateTimeColumnTuples() };
            this.FileDatabase.UpdateFiles(imageToUpdate);

            // update date and time controls if they're displayed
            DataEntryDateTime control = (DataEntryDateTime)dateTimePicker.Tag;
            if (control.DateControl != null)
            {
                control.DateControl.SetContentAndTooltip(this.ImageCache.Current.Date);
            }
            if (control.TimeControl != null)
            {
                control.TimeControl.SetContentAndTooltip(this.ImageCache.Current.Time);
            }
        }

        // When the UTC in the UTC box changes, update the UTC field(s) in the database 
        private void UtcOffsetControl_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (this.IsProgrammaticControlUpdate)
            {
                return;
            }
            UtcOffsetUpDown utcOffsetPicker = (UtcOffsetUpDown)sender;
            if (utcOffsetPicker.Value.HasValue == false)
            {
                return;
            }

            DateTimeOffset currentImageDateTime = this.ImageCache.Current.DateTimeIncorporatingOffset;
            DateTimeOffset newImageDateTime = currentImageDateTime.SetOffset(utcOffsetPicker.Value.Value);
            this.ImageCache.Current.SetDateTimeOffset(newImageDateTime);
            // System.Diagnostics.Debug.Print(newImageDateTime.ToString());
            List<ColumnTuplesWithWhere> imageToUpdate = new List<ColumnTuplesWithWhere>() { this.ImageCache.Current.GetDateTimeColumnTuples() };
            this.FileDatabase.UpdateFiles(imageToUpdate);  // write the new UtcOffset to the database
        }

        // When the text in a particular note box changes, update the particular note field(s) in the database 
        private void NoteControl_TextAutocompleted(object sender, TextChangedEventArgs e)
        {
            if (this.IsProgrammaticControlUpdate)
            {
                return;
            }

            DataEntryNote control = (DataEntryNote)((TextBox)sender).Tag;
            control.ContentChanged = true;

            // Note that  trailing whitespace is removed only from the database as further edits may use it.
            this.UpdateRowsDependingOnClickableImageGridState(control.DataLabel, control.Content.Trim());
        }

        // When the number in a particular counter box changes, update the particular counter field(s) in the database
        private void CounterControl_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (this.IsProgrammaticControlUpdate)
            {
                return;
            }
            IntegerUpDown integerUpDown = (IntegerUpDown)sender;

            // Get the key identifying the control, and then add its value to the database
            DataEntryControl control = (DataEntryControl)integerUpDown.Tag;
            control.SetContentAndTooltip(integerUpDown.Value.ToString());
            this.UpdateRowsDependingOnClickableImageGridState(control.DataLabel, control.Content);
        }

        // When a choice changes, update the particular choice field(s) in the database
        private void ChoiceControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.IsProgrammaticControlUpdate)
            {
                return;
            }
            ComboBox comboBox = (ComboBox)sender;

            if (comboBox.SelectedItem == null)
            {
                // no item selected (probably the user cancelled)
                return;
            }

            // Get the key identifying the control, and then add its value to the database
            DataEntryControl control = (DataEntryControl)comboBox.Tag;
            control.SetContentAndTooltip(((ComboBoxItem)comboBox.SelectedItem).Content.ToString());
            this.UpdateRowsDependingOnClickableImageGridState(control.DataLabel, control.Content);
        }

        // When a flag changes, update the particular flag field(s) in the database
        private void FlagControl_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (this.IsProgrammaticControlUpdate)
            {
                return;
            }
            CheckBox checkBox = (CheckBox)sender;
            DataEntryControl control = (DataEntryControl)checkBox.Tag;
            string value = ((bool)checkBox.IsChecked) ? Constant.BooleanValue.True : Constant.BooleanValue.False;

            control.SetContentAndTooltip(value);
            this.UpdateRowsDependingOnClickableImageGridState(control.DataLabel, control.Content);
        }
        #endregion

        #region Update Rows
        // Update either the current row or the selected rows in the database, 
        // depending upon whether we are in the single image or  the ClickableImagesGrid view respectively.
        private void UpdateRowsDependingOnClickableImageGridState(string datalabel, string content)
        {
            if (this.ClickableImagesGrid.IsVisible == false && this.MarkableCanvas.ClickableImagesState == 0)
            {
                // Only a single image is displayed: update the database for the current row with the control's value
                this.FileDatabase.UpdateFile(this.ImageCache.Current.ID, datalabel, content);
            }
            else
            {
                // Multiple images are displayed: update the database for all selected rows with the control's value
                this.FileDatabase.UpdateFiles(this.ClickableImagesGrid.GetSelected(), datalabel, content.Trim());
            }
        }
        #endregion

        #region Utilities
        public static bool TryFindFocusedControl(IInputElement focusedElement, out DataEntryControl focusedControl)
        {
            if (focusedElement is FrameworkElement focusedFrameworkElement)
            {
                focusedControl = (DataEntryControl)focusedFrameworkElement.Tag;
                if (focusedControl != null)
                {
                    return true;
                }

                // for complex controls which dynamic generate child controls, such as date time pickers, the tag of the focused element can't be set
                // so try to locate a parent of the focused element with a tag indicating the control
                FrameworkElement parent = null;
                if (focusedFrameworkElement.Parent != null && focusedFrameworkElement.TemplatedParent is FrameworkElement)
                {
                    parent = (FrameworkElement)focusedFrameworkElement.Parent;
                }
                else if (focusedFrameworkElement.TemplatedParent != null && focusedFrameworkElement.TemplatedParent is FrameworkElement)
                {
                    parent = (FrameworkElement)focusedFrameworkElement.TemplatedParent;
                }

                if (parent != null)
                {
                    return DataEntryHandler.TryFindFocusedControl(parent, out focusedControl);
                }
            }
            focusedControl = null;
            return false;
        }

        // If the is a common (trimmed) data value for the provided data label in the given fileIDs, return that value, otherwise null.
        public string GetValueDisplayStringCommonToFileIds(string dataLabel)
        {
            List<int> fileIds = this.ClickableImagesGrid.GetSelected();
            // There used to be a bug in this code, which resulted from this being invoked in SwitchToClickableGridView() when the grid was already being displayed.
            //  I have kept the try/catch in just in case it rears its ugly head elsewhere. Commented out Debug statements are here just in case we need to reexamine it.
            try
            {
                // If there are no file ids, there is nothing to show
                if (fileIds.Count == 0)
                {
                    return null;
                }

                // This can cause the crash, when the id in fileIds[0] doesn't exist
                ImageRow imageRow = this.FileDatabase.FileTable[fileIds[0]];

                // The above line is what causes the crash, when the id in fileIds[0] doesn't exist
                // System.Diagnostics.Debug.Print("Success: " + dataLabel + ": " + fileIds[0]);

                string contents = imageRow.GetValueDisplayString(dataLabel);
                contents = contents.Trim();

                // If the values of success imagerows (as defined by the fileIDs) are the same as the first one,
                // then return that as they all have a common value. Otherwise return an empty string.
                for (int i = 1; i < fileIds.Count; i++)
                {
                    imageRow = this.FileDatabase.FileTable[fileIds[i]];
                    string new_contents = imageRow.GetValueDisplayString(dataLabel);
                    new_contents = new_contents.Trim();
                    if (new_contents != contents)
                    {
                        // We have a mismatch
                        return null;
                    }
                }
                // All values match
                return contents;
            }
            catch
            {
                // This catch occurs when the id in fileIds[0] doesn't exist
                System.Diagnostics.Debug.Write("Catch in GetValueDisplayStringCommonToFileIds: " + dataLabel);
                return null;
            }
        }
        #endregion

        #region Static public methods
        // Get a file path from the global datahandler. 
        // If we can't, or if it does not exist, return String.Empty
        public static string TryGetFilePathFromGlobalDataHandler()
        {
            string path;
            // If anything is null, we defer resetting anything. Note that we may get an update later (e.g., via the timer)
            DataEntryHandler handler = Util.GlobalReferences.MainWindow?.DataHandler;
            if (handler?.ImageCache?.CurrentDifferenceState != null && handler?.FileDatabase != null)
            {
                // Get the path
                path = handler.ImageCache.Current.GetFilePath(handler.FileDatabase.FolderPath);
                return File.Exists(path) ? path : null;
            }
            return null;
        }
        #endregion
    }
}
