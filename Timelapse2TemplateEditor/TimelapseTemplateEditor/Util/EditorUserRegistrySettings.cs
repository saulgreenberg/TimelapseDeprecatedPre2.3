﻿using Microsoft.Win32;
using System;
using Timelapse.Util;

namespace Timelapse.Editor.Util
{
    internal class EditorUserRegistrySettings : UserRegistrySettings
    {
        // same key as Timelapse uses; intentional as both Timelapse and template editor are released together
        public DateTime MostRecentCheckForUpdates { get; set; }

        public RecencyOrderedList<string> MostRecentTemplates { get; private set; }

        public bool ShowUtcOffset { get; set; }

        public EditorUserRegistrySettings()
            : this(Constant.WindowRegistryKeys.RootKey)
        {
        }

        internal EditorUserRegistrySettings(string keyPath)
            : base(keyPath)
        {
            this.ReadFromRegistry();
        }

        public void ReadFromRegistry()
        {
            using (RegistryKey registryKey = this.OpenRegistryKey())
            {
                this.MostRecentCheckForUpdates = registryKey.GetDateTime(Constant.WindowRegistryKeys.MostRecentCheckForUpdates, DateTime.Now);
                this.MostRecentTemplates = registryKey.GetRecencyOrderedList(EditorConstant.Registry.EditorKey.MostRecentlyUsedTemplates);
                // We no longer want to show the UtcOffset, so even if it was set in the past, make sure its always false.
                this.ShowUtcOffset = false; // registryKey.GetBoolean(EditorConstant.Registry.EditorKey.ShowUtcOffset, false);
            }
        }

        public void WriteToRegistry()
        {
            using (RegistryKey registryKey = this.OpenRegistryKey())
            {
                registryKey.Write(Constant.WindowRegistryKeys.MostRecentCheckForUpdates, this.MostRecentCheckForUpdates);
                registryKey.Write(EditorConstant.Registry.EditorKey.MostRecentlyUsedTemplates, this.MostRecentTemplates);
                // We no longer want to show the UtcOffset, so there is no longer a need to write it to the registry. 
                // registryKey.Write(EditorConstant.Registry.EditorKey.ShowUtcOffset, this.ShowUtcOffset);
            }
        }
    }
}
