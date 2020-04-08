using System;

namespace Timelapse.Util
{
    /// <summary>
    /// Captures a single metadata record entry as extracted by MetaDataExtractor. 
    /// Each record is Directory.Name Value, where Directory.Name is the Key
    /// </summary>
    public class ImageMetadata
    {
        /// <summary>
        /// Directory in the record Directory.Name Value
        /// </summary>
        public string Directory { get; set; }

        /// <summary>
        /// Key in the record Directory.Name Value, where Key is Directory.Name
        /// </summary>
        public string Key
        {
            get
            {
                return this.Directory + "." + this.Name;
            }
        }

        /// <summary>
        /// Name in the record Directory.Name Value
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The Metadata Value in the record Directory.Name Value
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// Constructor: An empty metadata record in the form Directory.Name Value
        /// </summary>
        public ImageMetadata()
        {
            this.Initialize(String.Empty, String.Empty, String.Empty);
        }

        /// <summary>
        /// Constructor: A  metadata record in the form Directory.Name Value
        /// </summary>
        public ImageMetadata(string directory, string name, string value)
        {
            this.Initialize(directory, name, value);
        }

        /// <summary>
        /// Set a metadata record to its values in the form Directory.Name Value
        /// </summary>
        private void Initialize(string directory, string name, string value)
        {
            this.Directory = directory;
            this.Name = name;
            this.Value = value;
        }
    }
}
