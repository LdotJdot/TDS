using Avalonia;
using Avalonia.Media;
using TDS.ScreenShot.Core.Annotations;

namespace TDS.ScreenShot.UI.Services;

/// <summary>
/// Hit-testing, bounds, and in-place transforms for screenshot annotations.
/// </summary>
internal static class AnnotationEditor
{
    public enum HandleKind
    {
        Body,
        TopLeft, Top, TopRight,
        Left, Right,
        BottomLeft, Bottom, BottomRight,
        StartPoint, EndPoint,
    }

    private const double HitTolerance = 8;
    private const double HandleSize = 8;
    private const double HandleHalf = HandleSize / 2;

    public static Rect GetBounds(Annotation a)
    {
        switch (a)
        {
            case RectAnnotation r:
            case EllipseAnnotation e:
            case MosaicAnnotation m:
                return Pad(a switch
                {
                    RectAnnotation rr => rr.Rect,
                    EllipseAnnotation ee => ee.Rect,
                    MosaicAnnotation mm => mm.Rect,
                    _ => default,
                }, a.StrokeWidth);
            case ArrowAnnotation arr:
                return Pad(new Rect(
                    Math.Min(arr.Start.X, arr.End.X),
                    Math.Min(arr.Start.Y, arr.End.Y),
                    Math.Max(1, Math.Abs(arr.End.X - arr.Start.X)),
                    Math.Max(1, Math.Abs(arr.End.Y - arr.Start.Y))), a.StrokeWidth + 10);
            case PenAnnotation pen:
                if (pen.Points.Count == 0) return default;
                double minX = pen.Points[0].X, minY = pen.Points[0].Y;
                double maxX = minX, maxY = minY;
                foreach (var pt in pen.Points)
                {
                    minX = Math.Min(minX, pt.X);
                    minY = Math.Min(minY, pt.Y);
                    maxX = Math.Max(maxX, pt.X);
                    maxY = Math.Max(maxY, pt.Y);
                }
                return Pad(new Rect(minX, minY, maxX - minX, maxY - minY), pen.StrokeWidth);
            case TextAnnotation t:
                var size = EstimateTextSize(t);
                return new Rect(t.Origin.X - 4, t.Origin.Y - 2, size.Width + 8, size.Height + 4);
            default:
                return default;
        }
    }

    public static int HitTestAnnotations(IReadOnlyList<Annotation> items, Point p)
    {
        for (int i = items.Count - 1; i >= 0; i--)
        {
            if (HitTest(items[i], p))
                return i;
        }
        return -1;
    }

    public static bool HitTest(Annotation a, Point p)
    {
        switch (a)
        {
            case RectAnnotation r:
                return Inflate(r.Rect, HitTolerance).Contains(p);
            case EllipseAnnotation e:
                return Inflate(e.Rect, HitTolerance).Contains(p);
            case MosaicAnnotation m:
                return Inflate(m.Rect, HitTolerance).Contains(p);
            case ArrowAnnotation arr:
                return DistanceToSegment(p, arr.Start, arr.End) <= HitTolerance + arr.StrokeWidth;
            case PenAnnotation pen:
                for (int i = 1; i < pen.Points.Count; i++)
                {
                    if (DistanceToSegment(p, pen.Points[i - 1], pen.Points[i]) <= HitTolerance + pen.StrokeWidth)
                        return true;
                }
                return pen.Points.Count == 1 && Distance(p, pen.Points[0]) <= HitTolerance;
            case TextAnnotation t:
                return GetBounds(t).Contains(p);
            default:
                return false;
        }
    }

    public static HandleKind? HitTestHandle(Annotation a, Point p)
    {
        if (a is ArrowAnnotation arr)
        {
            if (InHandle(p, arr.Start)) return HandleKind.StartPoint;
            if (InHandle(p, arr.End)) return HandleKind.EndPoint;
            if (HitTest(arr, p)) return HandleKind.Body;
            return null;
        }

        var b = GetBounds(a);
        if (b.Width <= 0) return null;

        if (InHandle(p, new Point(b.X, b.Y))) return HandleKind.TopLeft;
        if (InHandle(p, new Point(b.Right, b.Y))) return HandleKind.TopRight;
        if (InHandle(p, new Point(b.X, b.Bottom))) return HandleKind.BottomLeft;
        if (InHandle(p, new Point(b.Right, b.Bottom))) return HandleKind.BottomRight;
        if (InHandle(p, new Point(b.Center.X, b.Y))) return HandleKind.Top;
        if (InHandle(p, new Point(b.Center.X, b.Bottom))) return HandleKind.Bottom;
        if (InHandle(p, new Point(b.X, b.Center.Y))) return HandleKind.Left;
        if (InHandle(p, new Point(b.Right, b.Center.Y))) return HandleKind.Right;
        if (HitTest(a, p)) return HandleKind.Body;
        return null;
    }

    public static Annotation Move(Annotation a, Vector delta)
    {
        return a switch
        {
            RectAnnotation r => r with { Rect = Offset(r.Rect, delta) },
            EllipseAnnotation e => e with { Rect = Offset(e.Rect, delta) },
            MosaicAnnotation m => m with { Rect = Offset(m.Rect, delta) },
            ArrowAnnotation arr => arr with { Start = arr.Start + delta, End = arr.End + delta },
            PenAnnotation pen => pen with { Points = pen.Points.Select(pt => pt + delta).ToArray() },
            TextAnnotation t => t with { Origin = t.Origin + delta },
            _ => a,
        };
    }

    public static Annotation ApplyHandle(Annotation a, HandleKind handle, Point current, Rect startBounds)
    {
        if (a is ArrowAnnotation arr)
        {
            return handle switch
            {
                HandleKind.StartPoint => arr with { Start = current },
                HandleKind.EndPoint => arr with { End = current },
                _ => arr,
            };
        }

        if (a is PenAnnotation)
            return a;

        if (a is TextAnnotation text)
            return ApplyTextHandle(text, handle, current, startBounds);

        var rect = a switch
        {
            RectAnnotation r => r.Rect,
            EllipseAnnotation e => e.Rect,
            MosaicAnnotation m => m.Rect,
            _ => startBounds,
        };

        double L = rect.X, T = rect.Y, R = rect.Right, B = rect.Bottom;
        switch (handle)
        {
            case HandleKind.TopLeft: L = current.X; T = current.Y; break;
            case HandleKind.Top: T = current.Y; break;
            case HandleKind.TopRight: R = current.X; T = current.Y; break;
            case HandleKind.Left: L = current.X; break;
            case HandleKind.Right: R = current.X; break;
            case HandleKind.BottomLeft: L = current.X; B = current.Y; break;
            case HandleKind.Bottom: B = current.Y; break;
            case HandleKind.BottomRight: R = current.X; B = current.Y; break;
            default: return a;
        }

        var next = Normalize(L, T, R, B);
        return a switch
        {
            RectAnnotation r => r with { Rect = next },
            EllipseAnnotation e => e with { Rect = next },
            MosaicAnnotation m => m with { Rect = next },
            _ => a,
        };
    }

    public static IEnumerable<(HandleKind Kind, Point Center)> GetHandleCenters(Annotation a)
    {
        if (a is ArrowAnnotation arr)
        {
            yield return (HandleKind.StartPoint, arr.Start);
            yield return (HandleKind.EndPoint, arr.End);
            yield break;
        }

        if (a is PenAnnotation)
            yield break;

        var b = GetBounds(a);
        yield return (HandleKind.TopLeft, new Point(b.X, b.Y));
        yield return (HandleKind.Top, new Point(b.Center.X, b.Y));
        yield return (HandleKind.TopRight, new Point(b.Right, b.Y));
        yield return (HandleKind.Left, new Point(b.X, b.Center.Y));
        yield return (HandleKind.Right, new Point(b.Right, b.Center.Y));
        yield return (HandleKind.BottomLeft, new Point(b.X, b.Bottom));
        yield return (HandleKind.Bottom, new Point(b.Center.X, b.Bottom));
        yield return (HandleKind.BottomRight, new Point(b.Right, b.Bottom));
    }

    public static bool SupportsResize(Annotation a) =>
        a is RectAnnotation or EllipseAnnotation or MosaicAnnotation or TextAnnotation;

    public static bool SupportsRotate(Annotation a) => a is ArrowAnnotation;

    private static Annotation ApplyTextHandle(TextAnnotation t, HandleKind handle, Point current, Rect startBounds)
    {
        double L = startBounds.X, T = startBounds.Y, R = startBounds.Right, B = startBounds.Bottom;
        switch (handle)
        {
            case HandleKind.TopLeft: L = current.X; T = current.Y; break;
            case HandleKind.Top: T = current.Y; break;
            case HandleKind.TopRight: R = current.X; T = current.Y; break;
            case HandleKind.Left: L = current.X; break;
            case HandleKind.Right: R = current.X; break;
            case HandleKind.BottomLeft: L = current.X; B = current.Y; break;
            case HandleKind.Bottom: B = current.Y; break;
            case HandleKind.BottomRight: R = current.X; B = current.Y; break;
            default: return t;
        }

        var next = Normalize(L, T, R, B);
        var origSize = EstimateTextSize(t);
        double origW = Math.Max(1, origSize.Width + 8);
        double origH = Math.Max(1, origSize.Height + 4);
        double scaleW = next.Width / origW;
        double scaleH = next.Height / origH;
        double scale = Math.Max(0.3, Math.Min(3.0, Math.Min(scaleW, scaleH)));
        double newFontSize = Math.Max(8, t.FontSize * scale);

        // Keep the text anchored at the top-left of the new bounds
        return t with
        {
            Origin = new Point(next.X + 4, next.Y + 2),
            FontSize = newFontSize,
        };
    }

    private static Size EstimateTextSize(TextAnnotation t)
    {
        var ft = new FormattedText(
            t.Text,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI, Inter, sans-serif"),
            t.FontSize,
            Brushes.White);
        return new Size(ft.Width, ft.Height);
    }

    private static Rect Pad(Rect r, double amount) => Inflate(r, amount);

    private static Rect Inflate(Rect r, double amount)
        => new(r.X - amount, r.Y - amount, Math.Max(1, r.Width + amount * 2), Math.Max(1, r.Height + amount * 2));

    private static Rect Offset(Rect r, Vector d) => new(r.X + d.X, r.Y + d.Y, r.Width, r.Height);

    private static Rect Normalize(double L, double T, double R, double B)
    {
        double x = Math.Min(L, R), y = Math.Min(T, B);
        double w = Math.Max(4, Math.Abs(R - L));
        double h = Math.Max(4, Math.Abs(B - T));
        return new Rect(x, y, w, h);
    }

    private static bool InHandle(Point p, Point center)
        => Math.Abs(p.X - center.X) <= HandleHalf + 2 && Math.Abs(p.Y - center.Y) <= HandleHalf + 2;

    private static double DistanceToSegment(Point p, Point a, Point b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var lenSq = dx * dx + dy * dy;
        if (lenSq < 0.001) return Distance(p, a);
        var t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lenSq, 0, 1);
        var proj = new Point(a.X + t * dx, a.Y + t * dy);
        return Distance(p, proj);
    }

    private static double Distance(Point a, Point b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
