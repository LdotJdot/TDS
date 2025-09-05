using System;
using System.Runtime.InteropServices;
using TDSAot.Utils;

/// <summary>
/// 全局热键管理器 - 单文件实现
/// </summary>
internal class GlobalHotkey : IDisposable
{
    internal readonly nint handle;

    public GlobalHotkey(nint hwd)
    {
        this.handle = hwd;
        UnregisterHotKey(handle, HotKeyId);
    }

    /// <summary>
    /// Registers a global hotkey for showing the window.
    /// Wraps the RegisterHotKey method.
    /// </summary>
    public void RegisterGlobalHotKey(uint key, uint keyModifiers)
    {
        UnregisterGlobalHotKey();
        var result = RegisterHotKey(handle, HotKeyId, (uint)keyModifiers, (uint)key);
        if (!result)
        {
            Message.ShowWaringOk("Hotkey", "Hotkey registration failed. Check for hotkey conflicts.\r\n\r\n");
        }
    }

    /// <summary>
    /// Deregisters a global shortcut.
    /// Wraps the UnregisterHotKey method.
    /// </summary>
    public void UnregisterGlobalHotKey()
    {
        UnregisterHotKey(handle, HotKeyId);
    }

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    internal const int HotKeyId = 9527; // ID for the hotkey to be registered.
    internal const int HotKeyMessage = 0x0312; // Message ID for the hotkey message

    public void Dispose()
    {
        UnregisterGlobalHotKey();
    }
}