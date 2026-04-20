using System;

namespace TDS.PeekDesktop;

internal enum DesktopClickTarget
{
    NonDesktop,
    DesktopBackground,
    DesktopIcon
}

/// <summary>
/// Identifies whether a window handle belongs to the Windows desktop surface
/// (Progman, WorkerW with SHELLDLL_DefView, or transient desktop UI like menus).
/// </summary>
public static class DesktopDetector
{
    /// <summary>
    /// When true, clicks on empty taskbar area also trigger desktop peek.
    /// </summary>
    public static bool PeekOnTaskbarClick { get; set; }

    /// <summary>
    /// Classifies a click as hitting the desktop wallpaper, a desktop icon,
    /// or something unrelated to the desktop.
    /// </summary>
    internal static DesktopClickTarget GetClickTarget(IntPtr hwnd, NativeMethods.POINT point)
    {
        if (PeekOnTaskbarClick && IsTaskbarBlankAreaWindow(hwnd, point))
        {
            AppDiagnostics.Log("Taskbar blank-area click detected");
            return DesktopClickTarget.DesktopBackground;
        }

        if (!IsDesktopRelatedWindow(hwnd))
        {
            AppDiagnostics.Log($"Desktop relationship check failed at {NativeMethods.DescribePoint(point)}");
            AppDiagnostics.Log($"Desktop relationship reason: {GetDesktopRelationshipReason(hwnd)}");

            if (NativeMethods.TryGetAccessibleDetailsAtPoint(point, out int role, out string name))
                AppDiagnostics.Log($"Non-desktop accessibility probe: role=0x{role:X} name=\"{name}\"");

            return DesktopClickTarget.NonDesktop;
        }

        if (IsDesktopIconWindow(hwnd))
        {
            if (NativeMethods.TryIsDesktopListViewItemAtPoint(hwnd, point, out bool isOnDesktopItem))
            {
                AppDiagnostics.Log($"Desktop list-view hit-test: {(isOnDesktopItem ? "icon" : "background")}");
                return isOnDesktopItem ? DesktopClickTarget.DesktopIcon : DesktopClickTarget.DesktopBackground;
            }

            if (NativeMethods.TryGetAccessibleDetailsAtPoint(point, out int role, out string name))
            {
                AppDiagnostics.Log($"Desktop accessibility hit-test: role=0x{role:X} name=\"{name}\"");
                if (role == NativeMethods.ROLE_SYSTEM_LISTITEM)
                    return DesktopClickTarget.DesktopIcon;
            }

            // LVM_HITTEST failed (permissions, timeout, etc.) — do not fall through to wallpaper or
            // double-clicks on icons mis-fire peek. MSAA fallback is disabled in baseline builds.
            AppDiagnostics.Log("Desktop SysListView32: hit-test unavailable; treating as icon area (conservative)");
            return DesktopClickTarget.DesktopIcon;
        }

        return DesktopClickTarget.DesktopBackground;
    }

    internal static string GetDesktopRelationshipReason(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return "window handle was zero";

        IntPtr current = hwnd;
        while (current != IntPtr.Zero)
        {
            string className = NativeMethods.GetWindowClassName(current);

            if (string.Equals(className, "Progman", StringComparison.OrdinalIgnoreCase))
                return $"matched Progman ancestor: {NativeMethods.DescribeWindow(current)}";

            if (string.Equals(className, "WorkerW", StringComparison.OrdinalIgnoreCase))
            {
                IntPtr shellView = NativeMethods.FindWindowEx(current, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (shellView != IntPtr.Zero)
                    return $"matched desktop WorkerW ancestor: {NativeMethods.DescribeWindow(current)}";
            }

            current = NativeMethods.GetParent(current);
        }

        return $"no desktop ancestor found. hierarchy={NativeMethods.DescribeWindowHierarchy(hwnd)}";
    }

    /// <summary>
    /// Checks whether the given window is part of the desktop surface
    /// by walking up its parent chain looking for Progman or the desktop WorkerW.
    /// </summary>
    public static bool IsDesktopRelatedWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return false;

        IntPtr current = hwnd;
        while (current != IntPtr.Zero)
        {
            string className = NativeMethods.GetWindowClassName(current);

            if (string.Equals(className, "Progman", StringComparison.OrdinalIgnoreCase))
                return true;

            if (string.Equals(className, "WorkerW", StringComparison.OrdinalIgnoreCase))
            {
                // Only the WorkerW that hosts SHELLDLL_DefView is the actual desktop.
                // Other WorkerW windows are unrelated shell helpers.
                IntPtr shellView = NativeMethods.FindWindowEx(
                    current, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (shellView != IntPtr.Zero)
                    return true;
            }

            current = NativeMethods.GetParent(current);
        }

        return false;
    }

    /// <summary>
    /// Returns true if the given foreground window should be treated as
    /// "the user is still on the desktop" — preventing a restore trigger.
    /// This includes the desktop itself plus transient UI (menus, tooltips).
    /// </summary>
    public static bool IsDesktopWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return true; // No foreground window — stay peeking

        string className = NativeMethods.GetWindowClassName(hwnd);

        // Context menus and tooltips are transient — treat as "still on desktop"
        if (string.Equals(className, "#32768", StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.Equals(className, "tooltips_class32", StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.Equals(className, "Shell_TrayWnd", StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.Equals(className, "Shell_SecondaryTrayWnd", StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.Equals(className, "TopLevelWindowForOverflowXamlIsland", StringComparison.OrdinalIgnoreCase))
            return true;

        return IsDesktopRelatedWindow(hwnd);
    }

    private static bool IsDesktopIconWindow(IntPtr hwnd)
    {
        IntPtr current = hwnd;
        while (current != IntPtr.Zero)
        {
            string className = NativeMethods.GetWindowClassName(current);
            if (string.Equals(className, "SysListView32", StringComparison.OrdinalIgnoreCase))
                return true;

            current = NativeMethods.GetParent(current);
        }

        return false;
    }

    /// <summary>
    /// Walk up the parent chain to find the taskbar root window (Shell_TrayWnd or Shell_SecondaryTrayWnd).
    /// </summary>
    private static IntPtr FindTaskbarRoot(IntPtr hwnd)
    {
        IntPtr current = hwnd;
        while (current != IntPtr.Zero)
        {
            string className = NativeMethods.GetWindowClassName(current);
            if (string.Equals(className, "Shell_TrayWnd", StringComparison.OrdinalIgnoreCase)
                || string.Equals(className, "Shell_SecondaryTrayWnd", StringComparison.OrdinalIgnoreCase))
                return current;
            current = NativeMethods.GetParent(current);
        }
        return IntPtr.Zero;
    }

    private static bool IsTaskbarBlankAreaWindow(IntPtr hwnd, NativeMethods.POINT screenPoint)
    {
        // Win11: WindowFromPoint often returns a child (e.g., MSTaskListWClass, tray icon)
        // Walk up to find the actual taskbar root window.
        IntPtr taskbarRoot = FindTaskbarRoot(hwnd);
        if (taskbarRoot == IntPtr.Zero)
            return false;

        // Now check if the point is on blank area of this taskbar.
        // Use ChildWindowFromPoint to find the real child at this location.
        NativeMethods.POINT clientPt = screenPoint;
        NativeMethods.ScreenToClient(taskbarRoot, ref clientPt);
        IntPtr child = NativeMethods.RealChildWindowFromPoint(taskbarRoot, clientPt);

        // If no child or child is the taskbar itself, it might be blank area (or composition overlay)
        if (child == IntPtr.Zero || child == taskbarRoot)
            return ClassifyTaskbarOverlayPoint(screenPoint);

        // Skip transparent composition overlays that span the whole taskbar
        string childClass = NativeMethods.GetWindowClassName(child);
        if (childClass.StartsWith("Windows.UI.Composition", StringComparison.OrdinalIgnoreCase)
            || childClass.StartsWith("Windows.UI.Input", StringComparison.OrdinalIgnoreCase))
        {
            AppDiagnostics.Log($"Taskbar blank check: overlay child class={childClass}; deferring to UIA");
            return ClassifyTaskbarOverlayPoint(screenPoint);
        }

        // Win10/11: ReBarWindow32 / task-list strip span the whole button area. Clicks on visual
        // "gaps" between icons still hit these container HWNDs first — must use UIA, not "not blank".
        if (IsTaskbarStripContainerClass(childClass))
        {
            AppDiagnostics.Log($"Taskbar blank check: container class={childClass}; deferring to UIA");
            return ClassifyTaskbarOverlayPoint(screenPoint);
        }

        // A specific leaf control (e.g. Start, Show desktop, a tray icon, clock) — not "blank strip"
        AppDiagnostics.Log($"Taskbar blank check: child=0x{child.ToInt64():X} class={childClass} — not blank");
        return false;
    }

    /// <summary>Large taskbar regions that own padding between buttons; RealChildWindowFromPoint
    /// still returns these for "empty" clicks between taskbar buttons.</summary>
    private static bool IsTaskbarStripContainerClass(string className)
    {
        if (string.Equals(className, "ReBarWindow32", StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.Equals(className, "MSTaskListWClass", StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.Equals(className, "MSTaskSwWClass", StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.Equals(className, "ToolbarWindow32", StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }

    private static bool ClassifyTaskbarOverlayPoint(NativeMethods.POINT screenPoint)
    {
        if (!UiAutomationCom.TryIsTaskbarElementInteractiveAtPoint(screenPoint, out bool isInteractive, out string description))
        {
            AppDiagnostics.Log($"Taskbar blank check: UIA unavailable at {NativeMethods.DescribePoint(screenPoint)}; treating as non-blank");
            return false;
        }

        AppDiagnostics.Log($"Taskbar UIA classification at {NativeMethods.DescribePoint(screenPoint)}: {description}");
        return !isInteractive;
    }
}
