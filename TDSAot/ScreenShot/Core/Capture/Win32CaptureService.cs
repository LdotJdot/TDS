using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace TDS.ScreenShot.Core.Capture;

/// <summary>
/// Windows 截屏：从 DWM 合成桌面 DC 做 GDI BitBlt（含 <see cref="Win32.CAPTUREBLT"/>）。
/// <para>
/// 首次截图在 overlay 出现前执行；滚动截屏在选区 Win32 挖洞后，BitBlt 直接读洞下 live 像素。
/// </para>
/// <para>
/// BitBlt 无法可靠捕获的情况（本工具未做额外兜底，遇到即表现为黑屏/空白/旧帧）：
/// <list type="bullet">
/// <item>DRM / HDCP 等受保护内容（浏览器视频、部分播放器）</item>
/// <item>DirectX / 全屏独占模式（BitBlt 读不到 GPU 独占帧缓冲）</item>
/// <item>Win10 1803 之前极个别 GPU 窗口可能黑屏（现代 DWM 合成路径通常正常）</item>
/// <item>窗口在屏幕上不可见或被完全遮挡的区域（本工具只截用户当前可见画面，属预期行为）</item>
/// </list>
/// </para>
/// </summary>
public sealed class Win32CaptureService : ICaptureService
{
    /// <summary>
    /// 向屏幕坐标处的滚动目标投递 <c>WM_MOUSEWHEEL</c>，不依赖当前光标位置。
    /// </summary>
    public static bool TrySendWheelAt(
        int physicalX,
        int physicalY,
        int delta,
        IntPtr skipOverlayHwnd = default
    )
    {
        if (delta == 0)
            return false;

        var pt = new Win32.POINT { X = physicalX, Y = physicalY };
        IntPtr target = ResolveScrollTargetAt(pt, skipOverlayHwnd);
        if (target == IntPtr.Zero)
            return false;

        IntPtr wParam = unchecked((IntPtr)((delta << 16) & 0xFFFFFFFF));
        IntPtr lParam = (IntPtr)(((physicalY & 0xFFFF) << 16) | (physicalX & 0xFFFF));
        Win32.SendMessage(target, Win32.WM_MOUSEWHEEL, wParam, lParam);
        Debug.WriteLine(
            $"[scroll] TrySendWheelAt: target=0x{target.ToInt64():X} pt=({physicalX},{physicalY}) delta={delta}"
        );
        return true;
    }

    private static IntPtr ResolveScrollTargetAt(Win32.POINT pt, IntPtr skipOverlayHwnd)
    {
        IntPtr hit = Win32.WindowFromPoint(pt);
        if (hit == IntPtr.Zero || hit == skipOverlayHwnd)
            return IntPtr.Zero;

        IntPtr root = Win32.GetAncestor(hit, Win32.GA_ROOT);
        if (root == IntPtr.Zero || root == skipOverlayHwnd)
            return IntPtr.Zero;

        return FindDeepestChildAt(root, pt);
    }

    private static IntPtr FindDeepestChildAt(IntPtr root, Win32.POINT screenPt)
    {
        IntPtr current = root;
        for (int i = 0; i < 32; i++)
        {
            var clientPt = screenPt;
            Win32.ScreenToClient(current, ref clientPt);
            IntPtr child = Win32.ChildWindowFromPoint(current, clientPt);
            if (child == IntPtr.Zero || child == current)
                break;
            current = child;
        }
        return current;
    }

    public IReadOnlyList<ScreenInfo> GetScreens()
    {
        var list = new List<ScreenInfo>();
        GetScreensInto(list);
        if (list.Count == 0)
            return list;

        int left = list.Min(s => (int)s.Bounds.X);
        int top = list.Min(s => (int)s.Bounds.Y);
        int right = list.Max(s => (int)s.Bounds.Right);
        int bottom = list.Max(s => (int)s.Bounds.Bottom);
        var primaryScale = list.FirstOrDefault(s => s.IsPrimary)?.DpiScale ?? list[0].DpiScale;
        list.Add(
            new ScreenInfo(
                Handle: IntPtr.Zero,
                DeviceName: "VirtualDesktop",
                Bounds: new Rect(left, top, right - left, bottom - top),
                WorkingArea: new Rect(left, top, right - left, bottom - top),
                DpiScale: primaryScale,
                IsPrimary: true
            )
        );
        return list;
    }

    private static void GetScreensInto(List<ScreenInfo> list)
    {
        Win32.MonitorEnumProc callback = (IntPtr hMonitor, IntPtr _, ref Win32.RECT _, IntPtr _) =>
        {
            var info = new Win32.MONITORINFOEX { cbSize = Marshal.SizeOf<Win32.MONITORINFOEX>() };
            if (!Win32.GetMonitorInfo(hMonitor, ref info))
                return true;

            var bounds = new Rect(
                info.rcMonitor.Left,
                info.rcMonitor.Top,
                info.rcMonitor.Width,
                info.rcMonitor.Height
            );
            var work = new Rect(
                info.rcWork.Left,
                info.rcWork.Top,
                info.rcWork.Width,
                info.rcWork.Height
            );
            list.Add(
                new ScreenInfo(
                    Handle: hMonitor,
                    DeviceName: info.szDevice,
                    Bounds: bounds,
                    WorkingArea: work,
                    DpiScale: GetMonitorDpiScale(hMonitor),
                    IsPrimary: (info.dwFlags & 1) != 0
                )
            );
            return true;
        };

        Win32.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);
    }

    private static double GetMonitorDpiScale(IntPtr hMonitor)
    {
        if (
            Win32.GetDpiForMonitor(hMonitor, Win32.MDT_EFFECTIVE_DPI, out var dpiX, out _) == 0
            && dpiX > 0
        )
            return dpiX / 96.0;
        return 1.0;
    }

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

    public WriteableBitmap CaptureScreen(ScreenInfo screen) =>
        CaptureScreenRect(
            (int)screen.Bounds.X,
            (int)screen.Bounds.Y,
            (int)screen.Bounds.Width,
            (int)screen.Bounds.Height
        );

    public WriteableBitmap CaptureScreenRect(int physicalX, int physicalY, int width, int height)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentException("Capture rect has zero area.", nameof(width));

        ClampToVirtualDesktop(ref physicalX, ref physicalY, ref width, ref height);

        Win32.DwmFlush();
        return CaptureDesktopBitBlt(physicalX, physicalY, width, height);
    }

    private static void ClampToVirtualDesktop(
        ref int physicalX,
        ref int physicalY,
        ref int width,
        ref int height
    )
    {
        int vLeft = int.MaxValue,
            vTop = int.MaxValue,
            vRight = int.MinValue,
            vBottom = int.MinValue;
        foreach (var s in new Win32CaptureService().GetScreens())
        {
            if (s.Bounds.X < vLeft)
                vLeft = (int)s.Bounds.X;
            if (s.Bounds.Y < vTop)
                vTop = (int)s.Bounds.Y;
            if (s.Bounds.Right > vRight)
                vRight = (int)s.Bounds.Right;
            if (s.Bounds.Bottom > vBottom)
                vBottom = (int)s.Bounds.Bottom;
        }
        if (physicalX < vLeft)
        {
            width -= vLeft - physicalX;
            physicalX = vLeft;
        }
        if (physicalY < vTop)
        {
            height -= vTop - physicalY;
            physicalY = vTop;
        }
        if (physicalX + width > vRight)
            width = vRight - physicalX;
        if (physicalY + height > vBottom)
            height = vBottom - physicalY;
        if (width <= 0 || height <= 0)
            throw new ArgumentException(
                "Capture rect is outside the virtual desktop.",
                nameof(width)
            );
    }

    /// <summary>
    /// 从屏幕 DC BitBlt。滚动挖洞后，选区矩形读到的即为洞下 live 桌面像素。
    /// </summary>
    private static WriteableBitmap CaptureDesktopBitBlt(
        int physicalX,
        int physicalY,
        int width,
        int height
    )
    {
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
            bmp = Win32.CreateCompatibleBitmap(screenDc, width, height);
            old = Win32.SelectObject(memDc, bmp);

            if (
                !Win32.BitBlt(
                    memDc,
                    0,
                    0,
                    width,
                    height,
                    screenDc,
                    physicalX,
                    physicalY,
                    Win32.SRCCOPY | (int)Win32.CAPTUREBLT
                )
            )
                throw new InvalidOperationException("BitBlt failed.");

            var result = ReadBitmapFromDc(memDc, bmp, width, height, screenDc, out bits);
            bits = IntPtr.Zero;
            return result ?? throw new InvalidOperationException("GetDIBits failed.");
        }
        finally
        {
            if (bits != IntPtr.Zero)
                Marshal.FreeHGlobal(bits);
            if (old != IntPtr.Zero && memDc != IntPtr.Zero)
                Win32.SelectObject(memDc, old);
            if (bmp != IntPtr.Zero)
                Win32.DeleteObject(bmp);
            if (memDc != IntPtr.Zero)
                Win32.DeleteDC(memDc);
            if (screenDc != IntPtr.Zero)
                Win32.ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    private static WriteableBitmap? ReadBitmapFromDc(
        IntPtr memDc,
        IntPtr hBitmap,
        int w,
        int h,
        IntPtr refDc,
        out IntPtr bits
    )
    {
        bits = IntPtr.Zero;
        var bmi = new Win32.BITMAPINFO
        {
            bmiHeader = new Win32.BITMAPINFOHEADER
            {
                biSize = (uint)Marshal.SizeOf<Win32.BITMAPINFOHEADER>(),
                biWidth = w,
                biHeight = -h,
                biPlanes = 1,
                biBitCount = 32,
                biCompression = Win32.BI_RGB,
            }
        };

        int stride = w * 4;
        int bufLen = stride * h;
        bits = Marshal.AllocHGlobal(bufLen);

        int lines = Win32.GetDIBits(
            memDc,
            hBitmap,
            0,
            (uint)h,
            bits,
            ref bmi,
            Win32.DIB_RGB_COLORS
        );
        if (lines == 0)
            return null;

        var wb = new WriteableBitmap(
            new PixelSize(w, h),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Opaque
        );
        using (var lk = wb.Lock())
        {
            unsafe
            {
                byte* src = (byte*)bits.ToPointer();
                byte* dst = (byte*)lk.Address;
                int dstStride = lk.RowBytes;
                int rowBytes = w * 4;
                for (int y = 0; y < h; y++)
                {
                    byte* dstRow = dst + (long)y * dstStride;
                    Buffer.MemoryCopy(src + (long)y * stride, dstRow, rowBytes, rowBytes);
                    for (int x = 0; x < w; x++)
                        dstRow[x * 4 + 3] = 255;
                    if (dstStride > rowBytes)
                        new Span<byte>(dstRow + rowBytes, dstStride - rowBytes).Clear();
                }
            }
        }
        return wb;
    }
}
