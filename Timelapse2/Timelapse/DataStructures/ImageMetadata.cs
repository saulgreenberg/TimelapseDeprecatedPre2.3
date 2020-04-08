using System;

namespace Timelapse.Util
{
    /// <summary>
    /// Captures a single metadata record entry as extracted by MetaDataExtractor. 
    /// </summary>
    public class ImageMetadata
    {
        public string Directory { get; set; }

        public string Key
        {
            get
            {
                return this.Directory + "." + this.Name;
            }
        }

        public string Name { get; set; }

        public string Value { get; set; }

        /// <summary>
        /// ImageMetadata: A data structure holding a single metadata record entry as extracted by MetaDataExtractor. 
        /// </summary>
        public ImageMetadata()
        {
            this.Initialize(String.Empty, String.Empty, String.Empty);
        }
        public ImageMetadata(string directory, string name, string value)
        {
            this.Initialize(directory, name, value);
        }
        public void Initialize(string directory, string name, string value)
        {
            this.Directory = directory;
            this.Name = name;
            this.Value = value;
        }
    }
}
