using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Threading;
using TDS.ScreenShot.UI.Services;
using TDS.ScreenShot.UI.Windows;
using TDSAot;

namespace TDS.Screenshot;

/// <summary>
/// Registers Win32 hotkeys on the screenshot window while the editor is open:
/// Esc to cancel, and the screenshot shortcut for auto-save (migrated from MainWindow).
/// </summary>
internal static class ScreenshotEscapeHotkey
{
    internal const int EscapeHotKeyId = 9529;
    private const int VkEscape = 0x1B;
    private const uint WM_HOTKEY = 0x0312;
    private const uint WM_KEYDOWN = 0x0100;
    private const uint WM_SYSKEYDOWN = 0x0104;
    private const int MaxMigrateRetries = 24;

    private static MainWindow? _mainWindow;
    private static uint _screenshotVk;
    private static uint _screenshotModifiers;
    private static bool _screenshotHotkeyOnEditor;
    private static bool _mainHotkeySuspended;

    internal static void Attach(ScreenshotWindow window, MainWindow mainWindow)
    {
        if (!OperatingSystem.IsWindows())
            return;

        Detach(window);
        _mainWindow = mainWindow;

        Win32Properties.AddWndProcHookCallback(window, (hWnd, msg, wParam, lParam, ref handled) =>
        {
            if (window is not { IsVisible: true })
                return IntPtr.Zero;

            if (msg == WM_HOTKEY && (int)wParam == GlobalHotkey.ScreenshotHotKeyId && _mainWindow != null)
            {
                ScreenshotHost.TriggerCaptureAsync(_mainWindow);
                handled = true;
                return IntPtr.Zero;
            }

            if ((msg == WM_HOTKEY && (int)wParam == EscapeHotKeyId)
                || ((msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN) && (int)wParam == VkEscape))
            {
                window.RequestCancelFromEscape();
                handled = true;
            }

            return IntPtr.Zero;
        });

        TryRegisterEscape(window);
        MigrateScreenshotHotkeyToEditor(window, mainWindow);
    }

    internal static void Detach(ScreenshotWindow window)
    {
        if (!OperatingSystem.IsWindows())
            return;

        if (window.TryGetPlatformHandle() is { } ph)
        {
            UnregisterHotKey(ph.Handle, EscapeHotKeyId);
            if (_screenshotHotkeyOnEditor)
                UnregisterHotKey(ph.Handle, GlobalHotkey.ScreenshotHotKeyId);
        }

        RestoreScreenshotHotkeyOnMain();
        _mainWindow = null;
        _screenshotHotkeyOnEditor = false;
        _mainHotkeySuspended = false;
    }

    private static void MigrateScreenshotHotkeyToEditor(ScreenshotWindow window, MainWindow mainWindow, int attempt = 0)
    {
        if (!mainWindow.Option.ScreenshotEnabled
            || ScreenshotHost.ConflictsWithMainHotkey(mainWindow.Option))
            return;

        if (ScreenshotService.ActiveWindow != window || !window.IsVisible)
            return;

        _screenshotVk = mainWindow.Option.ScreenshotHotKey;
        _screenshotModifiers = mainWindow.Option.ScreenshotModifierKey;

        if (window.TryGetPlatformHandle() is not { } editorPh)
        {
            if (attempt < MaxMigrateRetries)
            {
                Dispatcher.UIThread.Post(
                    () => MigrateScreenshotHotkeyToEditor(window, mainWindow, attempt + 1),
                    DispatcherPriority.Loaded);
            }
            return;
        }

        UnregisterHotKey(editorPh.Handle, GlobalHotkey.ScreenshotHotKeyId);
        if (!RegisterHotKey(editorPh.Handle, GlobalHotkey.ScreenshotHotKeyId, _screenshotModifiers, _screenshotVk))
            return;

        if (mainWindow.TryGetPlatformHandle() is { } mainPh)
        {
            UnregisterHotKey(mainPh.Handle, GlobalHotkey.ScreenshotHotKeyId);
            _mainHotkeySuspended = true;
        }

        _screenshotHotkeyOnEditor = true;
    }

    private static void RestoreScreenshotHotkeyOnMain()
    {
        var main = _mainWindow;
        if (main == null || (!_screenshotHotkeyOnEditor && !_mainHotkeySuspended))
            return;

        main.RegisterHotKeys();
    }

    private static void TryRegisterEscape(ScreenshotWindow window, int attempt = 0)
    {
        if (window.TryGetPlatformHandle() is not { } ph)
        {
            if (attempt < MaxMigrateRetries)
            {
                Dispatcher.UIThread.Post(
                    () => TryRegisterEscape(window, attempt + 1),
                    DispatcherPriority.Loaded);
            }
            return;
        }

        UnregisterHotKey(ph.Handle, EscapeHotKeyId);
        RegisterHotKey(ph.Handle, EscapeHotKeyId, 0, VkEscape);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
