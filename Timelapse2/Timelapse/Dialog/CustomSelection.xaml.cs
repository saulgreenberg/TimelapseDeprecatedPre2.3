using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Timelapse.Controls;
using Timelapse.Database;
using Timelapse.DataStructures;
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
        #region Private Variables
        private const int DefaultControlWidth = 200;
        private const double DefaultSearchCriteriaWidth = Double.NaN; // Same as xaml Width = "Auto"

        private const int SelectColumn = 0;
        private const int LabelColumn = 1;
        private const int OperatorColumn = 2;
        private const int ValueColumn = 3;
        private const int SearchCriteriaColumn = 4;

        // Detections variables
        private bool dontInvoke;
        private bool dontCount;
        private bool dontUpdateRangeSlider;

        // Variables
        private readonly FileDatabase database;
        private readonly DataEntryControls dataEntryControls;
        private readonly TimeZoneInfo imageSetTimeZone;
        private readonly bool excludeUTCOffset;
        private bool dontUpdate = true;

        // This timer is used to delay showing count information, which could be an expensive operation, as the user may be setting values quickly
        private readonly DispatcherTimer countTimer = new DispatcherTimer();

        private DetectionSelections DetectionSelections { get; set; }
        #endregion

        #region Constructors and Loading
        public CustomSelection(FileDatabase database, DataEntryControls dataEntryControls, Window owner, bool excludeUTCOffset, DetectionSelections detectionSelections)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(database, nameof(database));

            this.InitializeComponent();

            this.database = database;
            this.dataEntryControls = dataEntryControls;
            this.imageSetTimeZone = this.database.ImageSet.GetSystemTimeZone();
            this.Owner = owner;
            this.excludeUTCOffset = excludeUTCOffset;
            this.countTimer.Interval = TimeSpan.FromMilliseconds(500);
            this.countTimer.Tick += this.CountTimer_Tick;

            // Detections-specific
            if (GlobalReferences.DetectionsExists)
            {
                this.DetectionSelections = detectionSelections;
            }
        }

        // When the window is loaded, add SearchTerm controls to it
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Adjust this dialog window position 
            Dialogs.TryPositionAndFitDialogIntoWindow(this);

            // Detections-specific
            this.dontCount = true;
            this.dontInvoke = true;

            // Set the state of the detections to the last used ones (or to its defaults)
            if (GlobalReferences.DetectionsExists)
            {
                this.DetectionGroupBox.Visibility = Visibility.Visible;
                this.Detections2Panel.Visibility = Visibility.Visible;
                this.UseDetectionsCheckbox.IsChecked = this.DetectionSelections.UseRecognition;

                // Set the spinner and sliders to the last used values
                this.DetectionConfidenceSpinnerLower.Value = this.DetectionSelections.ConfidenceThreshold1ForUI;
                this.DetectionConfidenceSpinnerHigher.Value = this.DetectionSelections.ConfidenceThreshold2ForUI;
                this.DetectionRangeSlider.LowerValue = this.DetectionSelections.ConfidenceThreshold1ForUI;
                this.DetectionRangeSlider.HigherValue = this.DetectionSelections.ConfidenceThreshold2ForUI;

                // Set the Rank by Confidence
                this.RankByConfidenceCheckbox.IsChecked = this.DetectionSelections.RankByConfidence;

                // Put Detection and Classification categories in the combo box as human-readable labels
                // Note that we add "All" to the Detections list as that is a 'bogus' Timelapse-internal category.
                List<string> labels = this.database.GetDetectionLabels();
                this.DetectionCategoryComboBox.Items.Add(Constant.DetectionValues.AllDetectionLabel);
                foreach (string label in labels)
                {
                    this.DetectionCategoryComboBox.Items.Add(label);
                }

                if (Util.GlobalReferences.UseClassifications)
                {
                    // Now add classifications
                    labels = this.database.GetClassificationLabels();
                    if (labels.Count > 0)
                    {
                        // Add a separator
                        ComboBoxItem separator = new ComboBoxItem
                        {
                            BorderBrush = Brushes.Black,
                            BorderThickness = new Thickness(0, 0, 0, 2),
                            Focusable = false,
                            IsEnabled = false
                        };
                        this.DetectionCategoryComboBox.Items.Add(separator);
                        foreach (string label in labels)
                        {
                            this.DetectionCategoryComboBox.Items.Add(label);
                        }
                    }
                }

                // Set the combobox selection to the last used one.
                string categoryLabel = String.Empty;
                if (this.DetectionSelections.RecognitionType == RecognitionType.None)
                {
                    // If we don't know the recognition type, default to All
                    this.DetectionCategoryComboBox.SelectedValue = Constant.DetectionValues.AllDetectionLabel;
                }
                else if (this.DetectionSelections.RecognitionType == RecognitionType.Detection)
                {
                    categoryLabel = this.database.GetDetectionLabelFromCategory(this.DetectionSelections.DetectionCategory);
                    if (string.IsNullOrEmpty(this.DetectionSelections.DetectionCategory) || this.DetectionSelections.AllDetections)
                    {
                        // We need an 'All' detection category, which is the union of all categories (except empty).
                        // Because All is a bogus detection category (since its not part of the detection data), we have to set it explicitly
                        this.DetectionCategoryComboBox.SelectedValue = Constant.DetectionValues.AllDetectionLabel;
                    }
                    else
                    {
                        this.DetectionCategoryComboBox.SelectedValue = categoryLabel;
                    }
                }
                else
                {
                    categoryLabel = this.database.GetClassificationLabelFromCategory(this.DetectionSelections.ClassificationCategory);
                    this.DetectionCategoryComboBox.SelectedValue = (categoryLabel.Length != 0)
                        ? categoryLabel
                        : this.DetectionCategoryComboBox.SelectedValue = Constant.DetectionValues.AllDetectionLabel;
                }
                this.EnableDetectionControls((bool)this.UseDetectionsCheckbox.IsChecked);
            }
            else
            {
                this.DetectionGroupBox.Visibility = Visibility.Collapsed;
                this.Detections2Panel.Visibility = Visibility.Collapsed;
                if (this.DetectionSelections != null)
                {
                    this.DetectionSelections.ClearAllDetectionsUses();
                }
            }
            this.dontInvoke = false;
            this.dontCount = false;
            if (GlobalReferences.DetectionsExists)
            {
                this.SetDetectionCriteria();
                this.ShowMissingDetectionsCheckbox.IsChecked = this.database.CustomSelection.ShowMissingDetections;
            }
            this.InitiateShowCountsOfMatchingFiles();
            this.DetectionCategoryComboBox.SelectionChanged += this.DetectionCategoryComboBox_SelectionChanged;

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
                    FontWeight = FontWeights.DemiBold,
                    Margin = thickness,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    IsChecked = searchTerm.UseForSearching
                };
                if (searchTerm.Label == Constant.DatabaseColumn.RelativePath && Util.GlobalReferences.MainWindow.Arguments.ConstrainToRelativePath)
                {
                    useCurrentRow.IsChecked = true;
                    useCurrentRow.IsEnabled = false;
                }
                useCurrentRow.Checked += this.Select_CheckedOrUnchecked;
                useCurrentRow.Unchecked += this.Select_CheckedOrUnchecked;
                Grid.SetRow(useCurrentRow, gridRowIndex);
                Grid.SetColumn(useCurrentRow, CustomSelection.SelectColumn);
                this.SearchTerms.Children.Add(useCurrentRow);

                // LABEL column: The label associated with the control (Note: not the data label)
                TextBlock controlLabel = new TextBlock()
                {
                    FontWeight = searchTerm.UseForSearching ? FontWeights.DemiBold : FontWeights.Normal,
                    Margin = new Thickness(5)
                };

                if (searchTerm.Label != Constant.DatabaseColumn.RelativePath)
                {
                    // Not relative path, so just use the label's name
                    controlLabel.Text = searchTerm.Label;
                }
                else
                {
                    // RelativePath label adds details
                    controlLabel.Inlines.Add(searchTerm.Label + " folder");
                    controlLabel.Inlines.Add(new Run(Environment.NewLine + "includes subfolders") { FontStyle = FontStyles.Italic, FontSize = 10 });

                }
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
                // Only equals and not equals (For relative path this will be converted later to a glob to get subfolders) 
                else if (controlType == Constant.DatabaseColumn.DeleteFlag ||
                         controlType == Constant.DatabaseColumn.RelativePath ||
                         controlType == Constant.Control.Flag)
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
                    FontWeight = FontWeights.Normal,
                    IsEnabled = searchTerm.UseForSearching,
                    ItemsSource = termOperators,
                    Margin = thickness,
                    Width = 60,
                    Height = searchTerm.Label == Constant.DatabaseColumn.RelativePath
                        ? 25
                        : Double.NaN,
                    SelectedValue = searchTerm.Operator
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
                        Height = 25,
                        Margin = thickness,
                        // Create the dropdown menu containing only folders with images in it
                        SelectedItem = searchTerm.DatabaseValue
                    };

                    Arguments arguments = Util.GlobalReferences.MainWindow.Arguments;
                    if (false == arguments.ConstrainToRelativePath)
                    {
                        comboBoxValue.ItemsSource = this.database.GetFoldersFromRelativePaths();
                    }
                    else
                    {
                        // We need to generate a folder list that is just the relativePath and its sub-folders
                        List<string> newFolderList = new List<string>();
                        foreach (string folder in this.database.GetFoldersFromRelativePaths())
                        {
                            //if (string.IsNullOrEmpty(folder))
                            //{
                            //    // An empty header is actually the root folder. Since we already have an entry representng all files, we don't need it.
                            //    continue;
                            //}

                            // Add the folder to the menu only if it isn't constrained by the relative path arguments
                            if (arguments.ConstrainToRelativePath && !(folder == arguments.RelativePath || folder.StartsWith(arguments.RelativePath + @"\")))
                            {
                                continue;
                            }
                            newFolderList.Add(folder);
                        }
                        comboBoxValue.ItemsSource = newFolderList;
                    }
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
                        // Add existing autocompletions for this control
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
                else if (controlType == Constant.Control.FixedChoice)
                {
                    // FixedChoice presents combo boxes, so they can be constructed tGetDataTabelhe same way
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
                else if (controlType == Constant.DatabaseColumn.ImageQuality)
                {
                    // ImageQuality presents combo boxes but onlly with Ok/Dark/empty.
                    // SAULXXX THIS HAS TO FIXED ONCE WE CHANGE IMAGE QUALITY
                    ComboBox comboBoxValue = new ComboBox()
                    {
                        FontWeight = FontWeights.Normal,
                        IsEnabled = searchTerm.UseForSearching,
                        Width = CustomSelection.DefaultControlWidth,
                        Margin = thickness,

                        // Create the dropdown menu 
                        ItemsSource = new List<string>() { Constant.ImageQuality.Ok, Constant.ImageQuality.Dark, String.Empty },
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
                        IsChecked = !String.Equals(searchTerm.DatabaseValue, Constant.BooleanValue.False, StringComparison.OrdinalIgnoreCase),
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
        private void ResetToAllImagesButton_Click(object sender, RoutedEventArgs e)
        {
            this.UseDetectionsCheckbox.IsChecked = false;
            for (int row = 1; row <= this.database.CustomSelection.SearchTerms.Count; row++)
            {
                CheckBox select = this.GetGridElement<CheckBox>(CustomSelection.SelectColumn, row);
                select.IsChecked = false;
            }
            this.ShowMissingDetectionsCheckbox.IsChecked = false;
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
            if (this.dontUpdate)
            {
                return;
            }
            // We go backwards, as we don't want to print the AND or OR on the last expression
            bool lastExpression = true;
            int numberOfDateTimesSearchTerms = 0;
            string utcOffset = "Utc Offset";
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
                    if (DateTimeHandler.TryParseDatabaseUtcOffsetString(utcOffset, out TimeSpan tmpTimeSpan))
                    {
                        tmpDateTime.Add(tmpTimeSpan);
                        value = tmpDateTime.ToString(Constant.Time.DateTimeDisplayFormat);
                    }
                    else
                    {
                        value = String.Empty;
                    }
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
            this.ResetToAllImagesButton.IsEnabled = lastExpression == false ||
                (bool)this.ShowMissingDetectionsCheckbox.IsChecked;
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

        #region Detection-specific methods and callbacks
        private void UseDetections_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (this.dontInvoke)
            {
                return;
            }
            // Enable or disable the controls depending on the various checkbox states
            this.EnableDetectionControls((bool)this.UseDetectionsCheckbox.IsChecked);

            this.SetDetectionCriteria();
            this.InitiateShowCountsOfMatchingFiles();
        }

        private void SetDetectionCriteria()
        {
            if (this.IsLoaded == false || this.dontInvoke)
            {
                return;
            }
            this.DetectionSelections.UseRecognition = this.UseDetectionsCheckbox.IsChecked == true;
            if (this.DetectionSelections.UseRecognition)
            {
                this.SetDetectionCriteriaForComboBox(false);
                this.DetectionSelections.ConfidenceThreshold1ForUI = this.DetectionConfidenceSpinnerLower.Value == null ? 0 : Round2(this.DetectionConfidenceSpinnerLower.Value);
                this.DetectionSelections.ConfidenceThreshold2ForUI = this.DetectionConfidenceSpinnerHigher.Value == null ? 0 : Round2(this.DetectionConfidenceSpinnerHigher.Value);

                // The BoundingBoxDisplayThreshold is the user-defined default set in preferences, while the BoundingBoxThresholdOveride is the threshold
                // determined in this select dialog. For example, if (say) the preference setting is .6 but the selection is at .4 confidence, then we should 
                // show bounding boxes when the confidence is .4 or more. 
                Tuple<double, double> confidenceBounds = this.DetectionSelections.ConfidenceThresholdForSelect;
                Util.GlobalReferences.TimelapseState.BoundingBoxThresholdOveride = this.DetectionSelections.UseRecognition
                    ? confidenceBounds.Item1
                    : 1;
            }

            // Enable / alter looks and behavour of detecion UI to match whether detections should be used
            this.EnableDetectionControls((bool)this.UseDetectionsCheckbox.IsChecked);
        }

        private void ShowMissingDetectionsCheckbox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            this.database.CustomSelection.ShowMissingDetections = (bool)this.ShowMissingDetectionsCheckbox.IsChecked;
            this.SetDetectionCriteria();
            this.InitiateShowCountsOfMatchingFiles();
        }

        private void DetectionCategoryComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (this.IsLoaded == false)
            {
                return;
            }
            this.SetDetectionCriteriaForComboBox(true);
            // this.SetDetectionCriteria();
            this.InitiateShowCountsOfMatchingFiles();
        }

        private void SetDetectionCriteriaForComboBox(bool resetSlidersIfNeeded)
        {
            // Reset defaults to reasonable settings whenever the selected item changes
            if ((string)this.DetectionCategoryComboBox.SelectedItem == Constant.DetectionValues.NoDetectionLabel)
            {
                // If its empty, we want to default to 1.0, i.e.,to only show images where the recognizer has not found any detections.
                // We also want to set EmptyDetections to false so that the actual selection can invert the confidence settings.
                this.DetectionSelections.EmptyDetections = true;
                this.DetectionSelections.AllDetections = false;
                this.DetectionSelections.RecognitionType = RecognitionType.Detection;
                this.DetectionSelections.DetectionCategory = this.database.GetDetectionCategoryFromLabel(Constant.DetectionValues.NoDetectionLabel);

                if (resetSlidersIfNeeded)
                {
                    this.DetectionRangeSlider.HigherValue = 1.0;
                    this.DetectionRangeSlider.LowerValue = 1.0;
                }
            }
            else
            {
                // Not empty: reset it to these defaults. We use .8 - 1.0, as this range is one where the recognizer is reasonably accurate
                this.DetectionSelections.EmptyDetections = false;
                if (resetSlidersIfNeeded)
                {
                    this.DetectionRangeSlider.HigherValue = 1.0;
                    this.DetectionRangeSlider.LowerValue = 0.8;
                }

                if ((string)this.DetectionCategoryComboBox.SelectedItem == Constant.DetectionValues.AllDetectionLabel)
                {
                    // Set a flag if all detections was selected
                    this.DetectionSelections.AllDetections = true;
                    this.DetectionSelections.RecognitionType = RecognitionType.Detection;
                }
                else
                {
                    this.DetectionSelections.EmptyDetections = false;
                    this.DetectionSelections.AllDetections = false;

                    // Find out if its a detection or a classification
                    string detectionCategory = this.database.GetDetectionCategoryFromLabel((string)this.DetectionCategoryComboBox.SelectedItem);
                    // this.DetectionSelections.DetectionCategory = this.database.GetDetectionCategoryFromLabel((string)this.DetectionCategoryComboBox.SelectedItem);

                    if (!string.IsNullOrEmpty(detectionCategory))
                    {
                        // The selected item is a detection
                        this.DetectionSelections.DetectionCategory = detectionCategory;
                        this.DetectionSelections.RecognitionType = RecognitionType.Detection;

                    }
                    else
                    {
                        // The selected item is a classification
                        this.DetectionSelections.ClassificationCategory = this.database.GetClassificationCategoryFromLabel((string)this.DetectionCategoryComboBox.SelectedItem);
                        this.DetectionSelections.RecognitionType = RecognitionType.Classification;
                    }
                }
            }
        }

        // Note that for either of these, we avoid a race condition where each tries to update the other by
        // setting this.ignoreSpinnerUpdates to true, which will cancel the operation
        private bool ignoreSpinnerUpdates;
        private void DetectionConfidenceSpinnerLower_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (this.IsLoaded == false || this.ignoreSpinnerUpdates)
            {
                return;
            }

            // If the user has set the upper spinner to less than the lower spinner,
            // reset it to the value of the lower spinner. That is, don't allow the user to 
            // go below the lower spinner value.
            if (this.DetectionConfidenceSpinnerLower.Value > this.DetectionConfidenceSpinnerHigher.Value)
            {
                this.ignoreSpinnerUpdates = true;
                this.DetectionConfidenceSpinnerLower.Value = this.DetectionConfidenceSpinnerHigher.Value;
                this.ignoreSpinnerUpdates = false;
            }
            this.SetDetectionCriteria();

            if (this.dontUpdateRangeSlider == false)
            {
                this.DetectionRangeSlider.LowerValue = this.DetectionConfidenceSpinnerLower.Value == null ? 0 : (double)this.DetectionConfidenceSpinnerLower.Value;
            }
            else
            {
                this.dontUpdateRangeSlider = false;
            }
            this.InitiateShowCountsOfMatchingFiles();
        }

        private void DetectionConfidenceSpinnerHigher_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (this.IsLoaded == false || this.ignoreSpinnerUpdates)
            {
                return;
            }

            // If the user has set the upper spinner to less than the lower spinner,
            // reset it to the value of the lower spinner. That is, don't allow the user to 
            // go below the lower spinner value.
            if (this.DetectionConfidenceSpinnerHigher.Value < this.DetectionConfidenceSpinnerLower.Value)
            {
                this.ignoreSpinnerUpdates = true;
                this.DetectionConfidenceSpinnerHigher.Value = this.DetectionConfidenceSpinnerLower.Value;
                this.ignoreSpinnerUpdates = false;
            }
            this.SetDetectionCriteria();

            if (this.dontUpdateRangeSlider == false)
            {
                this.DetectionRangeSlider.HigherValue = this.DetectionConfidenceSpinnerHigher.Value == null ? 0 : (double)this.DetectionConfidenceSpinnerHigher.Value;
                this.dontUpdateRangeSlider = false;
            }
            else
            {
                this.dontUpdateRangeSlider = false;
            }
            this.InitiateShowCountsOfMatchingFiles();
        }

        // Detection range slider callback - Upper range
        // Note that this does not invoke this.SetDetectionCriteria(), as that is done as a side effect of invoking the spinner
        private void DetectionRangeSlider_HigherValueChanged(object sender, RoutedEventArgs e)
        {
            // Round up the value to the nearest 2 decimal places,
            // and update the spinner (also in two decimal places) only if the value differs
            // This stops the spinner from updated if values change in the 3rd decimal place and beyond
            double value = Round2(this.DetectionRangeSlider.HigherValue);
            if (value != Round2(this.DetectionConfidenceSpinnerHigher.Value))
            {
                this.dontUpdateRangeSlider = true;
                this.DetectionConfidenceSpinnerHigher.Value = value;
                this.dontUpdateRangeSlider = false;
            }
        }

        // Detection range slider callback - Lower range
        // Note that this does not invoke this.SetDetectionCriteria(), as that is done as a side effect of invoking the spinner
        private void DetectionRangeSlider_LowerValueChanged(object sender, RoutedEventArgs e)
        {
            // Round up the value to the nearest 2 decimal places,
            // and update the spinner (also in two decimal places) only if the value differs
            // This stops the spinner from updated if values change in the 3rd decimal place and beyond
            double value = Round2(this.DetectionRangeSlider.LowerValue);
            if (value != Round2(this.DetectionConfidenceSpinnerLower.Value))
            {
                this.dontUpdateRangeSlider = true;
                this.DetectionConfidenceSpinnerLower.Value = value;
                this.dontUpdateRangeSlider = false;
            }
        }

        // Enable or disable the controls depending on the parameter
        private void EnableDetectionControls(bool isEnabled)
        {
            // Various confidence controls are enabled only if useDetections is set and the rank by confidence is unchecked
            bool confidenceControlsEnabled = isEnabled && !this.DetectionSelections.RankByConfidence;
            this.DetectionConfidenceSpinnerLower.IsEnabled = confidenceControlsEnabled;
            this.DetectionConfidenceSpinnerHigher.IsEnabled = confidenceControlsEnabled;
            this.DetectionRangeSlider.IsEnabled = confidenceControlsEnabled;
            this.ConfidenceLabel.FontWeight = confidenceControlsEnabled ? FontWeights.DemiBold : FontWeights.Normal;
            this.FromLabel.FontWeight = confidenceControlsEnabled ? FontWeights.DemiBold : FontWeights.Normal;
            this.ToLabel.FontWeight = confidenceControlsEnabled ? FontWeights.DemiBold : FontWeights.Normal;
            this.DetectionRangeSlider.RangeBackground = confidenceControlsEnabled ? Brushes.Gold : Brushes.LightGray;

            // There remainder depends upon the use detections isEnable state only
            this.DetectionCategoryComboBox.IsEnabled = isEnabled;
            this.CategoryLabel.FontWeight = isEnabled ? FontWeights.DemiBold : FontWeights.Normal;
            this.RankByConfidenceCheckbox.IsEnabled = isEnabled;
            this.RankByConfidenceCheckbox.FontWeight = isEnabled ? FontWeights.DemiBold : FontWeights.Normal;

            // CHECK THE ONES BELOW TO SEE IF THIS IS THE BEST WAY TO DO THESE
            this.SelectionGroupBox.IsEnabled = !this.database.CustomSelection.ShowMissingDetections;
            this.SelectionGroupBox.Background = this.database.CustomSelection.ShowMissingDetections ? Brushes.LightGray : Brushes.White;

            this.DetectionGroupBox.IsEnabled = !this.database.CustomSelection.ShowMissingDetections;
            this.DetectionGroupBox.Background = this.database.CustomSelection.ShowMissingDetections ? Brushes.LightGray : Brushes.White;

            if ((bool)this.ShowMissingDetectionsCheckbox.IsChecked || (bool)this.UseDetectionsCheckbox.IsChecked)
            {
                this.ResetToAllImagesButton.IsEnabled = true;
            }
        }
        #endregion

        #region Common to Selections and Detections
        private void CountTimer_Tick(object sender, EventArgs e)
        {
            this.countTimer.Stop();
            // This is set everytime a selectin is made
            if (this.dontCount == true)
            {
                return;
            }
            int count = this.database.CountAllFilesMatchingSelectionCondition(FileSelectionEnum.Custom);
            this.QueryMatches.Text = count > 0 ? count.ToString() : "0";
            this.OkButton.IsEnabled = count > 0; // Dusable OK button if there are no matches
        }

        // Start the timere that will show how many files match the current selection
        private void InitiateShowCountsOfMatchingFiles()
        {
            this.countTimer.Stop();
            this.countTimer.Start();
        }

        // Apply the selection if the Ok button is clicked
        private void OkButton_Click(object sender, RoutedEventArgs args)
        {
            if (GlobalReferences.DetectionsExists)
            {
                this.SetDetectionCriteria();
            }
            this.DialogResult = true;
        }

        // Cancel - exit the dialog without doing anythikng.
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
        #endregion

        #region Static helpers
        private static double Round2(double? value)
        {
            return value == null ? 0 : Math.Round((double)value, 2);
        }
        #endregion

        private void RankByConfidence_CheckedChanged(object sender, RoutedEventArgs e)
        {
            // Need to disable confidence sliders/spinners depending on the state of this checkbox and use detections
            // ALso need to restore state of this checkbox between repeated uses in Window_Loaded.
            this.DetectionSelections.RankByConfidence = this.RankByConfidenceCheckbox.IsChecked == true;
            this.InitiateShowCountsOfMatchingFiles();
            this.EnableDetectionControls((bool)this.UseDetectionsCheckbox.IsChecked);
        }
    }
}
