using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using Timelapse.Database;

namespace Timelapse
{
    // AvalonDock callbacks and methods
    public partial class TimelapseWindow : Window, IDisposable
    {
        TimeSpan timeDifferenceThreshold = new TimeSpan(0, 5, 0);
        private int EpisodeGetNumberOfFilesInEpisodeFrom (int index)
        {
            DateTime date1;
            DateTime date2;
            ImageRow file;
            int numberOfFiles = 0;

            using (FileTable files = this.dataHandler.FileDatabase.Files)
            {
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

        List <KeyValuePair<int, Tuple<int,int>>> EpisodeGenerate()
        {
            List<KeyValuePair<int, Tuple<int, int>>> episodes = new List<KeyValuePair<int, Tuple<int, int>>>();

            int index = 0;
            int numberOfFiles = 0;

            using (FileTable files = this.dataHandler.FileDatabase.Files)
            {
                while (index < files.Count())
                {
                    int row = index;
                    int numberInSequence = 1;
                    numberOfFiles = EpisodeGetNumberOfFilesInEpisodeFrom(index);
                    for (int i = 0; i < numberOfFiles; i++)
                    {
                        episodes.Add(new KeyValuePair
                            <int, Tuple<int, int>>(index, new Tuple<int,int>(i+1, numberOfFiles))); //    Tuple.Create(index, i + 1, numberOfFiles));
                        System.Diagnostics.Debug.Print(String.Format("Tuple: {0} {1} {2} Index {3}", row, numberInSequence, numberOfFiles, index));
                        row++;
                        numberInSequence++;
                    }
                    index += numberOfFiles;
                }
                return episodes; // this should never be reached
            }
        }
    }
}
