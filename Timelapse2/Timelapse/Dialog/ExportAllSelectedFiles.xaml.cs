using System;
using System.IO;
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
    /// Interaction logic for ExportAllSelectedFiles.xaml
    /// </summary>
    public partial class ExportAllSelectedFiles : BusyableDialogWindow
    {
        #region Private Variables
        private readonly FileDatabase FileDatabase;
        #endregion

        #region Constructors / Loaded
        public ExportAllSelectedFiles(Window owner, FileDatabase fileDatabase) : base(owner)
        {
            InitializeComponent();
            this.FileDatabase = fileDatabase;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.FolderLocation.Text = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            int count = this.FileDatabase.CountAllCurrentlySelectedFiles;
            if (count >= 100)
            {
                // Reality check...
                this.Message.Hint = String.Format("Do you really want to copy {0} files? This seems like alot.{1}", count, Environment.NewLine) + this.Message.Hint;
            }
            this.Message.Title = String.Format("Export (by copying) {0} currently selected files", count);
        }
        #endregion

        #region Button Callbacks
        private void ChooseFolderButton_Click(object sender, RoutedEventArgs e)
        {
            // Set up a Folder Browser with some instructions
            if (Dialogs.TryGetFolderFromUserUsingOpenFileDialog("Locate folder to export your files...", this.FolderLocation.Text, out string folder))
            {
                // Display the folder
                this.FolderLocation.Text = folder;
            }
        }

        private async void Export_Click(object sender, RoutedEventArgs e)
        {
            if (this.GetPathAndCreateItIfNeeded(out string path) == false)
            {
                System.Windows.MessageBox.Show(String.Format("Could not create the folder: {1}  {0}{1}Export aborted.", path, Environment.NewLine), "Export aborted.", MessageBoxButton.OK, MessageBoxImage.Error);
                this.DialogResult = false;
            }
            this.BusyIndicator.IsBusy = true;
            string feedbackMessage = await CopyFiles(path).ConfigureAwait(true);
            this.BusyIndicator.IsBusy = false;

            this.Grid1.Visibility = Visibility.Collapsed;
            this.ButtonPanel1.Visibility = Visibility.Collapsed;

            this.TextBlockFeedback.Text = feedbackMessage;
            this.TextBlockFeedback.Visibility = Visibility.Visible;
            this.ButtonPanel2.Visibility = Visibility.Visible;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        private void Done_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }
        #endregion

        #region Private Methods - Copy Files (async with progress)
        private async Task<string> CopyFiles(string path)
        {
            // Set up a progress handler that will update the progress bar
            Progress<ProgressBarArguments> progressHandler = new Progress<ProgressBarArguments>(value =>
            {
                // Update the progress bar
                this.UpdateProgressBar(value.PercentDone, value.Message, value.IsCancelEnabled, value.IsIndeterminate);
            });
            IProgress<ProgressBarArguments> progress = progressHandler;

            string fileNamePrefix;
            string sourceFile;
            string destFile;
            int totalFiles = FileDatabase.FileTable.RowCount;
            int copiedFiles = 0;
            int skippedFiles = 0;
            bool cancelled = false;
            await Task.Run(() =>
            {
                foreach (ImageRow ir in FileDatabase.FileTable)
                {
                    if (this.TokenSource.IsCancellationRequested)
                    {
                        cancelled = true;
                        return;
                    }
                    fileNamePrefix = ir.RelativePath.Replace('\\', '.');
                    sourceFile = Path.Combine(FileDatabase.FolderPath, ir.RelativePath, ir.File);
                    destFile = String.IsNullOrWhiteSpace(fileNamePrefix)
                    ? Path.Combine(path, fileNamePrefix + ir.File)
                    : Path.Combine(path, fileNamePrefix + '.' + ir.File);

                    try
                    {
                        File.Copy(sourceFile, destFile, true);
                        copiedFiles++;
                    }
                    catch
                    {
                        skippedFiles++;
                    }
                    if (this.ReadyToRefresh())
                    {
                        int percentDone = Convert.ToInt32((copiedFiles + skippedFiles) / Convert.ToDouble(totalFiles) * 100.0);
                        progress.Report(new ProgressBarArguments(percentDone, String.Format("Copying {0} / {1} files", copiedFiles, totalFiles), true, true));
                        Thread.Sleep(Constant.ThrottleValues.RenderingBackoffTime);
                    }
                }
            }).ConfigureAwait(true);

            if (cancelled)
            {
                return String.Format("Export cancelled after copying {0} files", copiedFiles);
            }
            if (skippedFiles == 0)
            {
                return String.Format("Copied {0} out of {1} files{2}", copiedFiles, totalFiles, Environment.NewLine);
            }
            string feedbackMessage = String.Format("Copied {0} out of {1} files{2}", copiedFiles, totalFiles, Environment.NewLine);
            feedbackMessage += (skippedFiles == 1)
                    ? String.Format("Skipped {0} file (perhaps it is missing?)", skippedFiles)
                    : String.Format("Skipped {0} files (perhaps they are missing?)", skippedFiles);
            return feedbackMessage;
        }
        #endregion

        #region ProgressBar helper
        // Show progress information in the progress bar, and to enable or disable its cancel button
        private void UpdateProgressBar(int percent, string message, bool cancelEnabled, bool randomEnabled)
        {
            ProgressBar bar = VisualChildren.GetVisualChild<ProgressBar>(this.BusyIndicator);
            Label textMessage = VisualChildren.GetVisualChild<Label>(this.BusyIndicator);
            Button cancelButton = VisualChildren.GetVisualChild<Button>(this.BusyIndicator);

            if (bar != null & !randomEnabled)
            {
                // Treat it as a progressive progress bar
                bar.Value = percent;
                bar.IsIndeterminate = false;
            }
            else if (randomEnabled)
            {
                // If its at 100%, treat it as a random bar
                bar.IsIndeterminate = true;
            }

            // Update the text message
            if (textMessage != null)
            {
                textMessage.Content = message;
            }

            // Update the cancel button to reflect the cancelEnabled argument
            if (cancelButton != null)
            {
                cancelButton.IsEnabled = cancelEnabled;
                cancelButton.Content = cancelEnabled ? "Cancel" : "Copying files...";
            }
        }

        // We don't allow cancelling in the middle of a delete operation, so this is a no-op
        private void CancelAsyncOperationButton_Click(object sender, RoutedEventArgs e)
        {
            // Set this so that it will be caught in the above await task
            this.TokenSource.Cancel();
        }
        #endregion

        #region Private Methods - File/Folder Operations
        private bool GetPathAndCreateItIfNeeded(out string path)
        {
            path = (this.CBPutInSubFolder.IsChecked == true)
                ? Path.Combine(this.FolderLocation.Text, this.TextBoxPutInSubFolder.Text)
                : this.FolderLocation.Text;
            if (Directory.Exists(path) == false)
            {
                try
                {
                    DirectoryInfo dir = Directory.CreateDirectory(path);
                }
                catch
                {
                    return false;
                }
            }
            return true;
        }
        #endregion
    }
}
