using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;

namespace TDS.ScreenShot.UI.Services;

/// <summary>
/// Clipboard helper. On Windows we put a DIB on the clipboard directly so
/// pasting into Paint / Word / Discord works.
/// </summary>
public static class ClipboardService
{
    public static Task CopyBitmapAsync(Window window, Bitmap bitmap)
    {
        if (OperatingSystem.IsWindows())
        {
            try { CopyDibToClipboardWindows(bitmap); } catch { /* best effort */ }
        }
        return Task.CompletedTask;
    }

    private static void CopyDibToClipboardWindows(Bitmap bitmap)
    {
        bool localWb = false;
        if (bitmap is not WriteableBitmap wb)
        {
            using var ms = new MemoryStream();
            bitmap.Save(ms);
            ms.Position = 0;
            wb = WriteableBitmap.Decode(ms);
            localWb = true;
        }

        try
        {
            int w = wb.PixelSize.Width;
            int h = wb.PixelSize.Height;
            int stride = w * 4;
            int imageBytes = stride * h;
            int headerBytes = System.Runtime.InteropServices.Marshal.SizeOf<BITMAPINFOHEADER>();
            int total = headerBytes + imageBytes;

            IntPtr hGlobal = System.Runtime.InteropServices.Marshal.AllocHGlobal(total);
            try
            {
                var bmi = new BITMAPINFOHEADER
                {
                    biSize = (uint)headerBytes,
                    biWidth = w,
                    biHeight = h,
                    biPlanes = 1,
                    biBitCount = 32,
                    biCompression = 0,
                    biSizeImage = (uint)imageBytes,
                };
                System.Runtime.InteropServices.Marshal.StructureToPtr(bmi, hGlobal, false);

                using (var lk = wb.Lock())
                {
                    unsafe
                    {
                        byte* dst = (byte*)hGlobal + headerBytes;
                        byte* src = (byte*)lk.Address;
                        for (int y = 0; y < h; y++)
                        {
                            Buffer.MemoryCopy(src + (h - 1 - y) * stride, dst + y * stride, stride, stride);
                        }
                    }
                }

                if (!OpenClipboard(IntPtr.Zero)) return;
                try
                {
                    EmptyClipboard();
                    IntPtr hMem = SetClipboardData(8 /*CF_DIB*/, hGlobal);
                    _ = hMem;
                }
                finally
                {
                    CloseClipboard();
                }
            }
            catch
            {
                System.Runtime.InteropServices.Marshal.FreeHGlobal(hGlobal);
                throw;
            }
        }
        finally
        {
            if (localWb) wb.Dispose();
        }
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool CloseClipboard();

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool EmptyClipboard();

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);
}
