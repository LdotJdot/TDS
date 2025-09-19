namespace TDSAot.Utils
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices;
    using TDSAot.State;

    public class FilePropertiesOpener
    {
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern bool ShellExecuteEx(ref SHELLEXECUTEINFO lpExecInfo);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHELLEXECUTEINFO
        {
            public int cbSize;
            public uint fMask;
            public IntPtr hwnd;

            [MarshalAs(UnmanagedType.LPTStr)]
            public string lpVerb;

            [MarshalAs(UnmanagedType.LPTStr)]
            public string lpFile;

            [MarshalAs(UnmanagedType.LPTStr)]
            public string lpParameters;

            [MarshalAs(UnmanagedType.LPTStr)]
            public string lpDirectory;

            public int nShow;
            public IntPtr hInstApp;
            public IntPtr lpIDList;

            [MarshalAs(UnmanagedType.LPTStr)]
            public string lpClass;

            public IntPtr hkeyClass;
            public uint dwHotKey;
            public IntPtr hIcon;
            public IntPtr hProcess;
        }

        private const uint SEE_MASK_INVOKEIDLIST = 0x0000000C;
        private const int SW_SHOW = 5;

        /// <summary>
        /// 打开文件的属性对话框
        /// </summary>
        /// <param name="filePath">文件完整路径</param>
        /// <returns>是否成功</returns>
        public static bool ShowFileProperties(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
            {
                return false;
            }

            SHELLEXECUTEINFO info = new SHELLEXECUTEINFO();
            info.cbSize = Marshal.SizeOf(info);
            info.lpVerb = "properties";
            info.lpFile = filePath;
            info.nShow = SW_SHOW;
            info.fMask = SEE_MASK_INVOKEIDLIST;
            StaticState.CanBeHide = false;
            return ShellExecuteEx(ref info);
        }

        [DllImport("shell32.dll", SetLastError = true)]
        private static extern void SHOpenWithDialog(IntPtr hWndParent, ref OPENASINFO oOAI);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct OPENASINFO
        {
            [MarshalAs(UnmanagedType.LPTStr)]
            public string pcszFile;

            [MarshalAs(UnmanagedType.LPTStr)]
            public string pcszClass;

            public uint oaifInFlags;
        }

        private const uint OAIF_ALLOW_REGISTRATION = 0x0001;
        private const uint OAIF_REGISTER_EXT = 0x0002;
        private const uint OAIF_EXEC = 0x0004;
        private const uint OAIF_FILE_IS_URI = 0x0008;

        public static void ShowFileOpenWith(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Message.ShowWaringOk("文件不存在", filePath);
                return;
            }

            OPENASINFO info = new OPENASINFO();
            info.pcszFile = filePath;
            info.pcszClass = string.Empty;
            info.oaifInFlags = OAIF_ALLOW_REGISTRATION | OAIF_EXEC;
            StaticState.CanBeHide = false;
            SHOpenWithDialog(IntPtr.Zero, ref info);
        }
    }
}