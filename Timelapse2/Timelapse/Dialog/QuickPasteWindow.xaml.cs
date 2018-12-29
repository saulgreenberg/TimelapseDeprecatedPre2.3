using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Timelapse;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for QuickPasteWindow.xaml
    /// </summary>
    public partial class QuickPasteWindow : Window
    {
        private List<QuickPasteEntry> quickPasteEntries;

        #region Events
        public event EventHandler<QuickPasteEventArgs> QuickPasteEvent;

        private void SendQuickPasteEvent(QuickPasteEventArgs e)
        {
            this.QuickPasteEvent?.Invoke(this, e);
        }
        #endregion

        public List<QuickPasteEntry> QuickPasteEntries
        { 
            get {return quickPasteEntries;}
            set {quickPasteEntries = value;}
        }

        public QuickPasteWindow()
        {
            InitializeComponent();
        }

        // When the window is loaded
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Adjust this dialog window position 
            Dialogs.SetDefaultDialogPosition(this);
            Dialogs.TryFitDialogWindowInWorkingArea(this);

            // Build the window contents
            Refresh(this.quickPasteEntries);
        }

        public void Refresh(List<QuickPasteEntry> quickPasteEntries)
        {
            // Update the quickPasteEntries
            this.quickPasteEntries = quickPasteEntries;

            this.QuickPasteGrid.RowDefinitions.Clear();
            int gridRowIndex = 0;

            foreach (QuickPasteEntry quickPasteEntry in this.QuickPasteEntries)
            {
                // Create the tooltip text for the QuickPaste control
                string tooltipText = String.Empty;
                foreach (QuickPasteItem item in quickPasteEntry.Items)
                {
                    if (item.Use)
                    {
                        tooltipText += item.Label + ": " + item.Value.ToString() + Environment.NewLine;
                    }
                }

                // Create and configure the QuickPaste control, and add its callbacks
                Button quickPasteControl = new Button()
                {
                    Style = this.Owner.FindResource("CopyPreviousButtonStyle") as Style,
                    Content = quickPasteEntry.Title,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    ToolTip = tooltipText,
                    Tag = quickPasteEntry
                };

                // Create a Context Menu for each button that allows the user to
                // - Delete the item
                ContextMenu contextMenu = new ContextMenu();
                quickPasteControl.ContextMenu = contextMenu;

                MenuItem editItem = new MenuItem()
                {
                    Header = "Edit",
                    Tag = quickPasteEntry
                };
                editItem.Click += EditItem_Click;
                contextMenu.Items.Add(editItem);

                MenuItem deleteItem = new MenuItem()
                {
                    Header = "Delete",
                    Tag = quickPasteEntry
                };
                deleteItem.Click += DeleteItem_Click;
                contextMenu.Items.Add(deleteItem);



                quickPasteControl.Click += QuickPasteControl_Click;
                quickPasteControl.MouseEnter += QuickPasteControl_MouseEnter;
                quickPasteControl.MouseLeave += QuickPasteControl_MouseLeave;

                // Create a grid row and add the QuickPaste control to it
                RowDefinition gridRow = new RowDefinition()
                {
                    Height = GridLength.Auto
                };
                this.QuickPasteGrid.RowDefinitions.Add(gridRow); Grid.SetRow(quickPasteControl, gridRowIndex);
                Grid.SetColumn(quickPasteControl, gridRowIndex);
                this.QuickPasteGrid.Children.Add(quickPasteControl);
                gridRowIndex++;
            }
        }

        private void EditItem_Click(object sender, RoutedEventArgs e)
        {
            MenuItem menuItem = sender as MenuItem;
            QuickPasteEntry quickPasteEntry = (QuickPasteEntry)menuItem.Tag;
            this.SendQuickPasteEvent(new QuickPasteEventArgs(quickPasteEntry, 4)); // CHANGE NUMBERS TO ENUM
        }

        private void DeleteItem_Click(object sender, RoutedEventArgs e)
        {
            MenuItem menuItem = sender as MenuItem;
            QuickPasteEntry quickPasteEntry = (QuickPasteEntry)menuItem.Tag;
            this.SendQuickPasteEvent(new QuickPasteEventArgs(quickPasteEntry, 3)); // CHANGE NUMBERS TO ENUM
        }

        private void QuickPasteControl_MouseEnter(object sender, MouseEventArgs e)
        {
            Button button = sender as Button;
            QuickPasteEntry quickPasteEntry = (QuickPasteEntry)button.Tag;
            this.SendQuickPasteEvent(new QuickPasteEventArgs(quickPasteEntry, 1)); // CHANGE NUMBERS TO ENUM
        }

        private void QuickPasteControl_MouseLeave(object sender, MouseEventArgs e)
        {
            Button button = sender as Button;
            QuickPasteEntry quickPasteEntry = (QuickPasteEntry)button.Tag;
            this.SendQuickPasteEvent(new QuickPasteEventArgs(quickPasteEntry, 2)); // CHANGE NUMBERS TO ENUM
        }

        private void QuickPasteControl_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            QuickPasteEntry quickPasteEntry = (QuickPasteEntry) button.Tag;
            this.SendQuickPasteEvent(new QuickPasteEventArgs(quickPasteEntry, 0)); // CHANGE NUMBERS TO ENUM
        }

        private void NewQuickPasteEntryButton_Click(object sender, RoutedEventArgs e)
        {
            this.SendQuickPasteEvent(new QuickPasteEventArgs(null, 5)); // CHANGE NUMBERS TO ENUM
        }
    }
}
