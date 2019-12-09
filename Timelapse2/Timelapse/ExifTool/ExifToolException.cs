using System;
using System.Runtime.Serialization;

namespace Timelapse.ExifTool
{
    [Serializable]
    public class ExifToolException : Exception
    {
        // CA 1032 recommendation to add following constructor
        public ExifToolException()
        { }

        // CA 1032 recommendation to add following constructor
        public ExifToolException(string message, Exception innerExcepton)
        { }

        public ExifToolException(string msg) : base(msg)
        { }

        // CA 2229 recommendation
        protected ExifToolException(SerializationInfo info, StreamingContext context)
        { }
    }
}
