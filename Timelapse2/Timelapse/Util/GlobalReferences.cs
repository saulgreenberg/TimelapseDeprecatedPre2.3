
using Timelapse.Controls;

namespace Timelapse.Util
{
    public static class GlobalReferences
    {
        // Occassionaly, a class will have to access the MainWindow's methods.
        // Rather than contorting to try to pass it as a parameter, we just make it available here.
        public static TimelapseWindow MainWindow { get; set; }

        public static BusyCancelIndicator BusyCancelIndicator { get; set; }

        public static TimelapseState TimelapseState { get; set; }

        public static bool DetectionsExists { get; set; }
    }
}
