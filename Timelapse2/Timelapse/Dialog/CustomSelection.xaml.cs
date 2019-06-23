using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Timelapse.Controls;
using Timelapse.Database;
using Timelapse.Detection;
using Timelapse.Enums;
using Timelapse.Util;
using Xceed.Wpf.Toolkit;

namespace Timelapse.Dialog
{
    /// <summary>
    /// A dialog allowing a user to create a custom selection by setting conditions on data fields.
    /// </summary>
    public partial class CustomSelection : Window
    {
        private const int DefaultControlWidth = 200;
        private const double DefaultSearchCriteriaWidth = Double.NaN; // Same as xaml Width = "Auto"

        private const int SelectColumn = 0;
        private const int LabelColumn = 1;
        private const int OperatorColumn = 2;
        private const int ValueColumn = 3;
        private const int SearchCriteriaColumn = 4;

        private FileDatabase database;
        private DataEntryControls dataEntryControls;
        private TimeZoneInfo imageSetTimeZone;
        private bool excludeUTCOffset;
        private bool dontUpdate = true;
        // This timer is used to delay showing count information, which could be an expensive operation, as the user may be setting values quickly
        DispatcherTimer CountTimer = new DispatcherTimer();

        // Detections variables
        private bool dontInvoke = false;
        private bool dontCount;
        private const string LessThan = "\u2264";
        private const string GreaterThan = "\u2265";
        private const string Between = "Between";
        private Dictionary<ComparisonEnum, string> ComparisonDictionary = new Dictionary<ComparisonEnum, string>();
        private DetectionSelections DetectionSelections { get; set; }


        #region Constructors and Loading
        public CustomSelection(FileDatabase database, DataEntryControls dataEntryControls, Window owner, bool excludeUTCOffset, DetectionSelections detectionSelections)
        {
            this.InitializeComponent();

            this.database = database;
            this.dataEntryControls = dataEntryControls;
            this.imageSetTimeZone = this.database.ImageSet.GetSystemTimeZone();
            this.Owner = owner;
            this.excludeUTCOffset = excludeUTCOffset;
            CountTimer.Interval = TimeSpan.FromMilliseconds(500);
            CountTimer.Tick += CountTimer_Tick;

            // Detections-specific
            this.DetectionSelections = detectionSelections;
            ComparisonDictionary.Add(ComparisonEnum.LessThanEqual, LessThan);
            ComparisonDictionary.Add(ComparisonEnum.GreaterThan, GreaterThan);
            ComparisonDictionary.Add(ComparisonEnum.Between, Between); 
        }

        // When the window is loaded, add SearchTerm controls to it
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Adjust this dialog window position 
            Dialogs.SetDefaultDialogPosition(this);
            Dialogs.TryFitDialogWindowInWorkingArea(this);

            // Detections-specific
            this.dontCount = true;
            this.dontInvoke = true;
            this.DetectionRangeType.Items.Add(LessThan);
            this.DetectionRangeType.Items.Add(Between);
            this.DetectionRangeType.Items.Add(GreaterThan);

            // Set the state of the detections to the last used ones (or to its defaults)
            this.UseDetectionCategoryCheckbox.IsChecked = this.DetectionSelections.UseDetectionCategory;
            this.UseDetectionConfidenceCheckbox.IsChecked = this.DetectionSelections.UseDetectionConfidenceThreshold;
            this.CategoryLabel.FontWeight = this.DetectionSelections.UseDetectionCategory ? FontWeights.DemiBold : FontWeights.Normal;
            this.ConfidenceLabel.FontWeight = this.DetectionSelections.UseDetectionConfidenceThreshold ? FontWeights.DemiBold : FontWeights.Normal;

            this.DetectionRangeType.SelectedItem = this.ComparisonDictionary[this.DetectionSelections.DetectionComparison];
            this.DetectionConfidenceSpinner1.Value = this.DetectionSelections.DetectionConfidenceThreshold1;
            this.DetectionConfidenceSpinner2.Value = this.DetectionSelections.DetectionConfidenceThreshold2;


            // Put Detection categories in as human-readable labels and set it to the last used one.
            List<string> labels = this.database.GetDetectionLabels();
            foreach (string label in labels)
            {
                this.DetectionCategoryComboBox.Items.Add(label);
            }
            this.DetectionCategoryComboBox.SelectedValue = database.GetDetectionLabelFromCategory(this.DetectionSelections.DetectionCategory);

            this.SetDetectionSpinnerVisibility(this.DetectionSelections.DetectionCategory);
            this.SetDetectionSpinnerEnable();

            this.dontInvoke = false;
            this.dontCount = false;
            this.SetDetectionCriteria();
            this.InitiateShowCountsOfMatchingFiles();
            this.SetDetectionSpinnerVisibility(this.DetectionSelections.DetectionComparison);

            // Selection-specific
            this.dontUpdate = true;
            // And vs Or conditional
            if (this.database.CustomSelection.TermCombiningOperator == CustomSelectionOperatorEnum.And)
            {
                this.TermCombiningAnd.IsChecked = true;
                this.TermCombiningOr.IsChecked = false;
            }
            else
            {
                this.TermCombiningAnd.IsChecked = false;
                this.TermCombiningOr.IsChecked = true;
            }
            this.TermCombiningAnd.Checked += this.AndOrRadioButton_Checked;
            this.TermCombiningOr.Checked += this.AndOrRadioButton_Checked;

            // Create a new row for each search term. 
            // Each row specifies a particular control and how it can be searched
            int gridRowIndex = 0;
            foreach (SearchTerm searchTerm in this.database.CustomSelection.SearchTerms)
            {
                // start at 1 as there is already a header row
                ++gridRowIndex;
                RowDefinition gridRow = new RowDefinition()
                {
                    Height = GridLength.Auto
                };
                this.SearchTerms.RowDefinitions.Add(gridRow);

                // USE Column: A checkbox to indicate whether the current search row should be used as part of the search
                Thickness thickness = new Thickness(5, 2, 5, 2);
                CheckBox useCurrentRow = new CheckBox()
                {
                    FontWeight= FontWeights.DemiBold,
                    Margin = thickness,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    IsChecked = searchTerm.UseForSearching
                };
                useCurrentRow.Checked += this.Select_CheckedOrUnchecked;
                useCurrentRow.Unchecked += this.Select_CheckedOrUnchecked;
                Grid.SetRow(useCurrentRow, gridRowIndex);
                Grid.SetColumn(useCurrentRow, CustomSelection.SelectColumn);
                SearchTerms.Children.Add(useCurrentRow);

                // LABEL column: The label associated with the control (Note: not the data label)
                TextBlock controlLabel = new TextBlock()
                {
                    FontWeight = searchTerm.UseForSearching ? FontWeights.DemiBold : FontWeights.Normal,
                    Margin = new Thickness(5),
                    Text = searchTerm.Label
                };
                Grid.SetRow(controlLabel, gridRowIndex);
                Grid.SetColumn(controlLabel, CustomSelection.LabelColumn);
                this.SearchTerms.Children.Add(controlLabel);

                // The operators allowed for each search term type
                string controlType = searchTerm.ControlType;
                string[] termOperators;
                if (controlType == Constant.Control.Counter ||
                    controlType == Constant.DatabaseColumn.DateTime ||
                    controlType == Constant.DatabaseColumn.ImageQuality ||
                    controlType == Constant.Control.FixedChoice)
                {
                    // No globs in Counters as that text field only allows numbers, we can't enter the special characters Glob required
                    // No globs in Dates the date entries are constrained by the date picker
                    // No globs in Fixed Choices as choice entries are constrained by menu selection
                    termOperators = new string[]
                    {
                        Constant.SearchTermOperator.Equal,
                        Constant.SearchTermOperator.NotEqual,
                        Constant.SearchTermOperator.LessThan,
                        Constant.SearchTermOperator.GreaterThan,
                        Constant.SearchTermOperator.LessThanOrEqual,
                        Constant.SearchTermOperator.GreaterThanOrEqual
                    };
                }
                else if (controlType == Constant.DatabaseColumn.DeleteFlag ||
                         controlType == Constant.Control.Flag ||
                         controlType == Constant.DatabaseColumn.RelativePath)
                {
                    // Only equals and not equals in Flags, as other options don't make sense for booleans
                    termOperators = new string[]
                    {
                        Constant.SearchTermOperator.Equal,
                        Constant.SearchTermOperator.NotEqual
                    };
                }
                else
                {
                    termOperators = new string[]
                    {
                        Constant.SearchTermOperator.Equal,
                        Constant.SearchTermOperator.NotEqual,
                        Constant.SearchTermOperator.LessThan,
                        Constant.SearchTermOperator.GreaterThan,
                        Constant.SearchTermOperator.LessThanOrEqual,
                        Constant.SearchTermOperator.GreaterThanOrEqual,
                        Constant.SearchTermOperator.Glob
                    };
                }

                // term operator combo box
                ComboBox operatorsComboBox = new ComboBox()
                {
                    FontWeight = FontWeights.DemiBold,
                    IsEnabled = searchTerm.UseForSearching,
                    ItemsSource = termOperators,
                    Margin = thickness,
                    Width = 60,
                    SelectedValue = searchTerm.Operator // Default: equals sign
                };
                operatorsComboBox.SelectionChanged += this.Operator_SelectionChanged; // Create the callback that is invoked whenever the user changes the expresison
                Grid.SetRow(operatorsComboBox, gridRowIndex);
                Grid.SetColumn(operatorsComboBox, CustomSelection.OperatorColumn);
                this.SearchTerms.Children.Add(operatorsComboBox);

                // Value column: The value used for comparison in the search
                // Notes and Counters both uses a text field, so they can be constructed as a textbox
                // However, counter textboxes are modified to only allow integer input (both direct typing or pasting are checked)
                if (controlType == Constant.DatabaseColumn.DateTime)
                {
                    DateTimeOffset dateTime = this.database.CustomSelection.GetDateTime(gridRowIndex - 1, this.imageSetTimeZone);

                    DateTimePicker dateValue = new DateTimePicker()
                    {
                        FontWeight = FontWeights.Normal,
                        Format = DateTimeFormat.Custom,
                        FormatString = Constant.Time.DateTimeDisplayFormat,
                        IsEnabled = searchTerm.UseForSearching,
                        Width = DefaultControlWidth,
                        CultureInfo = CultureInfo.CreateSpecificCulture("en-US"),
                        Value = dateTime.DateTime
                    };
                    dateValue.ValueChanged += this.DateTime_SelectedDateChanged;
                    // TODO: It looks like this now works so its commented out. Remove?
                    // DateTimePicker has a bug where ValueChanged is not triggered as expected, so we use a mousemove event to check if the value has changed
                    // dateValue.MouseMove += DateValue_MouseMove;
                    Grid.SetRow(dateValue, gridRowIndex);
                    Grid.SetColumn(dateValue, CustomSelection.ValueColumn);
                    this.SearchTerms.Children.Add(dateValue);
                }
                else if (controlType == Constant.DatabaseColumn.RelativePath)
                {
                    // Relative path uses a dropdown that shows existing folders
                    ComboBox comboBoxValue = new ComboBox()
                    {
                        FontWeight = FontWeights.Normal,
                        IsEnabled = searchTerm.UseForSearching,
                        Width = CustomSelection.DefaultControlWidth,
                        Margin = thickness,

                        // Create the dropdown menu containing only folders with images in it
                        ItemsSource = this.database.GetDistinctValuesInColumn(Constant.DatabaseTable.FileData, Constant.DatabaseColumn.RelativePath),
                        SelectedItem = searchTerm.DatabaseValue
                    };
                    comboBoxValue.SelectionChanged += this.FixedChoice_SelectionChanged;
                    Grid.SetRow(comboBoxValue, gridRowIndex);
                    Grid.SetColumn(comboBoxValue, CustomSelection.ValueColumn);
                    this.SearchTerms.Children.Add(comboBoxValue);
                }
                else if (controlType == Constant.DatabaseColumn.File ||
                         controlType == Constant.Control.Counter ||
                         controlType == Constant.Control.Note)
                {
                    AutocompleteTextBox textBoxValue = new AutocompleteTextBox()
                    {
                        FontWeight = FontWeights.Normal,
                        Autocompletions = null,
                        IsEnabled = searchTerm.UseForSearching,
                        Text = searchTerm.DatabaseValue,
                        Margin = thickness,
                        Width = CustomSelection.DefaultControlWidth,
                        Height = 22,
                        TextWrapping = TextWrapping.NoWrap,
                        VerticalAlignment = VerticalAlignment.Center,
                        VerticalContentAlignment = VerticalAlignment.Center
                    };
                    if (controlType == Constant.Control.Note)
                    {
                        // Make autocompletions work for this control
                        // IMMEDIATE: NOT SURE THIS IS WORKING
                        textBoxValue.Autocompletions = this.dataEntryControls.AutocompletionGetForNote(searchTerm.DataLabel);
                    }

                    // The following is specific only to Counters
                    if (controlType == Constant.Control.Counter)
                    {
                        textBoxValue.PreviewTextInput += this.Counter_PreviewTextInput;
                        DataObject.AddPastingHandler(textBoxValue, this.Counter_Paste);
                    }
                    textBoxValue.TextChanged += this.NoteOrCounter_TextChanged;

                    Grid.SetRow(textBoxValue, gridRowIndex);
                    Grid.SetColumn(textBoxValue, CustomSelection.ValueColumn);
                    this.SearchTerms.Children.Add(textBoxValue);
                }
                else if (controlType == Constant.Control.FixedChoice ||
                         controlType == Constant.DatabaseColumn.ImageQuality)
                {
                    // FixedChoice and ImageQuality both present combo boxes, so they can be constructed the same way
                    ComboBox comboBoxValue = new ComboBox()
                    {
                        FontWeight = FontWeights.Normal,
                        IsEnabled = searchTerm.UseForSearching,
                        Width = CustomSelection.DefaultControlWidth,
                        Margin = thickness,

                        // Create the dropdown menu 
                        ItemsSource = searchTerm.List,
                        SelectedItem = searchTerm.DatabaseValue
                    };
                    comboBoxValue.SelectionChanged += this.FixedChoice_SelectionChanged;
                    Grid.SetRow(comboBoxValue, gridRowIndex);
                    Grid.SetColumn(comboBoxValue, CustomSelection.ValueColumn);
                    this.SearchTerms.Children.Add(comboBoxValue);
                }
                else if (controlType == Constant.DatabaseColumn.DeleteFlag ||
                         controlType == Constant.Control.Flag)
                {
                    // Flags present checkboxes
                    CheckBox flagCheckBox = new CheckBox()
                    {
                        FontWeight = FontWeights.Normal,
                        Margin = thickness,
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        IsChecked = String.Equals(searchTerm.DatabaseValue, Constant.BooleanValue.False, StringComparison.OrdinalIgnoreCase) ? false : true,
                        IsEnabled = searchTerm.UseForSearching
                    };
                    flagCheckBox.Checked += this.Flag_CheckedOrUnchecked;
                    flagCheckBox.Unchecked += this.Flag_CheckedOrUnchecked;

                    searchTerm.DatabaseValue = flagCheckBox.IsChecked.Value ? Constant.BooleanValue.True : Constant.BooleanValue.False;

                    Grid.SetRow(flagCheckBox, gridRowIndex);
                    Grid.SetColumn(flagCheckBox, CustomSelection.ValueColumn);
                    this.SearchTerms.Children.Add(flagCheckBox);
                }
                else if (controlType == Constant.DatabaseColumn.UtcOffset)
                {
                    UtcOffsetUpDown utcOffsetValue = new UtcOffsetUpDown()
                    {
                        FontWeight = FontWeights.Normal,
                        IsEnabled = searchTerm.UseForSearching,
                        Value = searchTerm.GetUtcOffset(),
                        Width = CustomSelection.DefaultControlWidth
                    };
                    utcOffsetValue.ValueChanged += this.UtcOffset_SelectedDateChanged;

                    Grid.SetRow(utcOffsetValue, gridRowIndex);
                    Grid.SetColumn(utcOffsetValue, CustomSelection.ValueColumn);
                    this.SearchTerms.Children.Add(utcOffsetValue);
                }
                else
                {
                    throw new NotSupportedException(String.Format("Unhandled control type '{0}'.", controlType));
                }

                // We need to exclude UTCOffsets from the search interface. If so, just hide the UTCOffset row.
                if (controlType == Constant.DatabaseColumn.UtcOffset && this.excludeUTCOffset)
                {
                    gridRow.Height = new GridLength(0);
                }

                // Search Criteria Column: initially as an empty textblock. Indicates the constructed query expression for this row
                TextBlock searchCriteria = new TextBlock()
                {
                    FontWeight = FontWeights.Normal,
                    Width = CustomSelection.DefaultSearchCriteriaWidth,
                    Margin = thickness,
                    IsEnabled = true,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Left
                };
                Grid.SetRow(searchCriteria, gridRowIndex);
                Grid.SetColumn(searchCriteria, CustomSelection.SearchCriteriaColumn);
                this.SearchTerms.Children.Add(searchCriteria);
            }
            this.dontUpdate = false;
            this.UpdateSearchCriteriaFeedback();
        }

        // TODO: It looks like this isn't needed anymore. Check
        // DateTimePicker has a bug where ValueChanged is not triggered as expected, so we use a mousemove event to check if the value has changed
        //private void DateValue_MouseMove(object sender, MouseEventArgs e)
        //{
        //    this.DateTime_SelectedDateChanged(sender, null);
        //}
        #endregion

        #region Query formation callbacks
        // Radio buttons for determing if we use And or Or
        private void AndOrRadioButton_Checked(object sender, RoutedEventArgs args)
        {
            RadioButton radioButton = sender as RadioButton;
            this.database.CustomSelection.TermCombiningOperator = (radioButton == this.TermCombiningAnd) ? CustomSelectionOperatorEnum.And : CustomSelectionOperatorEnum.Or;
            this.UpdateSearchCriteriaFeedback();
        }

        // Select: When the use checks or unchecks the checkbox for a row
        // - activate or deactivate the search criteria for that row
        // - update the searchterms to reflect the new status 
        // - update the UI to activate or deactivate (or show or hide) its various search terms
        private void Select_CheckedOrUnchecked(object sender, RoutedEventArgs args)
        {
            CheckBox select = sender as CheckBox;
            int row = Grid.GetRow(select);  // And you have the row number...

            SearchTerm searchterms = this.database.CustomSelection.SearchTerms[row - 1];
            searchterms.UseForSearching = select.IsChecked.Value;

            TextBlock label = this.GetGridElement<TextBlock>(CustomSelection.LabelColumn, row);
            ComboBox expression = this.GetGridElement<ComboBox>(CustomSelection.OperatorColumn, row);
            UIElement value = this.GetGridElement<UIElement>(CustomSelection.ValueColumn, row);

            label.FontWeight = select.IsChecked.Value ? FontWeights.DemiBold : FontWeights.Normal;
            expression.IsEnabled = select.IsChecked.Value;
            value.IsEnabled = select.IsChecked.Value;

            this.UpdateSearchCriteriaFeedback();
        }

        // Operator: The user has selected a new expression
        // - set its corresponding search term in the searchList data structure
        // - update the UI to show the search criteria 
        private void Operator_SelectionChanged(object sender, SelectionChangedEventArgs args)
        {
            ComboBox comboBox = sender as ComboBox;
            int row = Grid.GetRow(comboBox);  // Get the row number...
            this.database.CustomSelection.SearchTerms[row - 1].Operator = comboBox.SelectedValue.ToString(); // Set the corresponding expression to the current selection
            this.UpdateSearchCriteriaFeedback();
        }

        // Value (Counters and Notes): The user has selected a new value
        // - set its corresponding search term in the searchList data structure
        // - update the UI to show the search criteria 
        private void NoteOrCounter_TextChanged(object sender, TextChangedEventArgs args)
        {
            TextBox textBox = sender as TextBox;
            int row = Grid.GetRow(textBox);  // Get the row number...
            this.database.CustomSelection.SearchTerms[row - 1].DatabaseValue = textBox.Text;
            this.UpdateSearchCriteriaFeedback();
        }

        // Value (Counter) Helper function: textbox accept only typed numbers 
        private void Counter_PreviewTextInput(object sender, TextCompositionEventArgs args)
        {
            args.Handled = IsNumbersOnly(args.Text);
        }

        // Value (DateTime): we need to construct a string DateTime from it
        private void DateTime_SelectedDateChanged(object sender, RoutedPropertyChangedEventArgs<object> args)
        {
            DateTimePicker datePicker = sender as DateTimePicker;
            if (datePicker.Value.HasValue)
            {
                int row = Grid.GetRow(datePicker);
                // Because of the bug in the DateTimePicker, we have to get the changed value from the string
                // as DateTimePicker.Value.Value can have the old date rather than the new one.
                if (DateTimeHandler.TryParseDisplayDateTimeString(datePicker.Text, out DateTime newDateTime))
                { 
                    this.database.CustomSelection.SetDateTime(row - 1, newDateTime, this.imageSetTimeZone);
                    this.UpdateSearchCriteriaFeedback();
                }
            }
        }

        // Value (FixedChoice): The user has selected a new value 
        // - set its corresponding search term in the searchList data structure
        // - update the UI to show the search criteria 
        private void FixedChoice_SelectionChanged(object sender, SelectionChangedEventArgs args)
        {
            ComboBox comboBox = sender as ComboBox;
            int row = Grid.GetRow(comboBox);  // Get the row number...
            if (comboBox.SelectedValue == null)
            {
                return;
            }
            this.database.CustomSelection.SearchTerms[row - 1].DatabaseValue = comboBox.SelectedValue.ToString(); // Set the corresponding value to the current selection
            this.UpdateSearchCriteriaFeedback();
        }

        // Value (Flags): The user has checked or unchecked a new value 
        // - set its corresponding search term in the searchList data structure
        // - update the UI to show the search criteria 
        private void Flag_CheckedOrUnchecked(object sender, RoutedEventArgs e)
        {
            CheckBox checkBox = sender as CheckBox;
            int row = Grid.GetRow(checkBox);  // Get the row number...
            this.database.CustomSelection.SearchTerms[row - 1].DatabaseValue = checkBox.IsChecked.ToString().ToLower(); // Set the corresponding value to the current selection
            this.UpdateSearchCriteriaFeedback();
        }

        // When this button is pressed, all the search terms checkboxes are cleared, which is equivalent to showing all images
        private void ShowAllButton_Click(object sender, RoutedEventArgs e)
        {
            for (int row = 1; row <= this.database.CustomSelection.SearchTerms.Count; row++)
            {
                CheckBox select = this.GetGridElement<CheckBox>(CustomSelection.SelectColumn, row);
                select.IsChecked = false;
            }
        }

        // Value (UtcOffset): we need to construct a string TimeSpan from it
        private void UtcOffset_SelectedDateChanged(object sender, RoutedPropertyChangedEventArgs<object> args)
        {
            UtcOffsetUpDown utcOffsetPicker = sender as UtcOffsetUpDown;
            if (utcOffsetPicker.Value.HasValue)
            {
                int row = Grid.GetRow(utcOffsetPicker);
                this.database.CustomSelection.SearchTerms[row - 1].SetDatabaseValue(utcOffsetPicker.Value.Value);
                this.UpdateSearchCriteriaFeedback();
            }
        }
        #endregion

        #region Search Criteria feedback for each row
        // Updates the search criteria shown across all rows to reflect the contents of the search list,
        // which also show or hides the search term feedback for that row.
        private void UpdateSearchCriteriaFeedback()
        {
            if (this.dontUpdate) return;
            // We go backwards, as we don't want to print the AND or OR on the last expression
            bool lastExpression = true;
            int numberOfDateTimesSearchTerms = 0;
            string utcOffset = "Utc Offset";
            bool searchTermsInUse = false;
            for (int index = this.database.CustomSelection.SearchTerms.Count - 1; index >= 0; index--)
            {
                int row = index + 1; // we offset the row by 1 as row 0 is the header
                SearchTerm searchTerm = this.database.CustomSelection.SearchTerms[index];
                TextBlock searchCriteria = this.GetGridElement<TextBlock>(CustomSelection.SearchCriteriaColumn, row);

                // Remember the Utc offset, as we will use it to compose the DateTime feedback if needed
                if (searchTerm.DataLabel == Constant.DatabaseColumn.UtcOffset)
                {
                    utcOffset = searchTerm.DatabaseValue.Trim();
                }

                if (searchTerm.UseForSearching == false)
                {
                    // The search term is not used for searching, so clear the feedback field
                    searchCriteria.Text = String.Empty;
                    continue;
                }
                searchTermsInUse = true;

                // We want to see how many DateTime search terms we have. If there are two, we will be 'and-ing them nt matter what.
                if (searchTerm.ControlType == Constant.DatabaseColumn.DateTime)
                {
                    numberOfDateTimesSearchTerms++;
                }

                // Construct the search term 
                string searchCriteriaText = searchTerm.DataLabel + " " + searchTerm.Operator + " "; // So far, we have "Data Label = "

                string value;

                // The DateTime feedback is special case, as we want to include the offset in it.
                if (searchTerm.DataLabel == Constant.DatabaseColumn.DateTime)
                {
                    // Display UTC time in Timelapse's standard DateTime display format
                    DateTime tmpDateTime = DateTime.Parse(searchTerm.DatabaseValue.Trim());
                    DateTimeHandler.TryParseDatabaseUtcOffsetString(utcOffset, out TimeSpan tmpTimeSpan);
                    tmpDateTime.Add(tmpTimeSpan);
                    value = tmpDateTime.ToString(Constant.Time.DateTimeDisplayFormat);
                }
                else
                {
                    value = searchTerm.DatabaseValue.Trim();   
                }
                if (value.Length == 0)
                {
                    value = "\"\"";  // an empty string, display it as ""
                }
                searchCriteriaText += value;

                // If it's not the last expression and if there are multiple queries (i.e., search terms) then show the And or Or at its end.
                if (!lastExpression)
                {
                    // If there are two DateTime search terms selected, they are always  and'ed
                    if (searchTerm.ControlType == Constant.DatabaseColumn.DateTime && numberOfDateTimesSearchTerms == 2)
                    {
                        searchCriteriaText += " " + CustomSelectionOperatorEnum.And;
                    }
                    else
                    { 
                        searchCriteriaText += " " + this.database.CustomSelection.TermCombiningOperator.ToString();
                    }
                }
                searchCriteria.Text = searchCriteriaText;
                lastExpression = false;
            }
            this.InitiateShowCountsOfMatchingFiles();
            this.ShowAll.IsEnabled = lastExpression == false;
        }
        #endregion

        #region Helper functions
        // Get the corresponding grid element from a given a column, row, 
        private TElement GetGridElement<TElement>(int column, int row) where TElement : UIElement
        {
            return (TElement)this.SearchTerms.Children.Cast<UIElement>().First(control => Grid.GetRow(control) == row && Grid.GetColumn(control) == column);
        }

        // Value (Counter) Helper function:  textbox accept only pasted numbers 
        private void Counter_Paste(object sender, DataObjectPastingEventArgs args)
        {
            bool isText = args.SourceDataObject.GetDataPresent(DataFormats.UnicodeText, true);
            if (!isText)
            {
                args.CancelCommand();
            }

            string text = args.SourceDataObject.GetData(DataFormats.UnicodeText) as string;
            if (CustomSelection.IsNumbersOnly(text))
            {
                args.CancelCommand();
            }
        }

        // Value(Counter) Helper function: checks if the text contains only numbers
        private static bool IsNumbersOnly(string text)
        {
            Regex regex = new Regex("[^0-9.-]+"); // regex that matches allowed text
            return regex.IsMatch(text);
        }
        #endregion

        #region Detection-specific methos and callbacks
        private void UseCriteria_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (this.dontInvoke) return;
            // Enable or disable the controls depending on the various checkbox states
            SetDetectionSpinnerEnable();

            this.SetDetectionCriteria();
            this.InitiateShowCountsOfMatchingFiles();
        }

        private void SetDetectionCriteria()
        {
            if (this.IsLoaded == false || this.dontInvoke)
            {
                return;
            }
            this.DetectionSelections.UseDetectionCategory = this.UseDetectionCategoryCheckbox.IsChecked == true;
            if (this.DetectionSelections.UseDetectionCategory)
            {
                this.DetectionSelections.DetectionCategory = this.database.GetDetectionCategoryFromLabel((string)this.DetectionCategoryComboBox.SelectedItem);
            }

            this.DetectionSelections.UseDetectionConfidenceThreshold = this.UseDetectionConfidenceCheckbox.IsChecked == true;
            if (this.DetectionSelections.UseDetectionConfidenceThreshold)
            {
                this.DetectionSelections.DetectionConfidenceThreshold1 = (double)this.DetectionConfidenceSpinner1.Value;
            }

            if (this.DetectionSelections.UseDetectionConfidenceThreshold)
            {
                this.DetectionSelections.DetectionConfidenceThreshold2 = (double)this.DetectionConfidenceSpinner2.Value;
            }
            this.CategoryLabel.FontWeight = this.DetectionSelections.UseDetectionCategory ? FontWeights.DemiBold : FontWeights.Normal;
            this.ConfidenceLabel.FontWeight = this.DetectionSelections.UseDetectionConfidenceThreshold ? FontWeights.DemiBold : FontWeights.Normal;
        }

        private void DetectionCategoryComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (this.IsLoaded == false)
            {
                return;
            }
            this.SetDetectionCriteria();
            this.InitiateShowCountsOfMatchingFiles();
        }

        private bool ignoreSpinnerUpdates = false;
        private void DetectionConfidenceSpinner1_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (this.IsLoaded == false || ignoreSpinnerUpdates)
            {
                return;
            }

            if (this.DetectionConfidenceSpinner1.Value > this.DetectionConfidenceSpinner2.Value)
            {
                ignoreSpinnerUpdates = true;
                this.DetectionConfidenceSpinner2.Value = this.DetectionConfidenceSpinner1.Value;
                ignoreSpinnerUpdates = false;
            }
            this.SetDetectionCriteria();
            this.InitiateShowCountsOfMatchingFiles();
        }

        private void DetectionConfidenceSpinner2_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (this.IsLoaded == false || ignoreSpinnerUpdates)
            {
                return;
            }

            if (this.DetectionConfidenceSpinner2.Value < this.DetectionConfidenceSpinner1.Value)
            {
                ignoreSpinnerUpdates = true;
                this.DetectionConfidenceSpinner2.Value = this.DetectionConfidenceSpinner1.Value;
                ignoreSpinnerUpdates = false;
            }
            this.SetDetectionCriteria();
            this.InitiateShowCountsOfMatchingFiles();
        }

        private void DetectionRangeType_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (this.IsLoaded == false || this.dontInvoke)
            {
                return;
            }
            this.SetDetectionSpinnerVisibility((string)this.DetectionRangeType.SelectedValue);
            this.DetectionSelections.DetectionComparison = ComparisonDictionary.FirstOrDefault(x => x.Value == (string)this.DetectionRangeType.SelectedValue).Key;
            this.SetDetectionCriteria();
            this.InitiateShowCountsOfMatchingFiles();
        }

        // Depending on what comparision operator is used, set the visibility of particular spinners and labels
        private void SetDetectionSpinnerVisibility(ComparisonEnum comparisonEnum)
        {
            SetDetectionSpinnerVisibility(ComparisonDictionary[comparisonEnum]);
        }
        private void SetDetectionSpinnerVisibility(string comparison)
        {
            switch (comparison)
            {
                case LessThan:
                    this.DetectionConfidenceSpinner2.Visibility = Visibility.Hidden;
                    this.AndLabel.Visibility = Visibility.Hidden;
                    break;
                case Between:
                    this.DetectionConfidenceSpinner2.Visibility = Visibility.Visible;
                    this.AndLabel.Visibility = Visibility.Visible;
                    break;
                case GreaterThan:
                default:
                    this.DetectionConfidenceSpinner2.Visibility = Visibility.Hidden;
                    this.AndLabel.Visibility = Visibility.Hidden;
                    break;
            }
        }
        private void SetDetectionSpinnerEnable()
        {
            // Enable or disable the controls depending on the various checkbox states
            this.DetectionConfidenceSpinner1.IsEnabled = this.UseDetectionConfidenceCheckbox.IsChecked == true;
            this.DetectionConfidenceSpinner2.IsEnabled = this.UseDetectionConfidenceCheckbox.IsChecked == true;
            this.DetectionRangeType.IsEnabled = this.UseDetectionConfidenceCheckbox.IsChecked == true;
            this.DetectionCategoryComboBox.IsEnabled = this.UseDetectionCategoryCheckbox.IsChecked == true;
        }
        #endregion

        #region Common
        private void CountTimer_Tick(object sender, EventArgs e)
        {
            CountTimer.Stop();
            // This is set everytime a selectin is made
            if (this.dontCount == true)
            {
                return;
            }
            int count = this.database.GetFileCount(FileSelectionEnum.Custom);
            this.QueryMatches.Text = count > 0 ? count.ToString() : "0";
            this.OkButton.IsEnabled = (count > 0); // Dusable OK button if there are no matches
        }

        // Start the timere that will show how many files match the current selection
        private void InitiateShowCountsOfMatchingFiles()
        {
            CountTimer.Stop();
            CountTimer.Start();
        }

        // Apply the selection if the Ok button is clicked
        private void OkButton_Click(object sender, RoutedEventArgs args)
        {
            this.SetDetectionCriteria();
            this.DialogResult = true;
        }

        // Cancel - exit the dialog without doing anythikng.
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
        #endregion
    }
}
