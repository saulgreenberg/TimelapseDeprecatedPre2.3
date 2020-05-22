using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using Timelapse.Database;
using Timelapse.Enums;
using Timelapse.Util;
using Clipboard = System.Windows.Clipboard;
using Cursor = System.Windows.Input.Cursor;
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
                TracePrint.PrintStackTrace("Window's owner property is null. Is a set of it prior to calling ShowDialog() missing?", 1);
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

        #region OpenFileDialog: Get file or folder
        /// <summary>
        /// Prompt the user for a file location via an an open file dialog. Set selectedFilePath.
        /// </summary>
        /// <returns>True if the user indicated one, else false. selectedFilePath contains the selected path, if any, otherwise null </returns>
        public static bool TryGetFileFromUserUsingOpenFileDialog(string title, string defaultFilePath, string filter, string defaultExtension, out string selectedFilePath)
        {
            // Get the template file, which should be located where the images reside
            using (OpenFileDialog openFileDialog = new OpenFileDialog()
            {
                Title = title,
                CheckFileExists = true,
                CheckPathExists = true,
                Multiselect = false,
                AutoUpgradeEnabled = true,

                // Set filter for file extension and default file extension 
                DefaultExt = defaultExtension,
                Filter = filter
            })
            {
                if (String.IsNullOrWhiteSpace(defaultFilePath))
                {
                    openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                }
                else
                {
                    openFileDialog.InitialDirectory = Path.GetDirectoryName(defaultFilePath);
                    openFileDialog.FileName = Path.GetFileName(defaultFilePath);
                }

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    selectedFilePath = openFileDialog.FileName;
                    return true;
                }
                selectedFilePath = null;
                return false;
            }
        }

        /// <summary>
        /// Folder dialog where the user can only select a sub-folder of the root folder path
        /// It returns the relative path to the selected folder
        /// If folderNameToLocate is not empty, it displays that a desired folder to select in the dialog title.
        /// </summary>
        /// <param name="initialFolder">The path to the root folder containing the template</param>
        /// <param name="folderNameToLocate">If folderNameToLocate is not empty, it displays that a desired folder to select.</param>
        /// <returns></returns>

        public static string LocateRelativePathUsingOpenFileDialog(string initialFolder, string folderNameToLocate)
        {
            if (initialFolder == null)
            {
                return String.Empty;
            }
            using (CommonOpenFileDialog folderSelectionDialog = new CommonOpenFileDialog()
            {
                Title = "Locate folder" + folderNameToLocate + "...",
                DefaultDirectory = initialFolder,
                IsFolderPicker = true,
                Multiselect = false
            })
            {
                folderSelectionDialog.InitialDirectory = folderSelectionDialog.DefaultDirectory;
                folderSelectionDialog.FolderChanging += FolderSelectionDialog_FolderChanging;
                if (folderSelectionDialog.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    // Trim the root folder path from the folder name to produce a relative path. 
                    return (folderSelectionDialog.FileName.Length > initialFolder.Length) ? folderSelectionDialog.FileName.Substring(initialFolder.Length + 1) : String.Empty;
                }
                else
                {
                    return null;
                }
            }
        }

        // Limit the folder selection to only those that are sub-folders of the folder path
        private static void FolderSelectionDialog_FolderChanging(object sender, CommonFileDialogFolderChangeEventArgs e)
        {
            if (!(sender is CommonOpenFileDialog dialog))
            {
                return;
            }
            // require folders to be loaded be either the same folder as the .tdb and .ddb or subfolders of it
            if (e.Folder.StartsWith(dialog.DefaultDirectory, StringComparison.OrdinalIgnoreCase) == false)
            {
                e.Cancel = true;
            }
        }
        #endregion

        #region MessageBox: Prompt to apply operation if partial selection.
        // Warn the user that they are currently in a selection displaying only a subset of files, and make sure they want to continue.
        public static bool MaybePromptToApplyOperationOnSelectionDialog(Window owner, FileDatabase fileDatabase, bool promptState, string operationDescription, Action<bool> persistOptOut)
        {
            if (Dialogs.CheckIfPromptNeeded(promptState, fileDatabase, out int filesTotalCount, out int filesSelectedCount) == false)
            {
                // if showing all images, or if users had elected not to be warned, then no need for showing the warning message
                return true;
            }

            // Warn the user that the operation will only be applied to an image set.
            string title = "Apply " + operationDescription + " to this selection?";
            MessageBox messageBox = new MessageBox(title, owner, MessageBoxButton.OKCancel);

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

        #region MessageBox: Missing dependencies
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

        #region MessageBox: Path too long warnings
        // This version is for hard crashes. however, it may disappear from display too fast as the program will be shut down.
        public static void FilePathTooLongDialog(Window owner, UnhandledExceptionEventArgs e)
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
        public static void FilePathTooLongDialog(Window owner, List<string> folders)
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
        public static void TemplatePathTooLongDialog(Window owner, string templateDatabasePath)
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
        public static void DatabasePathTooLongDialog(Window owner, string databasePath)
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

        #region MessageBox: Corrupted template
        public static void TemplateFileNotLoadedAsCorruptDialog(Window owner, string templateDatabasePath)
        {
            Util.ThrowIf.IsNullArgument(owner, nameof(owner));
            // notify the user the template couldn't be loaded rather than silently doing nothing
            MessageBox messageBox = new MessageBox("Timelapse could not load the Template file.", owner);
            messageBox.Message.Problem = "Timelapse could not load the Template File :" + Environment.NewLine;
            messageBox.Message.Problem += "\u2022 " + templateDatabasePath;
            messageBox.Message.Reason = String.Format("The template ({0}) file may be corrupted, unreadable, or otherwise invalid.", Constant.File.TemplateDatabaseFileExtension);
            messageBox.Message.Solution = "Try one or more of the following:" + Environment.NewLine;
            messageBox.Message.Solution += "\u2022 recreate the template, or use another copy of it." + Environment.NewLine;
            messageBox.Message.Solution += String.Format("\u2022 check if there is a valid template file in your {0} folder.", Constant.File.BackupFolder) + Environment.NewLine;
            messageBox.Message.Solution += String.Format("\u2022 email {0} describing what happened, attaching a copy of your {1} file.", Constant.ExternalLinks.EmailAddress, Constant.File.TemplateDatabaseFileExtension);

            messageBox.Message.Result = "Timelapse did not affect any of your other files.";
            if (owner.Name.Equals("Timelapse"))
            {
                // Only displayed in Timelapse, not the template editor
                messageBox.Message.Hint = "See if you can open and examine the template file in the Timelapse Template Editor." + Environment.NewLine;
                messageBox.Message.Hint += "If you can't, and if you don't have a copy elsewhere, you will have to recreate it.";
            }
            messageBox.Message.Icon = MessageBoxImage.Error;
            messageBox.ShowDialog();
        }
        #endregion

        #region MessageBox: Corrupted .DDB file (no primary key)
        public static void DatabaseFileNotLoadedAsCorruptDialog(Window owner, string ddbDatabasePath, bool isEmpty)
        {
            // notify the user the database couldn't be loaded because there is a problem with it
            MessageBox messageBox = new MessageBox("Timelapse could not load your database file.", owner);
            messageBox.Message.Problem = "Timelapse could not load your .ddb database file:" + Environment.NewLine;
            messageBox.Message.Problem += "\u2022 " + ddbDatabasePath;
            if (isEmpty)
            {
                messageBox.Message.Reason = "Your database file is empty. Possible reasons include:" + Environment.NewLine;
            }
            else
            {
                messageBox.Message.Reason = "Your database is unreadable or corrupted. Possible reasons include:" + Environment.NewLine;
            }
            messageBox.Message.Reason += "\u2022 Timelapse was shut down (or crashed) in the midst of:" + Environment.NewLine;
            messageBox.Message.Reason += "    - loading your image set for the first time, or" + Environment.NewLine;
            messageBox.Message.Reason += "    - writing your data into the file, or" + Environment.NewLine;
            messageBox.Message.Reason += "\u2022 system, security or network  restrictions prohibited file reading and writing, or," + Environment.NewLine;
            messageBox.Message.Reason += "\u2022 some other unkown reason.";
            messageBox.Message.Solution = "\u2022 If you have not analyzed any images yet, delete the .ddb file and try again." + Environment.NewLine;
            messageBox.Message.Solution += "\u2022 Also, check for valid backups of your database in your " + Constant.File.BackupFolder + " folder that you can reuse.";
            messageBox.Message.Hint = "IMPORTANT: Send a copy of your .ddb and .tdb files along with an explanatory note to saul@ucalgary.ca." + Environment.NewLine;
            messageBox.Message.Hint += "He will check those files to see if there is a fixable bug.";
            messageBox.Message.Icon = MessageBoxImage.Error;
            messageBox.ShowDialog();
        }
        #endregion

        #region MessageBox: DataEntryHandler Confirmations / Warnings for Propagate, Copy Forward, Propagate to here
        /// <summary>
        /// Display a dialog box saying there is nothing to propagate. 
        /// </summary>
        public static void DataEntryNothingToPropagateDialog(Window owner)
        {

            MessageBox messageBox = new MessageBox("Nothing to Propagate to Here.", owner);
            messageBox.Message.Icon = MessageBoxImage.Exclamation;
            messageBox.Message.Reason = "All the earlier files have nothing in this field, so there are no values to propagate.";
            messageBox.ShowDialog();
        }

        /// <summary>
        /// Display a dialog box saying there is nothing to copy forward. 
        /// </summary>
        public static void DataEntryNothingToCopyForwardDialog(Window owner)
        {
            // Display a dialog box saying there is nothing to propagate. Note that this should never be displayed, as the menu shouldn't be highlit if there is nothing to propagate
            // But just in case...
            MessageBox messageBox = new MessageBox("Nothing to copy forward.", owner);
            messageBox.Message.Icon = MessageBoxImage.Exclamation;
            messageBox.Message.Reason = "As you are on the last file, there are no files after this.";
            messageBox.ShowDialog();
        }

        /// <summary>
        /// Ask the user to confirm value propagation from the last value
        /// </summary>
        public static bool? DataEntryConfirmCopyForwardDialog(Window owner, string text, int imagesAffected, bool checkForZero)
        {
            text = String.IsNullOrEmpty(text) ? String.Empty : text.Trim();

            MessageBox messageBox = new MessageBox("Please confirm 'Copy Forward' for this field...", owner, MessageBoxButton.YesNo);
            messageBox.Message.Icon = MessageBoxImage.Question;
            messageBox.Message.What = "Copy Forward is not undoable, and can overwrite existing values.";
            messageBox.Message.Result = "If you select yes, this operation will:" + Environment.NewLine;
            if (!checkForZero && String.IsNullOrEmpty(text))
            {
                messageBox.Message.Result += "\u2022 copy the (empty) value \u00AB" + text + "\u00BB in this field from here to the last file of your selected files.";
            }
            else
            {
                messageBox.Message.Result += "\u2022 copy the value \u00AB" + text + "\u00BB in this field from here to the last file of your selected files.";
            }
            messageBox.Message.Result += Environment.NewLine + "\u2022 over-write any existing data values in those fields";
            messageBox.Message.Result += Environment.NewLine + "\u2022 will affect " + imagesAffected.ToString() + " files.";
            return messageBox.ShowDialog();
        }

        /// <summary>
        /// Ask the user to confirm value propagation to all selected files
        /// </summary>
        public static bool? DataEntryConfirmCopyCurrentValueToAllDialog(Window owner, String text, int filesAffected, bool checkForZero)
        {
            text = String.IsNullOrEmpty(text) ? String.Empty : text.Trim();

            MessageBox messageBox = new MessageBox("Please confirm 'Copy to All' for this field...", owner, MessageBoxButton.YesNo);
            messageBox.Message.Icon = MessageBoxImage.Question;
            messageBox.Message.What = "Copy to All is not undoable, and can overwrite existing values.";
            messageBox.Message.Result = "If you select yes, this operation will:" + Environment.NewLine;
            if (!checkForZero && String.IsNullOrEmpty(text))
            {
                messageBox.Message.Result += "\u2022 clear this field across all " + filesAffected.ToString() + " of your selected files.";
            }
            else
            {
                messageBox.Message.Result += "\u2022 set this field to \u00AB" + text + "\u00BB across all " + filesAffected.ToString() + " of your selected files.";
            }
            messageBox.Message.Result += Environment.NewLine + "\u2022 over-write any existing data values in those fields";
            return messageBox.ShowDialog();
        }

        /// <summary>
        /// Ask the user to confirm value propagation from the last value
        /// </summary>
        public static bool? DataEntryConfirmPropagateFromLastValueDialog(Window owner, String text, int imagesAffected)
        {
            text = String.IsNullOrEmpty(text) ? String.Empty : text.Trim();
            MessageBox messageBox = new MessageBox("Please confirm 'Propagate to Here' for this field.", owner, MessageBoxButton.YesNo);
            messageBox.Message.Icon = MessageBoxImage.Question;
            messageBox.Message.What = "Propagate to Here is not undoable, and can overwrite existing values.";
            messageBox.Message.Reason = "\u2022 The last non-empty value \u00AB" + text + "\u00BB was seen " + imagesAffected.ToString() + " files back." + Environment.NewLine;
            messageBox.Message.Reason += "\u2022 That field's value will be copied across all files between that file and this one of your selected files";
            messageBox.Message.Result = "If you select yes: " + Environment.NewLine;
            messageBox.Message.Result = "\u2022 " + imagesAffected.ToString() + " files will be affected.";
            return messageBox.ShowDialog();
        }
        #endregion

        #region MessageBox: MarkableCanvas Can't Open External PhotoViewer
        /// <summary>
        /// // Can't Open the External Photo Viewer. 
        /// </summary>
        /// <param name="owner"></param>
        /// <param name="extension"></param>
        public static void MarkableCanvasCantOpenExternalPhotoViewerDialog(Window owner, string extension)
        {
            // Can't open the image file. Note that file must exist at this pint as we checked for that above.
            MessageBox messageBox = new MessageBox("Can't open a photo viewer.", owner);
            messageBox.Message.Icon = System.Windows.MessageBoxImage.Error;
            messageBox.Message.Reason = "You probably don't have a default program set up to display a photo viewer for " + extension + " files";
            messageBox.Message.Solution = "Set up a photo viewer in your Windows Settings." + Environment.NewLine;
            messageBox.Message.Solution += "\u2022 go to 'Default apps', select 'Photo Viewer' and choose a desired photo viewer." + Environment.NewLine;
            messageBox.Message.Solution += "\u2022 or right click on an " + extension + " file and set the default viewer that way";
            messageBox.ShowDialog();
        }
        #endregion

        #region MessageBox: Show Exception Reporting  
        // REPLACED BY ExceptionShutdownDialog  - DELETE after we are sure that other method works 
        /// <summary>
        /// Display a dialog showing unhandled exceptions. The dialog text is also placed in the clipboard so that the user can paste it into their email
        /// </summary>
        /// <param name="programName">The name of the program that generated the exception</param>
        /// <param name="e">the exception</param>
        /// <param name="owner">A window where the message will be positioned within it</param>
        //public static void ShowExceptionReportingDialog(string programName, UnhandledExceptionEventArgs e, Window owner)
        //{
        //    // Check the arguments for null 
        //    ThrowIf.IsNullArgument(e, nameof(e));

        //    // once .NET 4.5+ is used it's meaningful to also report the .NET release version
        //    // See https://msdn.microsoft.com/en-us/library/hh925568.aspx.
        //    string title = programName + " needs to close. Please report this error.";
        //    MessageBox exitNotification = new MessageBox(title, owner);
        //    exitNotification.Message.Icon = MessageBoxImage.Error;
        //    exitNotification.Message.Title = title;
        //    exitNotification.Message.Problem = programName + " encountered a problem, likely due to a bug. If you let us know, we will try and fix it. ";
        //    exitNotification.Message.What = "Please help us fix it! You should be able to paste the entire content of the Reason section below into an email to saul@ucalgary.ca , along with a description of what you were doing at the time.  To quickly copy the text, click on the 'Reason' details, hit ctrl+a to select all of it, ctrl+c to copy, and then email all that.";
        //    exitNotification.Message.Reason = String.Format("{0}, {1}, .NET runtime {2}{3}", typeof(TimelapseWindow).Assembly.GetName(), Environment.OSVersion, Environment.Version, Environment.NewLine);
        //    if (e.ExceptionObject != null)
        //    {
        //        exitNotification.Message.Reason += e.ExceptionObject.ToString();
        //    }
        //    exitNotification.Message.Result = String.Format("The data file is likely OK.  If it's not you can restore from the {0} folder.", Constant.File.BackupFolder);
        //    exitNotification.Message.Hint = "\u2022 If you do the same thing this'll probably happen again.  If so, that's helpful to know as well." + Environment.NewLine;

        //    // Modify text for custom exceptions
        //    Exception custom_excepton = (Exception)e.ExceptionObject;
        //    switch (custom_excepton.Message)
        //    {
        //        case Constant.ExceptionTypes.TemplateReadWriteException:
        //            exitNotification.Message.Problem =
        //                programName + "  could not read data from the template (.tdb) file. This could be because: " + Environment.NewLine +
        //                "\u2022 the .tdb file is corrupt, or" + Environment.NewLine +
        //                "\u2022 your system is somehow blocking Timelapse from manipulating that file (e.g., Citrix security will do that)" + Environment.NewLine +
        //                "If you let us know, we will try and fix it. ";
        //            break;
        //        default:
        //            exitNotification.Message.Problem = programName + " encountered a problem, likely due to a bug. If you let us know, we will try and fix it. ";
        //            break;
        //    }
        //    Clipboard.SetText(exitNotification.Message.Reason);
        //    exitNotification.ShowDialog();
        //}
        #endregion

        #region MessageBox: No Updates Available
        public static void NoUpdatesAvailableDialog(Window owner, string applicationName, Version currentVersionNumber)
        {
            MessageBox messageBox = new MessageBox(String.Format("No updates to {0} are available.", applicationName), owner);
            messageBox.Message.Reason = String.Format("You a running the latest version of {0}, version: {1}", applicationName, currentVersionNumber);
            messageBox.Message.Icon = MessageBoxImage.Information;
            messageBox.ShowDialog();
        }
        #endregion

        #region MessageBox: File Selection
        /// <summary>
        /// // No files were missing in the current selection
        /// </summary>
        public static void FileSelectionNoFilesAreMissingDialog(Window owner)
        {
            MessageBox messageBox = new MessageBox("No Files are Missing.", owner);
            messageBox.Message.Title = "No Files are Missing in the Current Selection.";
            messageBox.Message.Icon = MessageBoxImage.Information;
            messageBox.Message.What = "No files are missing in the current selection.";
            messageBox.Message.Reason = "All files in the current selection were checked, and all are present. None were missing.";
            messageBox.Message.Result = "No changes were made.";
            messageBox.ShowDialog();
        }

        public static void FileSelectionResettngSelectionToAllFilesDialog(Window owner, FileSelectionEnum selection)
        {
            // These cases are reached when 
            // 1) datetime modifications result in no files matching a custom selection
            // 2) all files which match the selection get deleted
            MessageBox messageBox = new MessageBox("Resetting selection to All files (no files currently match the current selection)", owner);
            messageBox.Message.Icon = MessageBoxImage.Information;
            messageBox.Message.Result = "The 'All files' selection will be applied, where all files in your image set are displayed.";

            switch (selection)
            {
                case FileSelectionEnum.Custom:
                    messageBox.Message.Problem = "No files currently match the custom selection so nothing can be shown.";
                    messageBox.Message.Reason = "No files match the criteria set in the current Custom selection.";
                    messageBox.Message.Hint = "Create a different custom selection and apply it view the matching files.";
                    break;
                case FileSelectionEnum.Folders:
                    messageBox.Message.Problem = "No files and/or image data were found for the selected folder.";
                    messageBox.Message.Reason = "Perhaps they were deleted during this session?";
                    messageBox.Message.Hint = "Try other folders or another selection. ";
                    break;
                case FileSelectionEnum.Dark:
                    messageBox.Message.Problem = "Dark files were previously selected but no files are currently dark so nothing can be shown.";
                    messageBox.Message.Reason = "No files have their 'ImageQuality' field set to Dark.";
                    messageBox.Message.Hint = "If you have files you think should be marked as 'Dark', set their 'ImageQuality' field to 'Dark' and then reselect dark files.";
                    break;
                case FileSelectionEnum.Missing:
                    // We should never invoke this, as its handled earlier.
                    messageBox.Message.Problem = "Missing files were previously selected. However, none of the files appear to be missing, so nothing can be shown.";
                    break;
                case FileSelectionEnum.MarkedForDeletion:
                    messageBox.Message.Problem = "Files marked for deletion were previously selected but no files are currently marked so nothing can be shown.";
                    messageBox.Message.Reason = "No files have their 'Delete?' field checked.";
                    messageBox.Message.Hint = "If you have files you think should be marked for deletion, check their 'Delete?' field and then reselect files marked for deletion.";
                    break;
                case FileSelectionEnum.Ok:
                    messageBox.Message.Problem = "Light files were previously selected but no files are currently marked 'Light' so nothing can be shown.";
                    messageBox.Message.Reason = "No files have their 'ImageQuality' field set to Light.";
                    messageBox.Message.Hint = "If you have files you think should be marked as 'Light', set their 'ImageQuality' field to 'Light' and then reselect Light files.";
                    break;
                default:
                    throw new NotSupportedException(String.Format("Unhandled selection {0}.", selection));
            }
            messageBox.ShowDialog();
        }
        #endregion

        #region MessageBox: MissingFilesNotFound / Missing Folders
        public static void MissingFileSearchNoMatchesFoundDialog(Window owner, string fileName)
        {
            string title = "Timelapse could not find any matches to " + fileName;
            Dialog.MessageBox messageBox = new Dialog.MessageBox(title, owner, MessageBoxButton.OK);

            messageBox.Message.What = "Timelapse tried to find the missing image with no success.";

            messageBox.Message.Reason = "Timelapse searched the other folders in this image set, but could not find another file that: " + Environment.NewLine;
            messageBox.Message.Reason += " - was named " + fileName + ", and  " + Environment.NewLine;
            messageBox.Message.Reason += " - was not already associated with another image entry.";

            messageBox.Message.Hint = "If the original file was:" + Environment.NewLine;
            messageBox.Message.Hint += "\u2022 deleted, check your " + Constant.File.DeletedFilesFolder + " folder to see if its there." + Environment.NewLine;
            messageBox.Message.Hint += "\u2022 moved outside of this image set, then you will have to find it and move it back in." + Environment.NewLine;
            messageBox.Message.Hint += "\u2022 renamed, then you have to find it yourself and restore its original name." + Environment.NewLine + Environment.NewLine;
            messageBox.Message.Hint += "Of course, you can just leave things as they are, or delete this image's data field if it has little value to you.";

            messageBox.Message.Icon = MessageBoxImage.Question;
            messageBox.ShowDialog();
        }

        public static void MissingFoldersInformationDialog(Window owner, int count)
        {
            Cursor cursor = Mouse.OverrideCursor;
            Mouse.OverrideCursor = null;

            string title = count.ToString() + " of your folders could not be found";
            Dialog.MessageBox messageBox = new Dialog.MessageBox(title, owner, MessageBoxButton.OK);

            messageBox.Message.Problem = "Timelapse checked for the folders containing your image and video files, and noticed that " + count.ToString() + " are missing.";

            messageBox.Message.Reason = "These folders may have been moved, renamed, or deleted since Timelapse last recorded their location.";

            messageBox.Message.Solution = "If you want to try to locate missing folders and files, select: " + Environment.NewLine;
            messageBox.Message.Solution += "\u2022 'Edit | Try to find missing folders...' to have Timelapse help locate those folders, or" + Environment.NewLine;
            messageBox.Message.Solution += "\u2022 'Edit | Try to find this (and other) missing files...' to have Timelapse help locate one or more missing files in a particular folder.";

            messageBox.Message.Hint = "Everything will still work as normal, except that a 'Missing file' image will be displayed instead of the actual image." + Environment.NewLine;

            messageBox.Message.Hint += "Searching for the missing folders is optional.";

            messageBox.Message.Icon = MessageBoxImage.Exclamation;
            messageBox.ShowDialog();
            Mouse.OverrideCursor = cursor;
        }
        #endregion

        #region MessageBox: ImageSetLoading
        /// <summary>
        /// If there are multiple missing folders, it will generate multiple dialog boxes. Thus we explain what is going on.
        /// </summary>
        /// DEPRACATED - CAN DELETE
        //public static bool? ImageSetLoadingMultipleImageFoldersNotFoundDialog(Window owner, List<string> missingRelativePaths)
        //{

        //    if (missingRelativePaths == null)
        //    {
        //        // this should never happen
        //        missingRelativePaths = new List<string>();
        //    }
        //    MessageBox messageBox = new MessageBox("Multiple image folders cannot be found. Locate them?", owner, MessageBoxButton.OKCancel);
        //    messageBox.Message.Problem = "Timelapse could not locate the following image folders" + Environment.NewLine;
        //    foreach (string relativePath in missingRelativePaths)
        //    {
        //        messageBox.Message.Problem += "\u2022 " + relativePath + Environment.NewLine;
        //    }
        //    messageBox.Message.Solution = "OK raises one or more dialog boxes asking you to locate a particular missing folder." + Environment.NewLine;
        //    messageBox.Message.Solution += "Cancel will still display the image's data, along with a 'missing' image placeholder";
        //    messageBox.Message.Icon = MessageBoxImage.Question;
        //    return messageBox.ShowDialog();
        //}

        /// <summary>
        /// No images were found in the root folder or subfolders, so there is nothing to do
        /// </summary>
        public static void ImageSetLoadingNoImagesOrVideosWereFoundDialog(Window owner, string selectedFolderPath)
        {
            MessageBox messageBox = new MessageBox("No images or videos were found", owner, MessageBoxButton.OK);
            messageBox.Message.Problem = "No images or videos were found in this folder or its subfolders:" + Environment.NewLine;
            messageBox.Message.Problem += "\u2022 " + selectedFolderPath + Environment.NewLine;
            messageBox.Message.Reason = "Neither the folder nor its sub-folders contain:" + Environment.NewLine;
            messageBox.Message.Reason += "\u2022 image files (ending in '.jpg') " + Environment.NewLine;
            messageBox.Message.Reason += "\u2022 video files (ending in '.avi or .mp4')";
            messageBox.Message.Solution = "Timelapse aborted the load operation." + Environment.NewLine;
            messageBox.Message.Hint = "Locate your template in a folder containing (or whose subfolders contain) image or video files ." + Environment.NewLine;
            messageBox.Message.Icon = MessageBoxImage.Exclamation;
            messageBox.ShowDialog();
        }

        /// <summary>
        /// Data imported from old XML file
        /// </summary>
        public static void ImageSetLoadingDataImportedFromOldXMLFileDialog(Window owner, int unskipped, int skipped)
        {
            MessageBox messageBox = new MessageBox("Data imported from old XML file", owner);
            messageBox.Message.What = "Data imported " + unskipped.ToString() + " existing images, each with an entry in your ImageData.xml file." + Environment.NewLine;
            if (skipped != 0)
            {
                messageBox.Message.What += Environment.NewLine + "However, " + skipped.ToString() + " data entries in the ImageData.xml file were skipped, as they did not match any existing files.";
                messageBox.Message.What += Environment.NewLine + "This can occur if you moved, renamed, or deleted some of your original files.";
                messageBox.Message.What += Environment.NewLine + "This is not necessarily an error, but you should check to make sure you have what you need.";
            }
            messageBox.ShowDialog();
        }
        #endregion

        #region MessageBox: MenuFile
        /// <summary>
        /// No matching folders in the DB and the detector
        /// </summary>
        public static void MenuFileRecognitionDataNotImportedDialog(Window owner, string details)
        {
            MessageBox messageBox = new MessageBox("Recognition data not imported.", owner);
            messageBox.Message.Problem = "No recognition information was imported, as none of its image folder paths were found in your Database file." + Environment.NewLine;
            messageBox.Message.Problem += "Thus no recognition information could be assigned to your images.";
            messageBox.Message.Reason = "The recognizer may have been run on a folder containing various image sets, each in a sub-folder. " + Environment.NewLine;
            messageBox.Message.Reason += "For example, if the recognizer was run on 'AllFolders/Camera1/' but your template and database is in 'Camera1/'," + Environment.NewLine;
            messageBox.Message.Reason += "the folder paths won't match, since AllFolders/Camera1/ \u2260 Camera1/.";
            messageBox.Message.Solution = "Microsoft provides a program to extract a subset of recognitions in the Recognition file" + Environment.NewLine;
            messageBox.Message.Solution += "that you can use to extract recognitions matching your sub-folder: " + Environment.NewLine;
            messageBox.Message.Solution += "  http://aka.ms/cameratraps-detectormismatch";
            messageBox.Message.Result = "Recognition information was not imported.";
            messageBox.Message.Details = details;
            messageBox.ShowDialog();
        }

        /// <summary>
        ///  Some folders missing - show which folder paths in the DB are not in the detector
        /// </summary>
        public static void MenuFileRecognitionDataImportedOnlyForSomeFoldersDialog(Window owner, string details)
        {
            // Some folders missing - show which folder paths in the DB are not in the detector
            MessageBox messageBox = new MessageBox("Recognition data imported for only some of your folders.", owner);
            messageBox.Message.Icon = MessageBoxImage.Information;
            messageBox.Message.Problem = "Some of the sub-folders in your image set's Database file have no corresponding entries in the Recognition file." + Environment.NewLine;
            messageBox.Message.Problem += "While not an error, we just wanted to bring it to your attention.";
            messageBox.Message.Reason = "This could happen if you have added, moved, or renamed the folders since supplying the originals to the recognizer:" + Environment.NewLine;
            messageBox.Message.Result = "Recognition data will still be imported for the other folders.";
            messageBox.Message.Hint = "You can also view which images are missing recognition data by choosing" + Environment.NewLine;
            messageBox.Message.Hint += "'Select|Custom Selection...' and checking the box titled 'Show all files with no recognition data'";
            messageBox.Message.Details = details;
            messageBox.ShowDialog();
        }

        /// <summary>
        /// Detections successfully imported message
        /// </summary>
        public static void MenuFileDetectionsSuccessfulyImportedDialog(Window owner, string details)
        {
            MessageBox messageBox = new MessageBox("Recognitions imported.", owner);
            messageBox.Message.Icon = MessageBoxImage.Information;
            messageBox.Message.Result = "Recognition data imported. You can select images matching particular recognitions by choosing 'Select|Custom Selection...'";
            messageBox.Message.Hint = "You can also view which images (if any) are missing recognition data by choosing" + Environment.NewLine;
            messageBox.Message.Hint += "'Select|Custom Selection...' and checking the box titled 'Show all files with no recognition data'";
            messageBox.Message.Details = details;
            messageBox.ShowDialog();
        }

        /// <summary>
        /// Export data for this image set as a.csv file, but confirm, as only a subset will be exported since a selection is active
        /// </summary>
        public static bool? MenuFileExportCSVOnSelectionDialog(Window owner)
        {
            MessageBox messageBox = new MessageBox("Exporting to a .csv file on a selected view...", owner, MessageBoxButton.OKCancel);
            messageBox.Message.What = "Only a subset of your data will be exported to the .csv file.";
            messageBox.Message.Reason = "As your selection (in the Selection menu) is not set to view 'All', ";
            messageBox.Message.Reason += "only data for these selected files will be exported. ";
            messageBox.Message.Solution = "If you want to export just this subset, then " + Environment.NewLine;
            messageBox.Message.Solution += "\u2022 click Okay" + Environment.NewLine + Environment.NewLine;
            messageBox.Message.Solution += "If you want to export data for all your files, then " + Environment.NewLine;
            messageBox.Message.Solution += "\u2022 click Cancel," + Environment.NewLine;
            messageBox.Message.Solution += "\u2022 select 'All Files' in the Selection menu, " + Environment.NewLine;
            messageBox.Message.Solution += "\u2022 retry exporting your data as a .csv file.";
            messageBox.Message.Hint = "If you check don't show this message this dialog can be turned back on via the Options menu.";
            messageBox.Message.Icon = MessageBoxImage.Warning;
            messageBox.DontShowAgain.Visibility = Visibility.Visible;

            bool? exportCsv = messageBox.ShowDialog();
            if (messageBox.DontShowAgain.IsChecked.HasValue)
            {
                Util.GlobalReferences.TimelapseState.SuppressSelectedCsvExportPrompt = messageBox.DontShowAgain.IsChecked.Value;
            }
            return exportCsv;
        }

        /// <summary>
        /// Cant write the spreadsheet file
        /// </summary>
        public static void MenuFileCantWriteSpreadsheetFileDialog(Window owner, string csvFilePath, string exceptionName, string exceptionMessage)
        {
            MessageBox messageBox = new MessageBox("Can't write the spreadsheet file.", owner);
            messageBox.Message.Icon = MessageBoxImage.Error;
            messageBox.Message.Problem = "The following file can't be written: " + csvFilePath;
            messageBox.Message.Reason = "You may already have it open in Excel or another application.";
            messageBox.Message.Solution = "If the file is open in another application, close it and try again.";
            messageBox.Message.Hint = String.Format("{0}: {1}", exceptionName, exceptionMessage);
            messageBox.ShowDialog();
        }

        /// <summary>
        /// Cant open the file using Excel
        /// </summary>
        public static void MenuFileCantOpenExcelDialog(Window owner, string csvFilePath)
        {
            MessageBox messageBox = new MessageBox("Can't open Excel.", owner);
            messageBox.Message.Icon = MessageBoxImage.Error;
            messageBox.Message.Problem = "Excel could not be opened to display " + csvFilePath;
            messageBox.Message.Solution = "Try again, or just manually start Excel and open the .csv file ";
            messageBox.ShowDialog();
        }

        /// <summary>
        /// Give the user some feedback about the CSV export operation
        /// </summary>
        public static void MenuFileCSVDataExportedDialog(Window owner, string csvFileName)
        {
            // since the exported file isn't shown give the user some feedback about the export operation
            MessageBox csvExportInformation = new MessageBox("Data exported.", owner);
            csvExportInformation.Message.What = "The selected files were exported to " + csvFileName;
            csvExportInformation.Message.Result = String.Format("This file is overwritten every time you export it (backups can be found in the {0} folder).", Constant.File.BackupFolder);
            csvExportInformation.Message.Hint = "\u2022 You can open this file with most spreadsheet programs, such as Excel." + Environment.NewLine;
            csvExportInformation.Message.Hint += "\u2022 If you make changes in the spreadsheet file, you will need to import it to see those changes." + Environment.NewLine;
            csvExportInformation.Message.Hint += "\u2022 If you check don't show this message again you can still see the name of the .csv file in the status bar at the lower right corner of the main Carnassial window.  This dialog can also be turned back on through the Options menu.";
            csvExportInformation.Message.Icon = MessageBoxImage.Information;
            csvExportInformation.DontShowAgain.Visibility = Visibility.Visible;

            bool? result = csvExportInformation.ShowDialog();
            if (result.HasValue && result.Value && csvExportInformation.DontShowAgain.IsChecked.HasValue)
            {
                Util.GlobalReferences.TimelapseState.SuppressCsvExportDialog = csvExportInformation.DontShowAgain.IsChecked.Value;
            }
        }

        /// <summary>
        /// Tell the user how importing CSV files work. Give them the opportunity to abort.
        /// </summary>
        public static bool? MenuFileHowImportingCSVWorksDialog(Window owner)
        {
            MessageBox messageBox = new MessageBox("How importing .csv data works", owner, MessageBoxButton.OKCancel);
            messageBox.Message.What = "Importing data from a .csv (comma separated value) file follows the rules below.";
            messageBox.Message.Reason = "\u2022 The first row in the CSV file must comprise column Headers that match the DataLabels in the .tdb template file." + Environment.NewLine;
            messageBox.Message.Reason += "\u2022 The column Header 'File' must be included." + Environment.NewLine;
            messageBox.Message.Reason += "\u2022 Subsequent rows defines the data for each File ." + Environment.NewLine;
            messageBox.Message.Reason += "\u2022 Column data should match the Header type. In particular," + Environment.NewLine;
            messageBox.Message.Reason += "  \u2022\u2022 File values should define name of the file you want to update." + Environment.NewLine;
            messageBox.Message.Reason += "  \u2022\u2022 Counter values must be blank or a positive integer. " + Environment.NewLine;
            messageBox.Message.Reason += "  \u2022\u2022 Flag and DeleteFlag values must be 'true' or 'false'." + Environment.NewLine;
            messageBox.Message.Reason += "  \u2022\u2022 FixedChoice values should be a string that exactly matches one of the FixedChoice menu options, or empty. ";
            messageBox.Message.Result = "Image values for identified files will be updated, except for values relating to a File's location or its dates / times.";
            messageBox.Message.Hint = "\u2022 Your CSV file columns can be a subset of your template's DataLabels." + Environment.NewLine;
            messageBox.Message.Hint += "\u2022 Warning will be generated for non-matching CSV fields, which you can then fix." + Environment.NewLine;
            messageBox.Message.Hint += "\u2022 If you check 'Don't show this message again' this dialog can be turned back on via the Options menu.";
            messageBox.Message.Icon = MessageBoxImage.Warning;
            messageBox.DontShowAgain.Visibility = Visibility.Visible;

            bool? result = messageBox.ShowDialog();
            if (messageBox.DontShowAgain.IsChecked.HasValue)
            {
                Util.GlobalReferences.TimelapseState.SuppressCsvImportPrompt = messageBox.DontShowAgain.IsChecked.Value;
            }
            return result;
        }

        /// <summary>
        /// Can't import CSV File
        /// </summary>
        public static void MenuFileCantImportCSVFileDialog(Window owner, string csvFileName, List<string> resultAndImportErrors)
        {
            MessageBox messageBox = new MessageBox("Can't import the .csv file.", owner);
            messageBox.Message.Icon = MessageBoxImage.Error;
            messageBox.Message.Problem = String.Format("The file {0} could not be read.", csvFileName);
            messageBox.Message.Reason = "The .csv file is not compatible with the current image set.";
            messageBox.Message.Solution = "Check that:" + Environment.NewLine;
            messageBox.Message.Solution += "\u2022 The first row of the .csv file is a header line." + Environment.NewLine;
            messageBox.Message.Solution += "\u2022 The column names in the header line match the database." + Environment.NewLine;
            messageBox.Message.Solution += "\u2022 Choice and ImageQuality values are in that DataLabel's Choice list." + Environment.NewLine;
            messageBox.Message.Solution += "\u2022 Counter values are numbers or blanks." + Environment.NewLine;
            messageBox.Message.Solution += "\u2022 Flag and DeleteFlag values are either 'true' or 'false'.";
            messageBox.Message.Result = "Importing of data from the CSV file was aborted. No changes were made.";
            if (resultAndImportErrors != null)
            {
                messageBox.Message.Hint = "Change your CSV file to fix the errors below and try again.";
                foreach (string importError in resultAndImportErrors)
                {
                    messageBox.Message.Hint += Environment.NewLine + "\u2022 " + importError;
                }
            }
            messageBox.ShowDialog();
        }

        /// <summary>
        /// CSV file imported
        /// </summary>
        public static void MenuFileCSVFileImportedDialog(Window owner, string csvFileName)
        {
            MessageBox messageBox = new MessageBox("CSV file imported", owner);
            messageBox.Message.Icon = MessageBoxImage.Information;
            messageBox.Message.What = String.Format("The file {0} was successfully imported.", csvFileName);
            messageBox.Message.Hint = "\u2022 Check your data. If it is not what you expect, restore your data by using latest backup file in " + Constant.File.BackupFolder + ".";
            messageBox.ShowDialog();

        }

        /// <summary>
        /// Can't import the .csv file
        /// </summary>
        public static void MenuFileCantImportCSVFileDialog(Window owner, string csvFileName, string exceptionMessage)
        {
            MessageBox messageBox = new MessageBox("Can't import the .csv file.", owner);
            messageBox.Message.Icon = MessageBoxImage.Error;
            messageBox.Message.Problem = String.Format("The file {0} could not be opened.", csvFileName);
            messageBox.Message.Reason = "Most likely the file is open in another program. The technical reason is:" + Environment.NewLine;
            messageBox.Message.Reason += exceptionMessage;
            messageBox.Message.Solution = "If the file is open in another program, close it.";
            messageBox.Message.Result = "Importing of data from the CSV file was aborted. No changes were made.";
            messageBox.Message.Hint = "Is the file open in Excel?";
            messageBox.ShowDialog();
        }

        /// <summary>
        /// Can't export the currently displayed image as a file
        /// </summary>
        public static void MenuFileCantExportCurrentImageDialog(Window owner)
        {
            MessageBox messageBox = new MessageBox("Can't export this file!", owner);
            messageBox.Message.Icon = MessageBoxImage.Error;
            messageBox.Message.Problem = "Timelapse can't export the currently displayed image or video file.";
            messageBox.Message.Reason = "It is likely a corrupted or missing file.";
            messageBox.Message.Solution = "Make sure you have navigated to, and are displaying, a valid file before you try to export it.";
            messageBox.ShowDialog();
        }

        /// <summary>
        /// Show a message that explains how merging databases works and its constraints. Give the user an opportunity to abort
        /// </summary>
        public static bool? MenuFileMergeDatabasesExplainedDialog(Window owner)
        {

            MessageBox messageBox = new MessageBox("Merge Databases.", owner, MessageBoxButton.OKCancel);
            messageBox.Message.Icon = MessageBoxImage.Question;
            messageBox.Message.Title = "Merge Databases Explained.";
            messageBox.Message.What = "Merging databases works as follows. Timelapse will:" + Environment.NewLine;
            messageBox.Message.What += "\u2022 ask you to locate a root folder containing a template (a.tdb file)," + Environment.NewLine;
            messageBox.Message.What += String.Format("\u2022 create a new database (.ddb) file in that folder, called {0},{1}", Constant.File.MergedFileName, Environment.NewLine);
            messageBox.Message.What += "\u2022 search for other database (.ddb) files in that folder's sub-folders, " + Environment.NewLine;
            messageBox.Message.What += "\u2022 try to merge all data found in those found databases into the new database.";
            messageBox.Message.Details = "\u2022 All databases must be based on the same template, otherwise the merge will fail." + Environment.NewLine;
            messageBox.Message.Details += "\u2022 Databases found in the Backup folders are ignored." + Environment.NewLine;
            messageBox.Message.Details += "\u2022 Detections and Classifications (if any) are merged; categories are taken from the first database found with detections." + Environment.NewLine;
            messageBox.Message.Details += "\u2022 The merged database is independent of the found databases: updates will not propagate between them." + Environment.NewLine;
            messageBox.Message.Details += "\u2022 The merged database is a normal Timelapse database, which you can open and use as expected.";
            messageBox.Message.Hint = "Press Ok to continue with the merge, otherwise Cancel.";
            messageBox.DontShowAgain.Visibility = Visibility.Visible;
            messageBox.ShowDialog();

            if (messageBox.DontShowAgain.IsChecked.HasValue)
            {
                Util.GlobalReferences.TimelapseState.SuppressMergeDatabasesPrompt = messageBox.DontShowAgain.IsChecked.Value;
            }
            return messageBox.DialogResult;
        }

        /// <summary>
        /// Merge databases: Show errors and/or warnings, if any.
        /// </summary>
        public static void MenuFileMergeDatabasesErrorsAndWarningsDialog(Window owner, ErrorsAndWarnings errorMessages)
        {
            if (errorMessages == null)
            {
                return;
            }
            MessageBox messageBox = new MessageBox("Merge Databases Results.", owner);
            messageBox.Message.Icon = MessageBoxImage.Error;
            if (errorMessages.Errors.Count != 0)
            {
                messageBox.Message.Title = "Merge Databases Failed.";
                messageBox.Message.What = "The merged database could not be created for the following reasons:";
            }
            else if (errorMessages.Warnings.Count != 0)
            {
                messageBox.Message.Title = "Merge Databases Left Out Some Files.";
                messageBox.Message.What = "The merged database left out some files for the following reasons:";
            }

            if (errorMessages.Errors.Count != 0)
            {
                messageBox.Message.What += String.Format("{0}{0}Errors:", Environment.NewLine);
                foreach (string error in errorMessages.Errors)
                {
                    messageBox.Message.What += String.Format("{0}\u2022 {1},", Environment.NewLine, error);
                }
            }
            if (errorMessages.Warnings.Count != 0)
            {
                messageBox.Message.What += String.Format("{0}{0}Warnings:", Environment.NewLine);
            }
            foreach (string warning in errorMessages.Warnings)
            {
                messageBox.Message.What += String.Format("{0}\u2022 {1},", Environment.NewLine, warning);
            }
            messageBox.ShowDialog();
        }
        #endregion

        #region MessageBox: MenuEdit
        public static void MenuEditCouldNotImportQuickPasteEntriesDialog(Window owner)
        {
            MessageBox messageBox = new MessageBox("Could not import QuickPaste entries", owner);
            messageBox.Message.Problem = "Timelapse could not find any QuickPaste entries in the selected database";
            messageBox.Message.Reason = "When an analyst creates QuickPaste entries, those entries are stored in the database file " + Environment.NewLine;
            messageBox.Message.Reason += "associated with the image set being analyzed. Since none where found, " + Environment.NewLine;
            messageBox.Message.Reason += "its likely that no one had created any quickpaste entries when analyzing that image set.";
            messageBox.Message.Hint = "Perhaps they are in a different database?";
            messageBox.Message.Icon = MessageBoxImage.Information;
            messageBox.ShowDialog();
        }
        /// <summary>
        /// There are no displayable images, and thus no metadata to choose from
        /// </summary>
        public static void MenuEditPopulateDataFieldWithMetadataDialog(Window owner)
        {
            MessageBox messageBox = new MessageBox("Populate a data field with image metadata of your choosing.", owner);
            messageBox.Message.Problem = "Timelapse can't extract any metadata, as the currently displayed image or video is missing or corrupted." + Environment.NewLine;
            messageBox.Message.Reason = "Timelapse tries to examines the currently displayed image or video for its metadata.";
            messageBox.Message.Hint = "Navigate to a displayable image or video, and try again.";
            messageBox.Message.Icon = MessageBoxImage.Error;
            messageBox.ShowDialog();
        }

        public static void MenuEditNoFilesMarkedForDeletionDialog(Window owner)
        {
            MessageBox messageBox = new MessageBox("No files are marked for deletion", owner);
            messageBox.Message.Problem = "You are trying to delete files marked for deletion, but no files have their 'Delete?' field checked.";
            messageBox.Message.Hint = "If you have files that you think should be deleted, check their Delete? field.";
            messageBox.Message.Icon = MessageBoxImage.Information;
            messageBox.ShowDialog();
        }

        public static void MenuEditNoFoldersAreMissing(Window owner)
        {
            MessageBox messageBox = new MessageBox("No folders appear to be missing", owner);
            messageBox.Message.What = "You asked to to find any missing folders, but none appear to be missing.";
            messageBox.Message.Hint = "You don't normally have to do this check yourself, as a check for missing folders is done automatically whenever you start Timelapse.";
            messageBox.Message.Icon = MessageBoxImage.Information;
            messageBox.ShowDialog();
        }
        #endregion

        #region MessageBox: related to DateTime
        public static void DateTimeNewTimeShouldBeLaterThanEarlierTimeDialog(Window owner)
        {
            MessageBox messageBox = new MessageBox("Your new time has to be later than the earliest time", owner);
            messageBox.Message.Icon = MessageBoxImage.Exclamation;
            messageBox.Message.Problem = "Your new time has to be later than the earliest time   ";
            messageBox.Message.Reason = "Even the slowest clock gains some time.";
            messageBox.Message.Solution = "The date/time was unchanged from where you last left it.";
            messageBox.Message.Hint = "The image on the left shows the earliest time recorded for images in this filtered view  shown over the left image";
            messageBox.ShowDialog();
        }

        #endregion
    }
}
