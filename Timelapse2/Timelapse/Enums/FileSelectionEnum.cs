namespace Timelapse.Enums
{
    public enum FileSelectionEnum : int
    {
        // file selections also used as image qualities
        Unknown = 0,
        Light = 1,
        Dark = 2,

        // file selections only
        Missing = 3,
        All = 4,
        MarkedForDeletion = 5,
        Custom = 6
    }
}
