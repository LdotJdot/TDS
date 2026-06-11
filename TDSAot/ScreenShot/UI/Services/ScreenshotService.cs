using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using TDS.ScreenShot.Core.Capture;
using TDS.ScreenShot.UI.Models;
using TDS.ScreenShot.UI.Windows;

namespace TDS.ScreenShot.UI.Services;

/// <summary>
/// Public entry point for the screenshot library. Hosts the topmost fullscreen
/// window, captures the screen, lets the user pick a region + annotate, and
/// returns the result (PNG bytes + Bitmap) or copies it to the clipboard.
/// </summary>
public static class ScreenshotService
{
    private static ScreenshotWindow? _activeWindow;

    internal static ScreenshotWindow? ActiveWindow => _activeWindow;

    internal static bool HasActiveEditor => _activeWindow != null;

    internal static void RegisterActiveWindow(ScreenshotWindow window) => _activeWindow = window;

    internal static void UnregisterActiveWindow(ScreenshotWindow window)
    {
        if (_activeWindow == window)
            _activeWindow = null;
    }

    /// <summary>Auto-save the open editor to <paramref name="saveDirectory"/>.</summary>
    public static Task<(bool Ok, string? SavedPath, string? Error)> TryAutoSaveActiveAsync(
        string? saveDirectory,
        string fallbackDirectory
    )
    {
        var win = _activeWindow;
        if (win == null)
            return Task.FromResult<(bool, string?, string?)>(
                (false, null, "Screenshot editor is not open.")
            );
        return win.TryQuickSaveAsync(saveDirectory, fallbackDirectory);
    }

    /// <summary>Camera-flash feedback then close the active editor.</summary>
    public static Task CompleteAutoSaveWithFlashAsync()
    {
        var win = _activeWindow;
        return win == null ? Task.CompletedTask : win.PlaySaveFlashAndCloseAsync();
    }

    /// <summary>
    /// Captures the entire virtual desktop and returns the raw PNG bytes (no UI).
    /// </summary>
    public static byte[] CaptureFullScreenPng()
    {
        var bmp = CaptureFullScreen();
        return PngEncoder.Encode(bmp);
    }

    /// <summary>
    /// Captures the entire virtual desktop and returns the Bitmap.
    /// </summary>
    public static Bitmap CaptureFullScreen()
    {
        var capture = CaptureFactory.Create();
        var screens = capture.GetScreens();
        var virtualDesktop =
            screens.FirstOrDefault(s => s.DeviceName == "VirtualDesktop")
            ?? screens.First(s => s.IsPrimary);
        return capture.CaptureScreen(virtualDesktop);
    }

    /// <summary>
    /// Shows the screenshot editor. Returns a result describing the user's choice
    /// and the final PNG bytes (or null if they cancelled).
    /// </summary>
    public static async Task<EditResult?> EditAsync(EditRequest? request = null)
    {
        request ??= new EditRequest();

        // Capture the monitor under the cursor (single display), not the full virtual desktop.
        var capture = CaptureFactory.Create();
        var screens = capture.GetScreens();
        var targetScreen = Win32CaptureService.GetScreenAtCursor(screens);
        var fullBitmap = capture.CaptureScreen(targetScreen);
        var screenBounds = targetScreen.Bounds;
        var physicalBounds = new PixelRect(
            (int)screenBounds.X,
            (int)screenBounds.Y,
            (int)screenBounds.Width,
            (int)screenBounds.Height
        );

        var tcs = new TaskCompletionSource<EditResult?>();
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var win = new ScreenshotWindow(
                fullBitmap,
                physicalBounds,
                targetScreen.DpiScale,
                request
            );
            win.Closed += (_, _) =>
            {
                UnregisterActiveWindow(win);
                if (!tcs.Task.IsCompleted)
                    tcs.TrySetResult(win.Result);
            };
            RegisterActiveWindow(win);
            // Fullscreen overlay: always Show(). TDS hides main window before capture,
            // so ShowDialog(owner) throws when owner is not visible.
            win.Show();
            win.Activate();
            win.Focus();
        });
        return await tcs.Task;
    }

    /// <summary>
    /// Convenience: capture, edit, copy PNG to clipboard.
    /// </summary>
    public static async Task<EditResult> EditAndCopyAsync(EditRequest? request = null)
    {
        var result = await EditAsync(request);
        if (result?.Result != null && TryGetActiveWindow() is { } owner)
            await ClipboardService.CopyBitmapAsync(owner, result.Result);
        return result ?? new EditResult { Outcome = EditOutcome.Cancelled };
    }

    /// <summary>
    /// Convenience: capture, edit, and save PNG to disk. Shows a Save File dialog.
    /// </summary>
    public static async Task<EditResult> EditAndSaveAsync(EditRequest? request = null)
    {
        var result = await EditAsync(request);
        if (result?.PngBytes == null)
            return new EditResult { Outcome = EditOutcome.Cancelled };
        var owner = TryGetActiveWindow();
        if (owner == null)
            return result;

        var sp = owner.StorageProvider;
        var file = await sp.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = "保存截屏",
                DefaultExtension = "png",
                SuggestedFileName = $"snapshot_{DateTime.Now:yyyyMMdd_HHmmss}.png",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("PNG 图片") { Patterns = new[] { "*.png" } }
                }
            }
        );
        if (file is null)
            return result;
        var path = file.Path.AbsolutePath;
        await using var fs = File.Create(path);
        await fs.WriteAsync(result.PngBytes);
        return result with { SavedPath = path };
    }

    private static Window? TryGetActiveWindow()
    {
        if (
            Application.Current?.ApplicationLifetime
            is IClassicDesktopStyleApplicationLifetime desktop
        )
        {
            return desktop.Windows.FirstOrDefault(w => w.IsActive) ?? desktop.MainWindow;
        }
        return null;
    }
}
