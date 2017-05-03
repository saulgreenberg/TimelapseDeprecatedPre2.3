using System;
using System.Windows;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    public partial class MessageBox : Window
    {
        public MessageBox(string title, Window owner)
            : this(title, owner, MessageBoxButton.OK)
        {
        }

        public MessageBox(string title, Window owner, MessageBoxButton buttonType)
        {
            if (String.IsNullOrWhiteSpace(title))
            {
                throw new ArgumentException("A title must be specified for the message box.", "title");
            }
            if (owner == null)
            {
                throw new ArgumentNullException("owner");
            }

            this.InitializeComponent();
            this.Message.Title = title;
            this.Owner = owner;
            this.Title = title;

            switch (buttonType)
            {
                case MessageBoxButton.OK:
                    this.OkButton.IsCancel = true;
                    this.CancelButton.IsCancel = false;
                    this.CancelButton.IsEnabled = false;
                    break;
                case MessageBoxButton.OKCancel:
                    this.CancelButton.Visibility = Visibility.Visible;
                    break;
                case MessageBoxButton.YesNo:
                    this.OkButton.Content = "_Yes";
                    this.CancelButton.Content = "_No";
                    this.CancelButton.Visibility = Visibility.Visible;
                    break;
                case MessageBoxButton.YesNoCancel:
                default:
                    throw new ArgumentOutOfRangeException("buttonType", String.Format("Unhandled button type {0}.", buttonType));
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Utilities.SetDefaultDialogPosition(this);
            Utilities.TryFitWindowInWorkingArea(this);
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
    }
}
