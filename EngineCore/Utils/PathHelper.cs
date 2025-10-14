using System.Runtime.InteropServices;
using System.Text;
using TDSNET.Engine.Actions;
using TDSNET.Engine.Actions.USN;
using TDSNET.Engine.Utils;

namespace TDSNET.Utils
{
    public static class PathHelper
    {
        private static string FormatBytes(long bytes)
        {
            return bytes switch
            {
                < 1024L => $"{bytes:0.###} B",
                < 1048576L => $"{(bytes / 1024.0):0.###} KB",
                < 1073741824L => $"{(bytes / 1048576.0):0.###} MB",
                < 1099511627776L => $"{(bytes / 1073741824.0):0.###} GB",
                < 1125899906842624L => $"{(bytes / 1099511627776.0):0.###} TB",
                _ => $"{(bytes / 1125899906842624.0):0.###} PB"
            };
        }


        /// <summary>
        ///  二进制转换逻辑搜索使用
        /// </summary>
        /// <param name="txt"></param>
        /// <returns></returns>
        public static string getFileInfoStr(FrnFileOrigin f)
        {
            var fileInfo = new FileInfo(GetPath(f).ToString());

            if (fileInfo.Exists)
            {
#if DEBUG
                return $"{FormatBytes(fileInfo.Length)}  {fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")} {f.fileReferenceNumber} {f.parentFileReferenceNumber}";
#else
                return $"{FormatBytes(fileInfo.Length)}  {fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")}";
#endif
            }
            else
            {
                return null;
            }
        }


        public static ReadOnlySpan<char> getfileNameNormalize(ReadOnlySpan<char> filename)
        {
            filename = filename.Trim('|');

            var index = filename.IndexOf('|');
            if (index < 0)
            {
                return ReadOnlySpan<char>.Empty;
            }
            else if (index + 1 < filename.Length)
            {
                return filename.Slice(index + 1, filename.Length - index - 1);
            }
            else
            {
                return ReadOnlySpan<char>.Empty;
            }
        }

        public static ReadOnlySpan<char> getfileName(ReadOnlySpan<char> filename)
        {
            filename = filename.Trim('|');
            
            var index = filename.IndexOf('|');
            if (index < 0)
            {
                return filename;
            }
            else
            {
                return filename.Slice(0, index);
            }
        }

        public static ReadOnlySpan<char> GetPath(FrnFileOrigin f)
        {
            var path = StringUtils.GetPathStr(f, ReadOnlySpan<char>.Empty);
            if (path.EndsWith(":".AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                var pathChar = new char[path.Length + 1];
                Array.Copy(path.ToArray(), pathChar, path.Length);
                pathChar[pathChar.Length - 1] = '\\';
                return pathChar.AsSpan();
            }
            else
            {
                return path;
            }
        }

        //#region 获取所有用户文件夹
        [DllImport("shfolder.dll", CharSet = CharSet.Auto)]
        private static extern int SHGetFolderPath(IntPtr hwndOwner, int nFolder, IntPtr hToken, int dwFlags, StringBuilder lpszPath);

        private const int MAX_PATH = 260;

        private const int CSIDL_COMMON_PROGRAMS = 0x0017;

        static PathHelper()
        {
            USER_PROGRAM_PATH = System.Environment.GetFolderPath(Environment.SpecialFolder.Programs, Environment.SpecialFolderOption.None);
            ALLUSER_PROGRAM_PATH = GetAllUsersDesktopFolderPath();
        }

        private static string GetAllUsersDesktopFolderPath()
        {
            StringBuilder sbPath = new StringBuilder(MAX_PATH);
            SHGetFolderPath(IntPtr.Zero, CSIDL_COMMON_PROGRAMS, IntPtr.Zero, 0, sbPath);
            return sbPath.ToString();
        }

        public static readonly string USER_PROGRAM_PATH = "";//获取环境目录变量USER
        public static readonly string ALLUSER_PROGRAM_PATH = "";   //获取环境目录变量ALLUSER
    }
}