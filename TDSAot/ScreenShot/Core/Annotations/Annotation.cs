using Avalonia;
using Avalonia.Media;

namespace TDS.ScreenShot.Core.Annotations;

/// <summary>
/// Immutable annotation record. Use <see cref="ShallowCopy"/> to create modified copies.
/// </summary>
public abstract record Annotation
{
    public required AnnotationKind Kind { get; init; }

    /// <summary>
    /// Color used to stroke/fill. For Mosaic the color is ignored.
    /// </summary>
    public required Color Stroke { get; init; }

    public required double StrokeWidth { get; init; }

    public abstract Annotation ShallowCopy();
}

/// <summary>
/// Free-hand pen with a polyline of points.
/// </summary>
public sealed record PenAnnotation : Annotation
{
    public required IReadOnlyList<Point> Points { get; init; }

    public override Annotation ShallowCopy() => this with { Points = Points.ToArray() };
}

/// <summary>
/// Arrow from <see cref="Start"/> to <see cref="End"/>.
/// </summary>
public sealed record ArrowAnnotation : Annotation
{
    public required Point Start { get; init; }
    public required Point End { get; init; }

    public override Annotation ShallowCopy() => this;
}

/// <summary>
/// Rectangle outline. Coordinates are in the bitmap (post-selection) space.
/// </summary>
public sealed record RectAnnotation : Annotation
{
    public required Rect Rect { get; init; }

    public override Annotation ShallowCopy() => this;
}

public sealed record EllipseAnnotation : Annotation
{
    public required Rect Rect { get; init; }

    public override Annotation ShallowCopy() => this;
}

/// <summary>
/// Text annotation anchored at <see cref="Origin"/>. The text is rendered in a single line
/// using the system default font (cross-platform).
/// </summary>
public sealed record TextAnnotation : Annotation
{
    public required string Text { get; init; }
    public required Point Origin { get; init; }
    public required double FontSize { get; init; }

    public override Annotation ShallowCopy() => this;
}

/// <summary>
/// Pixelated mosaic that hides the underlying area.
/// </summary>
public sealed record MosaicAnnotation : Annotation
{
    public required Rect Rect { get; init; }
    public required int BlockSize { get; init; }

    public override Annotation ShallowCopy() => this;
}