using System;

namespace Timelapse.ExifTool
{
    [Serializable]
    public class ExifToolException : Exception
    {
        public ExifToolException(string msg) : base(msg)
        { }
    }
}
