using MetadataExtractor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Timelapse.Util
{
    public class MetadataDictionary
    {
        // Returns a dictionary listing the metadata found in the given file
        // If the file cannot be read for metadata, it returns null
        // Keys are in the form  Directory.Name, e.g., "Reconyx Maker Notes.Ambient Temperature"
        // Values are instances of the class Metadata, i.e., Key, Directory, Name, Value
        public static Dictionary<string, Metadata> LoadMetadata(string filePath)
        {
            Dictionary<string, Metadata> metadataDictionary = new Dictionary<string, Metadata>();
            try
            {
                foreach (Directory metadataDirectory in ImageMetadataReader.ReadMetadata(filePath))
                {
                    foreach (Tag metadataTag in metadataDirectory.Tags)
                    {
                        Metadata metadata = new Metadata(metadataTag.DirectoryName, metadataTag.Name, metadataTag.Description);
                        // Check if the metadata name is already in the dictionary.
                        // If so, just skip it as its not clear what else to do with it
                        if (!metadataDictionary.ContainsKey(metadata.Key))
                        {
                            metadataDictionary.Add(metadata.Key, metadata);
                        }
                        else
                        {
                            Utilities.PrintFailure(String.Format("Metadata Dictionary: Duplicate metadata key: {0}", metadata.Key));
                        }
                    }
                }
            }
            catch
            {
                // Likely a corrupt file, Just return the empty dictionary
                metadataDictionary.Clear();
            }
            return metadataDictionary;
        }
    }
}
