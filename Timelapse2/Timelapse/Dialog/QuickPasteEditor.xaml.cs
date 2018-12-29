using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Timelapse.Dialog
{
    // Given a QuickPasteEntry (a name and a list of QuickPasteItems),
    // allow the user to edit it.
    // Currently, the only thing that is editable is its name and whether a particular item's data should be included when pasted
    public partial class QuickPasteEditor : Window
    {
        public QuickPasteEntry quickPasteEntry;

        // Columns where fields will be placed in the grid
        private const int UseColumn = 1;
        private const int LabelColumn = 2;
        private const int ValueColumn = 3;

        public QuickPasteEditor(QuickPasteEntry quickPasteEntry)
        {
            InitializeComponent();
            this.quickPasteEntry = quickPasteEntry;
        }

        // When the window is loaded
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Adjust this dialog window position 
            Dialogs.SetDefaultDialogPosition(this);
            Dialogs.TryFitDialogWindowInWorkingArea(this);

            // Display the title of the QuickPasteEntry
            this.QuickPasteTitle.Text = this.quickPasteEntry.Title;
            this.QuickPasteTitle.TextChanged += QuickPasteTitle_TextChanged;
            // Build rows, each displaying successive items in the QuickPasteItems list
            BuildRows();
        }

        // Get the QuickPaste items, and build a row displaying each one of them
        private void BuildRows()
        {
            // We don't start at zero, as the 1st two grid rows are already filled.
            int gridRowIndex = 1;

            foreach (QuickPasteItem quickPasteItem in this.quickPasteEntry.Items)
            {
                ++gridRowIndex;
                RowDefinition gridRow = new RowDefinition()
                {
                     Height = GridLength.Auto
                };
                this.QuickPasteGridRows.RowDefinitions.Add(gridRow);
                BuildRow(quickPasteItem, gridRow, gridRowIndex);
            }
        }

        // Given a quickPasteItem (essential the information representing a single data control and its value),
        // add a row to the grid with controls that display that information,
        // and a checkbox that can be selected to indicate whether that information should be included in a paste operation
        private void BuildRow(QuickPasteItem quickPasteItem, RowDefinition gridRow, int gridRowIndex)
        {
            // USE Column: A checkbox to indicate whether the current search row should be used as part of the search
            Thickness thickness = new Thickness(0, 2, 0, 2);
            CheckBox useCurrentRow = new CheckBox()
            {
                Margin = thickness,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                IsChecked = quickPasteItem.Use,
                Tag = quickPasteItem
            };
            useCurrentRow.Checked += UseCurrentRow_CheckChanged;
            useCurrentRow.Unchecked += UseCurrentRow_CheckChanged;

            Grid.SetRow(useCurrentRow, gridRowIndex);
            Grid.SetColumn(useCurrentRow, UseColumn);
            this.QuickPasteGridRows.Children.Add(useCurrentRow);

            // LABEL column: The label associated with the control (Note: not the data label)
            TextBlock controlLabel = new TextBlock()
            {
                Margin = new Thickness(5),
                Text = quickPasteItem.Label,
                Foreground = quickPasteItem.Use ? Brushes.Black : Brushes.Gray,
            };
            Grid.SetRow(controlLabel, gridRowIndex);
            Grid.SetColumn(controlLabel, LabelColumn);
            this.QuickPasteGridRows.Children.Add(controlLabel);

            // Vaue column: The label associated with the control (Note: not the data label)
            TextBlock controlValue = new TextBlock()
            {
                Margin = new Thickness(5),
                Text = quickPasteItem.Value,
                Foreground = quickPasteItem.Use ? Brushes.Black : Brushes.Gray
            };
            Grid.SetRow(controlValue, gridRowIndex);
            Grid.SetColumn(controlValue, ValueColumn);
            this.QuickPasteGridRows.Children.Add(controlValue);
        }

        // Invoke when the user clicks the checkbox to enable or disable the data row
        private void UseCurrentRow_CheckChanged(object sender, RoutedEventArgs e)
        {
            CheckBox cbox = sender as CheckBox;

            // Enable or disable the controls on that row to reflect whether the checkbox is checked or unchecked
            int row = Grid.GetRow(cbox);

            TextBlock label = this.GetGridElement<TextBlock>(LabelColumn, row);
            TextBlock value = this.GetGridElement<TextBlock>(ValueColumn, row);
            label.Foreground = cbox.IsChecked == true ? Brushes.Black : Brushes.Gray;
            value.Foreground = cbox.IsChecked == true ? Brushes.Black : Brushes.Gray;

            // Update the QuickPaste row data structure to reflect the current checkbox state
            QuickPasteItem quickPasteRow = (QuickPasteItem)cbox.Tag;
            quickPasteRow.Use = cbox.IsChecked == true;
        }

        // Get the corresponding grid element from a given column, row, 
        private TElement GetGridElement<TElement>(int column, int row) where TElement : UIElement
        {
            return (TElement)this.QuickPasteGridRows.Children.Cast<UIElement>().First(control => Grid.GetRow(control) == row && Grid.GetColumn(control) == column);
        }

        #region Ok buttons
        // Apply the selection if the Ok button is clicked
        private void OkButton_Click(object sender, RoutedEventArgs args)
        {
            this.DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs args)
        {
            this.DialogResult = false;
        }
        #endregion

        private void QuickPasteTitle_TextChanged(object sender, TextChangedEventArgs e)
        {
            this.quickPasteEntry.Title = this.QuickPasteTitle.Text;
        }
    }
}
