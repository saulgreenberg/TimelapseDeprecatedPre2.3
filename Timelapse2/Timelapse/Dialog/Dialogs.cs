
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Forms;
using Clipboard = System.Windows.Clipboard;
using Rectangle = System.Drawing.Rectangle;

namespace Timelapse.Dialog
{
    public static class Dialogs
    {
        #region Dialog Box Positioning
        // Position the dialog box within its owner's window
        public static void SetDefaultDialogPosition(Window window)
        {
            Debug.Assert(window.Owner != null, "Window's owner property is null.  Is a set of it prior to calling ShowDialog() missing?");
            window.Left = window.Owner.Left + (window.Owner.Width - window.ActualWidth) / 2; // Center it horizontally
            window.Top = window.Owner.Top + 20; // Offset it from the windows'top by 20 pixels downwards
        }

        // Used to ensure that the window is positioned within the screen
        // Note that all uses of this method is by dialog box windows (which should be initialy positioned relative to the main timelapse window) by a call to SetDefaultDialogPosition), 
        // rather than the main timelapse window (whose position, size and layout  is managed by the TimelapseAvalonExtension methods). 
        // We could likely collapse the two, but its not worth the bother. 
        public static bool TryFitDialogWindowInWorkingArea(Window window)
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

        #region Dialog Messages: Path too long warnings
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
