using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using TDS.ScreenShot.UI.Assets;
using TDS.ScreenShot.UI.Models;

namespace TDS.ScreenShot.UI.Controls;

/// <summary>
/// Floating annotation toolbar with grouped tools, color/width pickers, and actions.
/// </summary>
public sealed class ScreenshotToolbar : Border
{
    public event EventHandler<string>? ToolChanged;
    public event EventHandler? UndoClicked;
    public event EventHandler? SaveClicked;
    public event EventHandler? CancelClicked;
    public event EventHandler? ConfirmClicked;
    public event EventHandler<Color>? ColorChanged;
    public event EventHandler<double>? StrokeWidthChanged;

    public string ActiveTool { get; private set; } = ToolIds.Rect;
    public bool ShowSaveButton { get; }
    public Color CurrentStroke { get; private set; } = Color.FromRgb(238, 32, 77);
    public double CurrentStrokeWidth { get; private set; } = 3.0;

    private readonly Dictionary<string, ToolbarButton> _toolButtons = new();
    private readonly ToolbarButton _undoButton;
    private readonly Dictionary<Color, Border> _colorChips = new();
    private readonly Dictionary<double, Border> _widthChips = new();
    private readonly List<double> _widthPresets = new() { 2, 3, 5, 8 };

    private static readonly Color Accent = Color.FromRgb(72, 210, 110);

    public ScreenshotToolbar(EditRequest request)
    {
        ShowSaveButton = request.ShowSaveButton;
        CurrentStroke = request.DefaultStroke ?? CurrentStroke;
        CurrentStrokeWidth = request.DefaultStrokeWidth;
        ActiveTool = request.InitialTool;

        Background = new SolidColorBrush(Color.FromArgb(248, 26, 26, 29));
        CornerRadius = new CornerRadius(12);
        Padding = new Thickness(8, 6);
        BorderBrush = new SolidColorBrush(Color.FromArgb(40, 200, 200, 210));
        BorderThickness = new Thickness(0.5);
        BoxShadow = new BoxShadows(new BoxShadow
        {
            OffsetX = 0,
            OffsetY = 4,
            Blur = 16,
            Color = Color.FromArgb(80, 0, 0, 0),
        });

        var root = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        Child = root;

        var toolGroup = CreateGroup(out var tools);
        AddTool(tools, ToolIds.Select, ToolIcons.Select, "选择 / 移动 / 编辑");
        AddTool(tools, ToolIds.Rect, ToolIcons.Rect, "矩形");
        AddTool(tools, ToolIds.Ellipse, ToolIcons.Ellipse, "椭圆");
        AddTool(tools, ToolIds.Arrow, ToolIcons.Arrow, "箭头");
        AddTool(tools, ToolIds.Pen, ToolIcons.Pen, "画笔");
        AddTool(tools, ToolIds.Text, ToolIcons.Text, "文字");
        AddTool(tools, ToolIds.Mosaic, ToolIcons.Mosaic, "马赛克");
        root.Children.Add(toolGroup);

        root.Children.Add(Separator());

        var colorGroup = CreateGroup(out var colors);
        AddColorChips(colors);
        root.Children.Add(colorGroup);

        root.Children.Add(Separator());

        var widthGroup = CreateGroup(out var widths);
        AddWidthChips(widths);
        root.Children.Add(widthGroup);

        root.Children.Add(Separator());

        var actionGroup = CreateGroup(out var actions);
        _undoButton = AddIcon(actions, ToolIcons.Undo, "撤销 (Ctrl+Z)");
        _undoButton.IsEnabled = false;
        _undoButton.Click += (_, _) => UndoClicked?.Invoke(this, EventArgs.Empty);

        if (ShowSaveButton)
            AddIcon(actions, ToolIcons.Save, "保存 (Ctrl+S)").Click += (_, _) => SaveClicked?.Invoke(this, EventArgs.Empty);

        AddIcon(actions, ToolIcons.Cancel, "取消 (Esc)").Click += (_, _) => CancelClicked?.Invoke(this, EventArgs.Empty);

        var done = AddIcon(actions, ToolIcons.Done, "完成 (Enter)");
        done.IsAccent = true;
        done.Click += (_, _) => ConfirmClicked?.Invoke(this, EventArgs.Empty);
        root.Children.Add(actionGroup);

        UpdateActiveTool();
        UpdateColorChips();
        UpdateWidthChips();
    }

    public void NotifyUndoCount(int count) => _undoButton.IsEnabled = count > 0;

    private static Border CreateGroup(out StackPanel inner)
    {
        inner = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 1 };
        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(255, 20, 20, 22)),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(3, 2),
            BorderBrush = new SolidColorBrush(Color.FromArgb(30, 200, 200, 210)),
            BorderThickness = new Thickness(0.5),
            Child = inner,
        };
    }

    private static Border Separator() => new()
    {
        Width = 1,
        Height = 22,
        Margin = new Thickness(1, 0),
        VerticalAlignment = VerticalAlignment.Center,
        Background = new SolidColorBrush(Color.FromArgb(40, 200, 200, 210)),
    };

    private void AddTool(Panel panel, string id, string geom, string tip)
    {
        var b = new ToolbarButton { Geometry = geom };
        ToolTip.SetTip(b, tip);
        b.Click += (_, _) => SetActiveTool(id);
        _toolButtons[id] = b;
        panel.Children.Add(b);
    }

    public void SetActiveTool(string id)
    {
        if (ActiveTool == id) return;
        ActiveTool = id;
        UpdateActiveTool();
        ToolChanged?.Invoke(this, id);
    }

    private ToolbarButton AddIcon(Panel panel, string geom, string tip)
    {
        var b = new ToolbarButton { Geometry = geom };
        ToolTip.SetTip(b, tip);
        panel.Children.Add(b);
        return b;
    }

    private static readonly Color[] Palette =
    {
        Color.FromRgb(238, 32, 77),
        Color.FromRgb(0, 122, 255),
        Color.FromRgb(52, 199, 89),
        Color.FromRgb(255, 149, 0),
        Color.FromRgb(255, 214, 10),
        Color.FromRgb(255, 255, 255),
        Color.FromRgb(0, 0, 0),
    };

    private void AddColorChips(StackPanel row)
    {
        foreach (var c in Palette)
        {
            var chip = new Border
            {
                Width = 18,
                Height = 18,
                CornerRadius = new CornerRadius(9),
                Background = new SolidColorBrush(c),
                BorderBrush = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
                BorderThickness = new Thickness(1.5),
                Cursor = new Cursor(StandardCursorType.Hand),
                Tag = c,
            };
            chip.PointerEntered += (_, _) => chip.BorderBrush = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255));
            chip.PointerExited += (_, _) => UpdateColorChipBorder(chip, c);
            chip.PointerPressed += (_, e) =>
            {
                if (chip.Tag is Color cc)
                {
                    CurrentStroke = cc;
                    UpdateColorChips();
                    ColorChanged?.Invoke(this, cc);
                }
                e.Handled = true;
            };
            _colorChips[c] = chip;
            row.Children.Add(chip);
        }
    }

    private void AddWidthChips(StackPanel row)
    {
        for (int i = 0; i < _widthPresets.Count; i++)
        {
            var w = _widthPresets[i];
            var size = 5 + i * 2;
            var chip = new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(6),
                Background = Brushes.Transparent,
                BorderBrush = new SolidColorBrush(Color.FromArgb(48, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Cursor = new Cursor(StandardCursorType.Hand),
                Tag = w,
                Child = new Ellipse
                {
                    Width = size,
                    Height = size,
                    Fill = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    IsHitTestVisible = false,
                }
            };
            chip.PointerEntered += (_, _) =>
            {
                if ((double)chip.Tag! != CurrentStrokeWidth)
                    chip.Background = new SolidColorBrush(Color.FromArgb(28, 255, 255, 255));
            };
            chip.PointerExited += (_, _) => UpdateWidthChip(chip, w);
            chip.PointerPressed += (_, e) =>
            {
                if (chip.Tag is double ww)
                {
                    CurrentStrokeWidth = ww;
                    UpdateWidthChips();
                    StrokeWidthChanged?.Invoke(this, ww);
                }
                e.Handled = true;
            };
            _widthChips[w] = chip;
            row.Children.Add(chip);
        }
    }

    private void UpdateActiveTool()
    {
        foreach (var (k, b) in _toolButtons)
            b.IsActive = k == ActiveTool;
    }

    private void UpdateColorChips()
    {
        foreach (var (c, chip) in _colorChips)
            UpdateColorChipBorder(chip, c);
    }

    private void UpdateColorChipBorder(Border chip, Color c)
    {
        chip.BorderBrush = new SolidColorBrush(c == CurrentStroke
            ? Accent
            : Color.FromArgb(80, 255, 255, 255));
        chip.BorderThickness = new Thickness(c == CurrentStroke ? 2.5 : 1.5);
    }

    private void UpdateWidthChips()
    {
        foreach (var (w, chip) in _widthChips)
            UpdateWidthChip(chip, w);
    }

    private void UpdateWidthChip(Border chip, double w)
    {
        var active = Math.Abs(w - CurrentStrokeWidth) < 0.01;
        chip.Background = active
            ? new SolidColorBrush(Color.FromArgb(52, 72, 210, 110))
            : Brushes.Transparent;
        chip.BorderBrush = new SolidColorBrush(active
            ? Color.FromArgb(160, 72, 210, 110)
            : Color.FromArgb(48, 255, 255, 255));
    }
}
