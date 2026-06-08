using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using TDS.ScreenShot.Core.Annotations;
using TDS.ScreenShot.UI.Controls;
using TDS.ScreenShot.UI.Models;
using TDS.ScreenShot.UI.Services;
using TDS.Screenshot;
using Path = Avalonia.Controls.Shapes.Path;

namespace TDS.ScreenShot.UI.Windows;

/// <summary>
/// Fullscreen borderless window that hosts the screenshot capture, selection,
/// and annotation UI. Closes itself when the user confirms / cancels.
/// </summary>
public sealed class ScreenshotWindow : Window
{
    public EditResult? Result { get; private set; }
    public EditRequest Request { get; }

    private readonly WriteableBitmap _source;
    private readonly Rect _captureBounds; // in window/pixel space

    private readonly Image _screenshotImage;       // Layer 1: single full-screen screenshot
    private readonly Path _dimPath;                // Layer 2: dim overlay (Xor cutout in selection)
    private readonly Border _inputCatcher;         // Layer 3: outside-selection pointer catcher
    private readonly AnnotationCanvas _annoCanvas; // Layer 5: drawing surface
    private readonly SelectionAdorner _adorner;    // Layer 6: selection frame + 8 handles
    private readonly ScreenshotToolbar _toolbar;     // Layer 7: bottom toolbar
    private Rect _selection;          // current selection in window coordinates
    private bool _hasSelection;
    private bool _dragCreating;
    private Rect _dragStartSelection;
    private Point _dragStartPoint;

    public ScreenshotWindow(WriteableBitmap source, Rect captureBounds, EditRequest request)
    {
        Request = request;
        _source = source;
        _captureBounds = captureBounds;

        // Window configuration: borderless, topmost, full coverage
        SystemDecorations = SystemDecorations.None;
        WindowState = WindowState.Normal;
        ShowInTaskbar = false;
        Topmost = true;
        CanResize = false;
        Focusable = true;
        Background = Brushes.Transparent;
        ExtendClientAreaToDecorationsHint = false;
        SizeToContent = SizeToContent.Manual;
        Position = new PixelPoint((int)captureBounds.X, (int)captureBounds.Y);
        Width = captureBounds.Width;
        Height = captureBounds.Height;
        Cursor = new Cursor(StandardCursorType.Cross);

        // Single screenshot layer: the dim overlay uses Xor to punch a hole so
        // the selection area stays bright without a second Image / GPU texture.
        _screenshotImage = new Image
        {
            Source = source,
            Stretch = Stretch.Fill,
            Width = captureBounds.Width,
            Height = captureBounds.Height,
            IsHitTestVisible = false,
        };
        _dimPath = new Path
        {
            Fill = SelectionDimBrush,
            IsHitTestVisible = false,
        };
        _annoCanvas = new AnnotationCanvas
        {
            SourceBitmap = source,
            SourceOffset = new Point(0, 0),
            ActiveTool = request.InitialTool,
            CurrentStroke = request.DefaultStroke ?? Color.FromRgb(238, 32, 77),
            CurrentStrokeWidth = request.DefaultStrokeWidth,
            Width = captureBounds.Width,
            Height = captureBounds.Height,
            ClipToBounds = true,
            IsHitTestVisible = true,
            Focusable = true,
        };
        _inputCatcher = new Border
        {
            Background = Brushes.Transparent,
            IsHitTestVisible = true,
            Width = captureBounds.Width,
            Height = captureBounds.Height,
        };
        _toolbar = new ScreenshotToolbar(request) { IsVisible = false };

        _annoCanvas.UndoCountChanged += (_, c) => _toolbar.NotifyUndoCount(c);
        _toolbar.ToolChanged += OnToolbarToolChanged;
        _toolbar.ColorChanged += (_, c) => _annoCanvas.CurrentStroke = c;
        _toolbar.StrokeWidthChanged += (_, w) => _annoCanvas.CurrentStrokeWidth = w;
        _toolbar.UndoClicked += (_, _) => _annoCanvas.Undo();
        _toolbar.SaveClicked += async (_, _) => await DoSaveAsync();
        _toolbar.CancelClicked += (_, _) => { Result = new EditResult { Outcome = EditOutcome.Cancelled }; Close(); };
        _toolbar.ConfirmClicked += async (_, _) => await DoConfirmAsync();

        var root = new Canvas { ClipToBounds = true, Background = Brushes.Transparent };
        root.Children.Add(_screenshotImage);
        Canvas.SetLeft(_screenshotImage, 0); Canvas.SetTop(_screenshotImage, 0);
        root.Children.Add(_dimPath);
        // _inputCatcher sits below the annotation/adorner layers so clicks
        // inside the selection reach AnnotationCanvas, while clicks outside
        // still bubble up for starting a new selection drag.
        root.Children.Add(_inputCatcher);
        Canvas.SetLeft(_inputCatcher, 0); Canvas.SetTop(_inputCatcher, 0);
        _inputCatcher.ZIndex = 1;
        root.Children.Add(_annoCanvas);
        Canvas.SetLeft(_annoCanvas, 0);
        Canvas.SetTop(_annoCanvas, 0);
        _annoCanvas.ZIndex = 10;
        // Border + 8 resize handles are added directly to root so they sit
        // above AnnotationCanvas but do not block pointer events elsewhere.
        _adorner = new SelectionAdorner(root);
        _adorner.HandleDrag += OnHandleDrag;
        _toolbar.ZIndex = 30;
        root.Children.Add(_toolbar);

        Content = root;

        _inputCatcher.PointerPressed += OnRootPointerPressed;
        _inputCatcher.PointerMoved += OnRootPointerMoved;
        _inputCatcher.PointerReleased += OnRootPointerReleased;
        // Also listen on root so drags that start on the catcher keep
        // receiving move/release even when the pointer leaves its bounds.
        root.PointerMoved += OnRootPointerMoved;
        root.PointerReleased += OnRootPointerReleased;

        // Tunnel so Esc closes immediately even when a child (e.g. inline TextBox) has focus.
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        Opened += OnScreenshotOpened;
        Closed += OnScreenshotClosed;

        // Initial state: light full-screen dim until the user drags a real selection.
        UpdateSelectionVisuals();
    }

    /// <summary>Subtle hint that screenshot mode is active (no selection yet).</summary>
    private static readonly IBrush IdleDimBrush =
        new SolidColorBrush(Color.FromArgb(88, 0, 0, 0));

    /// <summary>Stronger dim outside the selected region.</summary>
    private static readonly IBrush SelectionDimBrush =
        new SolidColorBrush(Color.FromArgb(210, 0, 0, 0));

    private bool ShouldShowSelectionVignette()
        => _hasSelection
           || (_dragCreating && _selection.Width >= 5 && _selection.Height >= 5);

    // -----------------------------------------------------------------------------------
    //  Selection drag (creating a new region)
    // -----------------------------------------------------------------------------------
    private void OnRootPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_dragMovingHandle) return;
        var p = e.GetPosition(Content as Visual);
        // Inside an existing selection: AnnotationCanvas (above) or a resize
        // handle on the adorner (above) owns the event — do nothing here.
        if (_hasSelection && _selection.Contains(p)) return;
        _dragCreating = true;
        _dragStartPoint = p;
        _dragStartSelection = default;
        _hasSelection = false;
        _selection = new Rect(p.X, p.Y, 0, 0);
        _toolbar.IsVisible = false;
        e.Pointer.Capture(_inputCatcher);
        e.Handled = true;
    }

    private void OnRootPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_dragCreating) return;
        var p = e.GetPosition(Content as Visual);
        _selection = NormalizeRect(_dragStartPoint, p);
        UpdateSelectionVisuals();
        e.Handled = true;
    }

    private void OnRootPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_dragCreating) return;
        _dragCreating = false;
        if (_selection.Width < 5 || _selection.Height < 5)
        {
            _selection = default;
            _hasSelection = false;
            UpdateSelectionVisuals();
            return;
        }
        _hasSelection = true;
        UpdateSelectionVisuals();
        _annoCanvas.Focus();
        e.Handled = true;
    }

    private bool _dragMovingHandle;

    // -----------------------------------------------------------------------------------
    //  Resize handles
    // -----------------------------------------------------------------------------------
    private void OnHandleDrag(object? sender, HandleDragEventArgs e)
    {
        if (e.State == HandleDragState.Start)
        {
            _dragMovingHandle = true;
            _dragStartPoint = e.Position;
            _dragStartSelection = _selection;
        }
        else if (e.State == HandleDragState.Move)
        {
            var p = e.Position;
            double dx = p.X - _dragStartPoint.X;
            double dy = p.Y - _dragStartPoint.Y;
            _selection = ApplyHandle(_dragStartSelection, e.Kind, dx, dy);
            UpdateSelectionVisuals();
        }
        else
        {
            _dragMovingHandle = false;
            if (_selection.Width < 5 || _selection.Height < 5)
            {
                _hasSelection = false;
                _selection = default;
                _toolbar.IsVisible = false;
                UpdateSelectionVisuals();
            }
        }
    }

    private static Rect ApplyHandle(Rect start, HandleKind k, double dx, double dy)
    {
        double L = start.X, T = start.Y, R = start.Right, B = start.Bottom;
        switch (k)
        {
            case HandleKind.TopLeft:    L += dx; T += dy; break;
            case HandleKind.Top:        T += dy; break;
            case HandleKind.TopRight:   R += dx; T += dy; break;
            case HandleKind.Left:       L += dx; break;
            case HandleKind.Right:      R += dx; break;
            case HandleKind.BottomLeft: L += dx; B += dy; break;
            case HandleKind.Bottom:     B += dy; break;
            case HandleKind.BottomRight:R += dx; B += dy; break;
        }
        double x = Math.Min(L, R), y = Math.Min(T, B);
        double w = Math.Max(1, Math.Abs(R - L));
        double h = Math.Max(1, Math.Abs(B - T));
        return new Rect(x, y, w, h);
    }

    // -----------------------------------------------------------------------------------
    //  Visual update
    // -----------------------------------------------------------------------------------
    private void UpdateSelectionVisuals()
    {
        if (ShouldShowSelectionVignette())
        {
            _dimPath.Fill = SelectionDimBrush;
            var outer = new RectangleGeometry(new Rect(0, 0, _captureBounds.Width, _captureBounds.Height));
            var inner = new RectangleGeometry(_selection);
            _dimPath.Data = new CombinedGeometry(GeometryCombineMode.Xor, outer, inner);
            _dimPath.IsVisible = true;

            // Annotations live in capture/screen coordinates; clip to the selection for display.
            _annoCanvas.Width = _captureBounds.Width;
            _annoCanvas.Height = _captureBounds.Height;
            Canvas.SetLeft(_annoCanvas, 0);
            Canvas.SetTop(_annoCanvas, 0);
            _annoCanvas.SourceOffset = new Point(0, 0);
            _annoCanvas.Clip = new RectangleGeometry(_selection);
            _annoCanvas.IsVisible = true;

            _toolbar.IsVisible = _hasSelection;
            if (_hasSelection) PositionToolbar();
        }
        else
        {
            _dimPath.Fill = IdleDimBrush;
            _dimPath.Data = new RectangleGeometry(new Rect(0, 0, _captureBounds.Width, _captureBounds.Height));
            _dimPath.IsVisible = true;
            _annoCanvas.IsVisible = false;
            _toolbar.IsVisible = false;
        }

        _adorner.Selection = _selection;
        _adorner.IsVisible = _hasSelection || _dragCreating;
        _adorner.UpdateLayout();
    }

    private void PositionToolbar()
    {
        _toolbar.Measure(Size.Infinity);
        var sz = _toolbar.DesiredSize;
        double x = _selection.Center.X - sz.Width / 2;
        double y = _selection.Bottom + 12;
        if (y + sz.Height > _captureBounds.Height - 8) y = _selection.Y - sz.Height - 12;
        if (y < 8) y = 8;
        if (x < 8) x = 8;
        if (x + sz.Width > _captureBounds.Width - 8) x = _captureBounds.Width - sz.Width - 8;
        Canvas.SetLeft(_toolbar, x);
        Canvas.SetTop(_toolbar, y);
    }

    // -----------------------------------------------------------------------------------
    //  Keyboard
    // -----------------------------------------------------------------------------------
    private void OnToolbarToolChanged(object? sender, string id)
    {
        _annoCanvas.ActiveTool = id;
        _annoCanvas.ClearSelection();
        Cursor = id == ToolIds.Select
            ? new Cursor(StandardCursorType.Arrow)
            : new Cursor(StandardCursorType.Cross);
    }

    private void OnScreenshotOpened(object? sender, EventArgs e)
    {
        Activate();
        Focus();
        // HWND may not be ready on the first Opened tick; register Esc after layout.
        Dispatcher.UIThread.Post(() =>
        {
            Activate();
            Focus();
            if (ScreenshotHost.CaptureMainWindow is { } main)
                ScreenshotEscapeHotkey.Attach(this, main);
        }, DispatcherPriority.Loaded);
    }

    private void OnScreenshotClosed(object? sender, EventArgs e)
    {
        ScreenshotEscapeHotkey.Detach(this);
        CleanupResources();
    }

    internal void RequestCancelFromEscape()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (!IsVisible || _disposed)
                return;
            Result = new EditResult { Outcome = EditOutcome.Cancelled };
            Close();
        });
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Result = new EditResult { Outcome = EditOutcome.Cancelled };
            e.Handled = true;
            Close();
            return;
        }
        if (e.Key == Key.Enter)
        {
            _ = DoConfirmAsync();
            return;
        }
        if (e.Key == Key.Delete)
        {
            _annoCanvas.DeleteSelected();
            e.Handled = true;
            return;
        }
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            if (e.Key == Key.Z) { _annoCanvas.Undo(); e.Handled = true; }
            else if (e.Key == Key.C) { _ = DoCopyAsync(); e.Handled = true; }
            else if (e.Key == Key.S) { _ = DoSaveAsync(); e.Handled = true; }
        }
    }

    // -----------------------------------------------------------------------------------
    //  Output
    // -----------------------------------------------------------------------------------
    private Rect GetEffectiveSelectionForSave()
    {
        if (_hasSelection && _selection.Width >= 1 && _selection.Height >= 1)
            return _selection;
        return new Rect(0, 0, _captureBounds.Width, _captureBounds.Height);
    }

    private Bitmap BuildResultBitmap(Rect captureRect)
    {
        int w = (int)captureRect.Width;
        int h = (int)captureRect.Height;
        // 1) Crop the source 1:1 to a fresh bitmap.
        var cropped = BitmapCropper.Crop(_source, new Rect(captureRect.X, captureRect.Y, w, h));

        // 2) Render annotations on top of the 1:1 crop. AnnotationCanvas only
        //    draws markup (not the background); mosaic still needs SourceBitmap
        //    + SourceOffset to sample the correct source pixels.
        var origParent = _annoCanvas.Parent as Panel;
        var origIndex = origParent != null ? origParent.Children.IndexOf(_annoCanvas) : -1;
        var origClip = _annoCanvas.Clip;
        if (origParent != null) origParent.Children.Remove(_annoCanvas);

        var visual = new Canvas { Width = w, Height = h, Background = Brushes.Transparent };
        var bg = new Image
        {
            Source = cropped,
            Width = w,
            Height = h,
            Stretch = Stretch.Fill,
            IsHitTestVisible = false,
        };
        visual.Children.Add(bg);
        Canvas.SetLeft(bg, 0); Canvas.SetTop(bg, 0);
        visual.Children.Add(_annoCanvas);
        _annoCanvas.Clip = null;
        _annoCanvas.Width = _captureBounds.Width;
        _annoCanvas.Height = _captureBounds.Height;
        Canvas.SetLeft(_annoCanvas, -captureRect.X);
        Canvas.SetTop(_annoCanvas, -captureRect.Y);
        try
        {
            visual.Measure(new Size(w, h));
            visual.Arrange(new Rect(0, 0, w, h));
            var rtb = new RenderTargetBitmap(new PixelSize(w, h));
            rtb.Render(visual);
            return rtb;
        }
        finally
        {
            (cropped as IDisposable)?.Dispose();
            visual.Children.Remove(_annoCanvas);
            if (origParent != null)
            {
                if (origIndex >= 0 && origIndex <= origParent.Children.Count)
                    origParent.Children.Insert(origIndex, _annoCanvas);
                else
                    origParent.Children.Add(_annoCanvas);
            }
            Canvas.SetLeft(_annoCanvas, 0);
            Canvas.SetTop(_annoCanvas, 0);
            _annoCanvas.Width = _captureBounds.Width;
            _annoCanvas.Height = _captureBounds.Height;
            _annoCanvas.Clip = origClip;
        }
    }

    /// <summary>
    /// Minimal control that draws an arbitrary sub-rect of an <see cref="IBitmap"/>
    /// into its own bounds, 1:1, with no scaling. Used by
    /// <see cref="BuildResultBitmap"/> to render the cropped selection without
    /// depending on the Image control's Stretch/RenderTransform quirks.
    /// </summary>
    // CroppedSourceView now lives in ScreenShot.UI.Services.BitmapCropper and
    // is reused by the public Crop() helper.

    private async Task DoConfirmAsync()
    {
        if (!_hasSelection) return;
        try
        {
            var bmp = BuildResultBitmap(_selection);
            // BuildResultBitmap returns a Bitmap (RenderTargetBitmap), not a
            // WriteableBitmap, so don't cast here — the old cast threw
            // InvalidCastException and left the window in a half-closed state.
            var png = await Task.Run(() => PngEncoder.Encode(bmp));
            Result = new EditResult
            {
                Outcome = EditOutcome.Confirmed,
                Selection = _selection,
                Annotations = _annoCanvas.Items.ToArray(),
                Result = bmp,
                PngBytes = png,
            };
            // Also copy to clipboard by default (matches Win+Shift+S behavior).
            await ClipboardService.CopyBitmapAsync(this, bmp);
        }
        catch (Exception ex)
        {
            // Never leave the user staring at a frozen overlay: always close
            // and surface a Cancelled result so the caller returns cleanly.
            System.Diagnostics.Debug.WriteLine($"DoConfirmAsync failed: {ex}");
            Result ??= new EditResult { Outcome = EditOutcome.Cancelled };
        }
        Close();
    }

    private async Task DoCopyAsync()
    {
        if (!_hasSelection) return;
        try
        {
            var bmp = BuildResultBitmap(_selection);
            var png = await Task.Run(() => PngEncoder.Encode(bmp));
            await ClipboardService.CopyBitmapAsync(this, bmp);
            Result = new EditResult
            {
                Outcome = EditOutcome.Copied,
                Selection = _selection,
                Annotations = _annoCanvas.Items.ToArray(),
                Result = bmp,
                PngBytes = png,
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"DoCopyAsync failed: {ex}");
            Result ??= new EditResult { Outcome = EditOutcome.Cancelled };
        }
        Close();
    }

    public async Task<(bool Ok, string? SavedPath, string? Error)> TryQuickSaveAsync(
        string? saveDirectory, string fallbackDirectory)
    {
        try
        {
            var captureRect = GetEffectiveSelectionForSave();
            var bmp = BuildResultBitmap(captureRect);
            var png = await Task.Run(() => PngEncoder.Encode(bmp));
            var (ok, path, error) = await ScreenshotFileSaver.SavePngAsync(
                png, saveDirectory, fallbackDirectory);
            if (ok)
            {
                Result = new EditResult
                {
                    Outcome = EditOutcome.Saved,
                    Selection = captureRect,
                    Annotations = _annoCanvas.Items.ToArray(),
                    Result = bmp,
                    PngBytes = png,
                    SavedPath = path,
                };
            }
            return (ok, path, error);
        }
        catch (Exception ex)
        {
            return (false, null, ex.Message);
        }
    }

    /// <summary>Vignette camera-flash (bright edges, dimmer center) then exit.</summary>
    public async Task PlaySaveFlashAndCloseAsync()
    {
        if (Content is not Canvas root)
        {
            Close();
            return;
        }

        _toolbar.IsVisible = false;
        _adorner.Selection = default;

        var vignette = new RadialGradientBrush
        {
            Center = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
            GradientOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
            Radius = 0.7,
            GradientStops =
            {
                new GradientStop(Color.FromArgb(48, 255, 255, 255), 0),
                new GradientStop(Color.FromArgb(170, 255, 255, 255), 0.45),
                new GradientStop(Color.FromArgb(255, 255, 255, 255), 1),
            },
        };

        var flash = new Border
        {
            Background = vignette,
            Opacity = 0,
            Width = _captureBounds.Width,
            Height = _captureBounds.Height,
            IsHitTestVisible = true,
            ZIndex = 2000,
        };
        Canvas.SetLeft(flash, 0);
        Canvas.SetTop(flash, 0);
        root.Children.Add(flash);

        // Burst in: edges light up first, ring expands (~45ms)
        for (var i = 0; i < 3; i++)
        {
            flash.Opacity = (i + 1) / 3.0;
            vignette.Radius = 0.62 + i * 0.14;
            await Task.Delay(15);
        }

        await Task.Delay(18);

        // Quick fade out (~40ms)
        for (var i = 0; i < 3; i++)
        {
            flash.Opacity = 1.0 - (i + 1) / 3.0;
            vignette.Radius = 0.9 + i * 0.1;
            await Task.Delay(13);
        }

        Close();
    }

    private async Task DoSaveAsync()
    {
        if (!_hasSelection) return;
        try
        {
            var bmp = BuildResultBitmap(_selection);
            var png = await Task.Run(() => PngEncoder.Encode(bmp));
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save Screenshot",
                DefaultExtension = "png",
                SuggestedFileName = ScreenshotFileSaver.BuildFileName(),
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("PNG 图片") { Patterns = new[] { "*.png" } }
                }
            });
            if (file is null) return;
            var path = file.Path.AbsolutePath;
            await using var fs = File.Create(path);
            await fs.WriteAsync(png);
            Result = new EditResult
            {
                Outcome = EditOutcome.Saved,
                Selection = _selection,
                Annotations = _annoCanvas.Items.ToArray(),
                Result = bmp,
                PngBytes = png,
                SavedPath = path,
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"DoSaveAsync failed: {ex}");
            Result ??= new EditResult { Outcome = EditOutcome.Cancelled };
        }
        Close();
    }

    // -----------------------------------------------------------------------------------
    //  Cleanup
    // -----------------------------------------------------------------------------------
    private bool _disposed;

    private void CleanupResources()
    {
        if (_disposed) return;
        _disposed = true;

        // Drop all references to the full-screen capture, then dispose it
        // immediately instead of waiting for GC (can be tens of MB).
        _screenshotImage.Source = null;
        _annoCanvas.SourceBitmap = null;
        _source.Dispose();
    }

    // -----------------------------------------------------------------------------------
    //  Util
    // -----------------------------------------------------------------------------------
    private static Rect NormalizeRect(Point a, Point b)
    {
        var x = Math.Min(a.X, b.X);
        var y = Math.Min(a.Y, b.Y);
        var w = Math.Max(1, Math.Abs(a.X - b.X));
        var h = Math.Max(1, Math.Abs(a.Y - b.Y));
        return new Rect(x, y, w, h);
    }
}