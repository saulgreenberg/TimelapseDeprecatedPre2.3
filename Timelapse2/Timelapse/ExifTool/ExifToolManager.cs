using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace Timelapse.ExifTool
{
    // The manager gives a high level management to the ExifTool, where it can start and stop it globally.
    public class ExifToolManager : IDisposable
    {
        private DispatcherTimer KillTimer;
        public ExifToolWrapper ExifTool { get; set; }

        public Dictionary<string, ImageMetadata> metadata = new Dictionary<string, ImageMetadata>();
        public ExifToolManager()
        {

        }

        public void Start()
        {
            // Check to see if the exiftool actually needs starting
            if (this.ExifTool == null)
            {
                this.ExifTool = new ExifToolWrapper();
                this.ExifTool.Start();
            }
        }

        public void Stop()
        {
            // Check to see if the exiftool actually needs stopping
            if (this.ExifTool != null)
            {
                Task.Run(() => this.ExifTool.Stop());
               
                // Sometimes Exiftool process seems to linger. This is a further way to destroy those processes.
                if (this.KillTimer == null)
                {
                    //this.KillTimer = new DispatcherTimer(DispatcherPriority.Background, Application.Current.Dispatcher) { Interval = TimeSpan.FromSeconds(1) }; 
                    this.KillTimer = new DispatcherTimer() { Interval = TimeSpan.FromSeconds(1) };
                    this.KillTimer.Tick += this.KillTimer_Tick; 
                }
                this.KillTimer.Start();
            }
        }

        // Kill Exiftool processes.
        private void KillTimer_Tick(object sender, EventArgs e)
        {
            Debug.Print("In kill timer");
            foreach (var process in Process.GetProcessesByName("exiftool(-k)"))
            {
                System.Diagnostics.Debug.Print(process.ProcessName);
                process.Kill();
            }
            KillTimer.Stop();
            this.ExifTool = null;
        }

        public bool IsStarted()
        {
            if (this.ExifTool == null) return false;
            return this.ExifTool.Status == ExifToolWrapper.ExeStatus.Ready;
        }

        #region Disposing
        // To follow design pattern in  CA1001 Types that own disposable fields should be disposable
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Dispose managed resources
                if (this.ExifTool != null)
                {
                    this.ExifTool.Stop();
                    // Stop  kills the process, but lets dispose the exif tool anyways
                    this.ExifTool.Dispose();
                }
            }
            // free native resources
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
