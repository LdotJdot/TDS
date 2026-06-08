namespace TDS.ScreenShot.Core.Capture;

/// <summary>
/// Factory returning the best available <see cref="ICaptureService"/> for the current OS.
/// </summary>
public static class CaptureFactory
{
    public static ICaptureService Create()
    {
        if (OperatingSystem.IsWindows())
            return new Win32CaptureService();

        // Other platforms can be plugged in by referencing additional projects.
        throw new PlatformNotSupportedException(
            $"Screen capture is not implemented for {System.Runtime.InteropServices.RuntimeInformation.OSDescription}.");
    }
}