using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using Timelapse.Database;

namespace Timelapse
{
    // Episodes
    // This is a static class that calculates and saves state information in various static data structures. Notabley, 
    // - ShowEpisodes is a boolean that detemines whether Episode information should be displayed
    // - EpisodeDictionary caches episode information by FileTable index, where that information is created on demand 
    public static class Episodes
    {
        // A dictionary defining episodes across files in the file table.
        // An example dictionary beginning with an episode of 2 files then of 1 file would return, e.g., 
        // - 0,(1,2) (0th file, 1 out of 2 images in the episode) 
        // - 1,(2,2) (1st file, 2 out of 2 images in the episode) 
        // - 2,(1,1) (2nd file, 1 out of 1 images in the episode) etc
        public static Dictionary<int, Tuple<int, int>> EpisodesDictionary { get; set; }

        // Sets the state of whether we should show episodes or not
        private static bool showEpisodes = false;
        public static bool ShowEpisodes
        {
            get
            {
                return showEpisodes;
            }
            set
            {
                showEpisodes = value;
            }
        }

        private static TimeSpan timeDifferenceThreshold = TimeSpan.FromMinutes(Constant.EpisodeDefaults.TimeThresholdDefault);
        public static TimeSpan TimeThreshold
        {
            get
            {
                return timeDifferenceThreshold;
            }
            set
            {
                timeDifferenceThreshold = value;
            }
        }

        // Set the EpisodesDictionary defining all episodes across all files in the file table. Note that it assumes :
        // - files are sorted to give meaningful results, e.g., by date or by RelativePath/date
        // - if the file table is the result of a selection (i.e. as subset of all files), the episode definition is still meaningful
        public static void Reset()
        {
            Episodes.EpisodesDictionary = new Dictionary<int, Tuple<int, int>>();
            return;
        }

        public static void EpisodeGetEpisodesInRange(FileTable fileTable, int fileTableIndex)
        {
            if (Episodes.EpisodesDictionary == null)
            { 
                Episodes.Reset();
            }
            int index = fileTableIndex;

            // Ensure the argument is valid
            if (fileTable == null || index < 0 || index >= fileTable.Count())
            {
                return;
            }

            bool inRange = Episodes.EpisodeGetAroundIndex(fileTable, fileTableIndex, out int first, out int last, out int count);

            // foreach fileindex within the episode, ranging from first to last, add its episode information to the episode dictionary
            for (int i = 1; i <= count; i++)
            {
                int currentFileIndex = first + i - 1;
                if (!Episodes.EpisodesDictionary.ContainsKey(currentFileIndex))
                {
                    Tuple<int, int> tuple = inRange ? new Tuple<int, int>(i, count) : new Tuple<int, int>(int.MaxValue, int.MaxValue);
                    Episodes.EpisodesDictionary.Add(currentFileIndex, tuple);
                }
            }
        }

        // Given an index into the filetable, get the episode (defined by the first and last index) that the indexed file belongs to
        private static bool EpisodeGetAroundIndex(FileTable files, int index,  out int first, out int last, out int count)
        {
            DateTime date1;
            DateTime date2;
            ImageRow file;

            // Default in case there is only one file in this episode
            first = index;
            last = index;
            count = 1;

            // Note that numberOfFiles should never return zero if the provided index is valid
            if (files == null)
            {
                return false;
            }

            file = files[index];
            date1 = file.DateTime;

            int current = index - 1;
            int minSearch = Constant.EpisodeDefaults.MaxRangeToSearch;
            int maxSearch = Constant.EpisodeDefaults.MaxRangeToSearch;
            // Go backwards in the filetable until we find the first file in the episode, or we fail
            // as we have gone back minSearch times
            while (current >= 0 && minSearch != 0)
            {
                file = files[current];
                date2 = file.DateTime;
                TimeSpan difference = date1 - date2;
                bool aboveThreshold = difference.Duration() > Episodes.TimeThreshold;
                if (aboveThreshold)
                {
                    break;
                }
                first = current;
                date1 = date2;
                current--;
                minSearch--;
            }

            // Now go forwards in the filetable until we find the last file in the episode, or we fail
            // as we have gone forwards maxSearch times
            current = index + 1;
            file = files[index];
            date1 = file.DateTime;
            while (current < files.Count() && maxSearch != 0)
            {
                file = files[current];
                date2 = file.DateTime;
                TimeSpan difference = date2 - date1;
                bool aboveThreshold = difference.Duration() > Episodes.TimeThreshold;
                if (aboveThreshold)
                {
                    break;
                }
                date1 = date2;
                last = current;
                current++;
                maxSearch--;
            }
            count = last - first + 1;
            return !(minSearch == 0 || maxSearch == 0);
        }

        #region Depracated - these functions returned all episodes across all files vs. the current version which is on demand
        //// Set the EpisodesDictionary defining all episodes across all files in the file table. Note that it assumes :
        //// - files are sorted to give meaningful results, e.g., by date or by RelativePath/date
        //// - if the file table is the result of a selection (i.e. as subset of all files), the episode definition is still meaningful
        //// static public void EpisodesGetAllEpisodes()
        //// {
        //// Episodes.EpisodesDictionary = new Dictionary<int, Tuple<int, int>>();
        //// int index = 0;
        //// int numberOfFiles = 0;

        ////// Ensure the argument is valid
        //// if (fileTable == null)
        //// {
        ////    return;
        //// }

        //// while (index < fileTable.Count())
        //// {
        ////    int row = index;
        ////    int numberInSequence = 1;
        ////    numberOfFiles = EpisodeGetNumberOfFilesInEpisodeFrom(fileTable, index);
        ////    for (int i = 0; i < numberOfFiles; i++)
        ////    {
        ////        EpisodesDictionary.Add(index + i, new Tuple<int, int>(i + 1, numberOfFiles)); 
        ////        row++;
        ////        numberInSequence++;
        ////    }
        ////    index += numberOfFiles;
        //// }
        //// }

        //// Determine the number of files (including the start index file)
        //// that comprise an episode starting with the start index file
        //// static private int EpisodeGetNumberOfFilesInEpisodeFrom(FileTable fileTable, int index)
        //// {
        ////    DateTime date1;
        ////    DateTime date2;
        ////    ImageRow file;
        ////    int numberOfFiles = 0;

        ////    // Get the first file
        ////    // Note that numberOfFiles should never return zero if the provided index is valid
        ////    if (fileTable == null)
        ////    {
        ////        return numberOfFiles;
        ////    }
        ////    file = fileTable[index];
        ////    date1 = file.DateTime;

        ////    while (index < fileTable.Count())
        ////    {
        ////        file = fileTable[index];
        ////        date2 = file.DateTime;
        ////        TimeSpan difference = date2 - date1;
        ////        bool aboveThreshold = (difference.Duration() > Episodes.TimeDifferenceThreshold);
        ////        if (aboveThreshold)
        ////        {
        ////            break;
        ////        }
        ////        numberOfFiles++;
        ////        date1 = date2;
        ////        index++;
        ////    }
        ////    return numberOfFiles;
        //// }
        #endregion
    }
}
