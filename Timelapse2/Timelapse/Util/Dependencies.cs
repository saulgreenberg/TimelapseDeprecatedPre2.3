using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;

namespace Timelapse.Util
{
    public static class Dependencies
    {
        private static readonly List<string> CommonRequiredBinaries = new List<string>()
        {
            // MetadataExtractor
            //"ICSharpCode.SharpZipLib.dll",
            "MetadataExtractor.dll",
            "XmpCore.dll",

            // SQLite
            "System.Data.SQLite.dll",
            "System.Data.SQLite.xml",
            "x64/SQLite.Interop.dll",
            "x86/SQLite.Interop.dll",

            // Extended WPF toolkit
            "Xceed.Wpf.Toolkit.dll",
            "Xceed.Wpf.AvalonDock.dll",
        };

        private static readonly List<string> TimelapseRequiredBinaries = new List<string>()
        {
            "Microsoft.WindowsAPICodePack.dll", // required by Microsoft.WindowsAPICodePack.Shell.dll
            "Microsoft.WindowsAPICodePack.Shell.dll", // just for TimelapseWindow's use of CommonOpenFileDialog
        };

        private static readonly List<string> EditorRequiredBinaries = new List<string>()
        {
            "Timelapse2.exe"
        };

        /// <summary>
        /// If any dependency files are missing, return false else true
        /// </summary>
        public static bool AreRequiredBinariesPresent(string applicationName, Assembly executingAssembly)
        {
            string directoryContainingCurrentExecutable = Path.GetDirectoryName(executingAssembly.Location);
            foreach (string binaryName in Dependencies.CommonRequiredBinaries)
            {
                if (File.Exists(Path.Combine(directoryContainingCurrentExecutable, binaryName)) == false)
                {
                    return false;
                }
            }

            if (applicationName == Constant.ApplicationName)
            {
                foreach (string binaryName in Dependencies.TimelapseRequiredBinaries)
                {
                    if (File.Exists(Path.Combine(directoryContainingCurrentExecutable, binaryName)) == false)
                    {
                        return false;
                    }
                }
            }
            else
            {
                foreach (string binaryName in Dependencies.EditorRequiredBinaries)
                {
                    if (File.Exists(Path.Combine(directoryContainingCurrentExecutable, binaryName)) == false)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        public static void ShowMissingBinariesDialog(string applicationName)
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
            MessageBox.Show(message.ToString(), messageTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
