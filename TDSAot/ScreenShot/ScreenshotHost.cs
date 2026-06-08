using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using TDS.Globalization;
using TDS.ScreenShot.UI.Models;
using TDS.ScreenShot.UI.Services;
using TDSAot;
using TDSAot.State;
using TDSAot.Utils;

namespace TDS.Screenshot;

/// <summary>
/// Launches the screenshot editor on the UI thread. Independent from the main TDS window lifecycle.
/// </summary>
public static class ScreenshotHost
{
    private static int _busy;

    /// <summary>Main window that launched the current editor (for hotkey callbacks).</summary>
    internal static MainWindow? CaptureMainWindow { get; private set; }

    public static bool IsBusy => Volatile.Read(ref _busy) != 0;

    public static void TriggerCaptureAsync(MainWindow mainWindow)
    {
        if (!mainWindow.Option.ScreenshotEnabled)
            return;

        if (ScreenshotService.HasActiveEditor)
        {
            _ = Dispatcher.UIThread.InvokeAsync(() => AutoSaveActiveAsync(mainWindow));
            return;
        }

        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
            return;
        _ = RunCaptureAsync(mainWindow);
    }

    private static async Task AutoSaveActiveAsync(MainWindow mainWindow)
    {
        try
        {
            var lang = LangManager.Instance.CurrentLang;
            var (ok, _, error) = await ScreenshotService.TryAutoSaveActiveAsync(
                mainWindow.Option.ScreenshotSavePath,
                AppOption.CurrentFolder);

            if (!ok)
            {
                var reason = string.IsNullOrWhiteSpace(error)
                    ? lang.ScreenshotSaveFailed
                    : error!;
                Message.ShowWaringOk(lang.ScreenshotSaveFailed, reason);
                return;
            }

            await ScreenshotService.CompleteAutoSaveWithFlashAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ScreenshotHost auto-save: {ex}");
            var lang = LangManager.Instance.CurrentLang;
            Message.ShowWaringOk(lang.ScreenshotSaveFailed, ex.Message);
        }
    }

    private static async Task RunCaptureAsync(MainWindow mainWindow)
    {
        try
        {
            if (mainWindow.IsVisible)
                mainWindow.HideWindow();

            await Task.Delay(80);

            var request = new EditRequest
            {
                ShowSaveButton = true,
                InitialTool = ToolIds.Select,
            };
            CaptureMainWindow = mainWindow;
            await ScreenshotService.EditAsync(request);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ScreenshotHost: {ex}");
        }
        finally
        {
            CaptureMainWindow = null;
            Volatile.Write(ref _busy, 0);
        }
    }

    internal static bool ConflictsWithMainHotkey(AppOption option)
        => option.ScreenshotEnabled
           && option.ScreenshotHotKey == option.HotKey
           && option.ScreenshotModifierKey == option.ModifierKey;
}
