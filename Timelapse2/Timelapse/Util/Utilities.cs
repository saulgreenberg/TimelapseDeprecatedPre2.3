using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using Clipboard = System.Windows.Clipboard;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using MessageBox = Timelapse.Dialog.MessageBox;
using OpenFileDialog = System.Windows.Forms.OpenFileDialog;
using Rectangle = System.Drawing.Rectangle;

namespace Timelapse.Util
{
    /// <summary>
    /// A variety of miscellaneous utility functions
    /// </summary>
    public static class Utilities
    {
        // Given two dictionaries, return a dictionary that contains only those key / value pairs in dictionary1 that are not in dictionary2 
        public static Dictionary<string, string> Dictionary1ExceptDictionary2(Dictionary<string, string> dictionary1, Dictionary<string, string> dictionary2)
        {
            Dictionary<string, string> dictionaryDifferences = new Dictionary<string, string>();
            List<string> differencesByKeys = dictionary1.Keys.Except(dictionary2.Keys).ToList();
            foreach (string key in differencesByKeys)
            {
                dictionaryDifferences.Add(key, dictionary1[key]);
            }
            return dictionaryDifferences;
        }

    // This isn't used yet, but we could use it when we switch to .Net 4.5 or higher
    public static string GetDotNetVersion()
        {
            // adapted from https://msdn.microsoft.com/en-us/library/hh925568.aspx.
            int release = 0;
            using (RegistryKey ndpKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32).OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full\"))
            {
                if (ndpKey != null)
                {
                    object releaseAsObject = ndpKey.GetValue("Release");
                    if (releaseAsObject != null)
                    {
                        release = (int)releaseAsObject;
                    }
                }
            }

            if (release >= 394802)
            {
                return "4.6.2 or later";
            }
            if (release >= 394254)
            {
                return "4.6.1";
            }
            if (release >= 393295)
            {
                return "4.6";
            }
            if (release >= 379893)
            {
                return "4.5.2";
            }
            if (release >= 378675)
            {
                return "4.5.1";
            }
            if (release >= 378389)
            {
                return "4.5";
            }

            return "4.5 or later not detected";
        }

        public static ParallelOptions GetParallelOptions(int maximumDegreeOfParallelism)
        {
            ParallelOptions parallelOptions = new ParallelOptions()
            {
                MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, maximumDegreeOfParallelism)
            };
            return parallelOptions;
        }

        public static bool IsSingleTemplateFileDrag(DragEventArgs dragEvent, out string templateDatabasePath)
        {
            if (dragEvent.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] droppedFiles = (string[])dragEvent.Data.GetData(DataFormats.FileDrop);
                if (droppedFiles != null && droppedFiles.Length == 1)
                {
                    templateDatabasePath = droppedFiles[0];
                    if (Path.GetExtension(templateDatabasePath) == Constant.File.TemplateDatabaseFileExtension)
                    {
                        return true;
                    }
                }
            }

            templateDatabasePath = null;
            return false;
        }

        /// <summary>
        /// Returns true only if every character in the string is a digit
        /// </summary>
        public static bool IsDigits(string value)
        {
            foreach (char character in value)
            {
                if (!Char.IsDigit(character))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Returns true only if every character in the string is a letter or a digit
        /// </summary>
        public static bool IsLetterOrDigit(string str)
        {
            foreach (char c in str)
            {
                if (!Char.IsLetterOrDigit(c))
                {
                    return false;
                }
            }
            return true;
        }

        public static void OnHelpDocumentPreviewDrag(DragEventArgs dragEvent)
        {
            if (Utilities.IsSingleTemplateFileDrag(dragEvent, out string templateDatabaseFilePath))
            {
                dragEvent.Effects = DragDropEffects.All;
            }
            else
            {
                dragEvent.Effects = DragDropEffects.None;
            }
            dragEvent.Handled = true;
        }

        public static void SetDefaultDialogPosition(Window window)
        {
            Debug.Assert(window.Owner != null, "Window's owner property is null.  Is a set of it prior to calling ShowDialog() missing?");
            window.Left = window.Owner.Left + (window.Owner.Width - window.ActualWidth) / 2; // Center it horizontally
            window.Top = window.Owner.Top + 20; // Offset it from the windows'top by 20 pixels downwards
        }

        public static void ShowExceptionReportingDialog(string title, UnhandledExceptionEventArgs e, Window owner)
        {
            // once .NET 4.5+ is used it's meaningful to also report the .NET release version
            // See https://msdn.microsoft.com/en-us/library/hh925568.aspx.
            MessageBox exitNotification = new MessageBox(title, owner);
            exitNotification.Message.Icon = MessageBoxImage.Error;
            exitNotification.Message.Title = title;
            exitNotification.Message.Problem = "Timelapse encountered a problem, likely due to a bug. If you let us know, we will try and fix it. ";
            exitNotification.Message.What = "Please help us fix it! You should be able to paste the entire content of the Reason section below into an email to saul@ucalgary.ca , along with a description of what you were doing at the time.  To quickly copy the text, click on the 'Reason' details, hit ctrl+a to select all of it, ctrl+c to copy, and then email all that.";
            exitNotification.Message.Reason = String.Format("{0}, {1}, .NET runtime {2}{3}", typeof(TimelapseWindow).Assembly.GetName(), Environment.OSVersion, Environment.Version, Environment.NewLine);
            if (e.ExceptionObject != null)
            {
                exitNotification.Message.Reason += e.ExceptionObject.ToString();
            }
            exitNotification.Message.Result = String.Format("The data file is likely OK.  If it's not you can restore from the {0} folder.", Constant.File.BackupFolder);
            exitNotification.Message.Hint = "\u2022 If you do the same thing this'll probably happen again.  If so, that's helpful to know as well." + Environment.NewLine;
            Clipboard.SetText(exitNotification.Message.Reason);
            exitNotification.ShowDialog();
        }

        public static void ShowFilePathTooLongDialog(UnhandledExceptionEventArgs e, Window owner)
        {
            string title = "Your File Path Names are Too Long to Handle";
            MessageBox exitNotification = new MessageBox(title, owner);
            exitNotification.Message.Icon = MessageBoxImage.Error;
            exitNotification.Message.Title = title;
            exitNotification.Message.Problem = "Timelapse has to shut down as one or more of your file paths are too long.";
            exitNotification.Message.Solution = "\u2022 Shorten the path name by moving your image folder higher up the folder hierarchy, or" + Environment.NewLine + "\u2022 Use shorter folder or file names.";
            exitNotification.Message.Reason = "Windows cannot perform file operations if the folder path combined with the file name is more than " + Constant.File.MaxPathLength.ToString() + " characters.";
            exitNotification.Message.Result = "Timelapse will shut down until you fix this.";
            exitNotification.Message.Hint = "Files created in your " + Constant.File.BackupFolder + " folder must also be less than " + Constant.File.MaxPathLength.ToString() + " characters.";
            Clipboard.SetText(e.ExceptionObject.ToString());
            exitNotification.ShowDialog();
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

        // get a location for the template database from the user
        public static bool TryGetFileFromUser(string title, string defaultFilePath, string filter, out string selectedFilePath)
        {
            // Get the template file, which should be located where the images reside
            OpenFileDialog openFileDialog = new OpenFileDialog()
            {
                Title = title,
                CheckFileExists = true,
                CheckPathExists = true,
                Multiselect = false,
                AutoUpgradeEnabled = true,

                // Set filter for file extension and default file extension 
                DefaultExt = Constant.File.TemplateDatabaseFileExtension,
                Filter = filter
            };
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

        /// <summary>
        /// Format the passed value for use as string value in a SQL statement or query.
        /// </summary>
        public static string QuoteForSql(string value)
        {
            // promote null values to empty strings
            if (value == null)
            {
                return "''";
            }

            // for an input of "foo's bar" the output is "'foo''s bar'"
            return "'" + value.Replace("'", "''") + "'";
        }

        [Conditional("TRACE")]
        // Option to print various failure messagesfor debugging
        public static void PrintFailure(string message)
        {
            Debug.Print("PrintFailure: " + message);
        }

        [Conditional("TRACE")]
        // Option to print various failure messagesfor debugging
        public static void PrintMethodName(int level)
        {
            PrintMethodName(String.Empty, level);
        }
        [Conditional("TRACE")]
        public static void PrintMethodName(string message)
        {
            PrintMethodName(message, 1);
        }

        [Conditional("TRACE")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        // Insert this call into the beginning oa method name with the TRACE flag set in properties
        // Helpful to see the order and number of calls on a method.
        // The optional message string can be anything you want included in the output.
        // The optional level is the depth of the stack that should be printed 
        // (1 just prints the current method name; 2 adds the caller name of that method, etc.)
        public static void PrintMethodName(string description = "", int level = 1)
        {
            StackTrace st = new StackTrace(true);
            StackFrame sf;
            string message = String.Empty;
            for (int i = 1; i <= level; i++)
            { 
                sf = st.GetFrame(i);
                message += Path.GetFileName(sf.GetFileName()) + ": ";
                message += sf.GetMethod().Name;
                if (i < level)
                {
                    message += " <- ";
                }
            }
            message += ": " + description;
            Debug.Print(message);
        }
    }
}
