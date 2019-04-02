using Xceed.Wpf.Toolkit;

namespace Timelapse.Util
{
    public static class GlobalReferences
    {
        // Occassionaly, a class will have to access the MainWindow's methods.
        // Rather than contorting to try to pass it as a parameter, we just make it available here.
        public static TimelapseWindow MainWindow { get; set; }

        public static BusyIndicator BusyIndicator { get; set; }
    }
}
