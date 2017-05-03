using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Timelapse.Util
{
    public class Metadata
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

        public Metadata()
        {
            this.Initialize(String.Empty, String.Empty, String.Empty);
        }
        public Metadata(string directory, string name, string value)
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
