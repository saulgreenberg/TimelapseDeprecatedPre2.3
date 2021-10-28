﻿using System.Collections.Generic;
using Timelapse.Enums;

namespace Timelapse
{
    // State data.
    // This data structure is filled in only when the user has specified metadata / data field pairs
    // that should be populated when loading images for the first time. 
    // Population on load is triggered via the Preferences dialog, which sets the state boolean 'MetadataAskOnLoad' (saved in the Registry)
    // If that flag is true, Timelapse will raise the dialog PopulateFieldsWithMetadataOnLoad
    // whenever a new image set is being created or added to (via the File menu)
    public class MetadataOnLoad
    {
        // Whether the MetadataExtractor or ExifTool was used to get the metadata fields
        public MetadataToolEnum MetadataToolSelected { get; set; }

        // Contains the metadata / data field pairs selected by the user that should be populated
        // Note:
        // Key is the metadata name, which could include Directory information depending on which metadata tool was selected.
        //     If we only want the tag, then we have to parse that out of the key ie. by checking and getting everything after the last '.'
        // Value is the data field that should be populated
        public List<KeyValuePair<string, string>> SelectedMetadataDataLabels { get; set; }

        // Returns the metadata tags as an array.
        public string[] Tags
        {
            get 
            {
                List<string> tagList = new List<string>();
                foreach (KeyValuePair<string, string> kvp in this.SelectedMetadataDataLabels)
                {
                    tagList.Add(kvp.Key);
                }
                return tagList.ToArray();
            }
        } 
    }
}