using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace TDS.ScreenShot.UI.Controls;

/// <summary>
/// Draws a bitmap by mapping a source pixel rectangle to the control bounds 1:1 in
/// layout space. Avoids <see cref="Image"/> Stretch/DPI layout quirks on high-DPI displays.
/// </summary>
public sealed class ScreenshotBackground : Control
{
    public static readonly StyledProperty<Bitmap?> SourceProperty =
        AvaloniaProperty.Register<ScreenshotBackground, Bitmap?>(nameof(Source));

    public static readonly StyledProperty<Rect> SourcePixelRectProperty =
        AvaloniaProperty.Register<ScreenshotBackground, Rect>(nameof(SourcePixelRect), default);

    public Bitmap? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public Rect SourcePixelRect
    {
        get => GetValue(SourcePixelRectProperty);
        set => SetValue(SourcePixelRectProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var src = Source;
        if (src == null) return;

        var srcRect = SourcePixelRect;
        if (srcRect.Width <= 0 || srcRect.Height <= 0)
            srcRect = new Rect(0, 0, src.PixelSize.Width, src.PixelSize.Height);

        var dest = Bounds.Size;
        if (dest.Width <= 0 || dest.Height <= 0) return;
        context.DrawImage(src, srcRect, new Rect(dest));
    }
}
