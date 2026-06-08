using Avalonia.Controls;
using System;
using TDS.Screenshot;

namespace TDSAot
{
    public partial class MainWindow : Window
    {
        private GlobalHotkey? hotkeyManager;

        private void InitializeHotKeys(nint hwnd)
        {
            hotkeyManager = new GlobalHotkey(hwnd);
            Win32Properties.AddWndProcHookCallback(this, WndProc);
        }
        internal void RegisterHotKeys()
        {
            hotkeyManager?.RegisterGlobalHotKey(Option.HotKey, Option.ModifierKey);
            hotkeyManager?.UnregisterScreenshotHotKey();
            if (Option.ScreenshotEnabled)
            {
                if (ScreenshotHost.ConflictsWithMainHotkey(Option))
                {
                    TDSAot.Utils.Message.ShowWaringOk("Screenshot",
                        "Screenshot hotkey conflicts with the main window hotkey. Change one of them in Settings.");
                }
                else if (hotkeyManager?.TryRegisterScreenshotHotKey(
                             Option.ScreenshotHotKey, Option.ScreenshotModifierKey) == false)
                {
                    TDSAot.Utils.Message.ShowWaringOk("Screenshot",
                        "Screenshot hotkey registration failed. It may be used by another application.");
                }
            }
        }

   
    }
}