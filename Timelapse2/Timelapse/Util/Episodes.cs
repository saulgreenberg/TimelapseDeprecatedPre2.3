using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using Timelapse.Database;

namespace Timelapse
{
    // Episodes
    partial class Episodes
    {
        // Get a list defining all episodes across all files in the file table. Note that it assumes :
        // - files are sorted to give meaningful results, e.g., by date or by RelativePath/date
        // - if the file table is the result of a selection (i.e. as subset of all files), the episode definition is still meaningful

        // An example list beginning with an episode of 2 files then of 1 file would return, e.g., 
        // - 0,(1,2) (0th file, 1 out of 2 images in the episode) 
        // - 1,(2,2) (1st file, 2 out of 2 images in the episode) 
        // - 2,(1,1) (2nd file, 1 out of 1 images in the episode) etc
        static public List<KeyValuePair<int, Tuple<int, int>>> GetEpisodesFromFileTable(FileTable fileTable)
        {
            List<KeyValuePair<int, Tuple<int, int>>> episodes = new List<KeyValuePair<int, Tuple<int, int>>>();

            int index = 0;
            int numberOfFiles = 0;

            // Ensure the argument is valid
            if (fileTable == null)
            {
                return episodes;
            }

            while (index < fileTable.Count())
            {
                int row = index;
                int numberInSequence = 1;
                numberOfFiles = EpisodeGetNumberOfFilesInEpisodeFrom(fileTable, index);
                for (int i = 0; i < numberOfFiles; i++)
                {
                    episodes.Add(new KeyValuePair<int, Tuple<int, int>>(index, new Tuple<int, int>(i + 1, numberOfFiles))); 
                    row++;
                    numberInSequence++;
                }
                index += numberOfFiles;
            }
            return episodes;
        }

        // Determine the number of files (including the start index file)
        // that comprise an episode starting with the start index file
        static private int EpisodeGetNumberOfFilesInEpisodeFrom(FileTable files, int index)
        {
            TimeSpan timeDifferenceThreshold = Constant.EpisodeDefaults.TimeDifferenceThreshold;

            DateTime date1;
            DateTime date2;
            ImageRow file;
            int numberOfFiles = 0;

            // Get the first file
            // Note that numberOfFiles should never return zero if the provided index is valid
            if (files == null)
            {
                return numberOfFiles;
            }
            file = files[index];
            date1 = file.DateTime;

            while (index < files.Count())
            {
                file = files[index];
                date2 = file.DateTime;
                TimeSpan difference = date2 - date1;
                bool aboveThreshold = (difference > timeDifferenceThreshold);
                if (aboveThreshold)
                {
                    break;
                }
                numberOfFiles++;
                date1 = date2;
                index++;
            }
            return numberOfFiles;
        }
    }
}
