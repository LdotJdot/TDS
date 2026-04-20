using System;
using System.Runtime.InteropServices;

namespace TDS.PeekDesktop;

internal static partial class UiAutomationCom
{
    internal const int UIA_ButtonControlTypeId = 50000;
    internal const int UIA_EditControlTypeId = 50004;
    internal const int UIA_ListItemControlTypeId = 50007;
    internal const int UIA_MenuItemControlTypeId = 50011;
    internal const int UIA_TabItemControlTypeId = 50019;
    internal const int UIA_ToolBarControlTypeId = 50021;
    internal const int UIA_CustomControlTypeId = 50025;
    internal const int UIA_SplitButtonControlTypeId = 50031;
    internal const int UIA_WindowControlTypeId = 50032;
    internal const int UIA_PaneControlTypeId = 50033;

    private const uint CLSCTX_INPROC_SERVER = 0x1;
    private const uint COINIT_APARTMENTTHREADED = 0x2;
    private const int RPC_E_CHANGED_MODE = unchecked((int)0x80010106);

    // Slots are taken from the Windows SDK IUIAutomation/IUIAutomationElement vtables.
    private const int IUIAutomation_ElementFromPoint_Slot = 7;
    private const int IUIAutomationElement_CurrentControlType_Slot = 21;
    private const int IUIAutomationElement_CurrentLocalizedControlType_Slot = 22;
    private const int IUIAutomationElement_CurrentName_Slot = 23;
    private const int IUIAutomationElement_CurrentAutomationId_Slot = 29;
    private const int IUIAutomationElement_CurrentClassName_Slot = 30;
    private const int IUIAutomationElement_CurrentNativeWindowHandle_Slot = 36;
    private const int IUIAutomationElement_CurrentFrameworkId_Slot = 40;

    private static readonly Guid CLSID_CUIAutomation = new("FF48DBA4-60EF-4201-AA87-54103EEF594E");
    private static readonly Guid IID_IUIAutomation = new("30CBE57D-D9D0-452A-AB13-7AC5AC4825EE");

    public static bool TryIsTaskbarElementInteractiveAtPoint(
        NativeMethods.POINT point,
        out bool isInteractive,
        out string description)
    {
        isInteractive = true;
        description = "UIA unavailable";

        if (!TryGetElementDetailsAtPoint(
                point,
                out int controlType,
                out string name,
                out string className,
                out string automationId,
                out string frameworkId,
                out string localizedControlType,
                out IntPtr nativeWindowHandle))
        {
            return false;
        }

        bool neutralContainer =
            controlType is UIA_PaneControlTypeId or UIA_ToolBarControlTypeId or UIA_WindowControlTypeId or UIA_CustomControlTypeId;

        bool hasInteractiveType =
            controlType is UIA_ButtonControlTypeId
                or UIA_SplitButtonControlTypeId
                or UIA_EditControlTypeId
                or UIA_ListItemControlTypeId
                or UIA_MenuItemControlTypeId
                or UIA_TabItemControlTypeId;

        bool hasMeaningfulIdentity =
            !string.IsNullOrWhiteSpace(name)
            || !string.IsNullOrWhiteSpace(automationId)
            || !string.IsNullOrWhiteSpace(className);

        isInteractive = hasInteractiveType || (hasMeaningfulIdentity && !neutralContainer);
        description =
            $"type={controlType} localizedType=\"{localizedControlType}\" name=\"{name}\" class=\"{className}\" aid=\"{automationId}\" framework=\"{frameworkId}\" hwnd=0x{nativeWindowHandle.ToInt64():X} interactive={isInteractive}";
        return true;
    }

    public static bool TryGetElementDetailsAtPoint(
        NativeMethods.POINT point,
        out int controlType,
        out string name,
        out string className,
        out string automationId,
        out string frameworkId,
        out string localizedControlType)
    {
        return TryGetElementDetailsAtPoint(
            point,
            out controlType,
            out name,
            out className,
            out automationId,
            out frameworkId,
            out localizedControlType,
            out _);
    }

    private static bool TryGetElementDetailsAtPoint(
        NativeMethods.POINT point,
        out int controlType,
        out string name,
        out string className,
        out string automationId,
        out string frameworkId,
        out string localizedControlType,
        out IntPtr nativeWindowHandle)
    {
        controlType = 0;
        name = string.Empty;
        className = string.Empty;
        automationId = string.Empty;
        frameworkId = string.Empty;
        localizedControlType = string.Empty;
        nativeWindowHandle = IntPtr.Zero;

        int initHr = CoInitializeEx(IntPtr.Zero, COINIT_APARTMENTTHREADED);
        bool shouldUninitialize = initHr >= 0;
        if (initHr < 0 && initHr != RPC_E_CHANGED_MODE)
        {
            AppDiagnostics.Log($"UIA initialization failed: 0x{initHr:X8}");
            return false;
        }

        IntPtr automationPtr = IntPtr.Zero;
        IntPtr elementPtr = IntPtr.Zero;

        try
        {
            int hr = CoCreateInstance(
                in CLSID_CUIAutomation,
                IntPtr.Zero,
                CLSCTX_INPROC_SERVER,
                in IID_IUIAutomation,
                out automationPtr);

            if (hr < 0 || automationPtr == IntPtr.Zero)
            {
                AppDiagnostics.Log($"UIA CoCreateInstance failed: 0x{hr:X8}");
                return false;
            }

            hr = ElementFromPoint(automationPtr, point, out elementPtr);
            if (hr < 0 || elementPtr == IntPtr.Zero)
            {
                AppDiagnostics.Log($"UIA ElementFromPoint failed at {NativeMethods.DescribePoint(point)}: 0x{hr:X8}");
                return false;
            }

            _ = GetIntProperty(elementPtr, IUIAutomationElement_CurrentControlType_Slot, out controlType);
            _ = GetBstrProperty(elementPtr, IUIAutomationElement_CurrentName_Slot, out name);
            _ = GetBstrProperty(elementPtr, IUIAutomationElement_CurrentClassName_Slot, out className);
            _ = GetBstrProperty(elementPtr, IUIAutomationElement_CurrentAutomationId_Slot, out automationId);
            _ = GetBstrProperty(elementPtr, IUIAutomationElement_CurrentFrameworkId_Slot, out frameworkId);
            _ = GetBstrProperty(elementPtr, IUIAutomationElement_CurrentLocalizedControlType_Slot, out localizedControlType);
            _ = GetIntPtrProperty(elementPtr, IUIAutomationElement_CurrentNativeWindowHandle_Slot, out nativeWindowHandle);
            return true;
        }
        catch (Exception ex)
        {
            AppDiagnostics.Log($"UIA probe failed: {ex.Message}");
            return false;
        }
        finally
        {
            if (elementPtr != IntPtr.Zero)
                Marshal.Release(elementPtr);

            if (automationPtr != IntPtr.Zero)
                Marshal.Release(automationPtr);

            if (shouldUninitialize)
                CoUninitialize();
        }
    }

    private static unsafe int ElementFromPoint(IntPtr automationPtr, NativeMethods.POINT point, out IntPtr elementPtr)
    {
        IntPtr result = IntPtr.Zero;
        nint* vtable = *(nint**)automationPtr;
        var method = (delegate* unmanaged[Stdcall]<IntPtr, NativeMethods.POINT, IntPtr*, int>)vtable[IUIAutomation_ElementFromPoint_Slot];
        int hr = method(automationPtr, point, &result);
        elementPtr = result;
        return hr;
    }

    private static unsafe int GetIntProperty(IntPtr elementPtr, int slot, out int value)
    {
        int result = 0;
        nint* vtable = *(nint**)elementPtr;
        var method = (delegate* unmanaged[Stdcall]<IntPtr, int*, int>)vtable[slot];
        int hr = method(elementPtr, &result);
        value = result;
        return hr;
    }

    private static unsafe int GetIntPtrProperty(IntPtr elementPtr, int slot, out IntPtr value)
    {
        IntPtr result = IntPtr.Zero;
        nint* vtable = *(nint**)elementPtr;
        var method = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int>)vtable[slot];
        int hr = method(elementPtr, &result);
        value = result;
        return hr;
    }

    private static unsafe int GetBstrProperty(IntPtr elementPtr, int slot, out string value)
    {
        IntPtr bstr = IntPtr.Zero;
        try
        {
            nint* vtable = *(nint**)elementPtr;
            var method = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int>)vtable[slot];
            int hr = method(elementPtr, &bstr);
            value = bstr != IntPtr.Zero ? Marshal.PtrToStringBSTR(bstr) ?? string.Empty : string.Empty;
            return hr;
        }
        finally
        {
            if (bstr != IntPtr.Zero)
                Marshal.FreeBSTR(bstr);
        }
    }

    [LibraryImport("ole32.dll")]
    private static partial int CoInitializeEx(IntPtr pvReserved, uint dwCoInit);

    [LibraryImport("ole32.dll")]
    private static partial void CoUninitialize();

    [LibraryImport("ole32.dll")]
    private static partial int CoCreateInstance(
        in Guid rclsid,
        IntPtr pUnkOuter,
        uint dwClsContext,
        in Guid riid,
        out IntPtr ppv);
}
