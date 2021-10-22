using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using Timelapse.Controls;
using Timelapse.Database;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for TestPopulate.xaml
    /// </summary>
    public partial class TestPopulate : BusyableDialogWindow
    {
        private string FilePath;
        private FileDatabase FileDatabase;

        public TestPopulate(Window owner, FileDatabase fileDatabase, string filePath) : base(owner)
        {
            InitializeComponent();
            this.FilePath = filePath;
            this.FileDatabase = fileDatabase;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.MetadataGrid.viewModel.FilePath = this.FilePath;

            // Construct a dictionary of the available note fields as labels:datalabels
            // and a list of only the note field labels which will be used to populate the ComboBoxes in the datagrid
            Dictionary<string, string> collectLabels = new Dictionary<string, string>();
            foreach (ControlRow control in this.FileDatabase.Controls)
            {
                if (control.Type == Constant.Control.Note)
                {
                    collectLabels.Add(control.DataLabel, control.Label);
                }
            }
            this.MetadataGrid.DictDataLabel_Label = collectLabels;
            this.MetadataGrid.SelectedMetadata.CollectionChanged += this.SelectedMetadata_CollectionChanged;
            //this.SelectedMetadata = this.MetadataGrid.SelectedMetadata;
            //this.SelectedMetadata.CollectionChanged += this.SelectedMetadata_CollectionChanged;
        }

        private void SelectedMetadata_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // Check if there are items in the selectedMetadataList, and if so enable the Populate button
            if (this.MetadataGrid.SelectedMetadata != null && this.MetadataGrid.SelectedMetadata.Count > 0)
            {
                // We have items selected
            }
            else
            {
                // Nothing is selected
            }
            Show_Click(null, null);
        }

        #region Closing
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            //this.DialogResult = this.Token.IsCancellationRequested || this.IsAnyDataUpdated;
        }
        #endregion

        private void Show_Click(object sender, RoutedEventArgs e)
        {
            this.LBFeedback.Items.Clear();
            foreach (KeyValuePair<string,string> kvp in this.MetadataGrid.SelectedMetadata)
            {
                this.LBFeedback.Items.Add(kvp.Key + "|" + kvp.Value);
            }
        }
    }
}
