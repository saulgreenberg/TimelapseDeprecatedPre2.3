using System;

namespace Timelapse.Util
{
    // Captures a single metadata record entry as extracted by MetaDataExtractor. 
    public class ImageMetadata
    {
        public string Directory { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }
        public string Key
        {
            get
            {
                return this.Directory + "." + this.Name;
            }
        }

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
