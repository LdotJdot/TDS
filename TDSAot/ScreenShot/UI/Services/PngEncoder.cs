using Avalonia.Media.Imaging;

namespace TDS.ScreenShot.UI.Services;

/// <summary>
/// Encodes a <see cref="Bitmap"/> to PNG bytes. Wraps <see cref="Bitmap.Save(System.IO.Stream, int?)"/>
/// so callers do not have to manage a <see cref="System.IO.MemoryStream"/>.
/// </summary>
public static class PngEncoder
{
    public static byte[] Encode(Bitmap bitmap, int quality = 100)
    {
        using var ms = new MemoryStream();
        bitmap.Save(ms, quality);
        return ms.ToArray();
    }
}