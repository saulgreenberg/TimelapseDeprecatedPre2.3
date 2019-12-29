namespace Timelapse.Dialog
{
    // A class that stores various properties for each ambiguous date found
    // The start index in the file table

    public class AmbiguousDateRange
    {
        // The start and end indeces in the file table that define a range of ambiguous dates
        public int StartIndex { get; set; }
        public int EndIndex { get; set; }

        // How many images are affected
        public int Count { get; set; }

        // Whether those dates should be swapped
        public bool SwapDates { get; set; }

        public AmbiguousDateRange(int startRange, int endRange, int count, bool swapDates)
        {
            this.StartIndex = startRange;
            this.EndIndex = endRange;
            this.SwapDates = swapDates;
            this.Count = count;
        }
    }
}
