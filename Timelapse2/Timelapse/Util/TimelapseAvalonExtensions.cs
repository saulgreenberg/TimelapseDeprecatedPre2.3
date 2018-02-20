using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Forms;
using System.Xml;
using Xceed.Wpf.AvalonDock.Layout;
using Xceed.Wpf.AvalonDock.Layout.Serialization;

namespace Timelapse.Util
{
    // These extensions save and restore the Avalon layouts (including main window and floating windows positions and sizes).
    // Default layouts are stored as resources so they are always accessible, while user-created layouts (including the last saved layout) is stored in 
    // the user's computer registry. If there is no last-saved layout found in the registry, a standard data entry on top default layout is used. 
    public static class TimelapseAvalonExtensions
    {
        #region Loading layouts
        // Try to load the layout identified by the layoutKey, which, depending on the key, is stored in the resource file or the registry
        // This includes various adjustments, as detailed in the comments below.
        public static bool AvalonLayout_TryLoad(this TimelapseWindow timelapse, string layoutKey)
        {
            bool isResourceFile = false;
            string layoutName = String.Empty;
            
            // Layouts are loaded from either the registry or from a resource file
            // If from the registry, then the registry lookup key is the the layoutKey
            // If from the resource file, then we have to use the path of the resource file
            switch (layoutKey)
            {
                case Constant.AvalonLayoutTags.DataEntryOnTop:
                    layoutName = Constant.AvalonLayoutResourcePaths.DataEntryOnTop;
                    isResourceFile = true;
                    break;
                case Constant.AvalonLayoutTags.DataEntryOnSide:
                    layoutName = Constant.AvalonLayoutResourcePaths.DataEntryOnSide;
                    isResourceFile = true;
                    break;
                case Constant.AvalonLayoutTags.DataEntryFloating:
                    layoutName = Constant.AvalonLayoutResourcePaths.DataEntryFloating;
                    isResourceFile = true;
                    break;
                default:
                    layoutName = layoutKey;
                    break;
            }

            bool result;
            if (isResourceFile)
            {
                // Load thelayout from the resource file
                result = timelapse.AvalonLayout_TryLoadFromResource(layoutName);
            }
            else
            {
                // Load both the layout and the window position/size from the registry 
                result = timelapse.AvalonLayout_TryLoadFromRegistry(layoutName);
                if (result)
                {
                    timelapse.AvalonLayout_LoadWindowPositionAndSizeFromRegistry(layoutName + Constant.AvalonDock.WindowRegistryKeySuffix);
                    timelapse.AvalonLayout_LoadWindowMaximizeStateFromRegistry(layoutName + Constant.AvalonDock.WindowMaximizeStateRegistryKeySuffix);
                }
            }
            if (result == false)
            {
                // We are trying to load the last-used layout, but there isn't one. As a fallback, 
                // we use the default configuration as specified in the XAML: - all tiled with the data entry on top. 
                // Eve so, we check to see if the window position and size were saved; if they aren't there, it defaults to a reasonable size and position.
                timelapse.AvalonLayout_LoadWindowPositionAndSizeFromRegistry(layoutName + Constant.AvalonDock.WindowRegistryKeySuffix);
                timelapse.AvalonLayout_LoadWindowMaximizeStateFromRegistry(layoutName + Constant.AvalonDock.WindowMaximizeStateRegistryKeySuffix);
                return result;
            }

            // After deserializing, a completely new LayoutRoot boject is created.
            // This means we have to reset various things so the documents in the new object behave correctly.
            // This includes resetting the callbacks to the DataGrid.IsActiveChanged
            timelapse.DataEntryControlPanel.PropertyChanging -= timelapse.LayoutAnchorable_PropertyChanging;
            timelapse.DataGridPane.IsActiveChanged -= timelapse.DataGridPane_IsActiveChanged;
            timelapse.AvalonDock_ResetAfterDeserialize();
            timelapse.DataGridPane.IsActiveChanged += timelapse.DataGridPane_IsActiveChanged;
            timelapse.DataEntryControlPanel.PropertyChanging += timelapse.LayoutAnchorable_PropertyChanging;

            // Force an update to the DataGridPane if its visible, as the above doesn't trigger it
            if (timelapse.DataGridPane.IsVisible)
            {
                timelapse.DataGridPane_IsActiveChanged(true);
            }

            // Special case for DataEntryFloating:
            // Reposition the floating window in the middle of the main window, but just below the top
            // Note that this assumes there is just a single floating window (which should be the case for this configuration)
            if (layoutKey == Constant.AvalonLayoutTags.DataEntryFloating)
            {
                if (timelapse.DockingManager.FloatingWindows.Count() > 0)
                {
                    foreach (var floatingWindow in timelapse.DockingManager.FloatingWindows)
                    {
                        // We set the DataEntry Control Panel top / left as it remembers the values (i.e. so the layout will be saved correctly later)
                        // If we set the floating window top/left directly, it won't remember those values as its just the view.
                        timelapse.DataEntryControlPanel.FloatingTop = timelapse.Top + 100; 
                        timelapse.DataEntryControlPanel.FloatingLeft = timelapse.Left + ((timelapse.Width - floatingWindow.Width) / 2.0);
                        // floatingWindow.Left = 500;//  timelapse.Left + ((timelapse.Width - floatingWindow.Width) / 2.0);
                    }
                    // This cause the above values to 'stick'
                    timelapse.DataEntryControlPanel.Float();
                }
            }
            return true;
        }

        // Try to load a layout from the registry given the registry key
        public static bool AvalonLayout_TryLoadFromRegistry(this TimelapseWindow timelapse, string registryKey)
        {
            // Retrieve the layout configuration from the registry
            string layoutAsString = timelapse.state.ReadFromRegistryString(registryKey);
            if (string.IsNullOrEmpty(layoutAsString))
            {
                return false;
            }

            // Convert the string to a stream 
            MemoryStream layoutAsStream = new MemoryStream();
            StreamWriter writer = new StreamWriter(layoutAsStream);
            writer.Write(layoutAsString);
            writer.Flush();
            layoutAsStream.Position = 0;

            // Deserializa and load the layout
            XmlLayoutSerializer serializer = new XmlLayoutSerializer(timelapse.DockingManager);
            using (StreamReader streamReader = new StreamReader(layoutAsStream))
            { 
                serializer.Deserialize(streamReader);
            }
            return true;
        }

        // Load the window position and size from the registry
        private static void AvalonLayout_LoadWindowPositionAndSizeFromRegistry(this TimelapseWindow timelapse, string registryKey)
        {
            // Retrieve the window position and size
            Rect windowRect = timelapse.state.ReadTimelapseWindowPositionAndSizeFromRegistryRect(registryKey);
            // Height and Width should not be negative. There was an instance where it was, so this tries to catch it just in case
            //windowRect.Height = Math.Abs(windowRect.Height);
            //
            windowRect.Width = Math.Abs(windowRect.Width);

            // Adjust the window position and size, if needed, to fit into the current screen dimensions
            // System.Diagnostics.Debug.Print("Oldwin: " + windowRect.ToString());
            windowRect = timelapse.FitIntoScreen(windowRect);
            // System.Diagnostics.Debug.Print("Newwin: " + windowRect.ToString());
            timelapse.Left = windowRect.Left;
            timelapse.Top = windowRect.Top;
            timelapse.Width = windowRect.Width;
            timelapse.Height = windowRect.Height;

            foreach (var floatingWindow in timelapse.DockingManager.FloatingWindows)
            {
                windowRect = new Rect(floatingWindow.Left, floatingWindow.Top, floatingWindow.Width, floatingWindow.Height);
                // System.Diagnostics.Debug.Print("Oldfloat: " + windowRect.ToString());
                windowRect = timelapse.FitIntoScreen(windowRect);
                // System.Diagnostics.Debug.Print("Newfloat: " + windowRect.ToString());
                floatingWindow.Left = windowRect.Left;
                floatingWindow.Top = windowRect.Top;
                floatingWindow.Width = windowRect.Width;
                floatingWindow.Height = windowRect.Height;
            }
        }

        public static Rect FitIntoScreen(this TimelapseWindow timelapse, Rect windowRect)
        {
            System.Diagnostics.Debug.Print("windowRect: " + windowRect.ToString());
            // Height and Width should not be negative. There was an instance where it was, so this tries to catch it just in case
            if (windowRect.Height <= 0 || windowRect.Width < -0)
            {
                System.Diagnostics.Debug.Print("Height width is " + windowRect.Height + " " + windowRect.Width);
            }
            try
            {
                // Retrieve the bounds of the multi-screen coordinates, as top left and bottom right corners
                // We allow some space for the task bar, assuming its visible at the screen's bottom
                // and place the window at the very top. Note that this won't cater for the situation when
                // the task bar is at the top of the screen, but so it goes.
                System.Windows.Point screen_corner1 = new System.Windows.Point(0, 0);
                System.Windows.Point screen_corner2 = new System.Windows.Point(0, 0);
                int typicalTaskBarHeight = 40;
                foreach (Screen screen in Screen.AllScreens)
                {
                    screen_corner1.X = Math.Min(screen_corner1.X, screen.Bounds.Left);
                    screen_corner1.Y = Math.Min(screen_corner1.Y, screen.Bounds.Top);
                    screen_corner2.X = Math.Max(screen_corner2.X, screen.Bounds.Left + screen.Bounds.Width);
                    screen_corner2.Y = Math.Max(screen_corner2.Y, screen.Bounds.Top + screen.Bounds.Height - typicalTaskBarHeight);
                }
                // Convert the screen coordinates to wpf coordinates
                PresentationSource source = PresentationSource.FromVisual(timelapse);
                screen_corner1 = source.CompositionTarget.TransformFromDevice.Transform(screen_corner1);
                screen_corner2 = source.CompositionTarget.TransformFromDevice.Transform(screen_corner2);
                double screen_width = Math.Abs(screen_corner2.X - screen_corner1.X);
                double screen_height = Math.Abs(screen_corner2.Y - screen_corner1.Y);

                // Ensure that we have valid coordinates
                double wleft = Double.IsNaN(windowRect.Left) ? 0 : windowRect.Left;
                double wtop = Double.IsNaN(windowRect.Top) ? 0 : windowRect.Top;
                double wheight = Double.IsNaN(windowRect.Height) ? 740 : windowRect.Height;
                double wwidth = Double.IsNaN(windowRect.Height) ? 740 : windowRect.Width;
                // System.Diagnostics.Debug.Print("OldWindow: " + wleft + "," + wtop + "," + wwidth + "," + wheight);

                // If the window's height is larger than the screen's available height, 
                // reposition it to the screen's top and and adjust its height to fill the available height 
                if (wheight > screen_height)
                {
                    wheight = screen_height;
                    wtop = screen_corner1.Y;
                }
                // If the window's width is larger than the screen's available width, 
                // reposition it to the left and and adjust its width to fill the available width 
                if (wwidth > screen_width)
                {
                    wwidth = screen_width;
                    wleft = screen_corner1.X;
                }
                double wbottom = wtop + wheight;
                double wright = wleft + wwidth;

                // move window up if it extends below the working area
                if (wbottom > screen_corner2.Y)
                {
                    double pixelsToMoveUp = wbottom - screen_corner2.Y;
                    if (pixelsToMoveUp > wtop)
                    {
                        // window is too tall and has to shorten to fit screen
                        wtop = 0;
                        wheight = screen_height;
                    }
                    else if (pixelsToMoveUp > 0)
                    {
                        // move window up
                        wtop -= pixelsToMoveUp;
                    }
                }

                // move window down if it extends above the working area
                if (wtop < screen_corner1.Y)
                {
                    double pixelsToMoveDown = Math.Abs(screen_corner1.Y - wtop);
                    // move window down
                    wtop += pixelsToMoveDown;
                    if (wtop + wheight > screen_corner2.Y - wtop)
                    {
                        wheight = screen_corner2.Y - wtop;
                    }
                }

                // move window left if it extends right of the working area
                if (wright > screen_corner2.X)
                {
                    double pixelsToMoveLeft = wright - screen_corner2.X;
                    if (pixelsToMoveLeft > wleft)
                    {
                        // window is too wide and has to narrow to fit screen
                        wleft = screen_corner1.X;
                        wwidth = screen_width;
                    }
                    else if (pixelsToMoveLeft > 0)
                    {
                        // move window left
                        wleft -= pixelsToMoveLeft;
                    }
                }

                // move window right if it extends left of the working area
                if (wleft < screen_corner1.X)
                {
                    double pixelsToMoveRight = screen_corner1.X - wleft;
                    if (pixelsToMoveRight > 0)
                    {
                        // move window left
                        wleft += pixelsToMoveRight;
                    }
                    if (wleft + wwidth > screen_corner2.Y)
                    {
                        // window is too wide and has to narrow to fit screen
                        wwidth = screen_corner2.Y - wright;
                    }
                }
                // System.Diagnostics.Debug.Print("NewWindow: " + wleft + "," + wtop + "," + wwidth + "," + wheight);
                // System.Diagnostics.Debug.Print("Screen: " + screen_corner1 + "," + screen_corner2);
                return new Rect(wleft, wtop, wwidth, wheight);
            }
            catch
            {
                System.Diagnostics.Debug.Print("Catch: Problem in TimelapseAvalonExtensions - FitIntoScreen");
                return new Rect(5, 5, 740, 740);     
            }
        }

        // Retrieve the maximize state from the registry and set the timelapse window to that state
        private static void AvalonLayout_LoadWindowMaximizeStateFromRegistry(this TimelapseWindow timelapse, string registryKey)
        {
            bool windowMaximizeState = timelapse.state.ReadTimelapseWindowMaximizeStateFromRegistryBool(registryKey);
            timelapse.WindowState = windowMaximizeState ? WindowState.Maximized : WindowState.Normal;
        }

        // Try to load a layout from the given resourceFilePath
        private static bool AvalonLayout_TryLoadFromResource(this TimelapseWindow timelapse, string resourceFilePath)
        {
            XmlLayoutSerializer serializer = new XmlLayoutSerializer(timelapse.DockingManager);
            Uri uri = new Uri(resourceFilePath);
            try
            {
                using (Stream stream = System.Windows.Application.GetResourceStream(uri).Stream)
                { 
                    serializer.Deserialize(stream);
                }
            }
            catch
            {
                // Should only happen if there is something wrong with the uri address, e.g., if the resource doesn't exist
                return false;
            }
            return true;
        }

        // As Deserialization rebuilds the Docking Manager, we need to reset the original layoutAnchorable and layoutDocument pointers to the rebuilt ones
        // Note if we define new LayoutAnchorables and LayoutDocuments in the future, we will have tomodify this method accordingly
        private static void AvalonDock_ResetAfterDeserialize(this TimelapseWindow timelapse)
        {
            IEnumerable<LayoutAnchorable> layoutAnchorables = timelapse.DockingManager.Layout.Descendents().OfType<LayoutAnchorable>();
            foreach (LayoutAnchorable layoutAnchorable in layoutAnchorables)
            {
                if (layoutAnchorable.ContentId == timelapse.DataEntryControlPanel.ContentId)
                {
                    timelapse.DataEntryControlPanel = layoutAnchorable;
                }
            }

            IEnumerable<LayoutDocument> layoutDocuments = timelapse.DockingManager.Layout.Descendents().OfType<LayoutDocument>();
            foreach (LayoutDocument layoutDocument in layoutDocuments)
            {
                if (layoutDocument.ContentId == timelapse.InstructionPane.ContentId)
                {
                    timelapse.InstructionPane = layoutDocument;
                }
                else if (layoutDocument.ContentId == timelapse.ImageSetPane.ContentId)
                {
                    timelapse.ImageSetPane = layoutDocument;
                }
                else if (layoutDocument.ContentId == timelapse.DataGridPane.ContentId)
                {
                    timelapse.DataGridPane = layoutDocument;
                }
            }
        }
        #endregion

        #region Saving layouts
        // Save the current Avalon layout to the registry under the given registry key
        // and the current timelapse window position and size under the given registry key with the added suffix
        public static bool AvalonLayout_TrySave(this TimelapseWindow timelapse, string registryKey)
        {
            // Serialization normally creates a stream, so we have to do a few contortions to transform that stream into a string  
            StringBuilder xmlText = new StringBuilder();
            XmlWriter xmlWriter = XmlWriter.Create(xmlText);

            // Serialize the layout into a string
            XmlLayoutSerializer serializer = new XmlLayoutSerializer(timelapse.DockingManager);
            using (StringWriter stream = new StringWriter())
            { 
                serializer.Serialize(xmlWriter);
            }
            if (xmlText.ToString().Trim() != string.Empty)
            {
                // Write the string to the registry under the given key name
                timelapse.state.WriteToRegistry(registryKey, xmlText.ToString());
                AvalonLayout_TrySaveWindowPositionAndSizeAndMaximizeState(timelapse, registryKey);
                return true;
            }
            else
            {
                return false;
            }
        }

        public static void AvalonLayout_TrySaveWindowPositionAndSizeAndMaximizeState(this TimelapseWindow timelapse, string registryKey)
        {
            timelapse.AvalonLayout_SaveWindowPositionAndSizeToRegistry(registryKey + Constant.AvalonDock.WindowRegistryKeySuffix);
            timelapse.AvalonLayout_SaveWindowMaximizeStateToRegistry(registryKey + Constant.AvalonDock.WindowMaximizeStateRegistryKeySuffix);
        }

        // Save the current timelapse window position and size to the registry
        private static void AvalonLayout_SaveWindowPositionAndSizeToRegistry(this TimelapseWindow timelapse, string registryKey)
        {
            Rect windowPositionAndSize = new Rect(timelapse.Left, timelapse.Top, timelapse.Width, timelapse.Height);
            timelapse.state.WriteToRegistry(registryKey, windowPositionAndSize);
        }

        // Save the current timelapse window maximize state to the registry 
        private static void AvalonLayout_SaveWindowMaximizeStateToRegistry(this TimelapseWindow timelapse, string registryKey)
        {
            bool windowStateIsMaximized = timelapse.WindowState == WindowState.Maximized;
            timelapse.state.WriteToRegistry(registryKey, windowStateIsMaximized);
        }
        #endregion
    }
}
