using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace TDS.PeekDesktop;

internal static class AppDiagnostics
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern void OutputDebugString(string lpOutputString);

    [Conditional("TRACE")]
    public static void Metric(string message)
    {
        WriteLine("BENCH", message);
    }

    [Conditional("TRACE")]
    public static void Log(string message)
    {
        WriteLine(null, message);
    }

    [Conditional("TRACE")]
    public static void LogWindow(string prefix, IntPtr hwnd)
    {
        Log($"{prefix}: {NativeMethods.DescribeWindow(hwnd)}");
    }

    private static void WriteLine(string? category, string message)
    {
        string prefix = category is null ? "TDS.PeekDesktop" : $"TDS.PeekDesktop {category}";
        string line = $"[{prefix} {DateTime.Now:HH:mm:ss.fff}] {message}";
        Trace.WriteLine(line);
        OutputDebugString(line);
    }
}
