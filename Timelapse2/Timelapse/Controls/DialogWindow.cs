using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using Timelapse.Dialog;
using Timelapse.Util;

namespace Timelapse.Controls
{
    // A specialized window to be used as a dialog, where:
    // - it fits the window into the calling window
    // - Cancellation tokens (and disposes of them afterwards) are include
    // - CloseButtonIsEnabled(bool enable): the window's close button can be enabled or disabled
#pragma warning disable CA1001 // Types that own disposable fields should be disposable. Reason: Handled in Closed event
    public class DialogWindow : Window
#pragma warning restore CA1001 // Types that own disposable fields should be disposableReason: Handled in Closed event
    {
        // Token to let us cancel the task
        private readonly CancellationTokenSource tokenSource;
        protected CancellationToken Token { get; set; }
        protected CancellationTokenSource TokenSource => this.tokenSource;

        // To help determine periodic updates to the progress bar 
        private DateTime lastRefreshDateTime = DateTime.Now;

        // Allows us to access the close button on the window
        [DllImport("user32.dll")]
        static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);
        [DllImport("user32.dll")]
        static extern bool EnableMenuItem(IntPtr hMenu, uint uIDEnableItem, uint uEnable);
        private const uint MF_BYCOMMAND = 0x00000000;
        private const uint MF_GRAYED = 0x00000001;
        private const uint MF_ENABLED = 0x00000000;
        private const uint SC_CLOSE = 0xF060;

        #region Initialization 
        public DialogWindow(Window owner)
        {
            this.Owner = owner;
            // Initialize the cancellation token
            this.tokenSource = new CancellationTokenSource();
            this.Token = this.tokenSource.Token;
            this.Loaded += this.DialogWindow_Loaded;
            this.Closed += this.DialogWindow_Closed;
        }

        // Fit the dialog into the calling window
        private void DialogWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Dialogs.TryPositionAndFitDialogIntoWindow(this);
        }
        #endregion

        #region Protected methods
        // Show progress information in the passed in progress bar as indicated
        protected static void UpdateProgressBar(BusyCancelIndicator BusyCancelIndicator, int percent, string message, bool isCancelEnabled, bool isIndeterminate)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(BusyCancelIndicator, nameof(BusyCancelIndicator));

            // Set it as a progressive or indeterminate bar
            BusyCancelIndicator.IsIndeterminate = isIndeterminate;

            // Set the progress bar position (only visible if determinate)
            BusyCancelIndicator.Percent = percent;

            // Update the text message
            BusyCancelIndicator.Message = message;

            // Update the cancel button to reflect the cancelEnabled argument
            BusyCancelIndicator.CancelButtonIsEnabled = isCancelEnabled;
            BusyCancelIndicator.CancelButtonText = isCancelEnabled ? "Cancel" : "Writing data...";
        }

        protected bool ReadyToRefresh()
        {
            TimeSpan intervalFromLastRefresh = DateTime.Now - this.lastRefreshDateTime;
            if (intervalFromLastRefresh > Constant.ThrottleValues.ProgressBarRefreshInterval)
            {
                this.lastRefreshDateTime = DateTime.Now;
                return true;
            }
            return false;
        }

        // Set the Window's Close Button Enable state
        protected void CloseButtonIsEnabled(bool enable)
        {
            Window window = Window.GetWindow(this);
            var wih = new WindowInteropHelper(window);
            IntPtr hwnd = wih.Handle;

            IntPtr hMenu = GetSystemMenu(hwnd, false);
            uint enableAction = enable ? MF_ENABLED : MF_GRAYED;
            if (hMenu != IntPtr.Zero)
            {
                EnableMenuItem(hMenu, SC_CLOSE, MF_BYCOMMAND | enableAction);
            }
        }
        #endregion

        #region Internal methods
        private void DialogWindow_Closed(object sender, EventArgs e)
        {
            // TokenSources need to be disposed, so here it is.
            if (TokenSource != null)
            {
                TokenSource.Dispose();
            }
        }
        #endregion
    }
}
