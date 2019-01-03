using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Timelapse.Dialog;

namespace Timelapse.QuickPaste
{
    // A set of buttons including context menus that lets the user create and use quick paste controls
    public partial class QuickPasteWindow : Window
    {
        #region Events
        public event EventHandler<QuickPasteEventArgs> QuickPasteEvent;

        private void SendQuickPasteEvent(QuickPasteEventArgs e)
        {
            this.QuickPasteEvent?.Invoke(this, e);
        }
        #endregion

        public List<QuickPasteEntry> QuickPasteEntries
        { 
            get { return quickPasteEntries; }
            set { quickPasteEntries = value; }
        }

        private List<QuickPasteEntry> quickPasteEntries;

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

            // Clear the QuickPasteGrid, so we can start afresh
            this.QuickPasteGrid.RowDefinitions.Clear();
            this.QuickPasteGrid.Children.Clear();
            int gridRowIndex = 0;

            foreach (QuickPasteEntry quickPasteEntry in this.QuickPasteEntries)
            {
                // Create the tooltip text for the QuickPaste control
                string tooltipText = String.Empty;
                foreach (QuickPasteItem item in quickPasteEntry.Items)
                {
                    if (item.Use)
                    {
                        if (tooltipText != String.Empty)
                        {
                            tooltipText += Environment.NewLine;
                        }
                        tooltipText += item.Label + ": " + item.Value.ToString();
                    }
                }

                // Create and configure the QuickPaste control, and add its callbacks
                Button quickPasteControl = new Button()
                {
                    Style = this.Owner.FindResource("QuickPasteButtonStyle") as Style,
                    Content = quickPasteEntry.Title,
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
                this.QuickPasteGrid.RowDefinitions.Add(gridRow);
                Grid.SetRow(quickPasteControl, gridRowIndex);
                Grid.SetColumn(quickPasteControl, gridRowIndex);
                this.QuickPasteGrid.Children.Add(quickPasteControl); 
                 gridRowIndex++;
            }
        }

        // Check if the mouse is over any of the quickPasteControl buttons
        // If so, we should refresh the preview with that button's quickpaste entry
        public void RefreshQuickPasteWindowPreviewAsNeeded()
        { 
            // If the quickPaste Window is visible
            if (this.IsEnabled == false && this.IsLoaded == false)
            {
                return ;
            }
            foreach (Button quickPasteControl in this.QuickPasteGrid.Children)
            {
                if (quickPasteControl.IsMouseOver)
                {
                    this.SendQuickPasteEvent(new QuickPasteEventArgs((QuickPasteEntry) quickPasteControl.Tag, QuickPasteEventIdentifierEnum.MouseEnter));
                    return;
                }
            }
        }

        // Generate Event: New quickpaste emtru
        private void NewQuickPasteEntryButton_Click(object sender, RoutedEventArgs e)
        {
            this.SendQuickPasteEvent(new QuickPasteEventArgs(null, QuickPasteEventIdentifierEnum.New));
        }

        // Generate Event: Edit the quickpaste emtru
        private void EditItem_Click(object sender, RoutedEventArgs e)
        {
            MenuItem menuItem = sender as MenuItem;
            QuickPasteEntry quickPasteEntry = (QuickPasteEntry)menuItem.Tag;
            this.SendQuickPasteEvent(new QuickPasteEventArgs(quickPasteEntry, QuickPasteEventIdentifierEnum.Edit));
        }

        // Generate Event: Delete the quickpaste emtru
        private void DeleteItem_Click(object sender, RoutedEventArgs e)
        {
            MenuItem menuItem = sender as MenuItem;
            QuickPasteEntry quickPasteEntry = (QuickPasteEntry)menuItem.Tag;
            this.SendQuickPasteEvent(new QuickPasteEventArgs(quickPasteEntry, QuickPasteEventIdentifierEnum.Delete)); 
        }

        // Generate Event: MouseEnter on the quickpaste control
        private void QuickPasteControl_MouseEnter(object sender, MouseEventArgs e)
        {
            Button button = sender as Button;
            QuickPasteEntry quickPasteEntry = (QuickPasteEntry)button.Tag;
            this.SendQuickPasteEvent(new QuickPasteEventArgs(quickPasteEntry, QuickPasteEventIdentifierEnum.MouseEnter)); 
        }

        // Generate Event: MouseLeave on the quickpaste control
        private void QuickPasteControl_MouseLeave(object sender, MouseEventArgs e)
        {
            Button button = sender as Button;
            QuickPasteEntry quickPasteEntry = (QuickPasteEntry)button.Tag;
            this.SendQuickPasteEvent(new QuickPasteEventArgs(quickPasteEntry, QuickPasteEventIdentifierEnum.MouseLeave));
        }

        // Generate Event: Select the quickpaste entry (quickpaste control has been activated)
        private void QuickPasteControl_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            QuickPasteEntry quickPasteEntry = (QuickPasteEntry)button.Tag;
            this.SendQuickPasteEvent(new QuickPasteEventArgs(quickPasteEntry, QuickPasteEventIdentifierEnum.Paste));
        }
    }
}
