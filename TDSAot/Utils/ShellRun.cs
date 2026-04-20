using Microsoft.Win32;
using System;
using System.IO;
using TDSAot.State;
using TDSAot.Utils;

namespace TDS.Utils
{
    public static class StartUpUtils
    {
        private const string RunKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string RegistryValueName = "TDS";

        private static string LegacyShortcutPath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                Path.ChangeExtension(AppOption.CurrentFileName, ".lnk"));

        private static string StartupCommand
        {
            get
            {
                string exePath = Environment.ProcessPath;
                if (string.IsNullOrWhiteSpace(exePath))
                    exePath = Path.Combine(AppOption.CurrentFolder, AppOption.CurrentFileName);
                return $"\"{exePath}\" --hide";
            }
        }

        /// <summary>
        /// True when HKCU Run contains our entry, or a legacy Startup-folder shortcut exists (pre-registry migration).
        /// </summary>
        public static bool IsStartUp
        {
            get
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
                    string? val = key?.GetValue(RegistryValueName) as string;
                    if (!string.IsNullOrWhiteSpace(val)
                        && string.Equals(val.Trim(), StartupCommand.Trim(), StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                catch
                {
                    // ignore
                }

                return File.Exists(LegacyShortcutPath);
            }
        }

        public static void SwitchStartUp()
        {
            if (IsStartUp)
            {
                UnregisterRunKey();
                Message.ShowWaringOk("Success", "Auto startup removed.");
            }
            else
            {
                try
                {
                    RegisterRunKey();
                    Message.ShowWaringOk("Success", "Auto startup added.");
                }
                catch
                {
                    Message.ShowWaringOk("Failed", "Failed to update Windows startup.");
                }
            }
        }

        private static void RegisterRunKey()
        {
            TryDeleteLegacyShortcut();
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath)
                ?? throw new InvalidOperationException("Run key unavailable.");
            key.SetValue(RegistryValueName, StartupCommand);
        }

        private static void UnregisterRunKey()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
                key?.DeleteValue(RegistryValueName, throwOnMissingValue: false);
            }
            catch
            {
                // ignore
            }

            TryDeleteLegacyShortcut();
        }

        private static void TryDeleteLegacyShortcut()
        {
            try
            {
                if (File.Exists(LegacyShortcutPath))
                    File.Delete(LegacyShortcutPath);
            }
            catch
            {
                // ignore
            }
        }
    }
}
