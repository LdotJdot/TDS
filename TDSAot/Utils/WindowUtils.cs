using System;
using System.Runtime.InteropServices;

namespace TDSAot.Utils
{
    internal class WindowUtils
    {
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr ProcessId);

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        private const int SW_SHOW = 5;
        private const int SW_RESTORE = 9;

        public static void ForceForegroundWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;

            uint foregroundThreadId = GetWindowThreadProcessId(GetForegroundWindow(), IntPtr.Zero);
            uint currentThreadId = GetCurrentThreadId();

            if (foregroundThreadId != currentThreadId)
            {
                AttachThreadInput(currentThreadId, foregroundThreadId, true);
                SetForegroundWindow(hWnd);
                AttachThreadInput(currentThreadId, foregroundThreadId, false);
            }
            else
            {
                SetForegroundWindow(hWnd);
            }

            ShowWindow(hWnd, SW_SHOW);
        }
    }
}