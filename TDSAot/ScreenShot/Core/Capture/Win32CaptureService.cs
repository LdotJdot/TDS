using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace TDS.ScreenShot.Core.Capture;

/// <summary>
/// Windows capture implementation using GDI BitBlt. AOT friendly (no System.Drawing).
/// </summary>
public sealed class Win32CaptureService : ICaptureService
{
    public IReadOnlyList<ScreenInfo> GetScreens()
    {
        var list = new List<ScreenInfo>();
        GetScreensInto(list);
        if (list.Count == 0)
            return list;

        // Build a synthetic "virtual desktop" entry that covers the union of all monitor bounds.
        int left = list.Min(s => (int)s.Bounds.X);
        int top = list.Min(s => (int)s.Bounds.Y);
        int right = list.Max(s => (int)s.Bounds.Right);
        int bottom = list.Max(s => (int)s.Bounds.Bottom);
        var primaryScale = list.FirstOrDefault(s => s.IsPrimary)?.DpiScale ?? list[0].DpiScale;
        list.Add(new ScreenInfo(
            Handle: IntPtr.Zero,
            DeviceName: "VirtualDesktop",
            Bounds: new Rect(left, top, right - left, bottom - top),
            WorkingArea: new Rect(left, top, right - left, bottom - top),
            DpiScale: primaryScale,
            IsPrimary: true));
        return list;
    }

    private static void GetScreensInto(List<ScreenInfo> list)
    {
        Win32.MonitorEnumProc callback = (IntPtr hMonitor, IntPtr _, ref Win32.RECT _, IntPtr _) =>
        {
            var info = new Win32.MONITORINFOEX { cbSize = Marshal.SizeOf<Win32.MONITORINFOEX>() };
            if (!Win32.GetMonitorInfo(hMonitor, ref info))
                return true;

            var bounds = new Rect(info.rcMonitor.Left, info.rcMonitor.Top, info.rcMonitor.Width, info.rcMonitor.Height);
            var work = new Rect(info.rcWork.Left, info.rcWork.Top, info.rcWork.Width, info.rcWork.Height);
            list.Add(new ScreenInfo(
                Handle: hMonitor,
                DeviceName: info.szDevice,
                Bounds: bounds,
                WorkingArea: work,
                DpiScale: GetMonitorDpiScale(hMonitor),
                IsPrimary: (info.dwFlags & 1) != 0));
            return true;
        };

        Win32.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);
    }

    private static double GetMonitorDpiScale(IntPtr hMonitor)
    {
        if (Win32.GetDpiForMonitor(hMonitor, Win32.MDT_EFFECTIVE_DPI, out var dpiX, out _) == 0 && dpiX > 0)
            return dpiX / 96.0;
        return 1.0;
    }

    /// <summary>Returns the monitor under the cursor, or primary / first physical screen.</summary>
    public static ScreenInfo GetScreenAtCursor(IReadOnlyList<ScreenInfo> screens)
    {
        var physical = screens.Where(s => s.DeviceName != "VirtualDesktop").ToList();
        if (physical.Count == 0)
            return screens.First(s => s.IsPrimary);

        if (Win32.GetCursorPos(out var pt))
        {
            foreach (var s in physical)
            {
                if (s.Bounds.Contains(new Point(pt.X, pt.Y)))
                    return s;
            }
        }

        return physical.FirstOrDefault(s => s.IsPrimary) ?? physical[0];
    }

    public WriteableBitmap CaptureScreen(ScreenInfo screen)
    {
        int w = (int)screen.Bounds.Width;
        int h = (int)screen.Bounds.Height;
        if (w <= 0 || h <= 0)
            throw new ArgumentException("Screen has zero area", nameof(screen));

        IntPtr screenDc = Win32.GetDC(IntPtr.Zero);
        if (screenDc == IntPtr.Zero)
            throw new InvalidOperationException("GetDC(NULL) failed.");

        IntPtr memDc = IntPtr.Zero;
        IntPtr bmp = IntPtr.Zero;
        IntPtr old = IntPtr.Zero;
        IntPtr bits = IntPtr.Zero;
        try
        {
            memDc = Win32.CreateCompatibleDC(screenDc);
            bmp = Win32.CreateCompatibleBitmap(screenDc, w, h);
            old = Win32.SelectObject(memDc, bmp);

            if (!Win32.BitBlt(memDc, 0, 0, w, h, screenDc, (int)screen.Bounds.X, (int)screen.Bounds.Y, Win32.SRCCOPY | (int)Win32.CAPTUREBLT))
                throw new InvalidOperationException("BitBlt failed.");

            // GetDIBits requires positive biHeight for top-down bitmap - use negative for top-down BGRA
            var bmi = new Win32.BITMAPINFO
            {
                bmiHeader = new Win32.BITMAPINFOHEADER
                {
                    biSize = (uint)Marshal.SizeOf<Win32.BITMAPINFOHEADER>(),
                    biWidth = w,
                    biHeight = -h, // top-down
                    biPlanes = 1,
                    biBitCount = 32,
                    biCompression = Win32.BI_RGB,
                }
            };

            int stride = w * 4;
            int bufLen = stride * h;
            bits = Marshal.AllocHGlobal(bufLen);

            int lines = Win32.GetDIBits(memDc, bmp, 0, (uint)h, bits, ref bmi, Win32.DIB_RGB_COLORS);
            if (lines == 0)
                throw new InvalidOperationException("GetDIBits failed.");

            // GDI gives us BGRA bytes (bottom-up in memory when biHeight > 0; top-down with negative biHeight).
            // Our biHeight is negative so the data is top-down in BGRA byte order. Avalonia expects
            // WriteableBitmap.Format = PixelFormat.Bgra8888 with a top-down buffer.
            // Bitmap DPI metadata is not used for layout — ScreenshotBackground maps pixels
            // explicitly via DrawImage. Keep 96 DPI so pixel size == layout pixel count.
            var wb = new WriteableBitmap(
                new PixelSize(w, h),
                new Vector(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Opaque);
            using (var lk = wb.Lock())
            {
                unsafe
                {
                    byte* src = (byte*)bits.ToPointer();
                    byte* dst = (byte*)lk.Address;
                    int srcStride = stride;
                    int dstStride = lk.RowBytes;
                    int rowBytes = Math.Min(srcStride, dstStride);
                    for (int y = 0; y < h; y++)
                    {
                        Buffer.MemoryCopy(
                            src + (long)y * srcStride,
                            dst + (long)y * dstStride,
                            rowBytes,
                            rowBytes);
                    }
                }
            }
            return wb;
        }
        finally
        {
            if (bits != IntPtr.Zero) Marshal.FreeHGlobal(bits);
            if (old != IntPtr.Zero && memDc != IntPtr.Zero) Win32.SelectObject(memDc, old);
            if (bmp != IntPtr.Zero) Win32.DeleteObject(bmp);
            if (memDc != IntPtr.Zero) Win32.DeleteDC(memDc);
            if (screenDc != IntPtr.Zero) Win32.ReleaseDC(IntPtr.Zero, screenDc);
        }
    }
}