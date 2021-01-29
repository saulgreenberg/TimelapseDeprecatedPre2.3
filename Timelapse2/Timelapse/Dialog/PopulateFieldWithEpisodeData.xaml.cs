﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Controls;
using Timelapse.Database;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for PopulateFieldWithEpisodeData.xaml
    /// </summary>
    public partial class PopulateFieldWithEpisodeData : BusyableDialogWindow
    {
        #region Private Variables
        private readonly FileDatabase fileDatabase;
        private readonly Dictionary<string, string> dataLabelByLabel;
        private string dataFieldLabel = String.Empty;
        private double TotalImages;
        private double SingleCount;
        private double EpisodeCount;
        private double EpisodeNoSingletonsCount;

        // Tracks whether any changes to the data or database are made
        private bool IsAnyDataUpdated;
        private bool IncludeAnEpisodeIDNumber = true;
        private bool SingletonAsSingle = true;
        private bool SingletonAsZero;
        private bool SingletonAsEpisodeNumber;
        #endregion
        public PopulateFieldWithEpisodeData(Window owner, FileDatabase fileDatabase) : base(owner)
        {
            ThrowIf.IsNullArgument(fileDatabase, nameof(fileDatabase));
            this.dataLabelByLabel = new Dictionary<string, string>();
            this.fileDatabase = fileDatabase;
            this.InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Set up a progress handler that will update the progress bar
            this.InitalizeProgressHandler(this.BusyCancelIndicator);

            // Set up the initial UI and values
            this.CheckBoxIncludeEpisodeID.IsChecked = this.IncludeAnEpisodeIDNumber;
            this.RBSingletonEpisodeNumber.IsChecked = SingletonAsEpisodeNumber;
            this.RBSingletonSingle.IsChecked = SingletonAsSingle;
            this.RBSingletonZero.IsChecked = SingletonAsZero;

            this.CheckBoxIncludeEpisodeID.Checked += CheckBoxIncludeEpisodeID_CheckChanged;
            this.CheckBoxIncludeEpisodeID.Unchecked += CheckBoxIncludeEpisodeID_CheckChanged;
            this.RBSingletonEpisodeNumber.Checked += RB_CheckChanged;
            this.RBSingletonEpisodeNumber.Unchecked += RB_CheckChanged;
            this.RBSingletonSingle.Checked += RB_CheckChanged;
            this.RBSingletonSingle.Unchecked += RB_CheckChanged;
            this.RBSingletonZero.Checked += RB_CheckChanged;
            this.RBSingletonZero.Unchecked += RB_CheckChanged;

            // Construct a list showing the available note fields in the combobox
            foreach (ControlRow control in this.fileDatabase.Controls)
            {
                if (control.Type == Constant.Control.Note)
                {
                    this.dataLabelByLabel.Add(control.Label, control.DataLabel);
                    this.ComboBoxSelectNoteField.Items.Add(control.Label);
                }
            }

            this.IncludeAnEpisodeIDNumber = this.CheckBoxIncludeEpisodeID.IsChecked == true;

            // Show the current settings
            this.RunCurrentSettings.Text = String.Format(Episodes.TimeThreshold.ToString("g"));
            this.ShowExampleFormat();
        }

        #region Closing and Disposing
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            this.DialogResult = this.Token.IsCancellationRequested || this.IsAnyDataUpdated;
        }

        #endregion
        #region Button callbacks
        private async void Start_Click(object sender, RoutedEventArgs e)
        {
            // Update the UI before starting the operation, 
            this.CancelButton.IsEnabled = false;
            this.CancelButton.IsEnabled = false;
            this.StartDoneButton.Content = "_Processing";
            this.StartDoneButton.Click -= this.Start_Click;
            this.StartDoneButton.Click += this.Done_Click;
            this.StartDoneButton.IsEnabled = false;
            this.BusyCancelIndicator.IsBusy = true;
            this.WindowCloseButtonIsEnabled(false);

            // This call does all the actual populating...
            //ObservableCollection<KeyValuePair<string, string>> keyValueList = await this.PopulateAsync().ConfigureAwait(true);
            bool isCompleted = await this.PopulateAsync().ConfigureAwait(true);

            // Update the UI to its final state
            this.StartDoneButton.IsEnabled = true;
            this.StartDoneButton.Content = "_Done";
            this.BusyCancelIndicator.IsBusy = false;
            this.WindowCloseButtonIsEnabled(true);
            this.TextBlockFeedbackLine1.Text = isCompleted
                ? String.Format("Done, with {0} files processed.", TotalImages)
                : "Operation camcelled.";
            this.TextBlockFeedbackLine2.Text = isCompleted
                ? String.Format("Found {0} single image{1}, and {2} episode{3}.", SingleCount, SingleCount == 1 ? String.Empty : "s", EpisodeNoSingletonsCount, EpisodeNoSingletonsCount == 1 ? String.Empty : "s")
                : "No changes were made";
            this.PrimaryPanel.Visibility = Visibility.Collapsed;
            this.FeedbackPanel.Visibility = Visibility.Visible;
        }

        private async Task<bool> PopulateAsync()
        {
            return await Task.Run(() =>
            {
                string dataLabelToUpdate = this.dataLabelByLabel[this.dataFieldLabel];
                this.TotalImages = this.fileDatabase.CountAllCurrentlySelectedFiles;
                int percentDone = 0;
                ObservableCollection<KeyValuePair<string, string>> keyValueList = new ObservableCollection<KeyValuePair<string, string>>();
                List<ColumnTuplesWithWhere> imagesToUpdate = new List<ColumnTuplesWithWhere>();

                //for (int imageIndex = 0; imageIndex < totalImages; ++imageIndex)
                int imageIndex = 0;
                string singletonData;
                while (imageIndex < TotalImages)
                {
                    Episodes.Reset();
                    Episodes.EpisodeGetEpisodesInRange(this.fileDatabase.FileTable, imageIndex, Int32.MaxValue);

                    // Provide feedback if the operation was cancelled during the database update
                    if (Token.IsCancellationRequested == true)
                    {
                        keyValueList.Clear();
                        keyValueList.Add(new KeyValuePair<string, string>("Cancelled", "No changes were made"));
                        return false;
                    }

                    // Provide feedback to the busy indicator every now and then
                    if (this.ReadyToRefresh())
                    {
                        percentDone = Convert.ToInt32(imageIndex / TotalImages * 100.0);
                        this.Progress.Report(new ProgressBarArguments(percentDone, String.Format("Processing {0}/{1} images.  ", imageIndex, TotalImages), true, false));
                        Thread.Sleep(Constant.ThrottleValues.RenderingBackoffTime);  // Allows the UI thread to update every now and then
                    }

                    // Distinguish between single files vs and episode of files
                    if (Episodes.EpisodesDictionary.Count <= 1)
                    {
                        if (this.SingletonAsSingle)
                        {
                            singletonData = "Single";
                        }
                        else if (this.SingletonAsZero)
                        {
                            singletonData = "0:1/1";
                        }
                        else
                        {
                            this.EpisodeCount++;
                            singletonData = String.Format("{0}1/1", this.IncludeAnEpisodeIDNumber ? this.EpisodeCount + ":" : String.Empty);
                        }
                        List<ColumnTuple> ctl = new List<ColumnTuple>() { new ColumnTuple(this.dataLabelByLabel[this.dataFieldLabel], singletonData) };
                        imagesToUpdate.Add(new ColumnTuplesWithWhere(ctl, this.fileDatabase.FileTable[imageIndex].ID));
                        this.SingleCount++;
                        imageIndex++;
                    }
                    else
                    {
                        this.EpisodeCount++;
                        this.EpisodeNoSingletonsCount++;
                        foreach (KeyValuePair<int, Tuple<int, int>> episode in Episodes.EpisodesDictionary)
                        {
                            List<ColumnTuple> ctl = new List<ColumnTuple>() {
                                new ColumnTuple(this.dataLabelByLabel[this.dataFieldLabel],
                                String.Format("{0}{1}/{2}", this.IncludeAnEpisodeIDNumber ? this.EpisodeCount + ":" : String.Empty, episode.Value.Item1, episode.Value.Item2))};
                            imagesToUpdate.Add(new ColumnTuplesWithWhere(ctl, this.fileDatabase.FileTable[imageIndex].ID));
                            imageIndex++;
                        }
                    }
                }
                this.IsAnyDataUpdated = true;
                this.Progress.Report(new ProgressBarArguments(100, String.Format("Writing Episode data for {0} files. Please wait...", TotalImages), false, true));
                Thread.Sleep(Constant.ThrottleValues.RenderingBackoffTime);  // Allows the UI thread to update every now and then
                this.fileDatabase.UpdateFiles(imagesToUpdate);

                return true;//keyValueList;
            }, this.Token).ConfigureAwait(true);
        }

        // Provide an example of what the data will look like as various format options are selected
        private void ShowExampleFormat()
        {
            if (true == this.SingletonAsSingle)
            {
                this.TBSingleton.Text = "Singleton: Single";
            }
            if (true == this.CheckBoxIncludeEpisodeID.IsChecked)
            {
                this.TBEpisode.Text = "Episode:    23:1/7";
                if (true == this.SingletonAsZero)
                {
                    this.TBSingleton.Text = "Singleton: 0:1/1";
                }
                else if (true == this.SingletonAsEpisodeNumber)
                {
                    this.TBSingleton.Text = "Singleton: 23:1/1";
                }
            }
            else
            {
                this.TBEpisode.Text = "Episode:    1/7";
                if (false == this.SingletonAsSingle)
                {
                    this.TBSingleton.Text = "Singleton: 1/1";
                }
            }
        }
        private void Done_Click(object sender, RoutedEventArgs e)
        {

            // We return true if the database was altered but also if there was a cancellation, as a cancelled operation
            // may have changed the FileTable (but not database) date entries. Returning true will reset them, as a FileSelectAndShow will be done.
            // Kinda hacky as it expects a certain behaviour of the caller, but it works.
            this.DialogResult = this.Token.IsCancellationRequested || this.IsAnyDataUpdated;
        }
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = this.Token.IsCancellationRequested || this.IsAnyDataUpdated;
        }
        #endregion

        private void ComboBoxSelectNoteField_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox cb)
            {
                this.dataFieldLabel = ((string)cb.SelectedValue).Trim();
                this.StartDoneButton.IsEnabled = !String.IsNullOrEmpty(this.dataFieldLabel);
            }
        }

        private void CheckBoxIncludeEpisodeID_CheckChanged(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb)
            {
                this.IncludeAnEpisodeIDNumber = cb.IsChecked == true;
                this.ShowExampleFormat();
            }
        }

        private void RB_CheckChanged(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton _)
            {
                this.SingletonAsZero = this.RBSingletonZero.IsChecked == true;
                this.SingletonAsSingle = this.RBSingletonSingle.IsChecked == true;
                this.SingletonAsEpisodeNumber = this.RBSingletonEpisodeNumber.IsChecked == true;
                this.ShowExampleFormat();
            }
        }
    }
}
