using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TDSAot.Utils
{
    internal static class DriverUtils
    {
        public static IEnumerable<DriveInfo> GetAllFixedNtfsDrives()
        {
            return DriveInfo.GetDrives()
                .Where(d => (d.IsReady == true && (d.DriveType == DriveType.Fixed || d.DriveType == DriveType.Removable) && d.DriveFormat.ToUpperInvariant() == "NTFS"));//固定磁盘
        }
    }
}