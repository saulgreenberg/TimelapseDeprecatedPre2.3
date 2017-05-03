using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Threading;
using Timelapse.Controls;
using Timelapse.Database;
using Timelapse.Dialog;
using Timelapse.Editor;
using Timelapse.Editor.Dialog;
using Timelapse.Images;
using Timelapse.Util;
using MessageBox = Timelapse.Dialog.MessageBox;

namespace Timelapse.UnitTests
{
    [TestClass]
    public class UserInterfaceTests : TimelapseTest
    {
        public UserInterfaceTests()
        {
            this.EnsureTestClassSubdirectory();
        }

        [TestMethod]
        public void Editor()
        {
            // open, do nothing, close
            EditorWindow editor = new EditorWindow();
            editor.Show();
            this.WaitForRenderingComplete();
            editor.Close();

            // open, create template database, close
            string templateDatabaseFilePath = this.GetUniqueFilePathForTest(TestConstant.File.DefaultNewTemplateDatabaseFileName);
            if (File.Exists(templateDatabaseFilePath))
            {
                File.Delete(templateDatabaseFilePath);
            }
            editor = new EditorWindow();
            PrivateObject editorAccessor = new PrivateObject(editor);
            editor.Show();
            this.WaitForRenderingComplete();
            editorAccessor.Invoke(TestConstant.InitializeDataGridMethodName, templateDatabaseFilePath);
            this.WaitForRenderingComplete();
            editor.Close();

            // open, load existing database, pop dialogs, close
            // InitializeDataGrid() sets the template pane active but without the explicit set in test code the event gets dropped, resulting the EditChoiceList
            // show failing because the UIElement its position is referenced to is not visible.
            editor = new EditorWindow();
            editor.Show();
            this.WaitForRenderingComplete();

            editor.TemplatePane.IsActive = true;
            this.WaitForRenderingComplete();

            editorAccessor.Invoke(TestConstant.InitializeDataGridMethodName, templateDatabaseFilePath);
            this.WaitForRenderingComplete();
            this.ShowDialog(new EditChoiceList(editor.HelpMessageForTemplate, new List<string>() { "Choice0", "Choice1", "Choice2", "Choice3" }, false, editor));
            editor.Close();
        }

        [TestMethod]
        public void Timelapse()
        {
            // open, do nothing, close
            using (TimelapseWindow timelapse = new TimelapseWindow())
            {
                timelapse.Show();
                this.WaitForRenderingComplete();
                timelapse.Close();
                this.WaitForRenderingComplete();
            }

            // create template database and remove any image database from previous test executions
            string templateDatabaseFilePath;
            using (TemplateDatabase templateDatabase = this.CreateTemplateDatabase(TestConstant.File.DefaultTemplateDatabaseFileName2015))
            {
                templateDatabaseFilePath = templateDatabase.FilePath;
            }

            string fileDatabaseFilePath = Path.Combine(Path.GetDirectoryName(templateDatabaseFilePath), Path.GetFileNameWithoutExtension(templateDatabaseFilePath) + Constant.File.FileDatabaseFileExtension);
            if (File.Exists(fileDatabaseFilePath))
            {
                File.Delete(fileDatabaseFilePath);
            }

            // open, load database by scanning folder, move through images, close
            // The threading model for this is somewhat involved.  The test thread is the UI thread and therefore must drive the dispatcher.  This means the test
            // thread locks into UI message pumping when modal dialog is displayed, such as when the BackgroundWorker for loading files from a directory pops the
            // file count summary upon completion.  The test must therefore spin up a separate thread to close the dialogs and allow the main test thread to return
            // from the dispatcher and resume test execution.  If something jams up on the dialog handler thread Visual Studio may still consider the test running
            // when also attached as a debugger even if the test thread has completed.
            using (TimelapseWindow timelapse = new TimelapseWindow())
            {
                // show main window
                timelapse.Show();
                this.WaitForRenderingComplete();

                // start thread for handling file dialogs
                BackgroundWorker backgroundWorker = null;
                Task dialogDismissal = Task.Factory.StartNew(() =>
                {
                    AutomationElement timelapseAutomation = AutomationElement.RootElement.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, TestConstant.TimelapseAutomationID));
                    InvokePattern ambiguousDatesOkButton = this.FindDialogOkButton(timelapseAutomation, TestConstant.MessageBoxAutomationID);
                    ambiguousDatesOkButton.Invoke();

                    InvokePattern fileCountsOkButton = this.FindDialogOkButton(timelapseAutomation, TestConstant.FileCountsAutomationID);
                    fileCountsOkButton.Invoke();
                });
                // import files from directory
                Dispatcher.CurrentDispatcher.Invoke(() =>
                {
                    Assert.IsTrue((bool)timelapse.TryOpenTemplateAndBeginLoadFoldersAsync(templateDatabaseFilePath, out backgroundWorker));
                    this.WaitForWorkerComplete(backgroundWorker);
                });
                this.WaitForRenderingComplete();

                // verify import succeeded
                PrivateObject timelapseAccessor = new PrivateObject(timelapse);
                DataEntryHandler dataHandler = (DataEntryHandler)timelapseAccessor.GetField(TestConstant.DataHandlerFieldName);
                Assert.IsTrue(dataHandler.FileDatabase.CurrentlySelectedFileCount == 2);
                Assert.IsNotNull(dataHandler.ImageCache.Current);

                // verify forward and backward moves of the displayed image
                Assert.IsTrue((bool)timelapseAccessor.Invoke(TestConstant.TryShowImageWithoutSliderCallbackMethodName, true, ModifierKeys.None));
                this.WaitForRenderingComplete();
                Assert.IsTrue((bool)timelapseAccessor.Invoke(TestConstant.TryShowImageWithoutSliderCallbackMethodName, false, ModifierKeys.None));
                this.WaitForRenderingComplete();

                timelapse.Close();
            }

            // open, load existing database, pop dialogs, close
            using (TimelapseWindow timelapse = new TimelapseWindow())
            {
                timelapse.Show();
                this.WaitForRenderingComplete();

                BackgroundWorker backgroundWorker;
                Dispatcher.CurrentDispatcher.Invoke(() =>
                {
                    Assert.IsTrue((bool)timelapse.TryOpenTemplateAndBeginLoadFoldersAsync(templateDatabaseFilePath, out backgroundWorker));
                    this.WaitForWorkerComplete(backgroundWorker);
                });
                this.WaitForRenderingComplete();

                PrivateObject timelapseAccessor = new PrivateObject(timelapse);
                DataEntryHandler dataHandler = (DataEntryHandler)timelapseAccessor.GetField(TestConstant.DataHandlerFieldName);
                Assert.IsTrue(dataHandler.FileDatabase.CurrentlySelectedFileCount > 0);
                Assert.IsNotNull(dataHandler.ImageCache.Current);

                this.ShowDialog(new AboutTimelapse(timelapse));
                MarkableCanvas markableCanvas = (MarkableCanvas)timelapseAccessor.GetField("MarkableCanvas");
                TimelapseState state = (TimelapseState)timelapseAccessor.GetField("state");
                this.ShowDialog(new AdvancedTimelapseOptions(state, markableCanvas, timelapse));
                this.ShowDialog(new ChooseFileDatabaseFile(new string[] { TestConstant.File.DefaultFileDatabaseFileName }, templateDatabaseFilePath, timelapse));

                this.ShowDialog(new Dialog.CustomSelection(dataHandler.FileDatabase, timelapse, false));
                this.ShowDialog(new DateCorrectAmbiguous(dataHandler.FileDatabase, timelapse));
                this.ShowDialog(new DateDaylightSavingsTimeCorrection(dataHandler.FileDatabase, dataHandler.ImageCache, timelapse));

                DateTimeFixedCorrection clockSetCorrection = new DateTimeFixedCorrection(dataHandler.FileDatabase, dataHandler.ImageCache.Current, timelapse);
                this.ShowDialog(clockSetCorrection);

                DateTimeLinearCorrection clockDriftCorrection = new DateTimeLinearCorrection(dataHandler.FileDatabase, timelapse);
                this.ShowDialog(clockDriftCorrection);

                this.ShowDialog(new EditLog(dataHandler.FileDatabase.ImageSet.Log, timelapse));

                using (DarkImagesThreshold darkThreshold = new DarkImagesThreshold(dataHandler.FileDatabase, dataHandler.ImageCache.CurrentRow, new TimelapseState(), timelapse))
                {
                    this.ShowDialog(darkThreshold);
                }
                this.ShowDialog(new PopulateFieldWithMetadata(dataHandler.FileDatabase, dataHandler.ImageCache.Current.GetFilePath(dataHandler.FileDatabase.FolderPath), timelapse));

                this.ShowDialog(new RenameFileDatabaseFile(dataHandler.FileDatabase.FileName, timelapse));
                this.ShowDialog(new DateTimeRereadFromFiles(dataHandler.FileDatabase, timelapse));
                this.ShowDialog(new FileCountsByQuality(dataHandler.FileDatabase.GetFileCountsBySelection(), timelapse));
                this.ShowDialog(new TemplateSynchronization(dataHandler.FileDatabase.ControlSynchronizationIssues, timelapse));

                MessageBox okMessageBox = this.CreateMessageBox(timelapse, MessageBoxButton.OK, MessageBoxImage.Error);
                this.ShowDialog(okMessageBox);
                MessageBox okCancelMessageBox = this.CreateMessageBox(timelapse, MessageBoxButton.OKCancel, MessageBoxImage.Information);
                this.ShowDialog(okCancelMessageBox);
                MessageBox yesNoMessageBox = this.CreateMessageBox(timelapse, MessageBoxButton.YesNo, MessageBoxImage.Question);
                this.ShowDialog(yesNoMessageBox);

                timelapse.Close();
            }
        }

        private MessageBox CreateMessageBox(Window owner, MessageBoxButton buttonType, MessageBoxImage iconType)
        {
            MessageBox messageBox = new MessageBox("Message box title", owner, buttonType);
            messageBox.Message.Icon = iconType;
            messageBox.Message.Problem = "Problem description.";
            messageBox.Message.Reason = "Explanation of why issue is an issue.";
            messageBox.Message.Solution = "Suggested method for resolving the issue.";
            messageBox.Message.Result = "Current status.";
            messageBox.Message.Hint = "Additional suggestions as to how to resolve the issue.";
            return messageBox;
        }

        private InvokePattern FindDialogOkButton(AutomationElement parent, string automationID)
        {
            AutomationElement dialog = null;
            DateTime startTime = DateTime.UtcNow;
            while ((dialog == null) && (DateTime.UtcNow - startTime < TestConstant.UIElementSearchTimeout))
            {
                Thread.Sleep(TimeSpan.FromMilliseconds(100));
                dialog = parent.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, automationID));
            }
            Assert.IsNotNull(dialog);

            AutomationElement okButton = dialog.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, TestConstant.OkButtonAutomationID));
            Assert.IsNotNull(okButton);
            return (InvokePattern)okButton.GetCurrentPattern(InvokePattern.Pattern);
        }

        private void ShowDialog(Window dialog)
        {
            dialog.Loaded += (object sender, RoutedEventArgs eventArgs) => { dialog.Close(); };
            Dispatcher.CurrentDispatcher.InvokeAsync(() =>
            {
                dialog.ShowDialog();
            });
            this.WaitForRenderingComplete();
        }

        private void WaitForWorkerComplete(BackgroundWorker backgroundWorker)
        {
            if (backgroundWorker == null)
            {
                return;
            }

            while (backgroundWorker.IsBusy)
            {
                this.WaitForRenderingComplete();
            }
        }

        private void WaitForRenderingComplete()
        {
            // make a best effort at letting WPF's render thread(s) catch up
            // from https://msdn.microsoft.com/en-us/library/system.windows.threading.dispatcher.pushframe.aspx
            DispatcherFrame frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new DispatcherOperationCallback((object arg) =>
            {
                ((DispatcherFrame)arg).Continue = false;
                return null;
            }), frame);
            Dispatcher.PushFrame(frame);
        }
    }
}
