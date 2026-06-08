using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace TDS.ScreenShot.UI.Controls;

/// <summary>
/// Compact icon button with rounded chrome, hover/press/active feedback.
/// </summary>
public sealed class ToolbarButton : Border
{
    public static readonly StyledProperty<string?> GeometryProperty =
        AvaloniaProperty.Register<ToolbarButton, string?>(nameof(Geometry));

    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<ToolbarButton, bool>(nameof(IsActive));

    public static readonly StyledProperty<bool> IsAccentProperty =
        AvaloniaProperty.Register<ToolbarButton, bool>(nameof(IsAccent));

    public string? Geometry
    {
        get => GetValue(GeometryProperty);
        set => SetValue(GeometryProperty, value);
    }

    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public bool IsAccent
    {
        get => GetValue(IsAccentProperty);
        set => SetValue(IsAccentProperty, value);
    }

    public event EventHandler? Click;

    private static readonly Color Accent = Color.FromRgb(72, 210, 110);
    private static readonly Color AccentDark = Color.FromRgb(48, 175, 85);
    private static readonly Color IconDefault = Color.FromRgb(210, 210, 214);
    private static readonly Color IconActive = Color.FromRgb(230, 255, 236);

    private readonly Avalonia.Controls.Shapes.Path _icon;
    private bool _pressed;
    private bool _hover;

    public ToolbarButton()
    {
        Width = 28;
        Height = 28;
        MinWidth = 28;
        MinHeight = 28;
        Padding = new Thickness(5);
        CornerRadius = new CornerRadius(6);
        Background = Brushes.Transparent;
        BorderThickness = new Thickness(0);
        Cursor = new Cursor(StandardCursorType.Hand);
        Focusable = false;

        _icon = new Avalonia.Controls.Shapes.Path
        {
            Stretch = Stretch.Uniform,
            Width = 16,
            Height = 16,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
        };
        Child = _icon;

        GeometryProperty.Changed.AddClassHandler<ToolbarButton>((b, _) => b.RebuildIcon());
        IsActiveProperty.Changed.AddClassHandler<ToolbarButton>((b, _) => b.UpdateChrome());
        IsAccentProperty.Changed.AddClassHandler<ToolbarButton>((b, _) => b.UpdateChrome());
        IsEnabledProperty.Changed.AddClassHandler<ToolbarButton>((b, _) => b.UpdateEnabled());
        RebuildIcon();
        UpdateChrome();
        UpdateEnabled();
    }

    private void UpdateEnabled()
    {
        Opacity = IsEnabled ? 1.0 : 0.38;
        IsHitTestVisible = IsEnabled;
    }

    private void RebuildIcon()
    {
        if (string.IsNullOrEmpty(Geometry))
        {
            _icon.Data = null;
            return;
        }
        _icon.Data = PathGeometry.Parse(Geometry);
        UpdateChrome();
    }

    private void UpdateChrome()
    {
        if (IsAccent)
        {
            Background = new SolidColorBrush(_pressed ? AccentDark : Accent);
            BorderBrush = Brushes.Transparent;
            BorderThickness = new Thickness(0);
            _icon.Stroke = Brushes.White;
            _icon.Fill = null;
            return;
        }

        if (IsActive)
        {
            Background = new SolidColorBrush(Color.FromArgb(48, 72, 210, 110));
            BorderBrush = new SolidColorBrush(Color.FromArgb(150, 72, 210, 110));
            BorderThickness = new Thickness(1);
            _icon.Stroke = new SolidColorBrush(IconActive);
        }
        else if (_pressed)
        {
            Background = new SolidColorBrush(Color.FromArgb(55, 200, 200, 210));
            BorderBrush = new SolidColorBrush(Color.FromArgb(60, 200, 200, 210));
            BorderThickness = new Thickness(1);
            _icon.Stroke = new SolidColorBrush(IconDefault);
        }
        else if (_hover)
        {
            Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255));
            BorderBrush = new SolidColorBrush(Color.FromArgb(50, 200, 200, 210));
            BorderThickness = new Thickness(1);
            _icon.Stroke = new SolidColorBrush(Color.FromRgb(230, 230, 235));
        }
        else
        {
            Background = Brushes.Transparent;
            BorderBrush = Brushes.Transparent;
            BorderThickness = new Thickness(0);
            _icon.Stroke = new SolidColorBrush(IconDefault);
        }

        _icon.Fill = null;
        _icon.StrokeThickness = 1.5;
        _icon.StrokeLineCap = PenLineCap.Round;
        _icon.StrokeJoin = PenLineJoin.Round;
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        _hover = true;
        UpdateChrome();
        base.OnPointerEntered(e);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        _hover = false;
        _pressed = false;
        UpdateChrome();
        base.OnPointerExited(e);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (!IsEnabled || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        _pressed = true;
        UpdateChrome();
        Click?.Invoke(this, EventArgs.Empty);
        e.Pointer.Capture(this);
        e.Handled = true;
        base.OnPointerPressed(e);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        _pressed = false;
        e.Pointer.Capture(null);
        UpdateChrome();
        e.Handled = true;
        base.OnPointerReleased(e);
    }
}
