using Avalonia.Controls;
using System;
using TDS.Screenshot;
using TDSAot.Utils;

namespace TDSAot
{
    public partial class MainWindow : Window
    {
        const int WM_ENDSESSION = 0x0016;
        const int WM_QUERYENDSESSION = 0x0011;
        /// <summary>
        /// Callback for registering a global hotkey press.
        /// </summary>
        /// <param name="hWnd">The window handle.</param>
        /// <param name="msg">The returned message.</param>
        /// <param name="wParam">The parameters of the message.</param>
        private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == GlobalHotkey.HotKeyMessage)
            {
                if ((int)wParam == GlobalHotkey.ShowWindowHotKeyId)
                {
                    AutoShowOrHide();
                    handled = true;
                }
                else if ((int)wParam == GlobalHotkey.ScreenshotHotKeyId)
                {
                    ScreenshotHost.TriggerCaptureAsync(this);
                    handled = true;
                }
            }
            else if (msg == WM_QUERYENDSESSION)
            {
                OnAppClosed(null,null);
            }
            return IntPtr.Zero;
        }
    }
}