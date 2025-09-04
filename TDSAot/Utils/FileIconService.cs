using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

public static class FileIconService
{
    private static Dictionary<string, Stream> iconCache = new Dictionary<string, Stream>();

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

    public static Stream GetIcon(string filePath)
    {
        return GetFileIcon(filePath, true) ?? Stream.Null;
    }

    // 上面的 Win32 API 定义...

    public static Stream? GetFileIcon(string filePath, bool largeIcon = false)
    {
        if (string.IsNullOrEmpty(filePath))
            return null;

        try
        {
            var shfi = new SHFILEINFO();
            uint flags = SHGFI_ICON | (largeIcon ? SHGFI_LARGEICON : SHGFI_SMALLICON);

            // 如果文件不存在，使用文件属性模式
            if (!File.Exists(filePath))
            {
                flags |= SHGFI_USEFILEATTRIBUTES;
                SHGetFileInfo(Path.GetExtension(filePath), FILE_ATTRIBUTE_NORMAL,
                             ref shfi, Marshal.SizeOf(shfi), flags);
            }
            else
            {
                SHGetFileInfo(filePath, 0, ref shfi, Marshal.SizeOf(shfi), flags);
            }

            if (shfi.hIcon != IntPtr.Zero)
            {
                // 将 HICON 转换为 Avalonia Bitmap
                var bitmap = ConvertHIconToStream(shfi.hIcon);
                DestroyIcon(shfi.hIcon); // 释放资源
                return bitmap;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"获取文件图标失败: {ex.Message}");
        }

        return null;
    }

    private static Stream? ConvertHIconToStream(IntPtr hIcon)
    {
        // 这里需要将 HICON 转换为流
        // 可以使用 System.Drawing.Icon 作为中间步骤（如果可用）
        try
        {
            // 方法1：使用 System.Drawing（如果引用 Windows 兼容包）
            var icon = System.Drawing.Icon.FromHandle(hIcon);
            using var memoryStream = new MemoryStream();
            icon.Save(memoryStream);
            memoryStream.Position = 0;
            return memoryStream;
        }
        catch
        {
            return null;
        }
    }
}