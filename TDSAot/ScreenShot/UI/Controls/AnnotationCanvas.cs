using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using TDS.ScreenShot.Core.Annotations;
using TDS.ScreenShot.UI.Models;
using TDS.ScreenShot.UI.Services;
using AHandle = TDS.ScreenShot.UI.Services.AnnotationEditor.HandleKind;

namespace TDS.ScreenShot.UI.Controls;

/// <summary>
/// Drawing surface with creation, selection, move, resize, and delete support.
/// </summary>
public sealed class AnnotationCanvas : Control
{
    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        AvaloniaProperty.Register<AnnotationCanvas, IBrush?>(nameof(Background), Brushes.Transparent);

    public static readonly StyledProperty<double> CurrentStrokeWidthProperty =
        AvaloniaProperty.Register<AnnotationCanvas, double>(nameof(CurrentStrokeWidth), 3.0);

    public static readonly StyledProperty<Color> CurrentStrokeProperty =
        AvaloniaProperty.Register<AnnotationCanvas, Color>(nameof(CurrentStroke), Colors.Red);

    public static readonly StyledProperty<string> ActiveToolProperty =
        AvaloniaProperty.Register<AnnotationCanvas, string>(nameof(ActiveTool), ToolIds.Pen);

    public static readonly StyledProperty<WriteableBitmap?> SourceBitmapProperty =
        AvaloniaProperty.Register<AnnotationCanvas, WriteableBitmap?>(nameof(SourceBitmap));

    public static readonly StyledProperty<Point> SourceOffsetProperty =
        AvaloniaProperty.Register<AnnotationCanvas, Point>(nameof(SourceOffset), default);

    public IBrush? Background
    {
        get => GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public double CurrentStrokeWidth
    {
        get => GetValue(CurrentStrokeWidthProperty);
        set => SetValue(CurrentStrokeWidthProperty, value);
    }

    public Color CurrentStroke
    {
        get => GetValue(CurrentStrokeProperty);
        set => SetValue(CurrentStrokeProperty, value);
    }

    public string ActiveTool
    {
        get => GetValue(ActiveToolProperty);
        set => SetValue(ActiveToolProperty, value);
    }

    public WriteableBitmap? SourceBitmap
    {
        get => GetValue(SourceBitmapProperty);
        set => SetValue(SourceBitmapProperty, value);
    }

    public Point SourceOffset
    {
        get => GetValue(SourceOffsetProperty);
        set => SetValue(SourceOffsetProperty, value);
    }

    /// <summary>Logical-to-physical scale when sampling <see cref="SourceBitmap"/> (1.0 = 100% DPI).</summary>
    public double SourceDpiScale { get; set; } = 1.0;

    public List<Annotation> Items { get; } = new();
    public int? SelectedIndex { get; private set; }
    public int UndoLimit { get; set; } = 50;

    public event EventHandler<Annotation>? Committed;
    public event EventHandler<int>? UndoCountChanged;

    private Point? _start;
    private readonly List<Point> _penPoints = new();
    private bool _dragging;
    private Annotation? _livePreview;

    private AHandle? _editHandle;
    private Point _editStartPoint;
    private Annotation? _editSnapshot;
    private Rect _editStartBounds;
    private DateTime _lastClickUtc;
    private int _lastClickIndex = -1;

    // Inline text editing
    private TextBox? _inlineTextBox;
    private int _inlineTextIndex = -1;

    private static readonly Color SelectionColor = Color.FromRgb(72, 210, 110);

    public void Add(Annotation a)
    {
        Items.Add(a);
        if (Items.Count > UndoLimit) Items.RemoveAt(0);
        Committed?.Invoke(this, a);
        UndoCountChanged?.Invoke(this, Items.Count);
        InvalidateVisual();
    }

    public bool Undo()
    {
        if (Items.Count == 0) return false;
        Items.RemoveAt(Items.Count - 1);
        if (SelectedIndex >= Items.Count) SelectedIndex = null;
        UndoCountChanged?.Invoke(this, Items.Count);
        InvalidateVisual();
        return true;
    }

    public void DeleteSelected()
    {
        if (SelectedIndex is not int idx || idx < 0 || idx >= Items.Count) return;
        Items.RemoveAt(idx);
        SelectedIndex = null;
        UndoCountChanged?.Invoke(this, Items.Count);
        InvalidateVisual();
    }

    public void ClearSelection() => Select(null);

    public void Select(int? index)
    {
        SelectedIndex = index;
        InvalidateVisual();
    }

    public override void Render(DrawingContext ctx)
    {
        var bg = Background;
        if (bg != null) ctx.FillRectangle(bg, new Rect(Bounds.Size));
        for (int i = 0; i < Items.Count; i++)
        {
            DrawAnnotation(ctx, Items[i]);
            if (SelectedIndex == i)
                DrawSelectionChrome(ctx, Items[i]);
        }
        if (_livePreview != null)
            DrawAnnotation(ctx, _livePreview);
    }

    private void DrawSelectionChrome(DrawingContext ctx, Annotation a)
    {
        var bounds = AnnotationEditor.GetBounds(a);
        if (bounds.Width > 0)
        {
            var dash = new DashStyle(new double[] { 4, 3 }, 0);
            var pen = new Pen(new SolidColorBrush(SelectionColor), 1.5) { DashStyle = dash };
            ctx.DrawRectangle(null, pen, bounds);
        }

        foreach (var (kind, center) in AnnotationEditor.GetHandleCenters(a))
        {
            var fill = kind is AHandle.StartPoint or AHandle.EndPoint
                ? new SolidColorBrush(Color.FromRgb(255, 200, 60))
                : new SolidColorBrush(Colors.White);
            var rect = new Rect(center.X - 4, center.Y - 4, 8, 8);
            ctx.DrawRectangle(fill, new Pen(new SolidColorBrush(SelectionColor), 1.2), rect);
        }
    }

    private void DrawAnnotation(DrawingContext ctx, Annotation a)
    {
        switch (a)
        {
            case PenAnnotation pen:
                if (pen.Points.Count < 2) return;
                var p = new Pen(new SolidColorBrush(pen.Stroke), pen.StrokeWidth)
                {
                    LineCap = PenLineCap.Round,
                    LineJoin = PenLineJoin.Round,
                };
                ctx.DrawGeometry(null, p, new PolylineGeometry(pen.Points.ToArray(), isFilled: false));
                break;
            case ArrowAnnotation arr:
                DrawArrow(ctx, arr);
                break;
            case RectAnnotation r:
                ctx.DrawRectangle(null,
                    new Pen(new SolidColorBrush(r.Stroke), r.StrokeWidth),
                    r.Rect);
                break;
            case EllipseAnnotation e:
                ctx.DrawEllipse(null,
                    new Pen(new SolidColorBrush(e.Stroke), e.StrokeWidth),
                    e.Rect);
                break;
            case TextAnnotation t:
                {
                    var ft = new FormattedText(
                        t.Text,
                        System.Globalization.CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        new Typeface("Segoe UI, Inter, sans-serif"),
                        t.FontSize,
                        new SolidColorBrush(t.Stroke));
                    ctx.DrawText(ft, t.Origin);
                    break;
                }
            case MosaicAnnotation m:
                if (SourceBitmap == null) return;
                DrawMosaic(ctx, m);
                break;
        }
    }

    private void DrawArrow(DrawingContext ctx, ArrowAnnotation a)
    {
        var pen = new Pen(new SolidColorBrush(a.Stroke), a.StrokeWidth)
        {
            LineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round,
        };
        ctx.DrawGeometry(null, pen, new PolylineGeometry(new[] { a.Start, a.End }, isFilled: false));

        var dx = a.End.X - a.Start.X;
        var dy = a.End.Y - a.Start.Y;
        var len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1) return;
        var ux = dx / len;
        var uy = dy / len;
        const double headSize = 14.0;
        var back = new Point(a.End.X - ux * headSize, a.End.Y - uy * headSize);
        var perpX = -uy;
        var perpY = ux;
        const double headWidth = 8.0;
        var p1 = new Point(back.X + perpX * headWidth, back.Y + perpY * headWidth);
        var p2 = new Point(back.X - perpX * headWidth, back.Y - perpY * headWidth);
        var fill = new SolidColorBrush(a.Stroke);
        ctx.DrawGeometry(fill, pen, new PolylineGeometry(new[] { p1, a.End, p2 }, isFilled: true));
    }

    private void DrawMosaic(DrawingContext ctx, MosaicAnnotation m)
    {
        var src = SourceBitmap!;
        int sw = src.PixelSize.Width;
        int sh = src.PixelSize.Height;
        var origin = SourceOffset;
        double scale = SourceDpiScale;
        using var lk = src.Lock();
        unsafe
        {
            byte* ptr = (byte*)lk.Address;
            int srcStride = lk.RowBytes;
            int bs = Math.Max(4, (int)Math.Round(m.BlockSize * scale));
            var rect = m.Rect;
            int x0 = Math.Max(0, (int)Math.Floor((rect.X + origin.X) * scale));
            int y0 = Math.Max(0, (int)Math.Floor((rect.Y + origin.Y) * scale));
            int x1 = Math.Min(sw, (int)Math.Ceiling((rect.Right + origin.X) * scale));
            int y1 = Math.Min(sh, (int)Math.Ceiling((rect.Bottom + origin.Y) * scale));
            for (int by = y0; by < y1; by += bs)
            {
                int blockH = Math.Min(bs, y1 - by);
                for (int bx = x0; bx < x1; bx += bs)
                {
                    int blockW = Math.Min(bs, x1 - bx);
                    int bxEnd = bx + blockW;
                    int byEnd = by + blockH;
                    long sumR = 0, sumG = 0, sumB = 0, count = 0;
                    for (int py = by; py < byEnd; py++)
                    {
                        int row = py * srcStride;
                        for (int px = bx; px < bxEnd; px++)
                        {
                            int offset = row + px * 4;
                            sumB += ptr[offset + 0];
                            sumG += ptr[offset + 1];
                            sumR += ptr[offset + 2];
                            count++;
                        }
                    }
                    if (count == 0) continue;
                    var color = Color.FromRgb(
                        (byte)(sumR / count),
                        (byte)(sumG / count),
                        (byte)(sumB / count));
                    // Fresh brush per tile: reusing SolidColorBrush and mutating Color
                    // leaves every block at the default black during one Render pass.
                    ctx.FillRectangle(new SolidColorBrush(color),
                        new Rect(bx / scale - origin.X, by / scale - origin.Y, blockW / scale, blockH / scale));
                }
            }
        }
    }

    /// <summary>Maps toolbar stroke width to mosaic tile size (thicker = coarser).</summary>
    private static int MosaicBlockSizeFromStroke(double strokeWidth)
        => Math.Clamp((int)Math.Round(strokeWidth * 4), 4, 48);

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        var p = e.GetPosition(this);

        if (_inlineTextBox != null)
        {
            CommitInlineText();
            return;
        }

        if (ActiveTool == ToolIds.Select)
        {
            BeginSelectEdit(p);
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }

        if (ActiveTool == ToolIds.Text)
        {
            BeginInlineText(p, null);
            e.Handled = true;
            return;
        }

        _start = p;
        _dragging = true;
        if (ActiveTool == ToolIds.Pen)
        {
            _penPoints.Clear();
            _penPoints.Add(p);
            _livePreview = null;
        }
        else
            _livePreview = CreateShapeAt(p, p);

        e.Pointer.Capture(this);
        e.Handled = true;
        base.OnPointerPressed(e);
    }

    private void BeginSelectEdit(Point p)
    {
        if (SelectedIndex is int idx && idx >= 0 && idx < Items.Count)
        {
            var selected = Items[idx];
            var handle = AnnotationEditor.HitTestHandle(selected, p);
            if (handle is AHandle h && h != AHandle.Body)
            {
                _editHandle = h;
                _editStartPoint = p;
                _editSnapshot = selected;
                _editStartBounds = AnnotationEditor.GetBounds(selected);
                _dragging = true;
                return;
            }
        }

        var hit = AnnotationEditor.HitTestAnnotations(Items, p);
        if (hit >= 0)
        {
            SelectedIndex = hit;
            var now = DateTime.UtcNow;
            if (hit == _lastClickIndex && (now - _lastClickUtc).TotalMilliseconds < 450
                && Items[hit] is TextAnnotation text)
            {
                SelectedIndex = hit;
                _inlineTextIndex = hit;
                BeginInlineText(text.Origin, text.Text);
                return;
            }
            _lastClickIndex = hit;
            _lastClickUtc = now;

            _editHandle = AHandle.Body;
            _editStartPoint = p;
            _editSnapshot = Items[hit];
            _editStartBounds = AnnotationEditor.GetBounds(Items[hit]);
            _dragging = true;
            InvalidateVisual();
            return;
        }

        SelectedIndex = null;
        InvalidateVisual();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        if (!_dragging) return;
        var p = e.GetPosition(this);

        if (ActiveTool == ToolIds.Select && SelectedIndex is int idx && _editSnapshot != null)
        {
            if (_editHandle == AHandle.Body)
            {
                var delta = p - _editStartPoint;
                Items[idx] = AnnotationEditor.Move(_editSnapshot, delta);
            }
            else if (_editHandle is AHandle handle)
            {
                Items[idx] = AnnotationEditor.ApplyHandle(_editSnapshot, handle, p, _editStartBounds);
            }
            InvalidateVisual();
            return;
        }

        if (ActiveTool == ToolIds.Pen)
        {
            _penPoints.Add(p);
            if (_penPoints.Count >= 2)
            {
                _livePreview = new PenAnnotation
                {
                    Kind = AnnotationKind.Pen,
                    Stroke = CurrentStroke,
                    StrokeWidth = CurrentStrokeWidth,
                    Points = _penPoints.ToArray(),
                };
                InvalidateVisual();
            }
            return;
        }

        if (_start is Point s)
        {
            _livePreview = CreateShapeAt(s, p);
            InvalidateVisual();
        }
        base.OnPointerMoved(e);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (!_dragging) return;

        if (ActiveTool == ToolIds.Select)
        {
            _dragging = false;
            _editHandle = null;
            _editSnapshot = null;
            e.Pointer.Capture(null);
            UndoCountChanged?.Invoke(this, Items.Count);
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        var committed = _livePreview;
        _livePreview = null;
        _dragging = false;
        _start = null;
        _penPoints.Clear();
        e.Pointer.Capture(null);
        if (committed != null) Add(committed);
        InvalidateVisual();
        base.OnPointerReleased(e);
    }

    private Annotation? CreateShapeAt(Point s, Point e)
    {
        var rect = NormalizeRect(s, e);
        return ActiveTool switch
        {
            ToolIds.Rect => new RectAnnotation
            {
                Kind = AnnotationKind.Rect,
                Stroke = CurrentStroke,
                StrokeWidth = CurrentStrokeWidth,
                Rect = rect,
            },
            ToolIds.Ellipse => new EllipseAnnotation
            {
                Kind = AnnotationKind.Ellipse,
                Stroke = CurrentStroke,
                StrokeWidth = CurrentStrokeWidth,
                Rect = rect,
            },
            ToolIds.Arrow => new ArrowAnnotation
            {
                Kind = AnnotationKind.Arrow,
                Stroke = CurrentStroke,
                StrokeWidth = CurrentStrokeWidth,
                Start = s,
                End = e,
            },
            ToolIds.Mosaic => new MosaicAnnotation
            {
                Kind = AnnotationKind.Mosaic,
                Stroke = CurrentStroke,
                StrokeWidth = 0,
                Rect = rect,
                BlockSize = MosaicBlockSizeFromStroke(CurrentStrokeWidth),
            },
            _ => null,
        };
    }

    private static Rect NormalizeRect(Point a, Point b)
    {
        var x = Math.Min(a.X, b.X);
        var y = Math.Min(a.Y, b.Y);
        var w = Math.Max(1, Math.Abs(a.X - b.X));
        var h = Math.Max(1, Math.Abs(a.Y - b.Y));
        return new Rect(x, y, w, h);
    }

    /// <summary>
    /// Show an inline TextBox at the given position for creating or editing text.
    /// </summary>
    private void BeginInlineText(Point origin, string? initialText)
    {
        if (_inlineTextBox != null) return;

        var fontSize = Math.Max(12, CurrentStrokeWidth * 4 + 8);
        if (initialText != null)
        {
            // Editing existing text — measure current size
            var ft = new FormattedText(
                initialText,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI, Inter, sans-serif"),
                fontSize,
                Brushes.White);
            origin = new Point(origin.X, origin.Y);
        }

        _inlineTextBox = new TextBox
        {
            Text = initialText ?? "",
            Watermark = "输入文字",
            MinWidth = 60,
            MaxWidth = Math.Min(400, Bounds.Width - origin.X - 8),
            FontSize = fontSize,
            Background = Brushes.Transparent,
            BorderBrush = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(4, 2),
            Foreground = new SolidColorBrush(CurrentStroke),
            CaretBrush = new SolidColorBrush(CurrentStroke),
            SelectionBrush = new SolidColorBrush(Color.FromArgb(80, 72, 210, 110)),
            AcceptsReturn = false,
        };

        if (initialText != null)
        {
            _inlineTextBox.SelectAll();
            _inlineTextIndex = -1; // will set below
            for (int i = 0; i < Items.Count; i++)
            {
                if (Items[i] is TextAnnotation ta && ta.Origin == origin && ta.Text == initialText)
                {
                    _inlineTextIndex = i;
                    break;
                }
            }
        }
        else
            _inlineTextIndex = -1;

        // Place the TextBox on the parent Canvas at the annotation coordinates.
        // The AnnotationCanvas is positioned at (Left, Top) inside the parent Canvas,
        // so we must add that offset to place the TextBox at the correct screen position.
        var parent = Parent as Panel;
        if (parent == null) return;
        double parentX = Canvas.GetLeft(this);
        double parentY = Canvas.GetTop(this);
        _inlineTextBox.Tag = origin;
        parent.Children.Add(_inlineTextBox);
        Canvas.SetLeft(_inlineTextBox, origin.X + (double.IsNaN(parentX) ? 0 : parentX));
        Canvas.SetTop(_inlineTextBox, origin.Y + (double.IsNaN(parentY) ? 0 : parentY));
        _inlineTextBox.ZIndex = 50;
        // Defer Focus() to the next UI tick so the newly-added TextBox is
        // attached to the visual tree (PlatformImpl) before we try to focus it.
        Dispatcher.UIThread.Post(() => _inlineTextBox?.Focus(), DispatcherPriority.Input);
        _inlineTextBox.KeyDown += OnInlineTextBoxKeyDown;
        _inlineTextBox.LostFocus += OnInlineTextBoxLostFocus;
    }

    private void OnInlineTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            CommitInlineText();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CancelInlineText();
            e.Handled = true;
        }
    }

    private void OnInlineTextBoxLostFocus(object? sender, RoutedEventArgs e)
    {
        CommitInlineText();
    }

    private void CommitInlineText()
    {
        if (_inlineTextBox == null) return;
        var tb = _inlineTextBox;
        _inlineTextBox = null;
        var text = tb.Text ?? "";
        var origin = (Point)(tb.Tag ?? new Point());
        var parent = Parent as Panel;
        if (parent != null) parent.Children.Remove(tb);

        if (_inlineTextIndex >= 0 && _inlineTextIndex < Items.Count)
        {
            // Editing existing text
            if (string.IsNullOrWhiteSpace(text))
            {
                Items.RemoveAt(_inlineTextIndex);
                SelectedIndex = null;
            }
            else
            {
                var existing = Items[_inlineTextIndex] as TextAnnotation;
                if (existing != null)
                    Items[_inlineTextIndex] = existing with { Text = text, Stroke = CurrentStroke };
            }
            _inlineTextIndex = -1;
            UndoCountChanged?.Invoke(this, Items.Count);
            InvalidateVisual();
            return;
        }

        // Creating new text
        if (!string.IsNullOrWhiteSpace(text))
        {
            Add(new TextAnnotation
            {
                Kind = AnnotationKind.Text,
                Stroke = CurrentStroke,
                StrokeWidth = 0,
                Text = text,
                Origin = origin,
                FontSize = tb.FontSize,
            });
        }
    }

    private void CancelInlineText()
    {
        if (_inlineTextBox == null) return;
        var tb = _inlineTextBox;
        _inlineTextBox = null;
        _inlineTextIndex = -1;
        var parent = Parent as Panel;
        if (parent != null) parent.Children.Remove(tb);
        InvalidateVisual();
    }
}
