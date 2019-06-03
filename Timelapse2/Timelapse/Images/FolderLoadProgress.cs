using System;
using System.Windows.Media.Imaging;

namespace Timelapse.Images
{
    internal class FolderLoadProgress
    {
        public BitmapSource BitmapSource { get; set; }
        public int CurrentFile { get; set; }
        public string CurrentFileName { get; set; }
        public int TotalFiles { get; set; }
        public int CurrentPass { get; set; }
        public int TotalPasses { get; set; }

        public FolderLoadProgress(int totalFiles)
        {
            this.BitmapSource = null;
            this.CurrentFile = 0;
            this.CurrentFileName = null;
            this.TotalFiles = totalFiles;
            this.CurrentPass = 0;
            this.TotalPasses = 0;
        }
        public string GetMessage()
        {
            string message = (this.TotalPasses > 1) ? String.Format("Pass {0}/{1}: ", this.CurrentPass, this.TotalPasses) : String.Empty;
            return String.Format("{0} Loading file {1} of {2} ({3})", message, this.CurrentFile, this.TotalFiles, this.CurrentFileName);
        }
    }
}
