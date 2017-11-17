using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace Timelapse.Util
{
    internal class NativeMethods
    {
        // Get the cursor position
        // This purportedly corrects a WPF problem... not sure if its really needed.
        public static Point GetCursorPos(Visual relativeTo)
        {
            Win32Point w32Mouse = new Win32Point();
            NativeMethods.GetCursorPos(ref w32Mouse);

            // Check if the presentation source is actually there as otherwise relativeTo will return an error
            // This happens when the relativeTo  is deleted when we are still trying to get the magnifying glass position.
            if (PresentationSource.FromVisual(relativeTo) == null)
            {
                return new Point(0, 0); 
            }
            return relativeTo.PointFromScreen(new Point(w32Mouse.X, w32Mouse.Y));
        }

        public static string GetRelativePath(string fromPath, string toPath)
        {
            int fromAttr = NativeMethods.GetPathAttribute(fromPath);
            int toAttr = NativeMethods.GetPathAttribute(toPath);
            StringBuilder relativePathBuilder = new StringBuilder(260); // MAX_PATH
            if (NativeMethods.PathRelativePathTo(relativePathBuilder,
                                                 fromPath,
                                                 fromAttr,
                                                 toPath,
                                                 toAttr) == 0)
            {
                throw new ArgumentException("Paths must have a common prefix");
            }

            string relativePath = relativePathBuilder.ToString();
            if (relativePath.StartsWith(".\\"))
            {
                relativePath = relativePath.Substring(2);
            }
            return relativePath;
        }

        private static int GetPathAttribute(string path)
        {
            DirectoryInfo di = new DirectoryInfo(path);
            if (di.Exists)
            {
                return NativeMethods.FILE_ATTRIBUTE_DIRECTORY;
            }

            FileInfo fi = new FileInfo(path);
            if (fi.Exists)
            {
                return NativeMethods.FILE_ATTRIBUTE_NORMAL;
            }

            throw new FileNotFoundException(path);
        }

        private const int FILE_ATTRIBUTE_DIRECTORY = 0x10;
        private const int FILE_ATTRIBUTE_NORMAL = 0x80;

        [StructLayout(LayoutKind.Sequential)]
        internal struct Win32Point
        {
            public Int32 X;
            public Int32 Y;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(ref Win32Point pt);

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int PathRelativePathTo(StringBuilder pszPath, string pszFrom, int dwAttrFrom, string pszTo, int dwAttrTo);

        // Conversions between Pixels and device-independent pixels
        // Note that this depends on the DPI settings of the display. 
        // Typical dpi settings are 96dpi (which means the two are equivalent), but this is not always the case.
        [DllImport("gdi32.dll")]
        public static extern int GetDeviceCaps(IntPtr hDc, int nIndex);

        [DllImport("user32.dll")]
        public static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDc);

        public const int LOGPIXELSX = 88;
        public const int LOGPIXELSY = 90;

        // UNUSED - BUT LETS KEEP IT FOR NOW.
        /// <summary>
        /// Transforms device independent units (1/96 of an inch)
        /// to pixels
        /// </summary>
        /// <param name="widthInDeviceIndependentPixels">a device independent unit value X</param>
        /// <param name="heightInDeviceIndependentPixels">a device independent unit value Y</param>
        /// <param name="widthInPixels">returns the X value in pixels</param>
        /// <param name="heightInPixels">returns the Y value in pixels</param>
        //public static void TransformDeviceIndependentPixelsToPixels(double widthInDeviceIndependentPixels,
        //                              double heightInDeviceIndependentPixels,
        //                              out int widthInPixels,
        //                              out int heightInPixels)
        //{
        //    IntPtr hDc = GetDC(IntPtr.Zero);
        //    if (hDc != IntPtr.Zero)
        //    {
        //        int dpiX = GetDeviceCaps(hDc, LOGPIXELSX);
        //        int dpiY = GetDeviceCaps(hDc, LOGPIXELSY);

        //        ReleaseDC(IntPtr.Zero, hDc);

        //        widthInPixels = (int)(((double)dpiX / 96) * widthInDeviceIndependentPixels);
        //        heightInPixels = (int)(((double)dpiY / 96) * heightInDeviceIndependentPixels);
        //    }
        //    else
        //    {
        //        // This failure is unlikely. 
        //        // But just in case... we just return the original pixel size. While this may not be the correct size (depending on the actual dpi), 
        //        // it will not crash the program and at least maintains the correct aspect ration
        //        widthInPixels = Convert.ToInt32(widthInDeviceIndependentPixels);
        //        heightInPixels = Convert.ToInt32(heightInDeviceIndependentPixels);
        //        Utilities.PrintFailure("In TransformPixelsToDeviceIndependentPixels: Failed to get DC.");
        //    }
        //}
        
        // Given size units in normal pixels, translate them into device independent pixels
        public static void TransformPixelsToDeviceIndependentPixels(int widthInPixels,
                                      int heightInPixels,
                                      out double widthInDeviceIndependentPixels,
                                      out double heightInDeviceIndependentPixels)
        {
            IntPtr hDc = GetDC(IntPtr.Zero);
            if (hDc != IntPtr.Zero)
            {
                int dpiX = GetDeviceCaps(hDc, LOGPIXELSX);
                int dpiY = GetDeviceCaps(hDc, LOGPIXELSY);

                ReleaseDC(IntPtr.Zero, hDc);

                widthInDeviceIndependentPixels = (double)(96 * widthInPixels / (double)dpiX);
                heightInDeviceIndependentPixels = (double)(96 * heightInPixels / (double)dpiY);
            }
            else
            {
                // This is very unlikely. 
                // As a workaround, we just return the original pixel size. While this may not be the correct size (depending on the actual dpi), 
                // it will not crash the program and at least maintains the correct aspect ration
                widthInDeviceIndependentPixels = widthInPixels;
                heightInDeviceIndependentPixels = heightInPixels;
                Utilities.PrintFailure("In TransformPixelsToDeviceIndependentPixels: Failed to get DC.");
            }
        }
    }
}
