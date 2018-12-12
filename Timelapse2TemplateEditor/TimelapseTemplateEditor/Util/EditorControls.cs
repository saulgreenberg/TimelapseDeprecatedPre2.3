using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Controls;
using Timelapse.Database;
using Xceed.Wpf.Toolkit;

namespace Timelapse.Editor.Util
{
    /// <summary>Generates controls in the provided wrap panel based upon the information in the data grid templateTable.</summary>
    /// <remarks>
    /// It is meant to approximate what the controls will look like when rendered in the Timelapse UX by DataEntryControls but the
    /// two classes contain distinct code as rendering an immutable set of data entry controls is significantly different from the
    /// mutable set of controls which don't accept data in the editor.  Reusing the layout code in the DataEntryControl hierarchy
    /// is desirable but not currently feasible due to reliance on DataEntryControls.Propagate.
    /// </remarks>
    internal class EditorControls
    {
        public void Generate(EditorWindow mainWindow, WrapPanel parent, DataTableBackedList<ControlRow> templateTable)
        {
            // used for styling all content and label controls except ComboBoxes since the combo box style is commented out in DataEntryControls.xaml
            // and defined instead in MainWindow.xaml as an exception workaround
            DataEntryControls styleProvider = new DataEntryControls();

            parent.Children.Clear();
            foreach (ControlRow control in templateTable)
            {
                // instantiate control UX objects
                StackPanel stackPanel;
                switch (control.Type)
                {
                    case Constant.Control.Note:
                    case Constant.DatabaseColumn.Date:
                    case Constant.DatabaseColumn.File:
                    case Constant.DatabaseColumn.Folder:
                    case Constant.DatabaseColumn.RelativePath:
                    case Constant.DatabaseColumn.Time:
                        Label noteLabel = this.CreateLabel(styleProvider, control);
                        TextBox noteContent = this.CreateTextBox(styleProvider, control);
                        stackPanel = this.CreateStackPanel(styleProvider, noteLabel, noteContent);
                        break;
                    case Constant.Control.Counter:
                        RadioButton counterLabel = this.CreateCounterLabelButton(styleProvider, control);
                        IntegerUpDown counterContent = this.CreateIntegerUpDown(styleProvider, control);
                        stackPanel = this.CreateStackPanel(styleProvider, counterLabel, counterContent);
                        counterLabel.IsTabStop = false;
                        counterContent.GotFocus += this.Control_GotFocus;
                        counterContent.LostFocus += this.Control_LostFocus;
                        break;
                    case Constant.Control.Flag:
                    case Constant.DatabaseColumn.DeleteFlag:
                        Label flagLabel = this.CreateLabel(styleProvider, control);
                        CheckBox flagContent = this.CreateFlag(styleProvider, control);
                        flagContent.IsChecked = String.Equals(control.DefaultValue, Constant.BooleanValue.True, StringComparison.OrdinalIgnoreCase) ? true : false;
                        stackPanel = this.CreateStackPanel(styleProvider, flagLabel, flagContent);
                        break;
                    case Constant.Control.FixedChoice:
                    case Constant.DatabaseColumn.ImageQuality:
                        Label choiceLabel = this.CreateLabel(styleProvider, control);
                        ComboBox choiceContent = this.CreateComboBox(styleProvider, control);
                        stackPanel = this.CreateStackPanel(styleProvider, choiceLabel, choiceContent);
                        break;
                    case Constant.DatabaseColumn.DateTime:
                        Label dateTimeLabel = this.CreateLabel(styleProvider, control);
                        DateTimePicker dateTimeContent = this.CreateDateTimePicker(control);
                        stackPanel = this.CreateStackPanel(styleProvider, dateTimeLabel, dateTimeContent);
                        break;
                    case Constant.DatabaseColumn.UtcOffset:
                        Label utcOffsetLabel = this.CreateLabel(styleProvider, control);
                        UtcOffsetUpDown utcOffsetContent = this.CreateUtcOffsetPicker(control);
                        stackPanel = this.CreateStackPanel(styleProvider, utcOffsetLabel, utcOffsetContent);
                        break;
                    default:
                        throw new NotSupportedException(String.Format("Unhandled control type {0}.", control.Type));
                }

                stackPanel.Tag = control.DataLabel;
                if (control.Visible == false)
                {
                    stackPanel.Visibility = Visibility.Collapsed;
                }

                // add control to wrap panel
                parent.Children.Add(stackPanel);
            }
        }

        public static bool IsStandardControlType(string controlType)
        {
            return Constant.Control.StandardTypes.Contains(controlType);
        }

        private DateTimePicker CreateDateTimePicker(ControlRow control)
        {
            DateTimePicker dateTimePicker = new DateTimePicker()
            {
                ToolTip = control.Tooltip,
                Width = control.Width,
                CultureInfo = System.Globalization.CultureInfo.CreateSpecificCulture("en-US")
            };
            DataEntryHandler.Configure(dateTimePicker, Constant.ControlDefault.DateTimeValue.DateTime);
            dateTimePicker.GotFocus += this.Control_GotFocus;
            dateTimePicker.LostFocus += this.Control_LostFocus;
            return dateTimePicker;
        }

        // Returns a stack panel containing two controls
        // The stack panel ensures that controls are layed out as a single unit with certain spatial characteristcs 
        // i.e.,  a given height, right margin, where contents will not be broken durring (say) panel wrapping
        private StackPanel CreateStackPanel(DataEntryControls styleProvider, Control label, Control content)
        {
            StackPanel stackPanel = new StackPanel();
            stackPanel.Children.Add(label);
            stackPanel.Children.Add(content);

            Style style = styleProvider.FindResource(Constant.ControlStyle.ContainerStyle) as Style;
            stackPanel.Style = style;
            return stackPanel;
        }

        private UtcOffsetUpDown CreateUtcOffsetPicker(ControlRow control)
        {
            UtcOffsetUpDown utcOffsetPicker = new UtcOffsetUpDown()
            {
                ToolTip = control.Tooltip,
                Value = Constant.ControlDefault.DateTimeValue.Offset,
                Width = control.Width
            };
            utcOffsetPicker.GotFocus += this.Control_GotFocus;
            utcOffsetPicker.LostFocus += this.Control_LostFocus;
            return utcOffsetPicker;
        }

        private Label CreateLabel(DataEntryControls styleProvider, ControlRow control)
        {
            Label label = new Label()
            {
                Content = control.Label,
                ToolTip = control.Tooltip,
                Style = styleProvider.FindResource(ControlLabelStyle.DefaultLabel.ToString()) as Style
            };
            return label;
        }

        private TextBox CreateTextBox(DataEntryControls styleProvider, ControlRow control)
        {
            TextBox textBox = new TextBox()
            {
                Text = control.DefaultValue,
                ToolTip = control.Tooltip,
                Width = control.Width,
                Style = styleProvider.FindResource(ControlContentStyle.NoteTextBox.ToString()) as Style
            };
            return textBox;
        }

        private IntegerUpDown CreateIntegerUpDown(DataEntryControls styleProvider, ControlRow control)
        {
            IntegerUpDown integerUpDown = new IntegerUpDown()
            {
                Text = control.DefaultValue,
                ToolTip = control.Tooltip,
                Minimum = 0,
                Width = control.Width + 18, // accounts for the width of the spinner
                DisplayDefaultValueOnEmptyText = true,
                DefaultValue = null,
                UpdateValueOnEnterKey = true,
                Style = styleProvider.FindResource(ControlContentStyle.CounterTextBox.ToString()) as Style
            }; 
            return integerUpDown;
        }

        private RadioButton CreateCounterLabelButton(DataEntryControls styleProvider, ControlRow control)
        {
            RadioButton radioButton = new RadioButton()
            {
                GroupName = "DataEntryCounter",
                Content = control.Label,
                ToolTip = control.Tooltip,
                Style = styleProvider.FindResource(ControlLabelStyle.CounterButton.ToString()) as Style
            };
            return radioButton;
        }

        private CheckBox CreateFlag(DataEntryControls styleProvider, ControlRow control)
        {
            CheckBox checkBox = new CheckBox()
            {
                Visibility = Visibility.Visible,
                ToolTip = control.Tooltip,
                Style = styleProvider.FindResource(ControlContentStyle.FlagCheckBox.ToString()) as Style
            };
            checkBox.GotFocus += this.Control_GotFocus;
            checkBox.LostFocus += this.Control_LostFocus;
            return checkBox;
        }

        private ComboBox CreateComboBox(DataEntryControls styleProvider, ControlRow control)
        {
            ComboBox comboBox = new ComboBox()
            {
                ToolTip = control.Tooltip,
                Width = control.Width,
                Style = styleProvider.FindResource(ControlContentStyle.ChoiceComboBox.ToString()) as Style
            };

            // Add items to the combo box
            List<string> choices = control.GetChoices(out bool includesEmptyChoice);
            foreach (string choice in choices)
            {
                    comboBox.Items.Add(choice);
            }
            if (includesEmptyChoice)
            {
                // put empty choices at the end below a separator for visual clarity
                comboBox.Items.Add(new Separator());
                comboBox.Items.Add(String.Empty);
            }
            comboBox.SelectedIndex = 0;
            return comboBox;
        }

        // HIghlight control when it gets the focus (simulates aspects of tab control in Timelapse)
        private void Control_GotFocus(object sender, RoutedEventArgs e)
        {
            Control control = sender as Control;
            control.BorderThickness = new Thickness(Constant.Control.BorderThicknessHighlight);
            control.BorderBrush = Constant.Control.BorderColorHighlight;
        }

        private void Control_LostFocus(object sender, RoutedEventArgs e)
        {
            Control control = sender as Control;
            control.BorderThickness = new Thickness(Constant.Control.BorderThicknessNormal);
            control.BorderBrush = Constant.Control.BorderColorNormal;
        }
    }
}
