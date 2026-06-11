namespace TDS.ScreenShot.Core.Capture;

/// <summary>
/// Win32 窗口 region：在 overlay 上挖矩形洞，让选区内显示并操作真实桌面。
/// <para>
/// Avalonia 半透明/ per-pixel alpha 在 Windows 上不可靠，无法单靠 X11 式「透明窗口」实现洞；
/// 必须 SetWindowRgn。坐标一律物理像素，与 Avalonia DIP 的换算在 UI 层完成（× dpiScale）。
/// </para>
/// </summary>
public static class Win32WindowRegion
{
    /// <summary>
    /// Shrink the HWND to everything except <paramref name="hole"/> (client coords).
    /// </summary>
    public static void ApplySelectionHole(
        IntPtr hwnd,
        int clientWidth,
        int clientHeight,
        int holeX,
        int holeY,
        int holeWidth,
        int holeHeight
    )
    {
        if (hwnd == IntPtr.Zero || clientWidth <= 0 || clientHeight <= 0)
            return;

        int left = Math.Clamp(holeX, 0, clientWidth);
        int top = Math.Clamp(holeY, 0, clientHeight);
        int right = Math.Clamp(holeX + holeWidth, 0, clientWidth);
        int bottom = Math.Clamp(holeY + holeHeight, 0, clientHeight);
        if (right <= left || bottom <= top)
            return;

        IntPtr full = Win32.CreateRectRgn(0, 0, clientWidth, clientHeight);
        IntPtr hole = Win32.CreateRectRgn(left, top, right, bottom);
        IntPtr result = Win32.CreateRectRgn(0, 0, 0, 0);
        Win32.CombineRgn(result, full, hole, Win32.RGN_DIFF);
        Win32.DeleteObject(full);
        Win32.DeleteObject(hole);
        Win32.SetWindowRgn(hwnd, result, true);
    }

    /// <summary>Restore the HWND to its normal rectangular shape.</summary>
    public static void Clear(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return;
        Win32.SetWindowRgn(hwnd, IntPtr.Zero, true);
    }

    /// <summary>Client area size in physical pixels, or false if unavailable.</summary>
    public static bool TryGetClientSize(IntPtr hwnd, out int width, out int height)
    {
        width = height = 0;
        if (hwnd == IntPtr.Zero)
            return false;
        if (!Win32.GetClientRect(hwnd, out var rc))
            return false;
        width = rc.Width;
        height = rc.Height;
        return width > 0 && height > 0;
    }
}
