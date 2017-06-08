using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for TemplateChangedAndUpdate.xaml
    /// </summary>
    public partial class TemplateChangedAndUpdate : Window
    {
        public TemplateChangedAndUpdate(List<string> dataLabelsInImageButNotTemplateDatabase, List<string> dataLabelsInTemplateButNotImageDatabase, Window owner)
        {
            this.InitializeComponent();
            this.Owner = owner;

            // We have data fields that will be added
            if (dataLabelsInTemplateButNotImageDatabase.Count > 0)
            {
                // data fields that could be added to the image set
                this.TextBlockDetails.Inlines.Add(new Run { FontWeight = FontWeights.Bold, Text = "Added data fields " });
                this.TextBlockDetails.Inlines.Add(new Run { Text = "to be included to your image set:" + Environment.NewLine });

                foreach (string datalabel in dataLabelsInTemplateButNotImageDatabase)
                {
                    this.TextBlockDetails.Inlines.Add(new Run { Text = "  o " + datalabel + Environment.NewLine});
                }
                this.TextBlockDetails.Inlines.Add(new Run { Text = Environment.NewLine});
            }

            if (dataLabelsInImageButNotTemplateDatabase.Count > 0)
            {
                // data fields that could be deleted from the Image Set
                this.TextBlockDetails.Inlines.Add(new Run { FontWeight = FontWeights.Bold, Text = "Deleted data fields " });
                this.TextBlockDetails.Inlines.Add(new Run { Text = "to be deleted from your image set. " });
                this.TextBlockDetails.Inlines.Add(new Run { FontWeight = FontWeights.Bold, Text = "Any associated data is also permanently deleted: " + Environment.NewLine });
                foreach (string datalabel in dataLabelsInImageButNotTemplateDatabase)
                {
                    this.TextBlockDetails.Inlines.Add(new Run { Text = "  o " + datalabel + Environment.NewLine});
                }
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Utilities.SetDefaultDialogPosition(this);
            Utilities.TryFitWindowInWorkingArea(this);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        private void UseOriginalTemplateButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }
    }
}