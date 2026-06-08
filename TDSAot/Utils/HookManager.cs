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
        UnregisterHotKey(handle, ShowWindowHotKeyId);
        UnregisterHotKey(handle, ScreenshotHotKeyId);
    }

    /// <summary>Registers the main TDS show/hide hotkey.</summary>
    public void RegisterGlobalHotKey(uint key, uint keyModifiers)
    {
        UnregisterHotKey(handle, ShowWindowHotKeyId);
        var result = RegisterHotKey(handle, ShowWindowHotKeyId, keyModifiers, key);
        if (!result)
            Message.ShowWaringOk("Hotkey", "Hotkey registration failed. Check for hotkey conflicts.\r\n\r\n");
    }

    /// <summary>Registers the screenshot hotkey (independent id).</summary>
    public bool TryRegisterScreenshotHotKey(uint key, uint keyModifiers)
    {
        UnregisterHotKey(handle, ScreenshotHotKeyId);
        return RegisterHotKey(handle, ScreenshotHotKeyId, keyModifiers, key);
    }

    public void UnregisterScreenshotHotKey()
        => UnregisterHotKey(handle, ScreenshotHotKeyId);

    public void UnregisterGlobalHotKey()
        => UnregisterHotKey(handle, ShowWindowHotKeyId);

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    internal const int ShowWindowHotKeyId = 9527;
    internal const int ScreenshotHotKeyId = 9528;
    internal const int HotKeyId = ShowWindowHotKeyId; // backward-compatible alias
    internal const int HotKeyMessage = 0x0312;

    public void Dispose()
    {
        UnregisterGlobalHotKey();
        UnregisterScreenshotHotKey();
    }
}