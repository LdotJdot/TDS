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
    public class StartUpUtils
    {
        [DllImport("ProxyLibs.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]//ansi统一
        private static extern int CreateLink(string targetPath, string path, string arg, string workDir);

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

            var ptrTargetPath=Marshal.StringToHGlobalUni(path);
            var ptrSourcePath=Marshal.StringToHGlobalUni(appPath);
            var ptrArg=Marshal.StringToHGlobalUni("/hide");
            var ptrWorkDir = Marshal.StringToHGlobalUni(AppOption.CurrentFolder);
            var inpt=CreateLink(path, appPath, "/hide", AppOption.CurrentFolder);
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