﻿using System;
using System.Diagnostics;
namespace Timelapse_ViewOnly
{
    internal class Program
    {
        // This program invokes Timelapse with a -viewonly argument,
        // where Timelapse starts in a mode where the user can view but not edit any data.
        static void Main(string[] _)
        {
            ProcessStartInfo processStartInfo = new()
            {
                UseShellExecute = true,
                RedirectStandardOutput = false,
                Arguments = "-viewonly",
                FileName = "Timelapse2.exe",
            };
            TryProcessStart(processStartInfo);
        }

        /// <param name="processStartInfo">should contain the necessary information to configure the process</param>
        /// <returns>true/false if the process started or not</returns>
        public static void TryProcessStart(ProcessStartInfo processStartInfo)
        {
            if (processStartInfo == null)
            {
                return;
            }
            using Process process = new();
            process.StartInfo = processStartInfo;
            try
            {
                process.Start();
            }
            catch (Exception exception)
            {
                if (exception != null)
                {
                    // Error. A noop so we catch it cleanly but still leave the dialog running
                    System.Diagnostics.Debug.Print("TryProcessStart: Can't start " + processStartInfo.FileName);
                    return;
                }
            }
        }
    }
}
