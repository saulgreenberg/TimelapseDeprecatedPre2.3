using System;
using System.Collections.Generic;
using System.Windows;
using Timelapse.Dialog;

namespace Timelapse.Editor.Dialog
{
    public partial class EditChoiceList : Window
    {
        private static readonly string[] NewLineDelimiter = { Environment.NewLine };
        private UIElement positionReference;
        private bool includesEmptyChoice;

        public List<string> Choices { get; private set; }

        public EditChoiceList(UIElement positionReference, List<string> choices, bool includesEmptyChoice, Window owner)
        {
            this.InitializeComponent();
            this.includesEmptyChoice = includesEmptyChoice;
            this.ChoiceList.Text = String.Join(Environment.NewLine, choices);
            this.Choices = choices;

            this.IncludeEmptyChoiceCheckBox.IsChecked = this.includesEmptyChoice;

            this.Owner = owner;
            this.positionReference = positionReference;
        }

        // Position the window so it appears as a popup with its bottom aligned to the top of its owner button
        // Add callbacks as needed
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Point topLeft = this.positionReference.PointToScreen(new Point(0, 0));
            this.Top = topLeft.Y - this.ActualHeight;
            if (this.Top < 0)
            {
                this.Top = 0;
            }
            this.Left = topLeft.X;

            // On some older Windows versions the above positioning doesn't work as the list ends up to the right of the main window
            // Check to make sure it's in the main window, and if not, we try to position it there
            if (Application.Current != null)
            {
                double choiceRightSide = this.Left + this.ActualWidth;
                double mainWindowRightSide = Application.Current.MainWindow.Left + Application.Current.MainWindow.ActualWidth;
                if (choiceRightSide > mainWindowRightSide)
                {
                    this.Left = mainWindowRightSide - this.ActualWidth - 100;
                }
            }
            Dialogs.TryFitDialogWindowInWorkingArea(this);
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.ChoiceList.Text = this.TrimLinesAndRemoveEmptyLines(this.ChoiceList.Text);

            if (this.IncludeEmptyChoiceCheckBox.IsChecked == true && this.ChoiceList.Text.Length != 0)
            {
                // Include the empty choice at the end if it doesn't already exist
                if (this.ChoiceList.Text.EndsWith(Constant.ControlMiscellaneous.EmptyChoiceItem) == false)
                {
                    this.ChoiceList.Text += Environment.NewLine + Constant.ControlMiscellaneous.EmptyChoiceItem;
                }
            }
            this.Choices = new List<string>(this.ChoiceList.Text.Split(EditChoiceList.NewLineDelimiter, StringSplitOptions.RemoveEmptyEntries));
            this.DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        // Transform the list by trimming leading and trailing white space for each line, removing empty lines, and removing duplicate items
        private string TrimLinesAndRemoveEmptyLines(string textlist)
        {
            List<string> trimmedchoices = new List<string>();
            string trimmedchoice;
            List<string> choices = new List<string>(textlist.Split(EditChoiceList.NewLineDelimiter, StringSplitOptions.RemoveEmptyEntries));

            foreach (string choice in choices)
            {
                trimmedchoice = choice.Trim();
                if (String.IsNullOrWhiteSpace(choice) == false && trimmedchoices.Contains(trimmedchoice) == false)
                {
                    trimmedchoices.Add(trimmedchoice);
                }
            }
            return string.Join(string.Join(String.Empty, EditChoiceList.NewLineDelimiter), trimmedchoices);
        }
    }
}
