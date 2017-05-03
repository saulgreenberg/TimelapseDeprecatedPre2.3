using System;
using System.Windows.Controls;
using System.Windows.Media;

namespace Timelapse.Controls
{
    /// <summary>
    /// Display a progress bar and a message, used as feedback for loading an image set for the first time
    /// </summary>
    public partial class FeedbackControl : UserControl
    {
        public FeedbackControl()
        {
            this.InitializeComponent();
        }
    }
}
