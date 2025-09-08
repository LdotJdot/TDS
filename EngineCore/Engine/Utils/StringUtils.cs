using TDSNET.Engine.Actions.USN;
using TDSNET.Utils;

namespace TDSNET.Engine.Utils
{
    public class StringUtils
    {
        protected const ulong ROOT_FILE_REFERENCE_NUMBER = 0x5000000000005L;

        public static ReadOnlySpan<char> GetPathStr(FrnFileOrigin f, ReadOnlySpan<char> tailStr)
        {
            if (f.parentFrn != null)
            {
                tailStr = string.Concat("\\", PathHelper.getfileName(f.fileName), tailStr).AsSpan();
                return GetPathStr(f.parentFrn, tailStr);
            }
            else
            {
                var path = new char[1 + 1 + tailStr.Length];

                path[0] = f.fileName[1];
                path[1] = ':';
                Array.Copy(tailStr.ToArray(), 0, path, 2, tailStr.Length);

                return path.AsSpan();
            }
        }

        public static ReadOnlySpan<char> GetExtension(ReadOnlySpan<char> fullPath)
        {
            var index = fullPath.IndexOf('.');
            if (index != -1 && index < fullPath.Length - 2)
            {
                var ext = fullPath.Slice(index, fullPath.Length - index);
                return ext;
            }
            else
            {
                return ReadOnlySpan<char>.Empty;
            }
        }
    }
}