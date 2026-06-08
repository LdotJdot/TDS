using Avalonia.Media.Imaging;

namespace TDS.ScreenShot.Core.Capture;

public interface ICaptureService
{
    IReadOnlyList<ScreenInfo> GetScreens();

    /// <summary>
    /// Captures the given screen at native resolution. The returned bitmap is owned by the caller.
    /// </summary>
    WriteableBitmap CaptureScreen(ScreenInfo screen);
}