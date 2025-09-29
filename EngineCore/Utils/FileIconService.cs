using Microsoft.IO;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using Bitmap = Avalonia.Media.Imaging.Bitmap;

public static class FileIconService
{
    // Win32 常量
    private const uint SHGFI_ICON = 0x000000100;

    private const uint SHGFI_SMALLICON = 0x000000001;
    private const uint SHGFI_LARGEICON = 0x000000000;
    private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;

    // 文件属性常量
    private const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr SHGetFileInfo(
        string pszPath,
        uint dwFileAttributes,
        ref SHFILEINFO psfi,
        int cbSizeFileInfo,
        uint uFlags
    );

    [DllImport("user32.dll")]
    public static extern bool DestroyIcon(IntPtr hIcon);

    public static  Bitmap? GetIcon(string filePath)
    {
        return GetFileIcon(filePath, true);
    }

    public static Bitmap? GetFileIcon(string filePath, bool largeIcon)
    {
        var ext=Path.GetExtension(filePath).ToLower();
        if (string.IsNullOrWhiteSpace(ext))
        {
            if (filePath.Length < 5)
            {
                ext = "@driver";
            }
            else
            {
                ext = "@folder";
            }
        }

        if (ext != ".lnk" && ext != ".exe" && ext != ".ico")
        {
            if (GetCache(ext, out var bitmap))
            {
                return bitmap;
            }
        }

        IntPtr iconPtr = IntPtr.Zero;
        try
        {
            var shfi = new SHFILEINFO();
            uint flags = SHGFI_ICON | (largeIcon ? SHGFI_LARGEICON : SHGFI_SMALLICON);

            if (!ext.StartsWith("@") && (ext != ".lnk" && ext != ".exe" && ext != ".ico"))
            {
                flags |= SHGFI_USEFILEATTRIBUTES;
                SHGetFileInfo(Path.GetExtension(filePath), FILE_ATTRIBUTE_NORMAL,
                             ref shfi, Marshal.SizeOf(shfi), flags);
            }
            else
            {
                SHGetFileInfo(filePath, 0, ref shfi, Marshal.SizeOf(shfi), flags);
            }
            iconPtr = shfi.hIcon;
            if (iconPtr != IntPtr.Zero)
            {
                // 使用 System.Drawing.Icon
                using var icon = Icon.FromHandle(iconPtr);

                // 转换为 System.Drawing.Bitmap
                using var systemBitmap = icon.ToBitmap();

                // 转换为 Avalonia Bitmap
                var bitMap= ConvertToAvaloniaBitmap(systemBitmap);
                if (ext != ".lnk" && ext != ".exe" && ext != ".ico")
                {
                    SetCache(ext, bitMap);
                }
                return bitMap;
            }
        }
        catch (Exception ex)
        {
        }
        finally
        {
            if (iconPtr != IntPtr.Zero) DestroyIcon(iconPtr);
        }

        return null;
    }

    //const int maxCache = 500;

    static void SetCache(string ext,Bitmap? icon)
    {
        if (icon == null) return;


        if (!iconCache.ContainsKey(ext))
        {
            //if (iconCache.Count() > maxCache)
            //{
            //    foreach (var item in iconCache)
            //    {
            //        item.Value.Dispose();
            //    }
            //    iconCache.Clear();
            //}

            iconCache.TryAdd(ext, icon);
        }
    }

    static bool GetCache(string ext, out Bitmap? icon)
    {
        return iconCache.TryGetValue(ext, out icon);       
    }

    static ConcurrentDictionary<string , Bitmap> iconCache=new ConcurrentDictionary<string , Bitmap>();


    private static readonly RecyclableMemoryStreamManager
                recyclableMemoryStreamManager =
                new RecyclableMemoryStreamManager(/*new RecyclableMemoryStreamManager.Options(2048, 2048,4096, 2048 * 500, 2048 * 500)*/);
    private static Bitmap? ConvertToAvaloniaBitmap(System.Drawing.Bitmap systemBitmap)
    {
        int targetSize = 32;

        try
        {
            using var memoryStream = recyclableMemoryStreamManager.GetStream();

            // 调整大小（可选）
            //if (systemBitmap.Width != targetSize || systemBitmap.Height != targetSize)
            //{
            //    using var resizedBitmap = new System.Drawing.Bitmap(systemBitmap,
            //        new Size(targetSize, targetSize));
            //    resizedBitmap.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Png);
            //}
            //else
            
            systemBitmap.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Png);
            

            memoryStream.Position = 0;
            return new Bitmap(memoryStream);
        }
        catch
        {
            return null;
        }
    }
}