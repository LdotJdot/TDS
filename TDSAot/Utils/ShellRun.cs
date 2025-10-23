using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading.Tasks;
using TDSAot;
using TDSAot.State;
using TDSAot.Utils;

namespace TDS.Utils
{

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    internal interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, IntPtr pfd, short fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
        void Resolve(IntPtr hwnd, int fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    public class ShellLink { };

    public class StartUpUtils
    {
        static string StartPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), Path.ChangeExtension(AppOption.CurrentFileName, ".lnk"));
        public static bool IsStartUp => File.Exists(StartPath);

        public static void SwitchStartUp()
        {
            if (IsStartUp)
            {
                RemoveStartUp();
                Message.ShowWaringOk("Success", "Auto startup removed.");
            }
            else
            {
                try
                {
                    StartUp();
                    Message.ShowWaringOk("Success", "Auto startup added.");
                }
                catch (Exception ex)
                {
                    Message.ShowWaringOk("Failed", "Failed to add to startup folder.");
                }
            }
        }

        private static void StartUp()
        {

            var path = StartPath;
            var appPath = Path.Combine(AppOption.CurrentFolder, AppOption.CurrentFileName);
            RemoveStartUp();

            var lnk = (IShellLinkW)new ShellLink();

            lnk.SetPath(appPath);
            lnk.SetArguments("/hide"); // silent
            lnk.SetWorkingDirectory(AppOption.CurrentFolder);
            ((IPersistFile)lnk).Save(path, false);

        }

        private static void RemoveStartUp()
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), Path.ChangeExtension(AppOption.CurrentFileName, ".lnk"));
            try
            {
                File.Delete(path);
            }
            catch
            {

            }
        }

        // In many clients, the following way not work.
        private void RegisterInStartup(bool isChecked)
        {
            // 不起作用
            var path = Environment.ProcessPath;
            if (Message.ShowYesNo("startup", "sure?"))
            {
                RegistryKey? registryKey = Registry.CurrentUser.OpenSubKey
                        ("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                if (isChecked)
                {

                    registryKey?.SetValue("ApplicationName", Environment.ProcessPath);
                }
                else
                {
                    registryKey?.DeleteValue("ApplicationName");
                }
            }
        }
    }
}