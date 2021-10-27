using System.Collections.Generic;
using Timelapse.Enums;
using Timelapse.ExifTool;

namespace Timelapse
{
    // Holds state data on whether the user has specified metadata / data field pairs
    // that should be populated when loading images for the first time.
    public class MetadataOnLoad
    {
        // Whether the MetadataExtractor or ExifTool was used to get the metadata fields
        public MetadataToolEnum MetadataToolSelected { get; set; }

        // The  metadata / data field pairs that should be populated
        public List<KeyValuePair<string, string>> SelectedMetadata { get; set; }

        public ExifToolWrapper ExifTool { get; set; }
    }
}