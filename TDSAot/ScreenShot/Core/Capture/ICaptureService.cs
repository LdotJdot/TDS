using Avalonia.Media.Imaging;

namespace TDS.ScreenShot.Core.Capture;

public interface ICaptureService
{
    IReadOnlyList<ScreenInfo> GetScreens();

    /// <summary>
    /// Captures the given screen at native resolution. The returned bitmap is owned by the caller.
    /// </summary>
    WriteableBitmap CaptureScreen(ScreenInfo screen);

    /// <summary>
    /// Captures an arbitrary physical-pixel rectangle into a BGRA WriteableBitmap. The rectangle
    /// is clamped to the union of all display bounds. The returned bitmap is owned by the caller.
    /// </summary>
    /// <param name="physicalX">Left edge in physical desktop pixels.</param>
    /// <param name="physicalY">Top edge in physical desktop pixels.</param>
    /// <param name="width">Width in physical pixels (clamped to the desktop bounds).</param>
    /// <param name="height">Height in physical pixels (clamped to the desktop bounds).</param>
    WriteableBitmap CaptureScreenRect(
        int physicalX, int physicalY, int width, int height);
}