using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Forms;
using Timelapse.Database;
using Timelapse.Enums;
using Timelapse.Util;
using Clipboard = System.Windows.Clipboard;
using Rectangle = System.Drawing.Rectangle;

namespace Timelapse.Dialog
{
    public static class Dialogs
    {
        #region Dialog Box Positioning and Fitting
        // Most (but not all) invocations of SetDefaultDialogPosition and TryFitDialogWndowInWorkingArea 
        // are done together, so collapse it into a single call
        public static void TryPositionAndFitDialogIntoWindow(Window window)
        {
            Dialogs.SetDefaultDialogPosition(window);
            Dialogs.TryFitDialogInWorkingArea(window);
        }


        // Position the dialog box within its owner's window
        public static void SetDefaultDialogPosition(Window window)
        {
            // Check the arguments for null 
            if (window == null)
            {
                // this should not happen
                TraceDebug.PrintStackTrace("Window's owner property is null. Is a set of it prior to calling ShowDialog() missing?", 1);
                // Treat it as a no-op
                return;
            }

            window.Left = window.Owner.Left + (window.Owner.Width - window.ActualWidth) / 2; // Center it horizontally
            window.Top = window.Owner.Top + 20; // Offset it from the windows'top by 20 pixels downwards
        }

        // Used to ensure that the window is positioned within the screen
        // Note that all uses of this method is by dialog box windows (which should be initialy positioned relative to the main timelapse window) by a call to SetDefaultDialogPosition), 
        // rather than the main timelapse window (whose position, size and layout  is managed by the TimelapseAvalonExtension methods). 
        // We could likely collapse the two, but its not worth the bother. 
        public static bool TryFitDialogInWorkingArea(Window window)
        {
            if (window == null)
            {
                return false;
            }
            if (Double.IsNaN(window.Left))
            {
                window.Left = 0;
            }
            if (Double.IsNaN(window.Top))
            {
                window.Top = 0;
            }

            // If needed, adjust the window's height to be somewhat smaller than the screen 
            // We allow some space for the task bar, assuming its visible at the screen's bottom
            // and place the window at the very top. Note that this won't cater for the situation when
            // the task bar is at the top of the screen, but so it goes.
            int typicalTaskBarHeight = 40;
            double availableScreenHeight = System.Windows.SystemParameters.PrimaryScreenHeight - typicalTaskBarHeight;
            if (window.Height > availableScreenHeight)
            {
                window.Height = availableScreenHeight;
                window.Top = 0;
            }

            Rectangle windowPosition = new Rectangle((int)window.Left, (int)window.Top, (int)window.Width, (int)window.Height);
            Rectangle workingArea = Screen.GetWorkingArea(windowPosition);
            bool windowFitsInWorkingArea = true;

            // move window up if it extends below the working area
            if (windowPosition.Bottom > workingArea.Bottom)
            {
                int pixelsToMoveUp = windowPosition.Bottom - workingArea.Bottom;
                if (pixelsToMoveUp > windowPosition.Top)
                {
                    // window is too tall and has to shorten to fit screen
                    window.Top = 0;
                    window.Height = workingArea.Bottom;
                    windowFitsInWorkingArea = false;
                }
                else if (pixelsToMoveUp > 0)
                {
                    // move window up
                    window.Top -= pixelsToMoveUp;
                }
            }

            // move window left if it extends right of the working area
            if (windowPosition.Right > workingArea.Right)
            {
                int pixelsToMoveLeft = windowPosition.Right - workingArea.Right;
                if (pixelsToMoveLeft > windowPosition.Left)
                {
                    // window is too wide and has to narrow to fit screen
                    window.Left = 0;
                    window.Width = workingArea.Width;
                    windowFitsInWorkingArea = false;
                }
                else if (pixelsToMoveLeft > 0)
                {
                    // move window left
                    window.Left -= pixelsToMoveLeft;
                }
            }
            return windowFitsInWorkingArea;
        }
        #endregion

        #region Dialog Messages: Prompt to apply operation if partial selection.

        // Warn the user that they are currently in a selection displaying only a subset of files, and make sure they want to continue.
        public static bool MaybePromptToApplyOperationOnSelection(Window window, FileDatabase fileDatabase, bool promptState, string operationDescription, Action<bool> persistOptOut)
        {
            if (Dialogs.CheckIfPromptNeeded(promptState, fileDatabase, out int filesTotalCount, out int filesSelectedCount) == false)
            {
                // if showing all images, or if users had elected not to be warned, then no need for showing the warning message
                return true;
            }

            // Warn the user that the operation will only be applied to an image set.
            string title = "Apply " + operationDescription + " to this selection?";
            MessageBox messageBox = new MessageBox(title, window, MessageBoxButton.OKCancel);

            messageBox.Message.What = operationDescription + " will be applied only to a subset of your images." + Environment.NewLine;
            messageBox.Message.What += "Is this what you want?";

            messageBox.Message.Reason = String.Format("A 'selection' is active, where you are currently viewing {0}/{1} total files.{2}", filesSelectedCount, filesTotalCount, Environment.NewLine);
            messageBox.Message.Reason += "Only these selected images will be affected by this operation." + Environment.NewLine;
            messageBox.Message.Reason += "Data for other unselected images will be unaffected.";

            messageBox.Message.Solution = "Select " + Environment.NewLine;
            messageBox.Message.Solution += "\u2022 'Ok' for Timelapse to continue to " + operationDescription + " for these selected files" + Environment.NewLine;
            messageBox.Message.Solution += "\u2022 'Cancel' to abort";

            messageBox.Message.Hint = "This is not an error." + Environment.NewLine;
            messageBox.Message.Hint += "\u2022 We are just reminding you that you have an active selection that is displaying only a subset of your images." + Environment.NewLine;
            messageBox.Message.Hint += "\u2022 You can apply this operation to that subset ." + Environment.NewLine;
            messageBox.Message.Hint += "\u2022 However, if you did want to do this operaton for all images, choose the 'Select|All files' menu option.";

            messageBox.Message.Icon = MessageBoxImage.Question;
            messageBox.DontShowAgain.Visibility = Visibility.Visible;

            bool proceedWithOperation = (bool)messageBox.ShowDialog();
            if (proceedWithOperation && messageBox.DontShowAgain.IsChecked.HasValue && persistOptOut != null)
            {
                persistOptOut(messageBox.DontShowAgain.IsChecked.Value);
            }
            return proceedWithOperation;
        }

        // Check if a prompt dialog is needed
        private static bool CheckIfPromptNeeded(bool promptState, FileDatabase fileDatabase, out int filesTotalCount, out int filesSelectedCount)
        {
            filesTotalCount = 0;
            filesSelectedCount = 0;
            if (fileDatabase == null)
            {
                // This should not happen. Maybe raise an exception?
                // In any case, don't show the prompt
                return false;
            }

            if (promptState)
            {
                // We don't show the prompt as the user has turned it off.
                return false;
            }
            // We want to show the prompt only if the promptState is true, and we are  viewing all images
            filesTotalCount = fileDatabase.CountAllFilesMatchingSelectionCondition(FileSelectionEnum.All);
            filesSelectedCount = fileDatabase.FileTable.RowCount;
            return filesTotalCount != filesSelectedCount;
        }

        #endregion

        #region Dialog Messages: Missing dependencies
        public static void DependencyFilesMissingDialog(string applicationName)
        {
            // can't use DialogMessageBox to show this message as that class requires the Timelapse window to be displayed.
            string messageTitle = String.Format("{0} needs to be in its original downloaded folder.", applicationName);
            StringBuilder message = new StringBuilder("Problem:" + Environment.NewLine);
            message.AppendFormat("{0} won't run properly as it was not correctly installed.{1}{1}", applicationName, Environment.NewLine);
            message.AppendLine("Reason:");
            message.AppendFormat("When you downloaded {0}, it was in a folder with several other files and folders it needs. You probably dragged {0} out of that folder.{1}{1}", applicationName, Environment.NewLine);
            message.AppendLine("Solution:");
            message.AppendFormat("Move the {0} program back to its original folder, or download it again.{1}{1}", applicationName, Environment.NewLine);
            message.AppendLine("Hint:");
            message.AppendFormat("Create a shortcut if you want to access {0} outside its folder:{1}", applicationName, Environment.NewLine);
            message.AppendLine("1. From its original folder, right-click the Timelapse program icon.");
            message.AppendLine("2. Select 'Create Shortcut' from the menu.");
            message.Append("3. Drag the shortcut icon to the location of your choice.");
            System.Windows.MessageBox.Show(message.ToString(), messageTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        #endregion

        #region Dialog Messages: Path too long warnings
        // This version is for hard crashes. however, it may disappear from display too fast as the program will be shut down.
        public static void FilePathTooLongDialog(UnhandledExceptionEventArgs e, Window owner)
        {
            string title = "Your File Path Names are Too Long to Handle";
            MessageBox messageBox = new MessageBox(title, owner);
            messageBox.Message.Icon = MessageBoxImage.Error;
            messageBox.Message.Title = title;
            messageBox.Message.Problem = "Timelapse has to shut down as one or more of your file paths are too long.";
            messageBox.Message.Solution = "\u2022 Shorten the path name by moving your image folder higher up the folder hierarchy, or" + Environment.NewLine + "\u2022 Use shorter folder or file names.";
            messageBox.Message.Reason = "Windows cannot perform file operations if the folder path combined with the file name is more than " + Constant.File.MaxPathLength.ToString() + " characters.";
            messageBox.Message.Result = "Timelapse will shut down until you fix this.";
            messageBox.Message.Hint = "Files created in your " + Constant.File.BackupFolder + " folder must also be less than " + Constant.File.MaxPathLength.ToString() + " characters.";
            if (e != null)
            {
                Clipboard.SetText(e.ExceptionObject.ToString());
            }
            messageBox.ShowDialog();
        }

        // This version detects and displays warning messages.
        public static void FilePathTooLongDialog(List<string> folders, Window owner)
        {
            ThrowIf.IsNullArgument(folders, nameof(folders));

            string title = "Some of your Image File Path Names Were Too Long";
            MessageBox messageBox = new MessageBox(title, owner);
            messageBox.Message.Icon = MessageBoxImage.Error;
            messageBox.Message.Title = title;
            messageBox.Message.Problem = "Timelapse skipped reading some of your images in the folders below, as their file paths were too long.";
            if (folders.Count > 0)
            {
                messageBox.Message.Problem += "Those files are found in these folders:";
                foreach (string folder in folders)
                {
                    messageBox.Message.Problem += Environment.NewLine + "\u2022 " + folder;
                }
            }
            messageBox.Message.Reason = "Windows cannot perform file operations if the folder path combined with the file name is more than " + Constant.File.MaxPathLength.ToString() + " characters.";
            messageBox.Message.Solution = "Try reloading this image set after shortening the file path:" + Environment.NewLine;
            messageBox.Message.Solution += "\u2022 shorten the path name by moving your image folder higher up the folder hierarchy, or" + Environment.NewLine + "\u2022 use shorter folder or file names.";

            messageBox.Message.Hint = "Files created in your " + Constant.File.BackupFolder + " folder must also be less than " + Constant.File.MaxPathLength.ToString() + " characters.";
            messageBox.ShowDialog();
        }

        // notify the user when the path is too long
        public static void TemplatePathTooLongDialog(string templateDatabasePath, Window owner)
        {
            MessageBox messageBox = new MessageBox("Timelapse could not open the template ", owner);
            messageBox.Message.Problem = "Timelapse could not open the Template File as its name is too long:" + Environment.NewLine;
            messageBox.Message.Problem += "\u2022 " + templateDatabasePath;
            messageBox.Message.Reason = "Windows cannot perform file operations if the folder path combined with the file name is more than " + Constant.File.MaxPathLength.ToString() + " characters.";
            messageBox.Message.Solution = "\u2022 Shorten the path name by moving your image folder higher up the folder hierarchy, or" + Environment.NewLine + "\u2022 Use shorter folder or file names.";
            messageBox.Message.Hint = "Files created in your " + Constant.File.BackupFolder + " folder must also be less than " + Constant.File.MaxPathLength.ToString() + " characters.";
            messageBox.Message.Icon = MessageBoxImage.Error;
            messageBox.ShowDialog();
        }

        // notify the user the template couldn't be loaded because its path is too long
        public static void DatabasePathTooLongDialog(string databasePath, Window owner)
        {
            MessageBox messageBox = new MessageBox("Timelapse could not load the database ", owner);
            messageBox.Message.Problem = "Timelapse could not load the Template File as its name is too long:" + Environment.NewLine;
            messageBox.Message.Problem += "\u2022 " + databasePath;
            messageBox.Message.Reason = "Windows cannot perform file operations if the folder path combined with the file name is more than " + Constant.File.MaxPathLength.ToString() + " characters.";
            messageBox.Message.Solution = "\u2022 Shorten the path name by moving your image folder higher up the folder hierarchy, or" + Environment.NewLine + "\u2022 Use shorter folder or file names.";
            messageBox.Message.Hint = "Files created in your " + Constant.File.BackupFolder + " folder must also be less than " + Constant.File.MaxPathLength.ToString() + " characters.";
            messageBox.Message.Icon = MessageBoxImage.Error;
            messageBox.ShowDialog();
        }

        // Warn the user if backups may not be made
        public static void BackupPathTooLongDialog(Window owner)
        {
            MessageBox messageBox = new MessageBox("Timelapse may not be able to backup your files", owner);
            messageBox.Message.Problem = "Timelapse may not be able to backup your files as your file names are very long.";

            messageBox.Message.Reason = "Timelapse normally creates backups of your template, database, and csv files in the " + Constant.File.BackupFolder + " folder." + Environment.NewLine;
            messageBox.Message.Reason += "However, Windows cannot create those files if the " + Constant.File.BackupFolder + " folder path combined with the file name is more than " + Constant.File.MaxPathLength.ToString() + " characters.";

            messageBox.Message.Solution = "\u2022 Shorten the path name by moving your image folder higher up the folder hierarchy, or" + Environment.NewLine;
            messageBox.Message.Solution += "\u2022 Use shorter folder or file names.";
            messageBox.Message.Hint = "You can still use Timelapse, but backup files may not be created.";
            messageBox.Message.Icon = MessageBoxImage.Warning;
            messageBox.ShowDialog();
        }
        #endregion

        #region Dialog Message: Problem loading the template
        public static void TemplateCouldNotBeLoadedDialog(string templateDatabasePath, Window owner)
        {
            // notify the user the template couldn't be loaded rather than silently doing nothing
            MessageBox messageBox = new MessageBox("Timelapse could not load the template.", owner);
            messageBox.Message.Problem = "Timelapse could not load the Template File:" + Environment.NewLine;
            messageBox.Message.Problem += "\u2022 " + templateDatabasePath;
            messageBox.Message.Reason = "The template may be corrupted or somehow otherwise invalid. ";
            messageBox.Message.Solution = "You may have to recreate the template, or use another copy of it (check the Backups folder).";
            messageBox.Message.Result = "Timelapse won't do anything. You can try to select another template file.";
            messageBox.Message.Hint = "See if you can examine the template file in the Timelapse Template Editor.";
            messageBox.Message.Hint += "If you can't, there is likley something wrong with it and you will have to recreate it.";
            messageBox.Message.Icon = MessageBoxImage.Error;
            messageBox.ShowDialog();
        }
        #endregion
    }
}
