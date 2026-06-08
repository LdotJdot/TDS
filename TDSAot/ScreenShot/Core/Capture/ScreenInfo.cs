using Avalonia;

namespace TDS.ScreenShot.Core.Capture;

public sealed record ScreenInfo(
    IntPtr Handle,
    string DeviceName,
    Rect Bounds,           // Virtual-desktop pixel coordinates
    Rect WorkingArea,      // Excludes taskbar etc.
    double DpiScale,       // 1.0 = 100%
    bool IsPrimary);