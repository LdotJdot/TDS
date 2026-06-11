using Avalonia.Media;

namespace TDS.ScreenShot.UI.Models;

public sealed class EditRequest
{
    /// <summary>Default stroke color for tools. <c>null</c> = use UI default (red).</summary>
    public Color? DefaultStroke { get; init; } = Color.FromRgb(238, 32, 77);

    /// <summary>Default stroke width in DIPs.</summary>
    public double DefaultStrokeWidth { get; init; } = 3;

    /// <summary>If true, the toolbar offers a "Save" button in addition to "Done".</summary>
    public bool ShowSaveButton { get; init; } = true;

    /// <summary>Initial tool selected on toolbar (Rect by default).</summary>
    public string InitialTool { get; init; } = ToolIds.Rect;

    /// <summary>Optional hotkey for fast capture (e.g. Ctrl+Shift+S). Not implemented in v1.</summary>
    public string? HotKey { get; init; }

    /// <summary>
    /// Whether the toolbar exposes a "scroll capture" button. When true, the user
    /// can enter scroll-capture mode after drawing a selection: the mouse wheel
    /// will scroll the underlying window and auto-stitch the captured tiles.
    /// </summary>
    public bool EnableScrollCapture { get; init; } = true;
}

public static class ToolIds
{
    public const string Select = "select";
    public const string Rect = "rect";
    public const string Ellipse = "ellipse";
    public const string Arrow = "arrow";
    public const string Pen = "pen";
    public const string Text = "text";
    public const string Mosaic = "mosaic";
}