using System;
namespace Timelapse.Util
{
    public static class ThrowIf
    {
        public static void IsNullArgument<T>(T value, string name) where T : class
        {
            if (value == null)
            {
                throw new ArgumentNullException(name);
            }
        }
    }
}
