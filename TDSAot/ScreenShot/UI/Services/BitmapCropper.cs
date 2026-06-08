using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace TDS.ScreenShot.UI.Services;

/// <summary>
/// Crops a sub-rectangle of a <see cref="Bitmap"/> into a fresh
/// <see cref="RenderTargetBitmap"/>, 1:1 with the source pixels. Used by the
/// screenshot editor to produce the final selection image, and exposed as a
/// public static method so the cropping logic can be unit-tested without
/// going through the full UI.
/// </summary>
public static class BitmapCropper
{
    /// <summary>
    /// Renders the <paramref name="selection"/> region of <paramref name="source"/>
    /// into a new bitmap of size (<paramref name="selection"/>.Width,
    /// <paramref name="selection"/>.Height). No scaling, no squishing — every
    /// destination pixel corresponds to exactly one source pixel.
    /// </summary>
    public static Bitmap Crop(Bitmap source, Rect selection)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        int w = (int)selection.Width;
        int h = (int)selection.Height;
        if (w <= 0 || h <= 0) throw new ArgumentException("Selection has zero area", nameof(selection));

        var rtb = new RenderTargetBitmap(new PixelSize(w, h));
        var visual = new Canvas { Width = w, Height = h, Background = Brushes.Transparent };
        var cropped = new CroppedSourceView
        {
            Source = source,
            SourceRect = new Rect(selection.X, selection.Y, w, h),
            Width = w,
            Height = h,
            ClipToBounds = true,
        };
        visual.Children.Add(cropped);
        Canvas.SetLeft(cropped, 0);
        Canvas.SetTop(cropped, 0);
        visual.Measure(new Size(w, h));
        visual.Arrange(new Rect(0, 0, w, h));
        rtb.Render(visual);
        return rtb;
    }

    /// <summary>
    /// Minimal control that draws an arbitrary sub-rect of a <see cref="Bitmap"/>
    /// into its own bounds, 1:1, with no scaling. Going through
    /// <see cref="DrawingContext.DrawImage(Bitmap, Rect, Rect)"/> is the only
    /// way to get a deterministic 1:1 crop — the <see cref="Image"/> control's
    /// Stretch/translate interactions are layout-dependent and have historically
    /// produced wrong results.
    /// </summary>
    private sealed class CroppedSourceView : Control
    {
        public Bitmap? Source { get; set; }
        public Rect SourceRect { get; set; }

        public override void Render(DrawingContext ctx)
        {
            var src = Source;
            if (src == null) return;
            if (SourceRect.Width <= 0 || SourceRect.Height <= 0) return;
            ctx.DrawImage(src, SourceRect, new Rect(Bounds.Size));
        }
    }
}