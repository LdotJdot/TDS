using System.Runtime.InteropServices;

namespace TDS.ScreenShot.Core.Capture;

/// <summary>Win32 helpers for placing a native window in physical screen pixels.</summary>
public static class Win32WindowPlacement
{
    public static void SetPhysicalBounds(IntPtr hwnd, int x, int y, int width, int height)
    {
        if (hwnd == IntPtr.Zero) return;
        Win32.SetWindowPos(
            hwnd,
            IntPtr.Zero,
            x,
            y,
            width,
            height,
            Win32.SWP_NOZORDER | Win32.SWP_SHOWWINDOW);
    }
}
