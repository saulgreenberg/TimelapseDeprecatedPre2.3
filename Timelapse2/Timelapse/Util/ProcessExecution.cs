using System;
using System.Diagnostics;
using System.IO;

namespace Timelapse.Util
{
    /// <summary>
    /// Try to start a default external process on the given file.
    /// Each call type returns true / false if the Process successfully started
    /// </summary>
    public static class ProcessExecution
    {
        /// <param name="processStartInfo">should contain the necessary information to configure the process</param>
        /// <returns>true/false if the process started or not</returns>
        public static bool TryProcessStart(ProcessStartInfo processStartInfo)
        {
            if (processStartInfo == null)
            {
                return false;
            }
            using (Process process = new Process())
            {
                process.StartInfo = processStartInfo;
                try
                {
                    process.Start();
                    // While the DEPRACATED code below seems like it would be more robust, process.Start() is not reliable in terms of 
                    // how it returns true or false. For example, if that process is already running (such as a single instance video player), it would return false
                    //if (process.Start())
                    //{
                    //    // success
                    //    return true;
                    //}
                    //else
                    //{
                    //    return false;
                    //}
                }
                catch (Exception exception)
                {
                    if (exception != null)
                    {
                        // Error. A noop so we catch it cleanly but still leave the dialog running
                        System.Media.SystemSounds.Beep.Play();
                        return false;
                    }
                }
                return true;
            }
        }

        /// <param name="uri">should contain a valid Uri</param>
        /// <returns>true/false if the process started or not</returns>
        public static bool TryProcessStart(Uri uri)
        {
            if (uri == null)
            {
                return false;
            }
            ProcessStartInfo processStartInfo = new ProcessStartInfo(uri.AbsoluteUri);
            return ProcessExecution.TryProcessStart(processStartInfo);
        }

        /// <param name="filepath">should contain a valid file path</param>
        /// <returns>true/false if the process started or not</returns>
        public static bool TryProcessStart(string filePath)
        {
            if (File.Exists(filePath) == false)
            {
                // Don't even try to start the process if the file doesn't exist.
                return false;
            }
            ProcessStartInfo processStartInfo = new ProcessStartInfo
            {
                FileName = filePath
            };
            return ProcessExecution.TryProcessStart(processStartInfo);
        }
    }
}
