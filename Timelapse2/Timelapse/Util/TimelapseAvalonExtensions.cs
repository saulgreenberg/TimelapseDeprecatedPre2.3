using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Xml;
using Xceed.Wpf.AvalonDock.Layout;
using Xceed.Wpf.AvalonDock.Layout.Serialization;

namespace Timelapse.Util
{
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
                }
            }
            if (result == false)
            {
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
                        floatingWindow.Left = timelapse.Left + ((timelapse.Width - floatingWindow.Width) / 2.0);
                        floatingWindow.Top = timelapse.Top + 100;
                    }
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
            Rect windowPositionAndSize = timelapse.state.ReadFromRegistryRect(registryKey);
            timelapse.Top = windowPositionAndSize.Top;
            timelapse.Left = windowPositionAndSize.Left;
            timelapse.Width = windowPositionAndSize.Width;
            timelapse.Height = windowPositionAndSize.Height;
        }

        // Try to load a layout from the given resourceFilePath
        private static bool AvalonLayout_TryLoadFromResource(this TimelapseWindow timelapse, string resourceFilePath)
        {
            XmlLayoutSerializer serializer = new XmlLayoutSerializer(timelapse.DockingManager);
            Uri uri = new Uri(resourceFilePath);
            try
            {
                using (Stream stream = Application.GetResourceStream(uri).Stream)
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
                timelapse.AvalonLayout_SaveWindowPositionAndSizeToRegistry(registryKey + Constant.AvalonDock.WindowRegistryKeySuffix);
                return true;
            }
            else
            {
                return false;
            }
        }

        private static void AvalonLayout_SaveWindowPositionAndSizeToRegistry(this TimelapseWindow timelapse, string registryKey)
        {
            Rect windowPositionAndSize = new Rect(timelapse.Left, timelapse.Top, timelapse.Width, timelapse.Height);
            timelapse.state.WriteToRegistry(registryKey, windowPositionAndSize);
        }
        #endregion
    }
}
