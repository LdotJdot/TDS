using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using TDSNET.Engine.Actions.USN;
using TDSNET.Utils;

namespace TDSAot.Utils
{
    internal enum FileActionType
    {
        Open,
        OpenFolder,
        Delete,
        CopyFile,
        CopyPath
    }

    internal class FileAction
    {
        private Action<FrnFileOrigin?>? updateRecord;

        public FileAction(Action<FrnFileOrigin?>? updateRecord)
        {
            this.updateRecord = updateRecord;
        }

        internal void Execute(FrnFileOrigin file, FileActionType action)
        {
            switch (action)
            {
                case FileActionType.Open:
                    Open(file);
                    updateRecord?.Invoke(file);
                    return;

                case FileActionType.OpenFolder:
                    OpenFolder(file);
                    updateRecord?.Invoke(file);
                    return;

                case FileActionType.Delete:
                    Delete(file);
                    return;

                default:
                    return;
            }
        }

        internal void Execute(FrnFileOrigin[] files, FileActionType action)
        {
            foreach (var file in files)
            {
                Execute(file, action);
                updateRecord?.Invoke(file);
            }
        }

        private void Delete(FrnFileOrigin file)
        {
            if (!(file == null))
            {
                var path = file.FilePath;
                if (Message.ShowYesNo("Delete?", file.FilePath))
                {
                    try
                    {
                        FileAttributes attr = File.GetAttributes(path);
                        if (attr == FileAttributes.Directory)
                        {
                            Directory.Delete(path, true);
                        }
                        else
                        {
                            File.Delete(path);
                        }
                    }
                    catch (Exception ex)
                    {
                        Message.ShowWaringOk("Delete failed", ex.Message);
                    }
                }
            }
        }

        private void OpenFolder(FrnFileOrigin file)
        {
            if (file != null)
            {
                string path = string.Empty;
                try
                {
                    path = Path.GetDirectoryName(PathHelper.GetPath((FrnFileOrigin)file).ToString());

                    ExplorerFile(PathHelper.GetPath((FrnFileOrigin)file).ToString());
                    updateRecord?.Invoke(file);
                }
                catch (Exception ex)
                {
                    Message.ShowWaringOk("Open failed", ex.Message);
                }
            }
        }

        [DllImport("shell32.dll", ExactSpelling = true)]
        private static extern void ILFree(IntPtr pidlList);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern IntPtr ILCreateFromPathW(string pszPath);

        [DllImport("shell32.dll", ExactSpelling = true)]
        private static extern int SHOpenFolderAndSelectItems(IntPtr pidlList, uint cild, IntPtr children, uint dwFlags);

        private static void ExplorerFile(string filePath)
        {
            if (!File.Exists(filePath) && !Directory.Exists(filePath))
                return;

            if (Directory.Exists(filePath))
                Process.Start(@"explorer.exe", "/select,\"" + filePath + "\"");
            else
            {
                IntPtr pidlList = ILCreateFromPathW(filePath);
                if (pidlList != IntPtr.Zero)
                {
                    try
                    {
                        Marshal.ThrowExceptionForHR(SHOpenFolderAndSelectItems(pidlList, 0, IntPtr.Zero, 0));
                    }
                    finally
                    {
                        ILFree(pidlList);
                    }
                }
            }
        }

        private static void Open(FrnFileOrigin file)
        {
            if (!(file == null))
            {
                string path = string.Empty;
                //识别
                try
                {
                    path = PathHelper.GetPath((FrnFileOrigin)file).ToString();
                }
                catch (Exception ex)
                {
                    Message.ShowWaringOk("Open failed", ex.Message);
                }

                var ext = Path.GetExtension(PathHelper.getfilePath(file.fileName));
                if (ext.Length == 0)
                {
                    if (Directory.Exists(path))
                    {
                        try
                        {
                            Process.Start("explorer.exe", path);
                        }
                        catch (Exception ex)
                        {
                            Message.ShowWaringOk("Open failed", ex.Message);
                        }
                    }
                    else
                    {
                        Message.ShowWaringOk("Open failed", "Path not existed.");
                    }
                }
                else
                {
                    if (File.Exists(path))
                    {
                        try
                        {
                            Process p = new System.Diagnostics.Process();
                            p.StartInfo.WorkingDirectory = Path.GetDirectoryName(path);
                            p.StartInfo.UseShellExecute = true;
                            p.StartInfo.FileName = path;
                            p.StartInfo.WindowStyle = ProcessWindowStyle.Normal;
                            p.Start();
                        }
                        catch (Exception ex)
                        {
                            Message.ShowWaringOk("Open failed", ex.Message);
                        }
                    }
                    else
                    {
                        Message.ShowWaringOk("Open failed", "Path not existed.");
                    }
                }
            }
        }
    }

    public class ShellFileOpener
    {
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr ShellExecute(
            IntPtr hwnd,
            string lpOperation,
            string lpFile,
            string lpParameters,
            string lpDirectory,
            int nShowCmd);

        private const int SW_SHOWNORMAL = 1;

        public static void OpenFileWithShell(string filePath)
        {
            if (!File.Exists(filePath) && !Directory.Exists(filePath))
            {
                throw new FileNotFoundException("Path not existed.", filePath);
            }

            // 使用ShellExecute打开文件
            ShellExecute(IntPtr.Zero, "open", filePath, null, null, SW_SHOWNORMAL);
        }
    }
}