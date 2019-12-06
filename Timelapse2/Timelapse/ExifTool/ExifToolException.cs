using System;
using System.Runtime.Serialization;

namespace Timelapse.ExifTool
{
    [Serializable]
    public class ExifToolException : Exception
    {
        public ExifToolException(string msg) : base(msg)
        { }

        // CA 2229 recommendation
        protected ExifToolException(SerializationInfo info, StreamingContext context)
        { }
    }
}
