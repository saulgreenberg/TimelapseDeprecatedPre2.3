using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Timelapse.Dialog
{
    public static class FileFolderSelection
    {

        #region Locate a folder using File Explorer and Open File
        /// <summary>
        /// Folder dialog where the user can only select a sub-folder of the root folder path
        /// It returns the relative path to the selected folder
        /// If folderNameToLocate is not empty, it displays that a desired folder to select.
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
    }
}
