using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Interop;

namespace Timelapse.Controls
{
    // A specialized window to be used as a dialog, where:
    // - it includes Cancellation tokens (and disposes of them afterwards)
    // - CloseButtonIsEnabled(bool enable): the window's close button can be enabled or disabled
    #pragma warning disable CA1001 // Types that own disposable fields should be disposable. Reason: Handled in Closed event
    public class DialogWindow : Window
    #pragma warning restore CA1001 // Types that own disposable fields should be disposableReason: Handled in Closed event
    {
        // Token to let us cancel the task
        private readonly CancellationTokenSource tokenSource;
        protected CancellationToken Token { get; set; }

        protected CancellationTokenSource TokenSource => this.tokenSource;

        // Allows us to access the close button on the window
        [DllImport("user32.dll")]
        static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);
        [DllImport("user32.dll")]
        static extern bool EnableMenuItem(IntPtr hMenu, uint uIDEnableItem, uint uEnable);
        const uint MF_BYCOMMAND = 0x00000000;
        const uint MF_GRAYED = 0x00000001;
        const uint MF_ENABLED = 0x00000000;
        const uint SC_CLOSE = 0xF060;

        public DialogWindow()
        {   
            // Initialize the cancellation token
            this.tokenSource = new CancellationTokenSource();
            this.Token = this.tokenSource.Token;
            this.Closed += this.DialogWindow_Closed;
        }

        private void DialogWindow_Closed(object sender, EventArgs e)
        {
            // TokenSources need to be disposed, so here it is.
            if (TokenSource != null)
            {
                TokenSource.Dispose();
            }
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
    }
}
