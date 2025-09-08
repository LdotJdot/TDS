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
            Win32Properties.AddWndProcHookCallback(this, WndProc);
        }
        internal void RegisterHotKeys()
        {
            hotkeyManager?.RegisterGlobalHotKey(Option.HotKey, Option.ModifierKey);
        }

   
    }
}