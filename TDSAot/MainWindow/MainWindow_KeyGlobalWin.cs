using Avalonia.Controls;
using System;

namespace TDSAot
{
    public partial class MainWindow : Window
    {
        private GlobalHotkey? hotkeyManager;

        private void InitializeHotKeys(nint hwnd)
        {
            hotkeyManager = new GlobalHotkey(hwnd);
            Win32Properties.AddWndProcHookCallback(this, HotKeyCallback);
        }
        private void RegisterHotKeys()
        {
            hotkeyManager?.RegisterGlobalHotKey(Option.HotKey, Option.ModifierKey);
        }

        /// <summary>
        /// Callback for registering a global hotkey press.
        /// </summary>
        /// <param name="hWnd">The window handle.</param>
        /// <param name="msg">The returned message.</param>
        /// <param name="wParam">The parameters of the message.</param>
        private IntPtr HotKeyCallback(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // If not a hotkey message or the global hotkey for showing the window
            if ((int)wParam == GlobalHotkey.HotKeyId)
            {
                AutoShowOrHide();
            }
            return IntPtr.Zero;
        }
    }
}