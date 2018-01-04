using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Timelapse.Database;
using Timelapse.Images;
using Timelapse.Util;
using Xceed.Wpf.Toolkit;
using MessageBox = Timelapse.Dialog.MessageBox;

namespace Timelapse.Controls
{
    /// <summary>
    /// The code in here propagates values of a control across the various images in various ways.
    /// Note that this is control-type specific, which means this code would have to be modified to handle new control types
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
            dateTimePicker.AutoCloseCalendar = true;
            dateTimePicker.Format = DateTimeFormat.Custom;
            dateTimePicker.FormatString = Constant.Time.DateTimeDisplayFormat;
            dateTimePicker.TimeFormat = DateTimeFormat.Custom;
            dateTimePicker.TimeFormatString = Constant.Time.TimeFormat;
            dateTimePicker.Value = defaultValue;
        }
        
        /// <summary>
        /// Add data event handler callbacks for (possibly invisible) controls
        /// </summary>
        public void SetDataEntryCallbacks(Dictionary<string, DataEntryControl> controlsByDataLabel)
        {
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
                        DataEntryDateTime dateTime = (DataEntryDateTime)pair.Value;
                        dateTime.ContentControl.ValueChanged += this.DateTimeControl_ValueChanged;
                        dateTime.ContentControl.MouseLeave += this.DateTime_MouseLeave;
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
                        if (controlType == Constant.Control.FixedChoice)
                        {
                            this.SetContextMenuCallbacks(choice);
                        }
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
            menuItemCopyCurrentValue.Click += this.MenuItemCopyCurrentValue_Click;

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

        #region Copy Forward/Backwards etc.
        /// <summary>Propagate the current value of the current control forward from this point across the current set of selected images.</summary>
        public void CopyForward(string dataLabel, bool checkForZero)
        {
            int currentRowIndex = (this.ClickableImagesGrid.IsVisible == false) ? this.ImageCache.CurrentRow : this.ClickableImagesGrid.GetSelected()[0];
            int imagesAffected = this.FileDatabase.CurrentlySelectedFileCount - currentRowIndex - 1;
            if (imagesAffected == 0)
            {
                // Nothing to propagate. Note that we shouldn't really see this, as the menu shouldn't be highlit if we are on the last image
                // But just in case...
                MessageBox messageBox = new MessageBox("Nothing to copy forward.", Application.Current.MainWindow);
                messageBox.Message.Icon = MessageBoxImage.Exclamation;
                messageBox.Message.Reason = "As you are on the last file, there are no files after this.";
                messageBox.ShowDialog();
                return;
            }

            ImageRow imageRow = (this.ClickableImagesGrid.IsVisible == false) ? this.ImageCache.Current : this.FileDatabase.Files[this.ClickableImagesGrid.GetSelected()[0]];
            int nextRowIndex = (this.ClickableImagesGrid.IsVisible == false) ? this.ImageCache.CurrentRow + 1 : this.ClickableImagesGrid.GetSelected()[0] + 1;
            string valueToCopy = imageRow.GetValueDisplayString(dataLabel);
            if (this.ConfirmCopyForward(valueToCopy, imagesAffected, checkForZero) != true)
            {
                return;
            }

            // Update the files from the next row (as we are copying from the current row) to the end.
            this.FileDatabase.UpdateFiles(imageRow, dataLabel, nextRowIndex, this.FileDatabase.CurrentlySelectedFileCount - 1);
        }

        /// <summary>
        /// Copy the last non-empty value in this control preceding this file up to the current image
        /// </summary>
        public string CopyFromLastNonEmptyValue(DataEntryControl control)
        {
            bool checkForZero = control is DataEntryCounter;
            bool isFlag = control is DataEntryFlag;

            int indexToCopyFrom = -1;
            ImageRow valueSource = null;
            string valueToCopy = checkForZero ? "0" : String.Empty;
            int currentRowIndex = (this.ClickableImagesGrid.IsVisible == false) ? this.ImageCache.CurrentRow : this.ClickableImagesGrid.GetSelected()[0];
            for (int previousIndex = currentRowIndex - 1; previousIndex >= 0; previousIndex--)
            {
                // Search for the row with some value in it, starting from the previous row
                ImageRow file = this.FileDatabase.Files[previousIndex];
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

            if (indexToCopyFrom < 0)
            {
                // Nothing to propagate.  If the menu item is deactivated as expected, this should never be triggered.
                MessageBox messageBox = new MessageBox("Nothing to Propagate to Here.", Application.Current.MainWindow);
                messageBox.Message.Icon = MessageBoxImage.Exclamation;
                messageBox.Message.Reason = "All the earlier files have nothing in this field, so there are no values to propagate.";
                messageBox.ShowDialog();
                return this.FileDatabase.Files[this.ImageCache.CurrentRow].GetValueDisplayString(control.DataLabel); // No change, so return the current value
            }

            int filesAffected = currentRowIndex - indexToCopyFrom;
            if (this.ConfirmPropagateFromLastValue(valueToCopy, filesAffected) != true)
            {
                return this.FileDatabase.Files[currentRowIndex].GetValueDisplayString(control.DataLabel); // No change, so return the current value
            }

            // Update. Note that we start on the next row, as we are copying from the current row.
            this.FileDatabase.UpdateFiles(valueSource, control.DataLabel, indexToCopyFrom + 1, currentRowIndex);
            return valueToCopy;
        }

        /// <summary>Copy the current value of this control to all images</summary>
        public void CopyToAll(DataEntryControl control)
        {
            bool checkForZero = control is DataEntryCounter;
            int filesAffected = this.FileDatabase.CurrentlySelectedFileCount;
            
            string displayValueToCopy = control.Content;

            if (this.ConfirmCopyCurrentValueToAll(displayValueToCopy, filesAffected, checkForZero) != true)
            {
                return;
            }

            // WE  SHOULD ALLOW THE COPY TO ALL IF ALL THE SELECTED VALUES ARE THE SAME - CHANGE RESTRICTIVE CODE TO TEST FOR THIS?
            // BUT WE NEED TO DIFFERENTIATE BETWEEN BLANKS AND DISABLED.

            // Get the currently selected image row
            ImageRow imageRow = (this.ClickableImagesGrid.IsVisible == false) ? this.ImageCache.Current : this.FileDatabase.Files[this.ClickableImagesGrid.GetSelected()[0]];
            this.FileDatabase.UpdateFiles(imageRow, control.DataLabel);
        }

        public bool IsCopyForwardPossible(DataEntryControl control)
        {
            if (this.ImageCache.Current == null)
            {
                return false;
            }

            int filesAffected = this.FileDatabase.CurrentlySelectedFileCount - this.ImageCache.CurrentRow - 1;
            return (filesAffected > 0) ? true : false;
        }

        // Return true if there is a non-empty value available
        public bool IsCopyFromLastNonEmptyValuePossible(DataEntryControl control)
        {
            bool checkForZero = control is DataEntryCounter;
            int nearestRowWithCopyableValue = -1;
            for (int fileIndex = this.ImageCache.CurrentRow - 1; fileIndex >= 0; fileIndex--)
            {
                // Search for the row with some value in it, starting from the previous row
                string valueToCopy = this.FileDatabase.Files[fileIndex].GetValueDatabaseString(control.DataLabel);
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
      
        #region Confirmation Dialogs for Copy Forward/Backwards, etc
        // Ask the user to confirm value propagation from the last value
        private bool? ConfirmCopyForward(string text, int imagesAffected, bool checkForZero)
        {
            text = text.Trim();

            MessageBox messageBox = new MessageBox("Please confirm 'Copy Forward' for this field...", Application.Current.MainWindow, MessageBoxButton.YesNo);
            messageBox.Message.Icon = MessageBoxImage.Question;
            messageBox.Message.What = "Copy Forward is not undoable, and can overwrite existing values.";
            messageBox.Message.Result = "If you select yes, this operation will:" + Environment.NewLine;
            if (!checkForZero && text.Equals(String.Empty))
            {
                messageBox.Message.Result += "\u2022 copy the (empty) value \u00AB" + text + "\u00BB in this field from here to the last file of your selected files.";
            }
            else
            {
                messageBox.Message.Result += "\u2022 copy the value \u00AB" + text + "\u00BB in this field from here to the last file of your selected files.";
            }
            messageBox.Message.Result += Environment.NewLine + "\u2022 over-write any existing data values in those fields";
            messageBox.Message.Result += Environment.NewLine + "\u2022 will affect " + imagesAffected.ToString() + " files.";
            return messageBox.ShowDialog();
        }

        // Ask the user to confirm value propagation to all selected files
        private bool? ConfirmCopyCurrentValueToAll(String text, int filesAffected, bool checkForZero)
        {
            text = text.Trim();

            MessageBox messageBox = new MessageBox("Please confirm 'Copy to All' for this field...", Application.Current.MainWindow, MessageBoxButton.YesNo);
            messageBox.Message.Icon = MessageBoxImage.Question;
            messageBox.Message.What = "Copy to All is not undoable, and can overwrite existing values.";
            messageBox.Message.Result = "If you select yes, this operation will:" + Environment.NewLine;
            if (!checkForZero && text.Equals(String.Empty))
            {
                messageBox.Message.Result += "\u2022 clear this field across all " + filesAffected.ToString() + " of your selected files.";
            }
            else
            {
                messageBox.Message.Result += "\u2022 set this field to \u00AB" + text + "\u00BB across all " + filesAffected.ToString() + " of your selected files.";
            }
            messageBox.Message.Result += Environment.NewLine + "\u2022 over-write any existing data values in those fields";
            return messageBox.ShowDialog();
        }

        // Ask the user to confirm value propagation from the last value
        private bool? ConfirmPropagateFromLastValue(String text, int imagesAffected)
        {
            text = text.Trim();
            MessageBox messageBox = new MessageBox("Please confirm 'Propagate to Here' for this field.", Application.Current.MainWindow, MessageBoxButton.YesNo);
            messageBox.Message.Icon = MessageBoxImage.Question;
            messageBox.Message.What = "Propagate to Here is not undoable, and can overwrite existing values.";
            messageBox.Message.Reason = "\u2022 The last non-empty value \u00AB" + text + "\u00BB was seen " + imagesAffected.ToString() + " files back." + Environment.NewLine;
            messageBox.Message.Reason += "\u2022 That field's value will be copied across all files between that file and this one of your selected files";
            messageBox.Message.Result = "If you select yes: " + Environment.NewLine;
            messageBox.Message.Result = "\u2022 " + imagesAffected.ToString() + " files will be affected.";
            return messageBox.ShowDialog();
        }
        #endregion

        #region Event handlers - Content Selections and Changes

        // The DateTimePicker has a 'bug' where it does not trigger the value update event unless a return has been pressed (or similar)
        // As this does not always happen, this means some text changes don't actually get remembered. 
        // This workaround checks to see if the mouse has left the DateTimePicker. If so, it checks for changes to the date/time and updates
        // the values correctly. 
        private void DateTime_MouseLeave(object sender, MouseEventArgs e)
        {
            DateTimePicker dateTimePicker = sender as DateTimePicker;
            DateTime oldDateTime = dateTimePicker.Value.Value;
            DateTime newDateTime = Util.DateTimeHandler.ParseDisplayDateTimeString(dateTimePicker.Text);
            if (oldDateTime == newDateTime)
            {
                // No changes have been made to the date or time, so no need to do anything
                return;
            }
            this.DateTimeUpdate(dateTimePicker, newDateTime);
        }

        private void DateTimeControl_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
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
        }

        // Helper method for above two.
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
            DataEntryControl control = (DataEntryControl)utcOffsetPicker.Tag;

            DateTimeOffset currentImageDateTime = this.ImageCache.Current.GetDateTime();
            DateTimeOffset newImageDateTime = currentImageDateTime.SetOffset(utcOffsetPicker.Value.Value);
            this.ImageCache.Current.SetDateTimeOffset(newImageDateTime);
            System.Diagnostics.Debug.Print(newImageDateTime.ToString());
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

        // Update either the current row or the selected rows in the database, 
        // depending upon whether we are in the single image or  the ClickableImagesGrid view respectively.
        private void UpdateRowsDependingOnClickableImageGridState(string datalabel, string content)
        {
            if (this.ClickableImagesGrid.IsVisible == false)
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

        #region Menu event handlers
        // Menu selections for propagating or copying the current value of this control to all images
        protected virtual void MenuItemPropagateFromLastValue_Click(object sender, RoutedEventArgs e)
        {
            DataEntryControl control = (DataEntryControl)((MenuItem)sender).Tag;
            control.SetContentAndTooltip(this.CopyFromLastNonEmptyValue(control));
        }

        // Copy the current value of this control to all images
        protected virtual void MenuItemCopyCurrentValue_Click(object sender, RoutedEventArgs e)
        {
            DataEntryControl control = (DataEntryControl)((MenuItem)sender).Tag;
            this.CopyToAll(control);
        }

        // Propagate the current value of this control forward from this point across the current set of selected images
        protected virtual void MenuItemPropagateForward_Click(object sender, RoutedEventArgs e)
        {
            DataEntryControl control = (DataEntryControl)((MenuItem)sender).Tag;
            this.CopyForward(control.DataLabel, control is DataEntryCounter);
        }

        // Enable or disable particular context menu items
        protected virtual void Container_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
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
            menuItemCopyForward.IsEnabled = enabledIsPossible ? menuItemCopyForward.IsEnabled = this.IsCopyForwardPossible(control) : false;
            menuItemPropagateFromLastValue.IsEnabled = enabledIsPossible ? this.IsCopyFromLastNonEmptyValuePossible(control) : false;
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
                if (fileIds.Count() == 0)
                {
                    return null;
                }

                // This can cause the crash, when the id in fileIds[0] doesn't exist
                ImageRow imageRow = this.FileDatabase.Files[fileIds[0]];

                // The above line is what causes the crash, when the id in fileIds[0] doesn't exist
                // System.Diagnostics.Debug.Print("Success: " + dataLabel + ": " + fileIds[0]);

                string contents = imageRow.GetValueDisplayString(dataLabel);
                contents = contents.Trim(); 

                // If the values of success imagerows (as defined by the fileIDs) are the same as the first one,
                // then return that as they all have a common value. Otherwise return an empty string.
                for (int i = 1; i < fileIds.Count(); i++)
                {
                    imageRow = this.FileDatabase.Files[fileIds[i]];
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
        #region Defunct Code
        // When the number in a particular counter box changes, update the particular counter field(s) in the database
        // SAULXXX NOTE THAT WE DONT REFERENCE THIS ANYMORE AS ITS BEEN REPLACED BY CounterCOntrol_ValueChanged  BUT KEEP IT FOR NOW JUST IN CASE
        // private void CounterControl_TextChanged(object sender, TextChangedEventArgs e)
        // {
        // if (this.IsProgrammaticControlUpdate)
        //  {
        //    return;
        // }

        // TextBox textBox = (TextBox)sender;
        //  // Get the caret position, as we will have to restore it if we change the text
        //  // If the character under the caret isn't a digit, we will be deleting it.
        //    // That means we have to adjust the caret position so that it appears in the correct place 
        //    int pos = textBox.CaretIndex;
        //    string old_text = textBox.Text;

        // // The caret is already at the beginning, so don't change it.
        //    if (old_text.Length == 0 || pos == 0) 
        //    {
        //        pos = 0;
        //    }
        //    else
        //    { 
        //        // Adjust the caret backwards if the character won't be entered
        //        char ch = old_text[pos - 1];
        //        if (Char.IsDigit(ch) == false)
        //        {
        //            pos--;
        //        }
        //    }
        //    // Remove any characters that are not numbers
        //    // Note that we allow the field to be either a number or empty (i.e., blank).
        //    Regex rgx = new Regex("[^0-9]");
        //    string new_text = rgx.Replace(textBox.Text, String.Empty);
        //    if (String.Equals(new_text, textBox.Text) == false)
        //    {
        //        this.IsProgrammaticControlUpdate = true;
        //        textBox.Text = rgx.Replace(textBox.Text, String.Empty);
        //        this.IsProgrammaticControlUpdate = false;
        //    }
        //    textBox.CaretIndex = pos;

        // // Get the key identifying the control, and then add its value to the database
        //    DataEntryControl control = (DataEntryControl)textBox.Tag;
        //    control.SetContentAndTooltip(textBox.Text);
        //    this.UpdateRowsDependingOnClickableImageGridState(control.DataLabel, control.Content);
        // }
        #endregion
        }
    }
