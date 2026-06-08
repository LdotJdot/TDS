using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;

namespace TDS.ScreenShot.UI.Controls;

public enum HandleKind
{
    TopLeft, Top, TopRight,
    Left,                 Right,
    BottomLeft, Bottom, BottomRight,
}

public enum HandleDragState { Start, Move, End }

public sealed class HandleDragEventArgs : EventArgs
{
    public HandleKind Kind { get; }
    public HandleDragState State { get; }
    public Point Position { get; }
    public HandleDragEventArgs(HandleKind k, HandleDragState s, Point p) { Kind = k; State = s; Position = p; }
}

/// <summary>
/// Manages the selection border, size label, and 8 resize handles as direct
/// children of a host <see cref="Canvas"/>. Keeping handles as root-level
/// siblings (not inside a full-screen overlay) ensures they receive pointer
/// events while the annotation canvas below stays interactive.
/// </summary>
public sealed class SelectionAdorner
{
    private const double HandleSize = 10;
    private const double HandleHalf = HandleSize / 2;

    private readonly Canvas _host;
    private readonly Border _border;
    private readonly Border _labelHost;
    private readonly TextBlock _sizeLabel;
    private readonly Dictionary<HandleKind, Rectangle> _handles = new();

    public Rect Selection { get; set; }
    public bool IsVisible { get; set; }

    public event EventHandler<HandleDragEventArgs>? HandleDrag;

    public SelectionAdorner(Canvas host)
    {
        _host = host;

        _border = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(80, 220, 100)),
            BorderThickness = new Thickness(1.5),
            Background = Brushes.Transparent,
            IsHitTestVisible = false,
        };
        _host.Children.Add(_border);
        _border.ZIndex = 15;

        _sizeLabel = new TextBlock
        {
            Foreground = Brushes.White,
            FontSize = 11,
        };
        _labelHost = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(200, 0, 0, 0)),
            CornerRadius = new CornerRadius(2),
            Padding = new Thickness(4, 1),
            Child = _sizeLabel,
            IsHitTestVisible = false,
        };
        _host.Children.Add(_labelHost);
        _labelHost.ZIndex = 15;

        foreach (HandleKind k in Enum.GetValues<HandleKind>())
        {
            var h = new Rectangle
            {
                Width = HandleSize,
                Height = HandleSize,
                Fill = Brushes.White,
                Stroke = new SolidColorBrush(Color.FromRgb(80, 220, 100)),
                StrokeThickness = 1.5,
                IsHitTestVisible = true,
                Cursor = k switch
                {
                    HandleKind.TopLeft or HandleKind.BottomRight => new Cursor(StandardCursorType.TopLeftCorner),
                    HandleKind.TopRight or HandleKind.BottomLeft => new Cursor(StandardCursorType.TopRightCorner),
                    HandleKind.Top or HandleKind.Bottom => new Cursor(StandardCursorType.SizeNorthSouth),
                    HandleKind.Left or HandleKind.Right => new Cursor(StandardCursorType.SizeWestEast),
                    _ => new Cursor(StandardCursorType.Arrow)
                }
            };
            h.PointerPressed += (_, e) =>
            {
                e.Pointer.Capture(h);
                OnHandleDrag(k, HandleDragState.Start, e.GetPosition(_host));
                e.Handled = true;
            };
            h.PointerMoved += (_, e) =>
            {
                if (e.Pointer.Captured != h) return;
                OnHandleDrag(k, HandleDragState.Move, e.GetPosition(_host));
                e.Handled = true;
            };
            h.PointerReleased += (_, e) =>
            {
                if (e.Pointer.Captured != h) return;
                OnHandleDrag(k, HandleDragState.End, e.GetPosition(_host));
                e.Pointer.Capture(null);
                e.Handled = true;
            };
            _handles[k] = h;
            _host.Children.Add(h);
            h.ZIndex = 20;
        }
    }

    public void UpdateLayout()
    {
        var show = IsVisible && Selection.Width > 0 && Selection.Height > 0;
        _border.IsVisible = show;
        _labelHost.IsVisible = show;
        foreach (var h in _handles.Values)
            h.IsVisible = show;
        if (!show) return;

        var s = Selection;
        _border.Width = s.Width;
        _border.Height = s.Height;
        Canvas.SetLeft(_border, s.X);
        Canvas.SetTop(_border, s.Y);

        _sizeLabel.Text = $"{(int)s.Width} x {(int)s.Height}";
        _labelHost.Measure(Size.Infinity);
        var ls = _labelHost.DesiredSize;
        double labelY = s.Y - ls.Height - 2;
        if (labelY < 0) labelY = s.Bottom + 2;
        Canvas.SetLeft(_labelHost, s.X);
        Canvas.SetTop(_labelHost, labelY);

        // Handles sit outside the selection rect so they don't block drawing.
        SetHandle(HandleKind.TopLeft,     s.X - HandleSize, s.Y - HandleSize);
        SetHandle(HandleKind.Top,         s.Center.X - HandleHalf, s.Y - HandleSize);
        SetHandle(HandleKind.TopRight,    s.Right, s.Y - HandleSize);
        SetHandle(HandleKind.Left,        s.X - HandleSize, s.Center.Y - HandleHalf);
        SetHandle(HandleKind.Right,       s.Right, s.Center.Y - HandleHalf);
        SetHandle(HandleKind.BottomLeft,  s.X - HandleSize, s.Bottom);
        SetHandle(HandleKind.Bottom,      s.Center.X - HandleHalf, s.Bottom);
        SetHandle(HandleKind.BottomRight, s.Right, s.Bottom);
    }

    private void SetHandle(HandleKind k, double x, double y)
    {
        var h = _handles[k];
        Canvas.SetLeft(h, x);
        Canvas.SetTop(h, y);
    }

    private void OnHandleDrag(HandleKind k, HandleDragState s, Point p)
        => HandleDrag?.Invoke(this, new HandleDragEventArgs(k, s, p));
}
