using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using TDS.ScreenShot.Core.Capture;
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

    private WriteableBitmap _source;
    private readonly PixelRect _physicalBounds;
    private Rect _captureBounds; // logical (DIP) client area, synced on Opened
    private double _dpiScale;

    private readonly ScreenshotBackground _screenshotImage; // Layer 1: full-screen capture
    private readonly Path _dimPath;                // Layer 2: dim overlay (Xor cutout in selection)
    private readonly Border _inputCatcher;         // Layer 3: outside-selection pointer catcher
    private readonly AnnotationCanvas _annoCanvas; // Layer 5: drawing surface
    private readonly SelectionAdorner _adorner;    // Layer 6: selection frame + 8 handles
    private readonly ScreenshotToolbar _toolbar;     // Layer 7: bottom toolbar
    private readonly Border _stitchWaitLayer;        // full-screen wait during scroll stitch
    private Rect _selection;          // current selection in window coordinates
    private bool _hasSelection;
    private bool _dragCreating;
    private Rect _dragStartSelection;
    private Point _dragStartPoint;
    private ScrollCaptureController? _scrollController;
    private readonly Canvas _rootCanvas;

    public ScreenshotWindow(WriteableBitmap source, PixelRect physicalBounds, double initialDpiScale, EditRequest request)
    {
        Request = request;
        _source = source;
        _physicalBounds = physicalBounds;
        _dpiScale = initialDpiScale > 0 ? initialDpiScale : 1.0;
        _captureBounds = new Rect(0, 0, physicalBounds.Width / _dpiScale, physicalBounds.Height / _dpiScale);

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
        Position = new PixelPoint(physicalBounds.X, physicalBounds.Y);
        Width = _captureBounds.Width;
        Height = _captureBounds.Height;
        Cursor = new Cursor(StandardCursorType.Cross);

        _screenshotImage = new ScreenshotBackground
        {
            Source = source,
            SourcePixelRect = new Rect(0, 0, source.PixelSize.Width, source.PixelSize.Height),
            Width = _captureBounds.Width,
            Height = _captureBounds.Height,
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
            SourceDpiScale = _dpiScale,
            ActiveTool = request.InitialTool,
            CurrentStroke = request.DefaultStroke ?? Color.FromRgb(238, 32, 77),
            CurrentStrokeWidth = request.DefaultStrokeWidth,
            Width = _captureBounds.Width,
            Height = _captureBounds.Height,
            ClipToBounds = true,
            IsHitTestVisible = true,
            Focusable = true,
        };
        _inputCatcher = new Border
        {
            Background = Brushes.Transparent,
            IsHitTestVisible = true,
            Width = _captureBounds.Width,
            Height = _captureBounds.Height,
        };
        _toolbar = new ScreenshotToolbar(request) { IsVisible = false };

        _annoCanvas.UndoCountChanged += (_, c) => _toolbar.NotifyUndoCount(c);
        _toolbar.ToolChanged += OnToolbarToolChanged;
        _toolbar.ColorChanged += (_, c) => _annoCanvas.CurrentStroke = c;
        _toolbar.StrokeWidthChanged += (_, w) => _annoCanvas.CurrentStrokeWidth = w;
        _toolbar.UndoClicked += (_, _) => _annoCanvas.Undo();
        _toolbar.SaveClicked += async (_, _) => await DoSaveAsync();
        _toolbar.CancelClicked += (_, _) =>
        {
            if (_scrollController is { IsActive: true })
            {
                _scrollController.Cancel();
                return;
            }
            Result = new EditResult { Outcome = EditOutcome.Cancelled };
            Close();
        };
        _toolbar.ConfirmClicked += async (_, _) =>
        {
            if (_scrollController is { IsActive: true } or { HasTiles: true })
            {
                await FinishScrollCaptureAndConfirmAsync();
                return;
            }
            await DoConfirmAsync();
        };
        _toolbar.ScrollCaptureClicked += OnScrollCaptureToggled;

        _rootCanvas = new Canvas { ClipToBounds = true, Background = Brushes.Transparent };
        var root = _rootCanvas;
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

        _stitchWaitLayer = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(170, 0, 0, 0)),
            IsVisible = false,
            IsHitTestVisible = true,
            ZIndex = 60,
            Child = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 6,
                Children =
                {
                    new TextBlock
                    {
                        Text = "拼接中…",
                        FontSize = 20,
                        FontWeight = FontWeight.SemiBold,
                        Foreground = Brushes.White,
                        HorizontalAlignment = HorizontalAlignment.Center,
                    },
                    new TextBlock
                    {
                        Text = "正在合成滚动长图，请稍候",
                        FontSize = 13,
                        Foreground = new SolidColorBrush(Color.FromArgb(220, 220, 220, 230)),
                        HorizontalAlignment = HorizontalAlignment.Center,
                    },
                },
            },
        };
        root.Children.Add(_stitchWaitLayer);
        Canvas.SetLeft(_stitchWaitLayer, 0);
        Canvas.SetTop(_stitchWaitLayer, 0);
        _stitchWaitLayer.Width = _captureBounds.Width;
        _stitchWaitLayer.Height = _captureBounds.Height;

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

    /// <summary>Dim outside the selected region — matches the initial full-screen dim level.</summary>
    private static readonly IBrush SelectionDimBrush =
        new SolidColorBrush(Color.FromArgb(168, 0, 0, 0));

    private bool ShouldShowSelectionVignette()
        => _hasSelection
           || (_dragCreating && _selection.Width >= 5 && _selection.Height >= 5);

    // -----------------------------------------------------------------------------------
    //  Selection drag (creating a new region)
    // -----------------------------------------------------------------------------------
    private void OnRootPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_dragMovingHandle) return;
        if (_scrollController is { IsActive: true }) return; // block new selections during scroll capture
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
        ApplyScreenLayout();
        // HWND + Screens API are ready after the first layout pass.
        Dispatcher.UIThread.Post(() =>
        {
            ApplyScreenLayout();
            Activate();
            Focus();
            if (ScreenshotHost.CaptureMainWindow is { } main)
                ScreenshotEscapeHotkey.Attach(this, main);
        }, DispatcherPriority.Loaded);
    }

    /// <summary>
    /// Align window logical size with Avalonia's per-monitor scaling and force the
    /// native window to cover the monitor in physical pixels.
    /// </summary>
    private void ApplyScreenLayout()
    {
        var anchor = new PixelPoint(_physicalBounds.X + 1, _physicalBounds.Y + 1);
        var screen = Screens.ScreenFromPoint(anchor)
                     ?? Screens.ScreenFromPoint(new PixelPoint(_physicalBounds.X, _physicalBounds.Y))
                     ?? Screens.Primary;
        if (screen != null && screen.Scaling > 0)
            _dpiScale = screen.Scaling;

        var logicalW = _physicalBounds.Width / _dpiScale;
        var logicalH = _physicalBounds.Height / _dpiScale;
        _captureBounds = new Rect(0, 0, logicalW, logicalH);

        Position = new PixelPoint(_physicalBounds.X, _physicalBounds.Y);
        Width = logicalW;
        Height = logicalH;

        ResizeLayoutChildren();
        ApplyNativePhysicalBounds();
        UpdateSelectionVisuals();
    }

    private void ResizeLayoutChildren()
    {
        _screenshotImage.Width = _captureBounds.Width;
        _screenshotImage.Height = _captureBounds.Height;
        _screenshotImage.SourcePixelRect = new Rect(0, 0, _source.PixelSize.Width, _source.PixelSize.Height);
        _inputCatcher.Width = _captureBounds.Width;
        _inputCatcher.Height = _captureBounds.Height;
        _annoCanvas.Width = _captureBounds.Width;
        _annoCanvas.Height = _captureBounds.Height;
        _annoCanvas.SourceDpiScale = _dpiScale;
    }

    private void ApplyNativePhysicalBounds()
    {
        if (TryGetPlatformHandle()?.Handle is not IntPtr hwnd || hwnd == IntPtr.Zero)
            return;

        Win32WindowPlacement.SetPhysicalBounds(
            hwnd,
            _physicalBounds.X,
            _physicalBounds.Y,
            _physicalBounds.Width,
            _physicalBounds.Height);
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
        // Scroll-capture mode hijacks Esc/Enter so the user can finish or
        // abort the session without clicking through the toolbar.
        if (_scrollController is { IsActive: true } or { HasTiles: true })
        {
            if (e.Key == Key.Enter)
            {
                _ = FinishScrollCaptureAndConfirmAsync();
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Escape && _scrollController.IsActive)
            {
                _scrollController.Cancel();
                e.Handled = true;
                return;
            }
        }

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
    //  Scroll capture mode
    // -----------------------------------------------------------------------------------
    private void OnScrollCaptureToggled(object? sender, EventArgs e)
    {
        try
        {
            if (_scrollController is { IsActive: true })
                _ = StopScrollCaptureAsync();
            else if (_hasSelection)
                StartScrollCapture();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[scroll] toggle failed: {ex}");
            EnsureScrollUiRecovered();
        }
    }

    private void StartScrollCapture()
    {
        if (!_hasSelection) return;
        var physicalRect = new PixelRect(
            _physicalBounds.X + (int)(_selection.X * _dpiScale),
            _physicalBounds.Y + (int)(_selection.Y * _dpiScale),
            Math.Max(1, (int)(_selection.Width * _dpiScale)),
            Math.Max(1, (int)(_selection.Height * _dpiScale)));

        var capture = CaptureFactory.Create();
        var overlayHost = Content as Panel
                          ?? throw new InvalidOperationException("ScreenshotWindow.Content must be a Panel.");
        _scrollController = new ScrollCaptureController(
            capture, overlayHost, physicalRect, _dpiScale, TryGetHwnd());
        _scrollController.CapturingStopped += OnScrollCapturingStopped;
        _scrollController.Cancelled += OnScrollCaptureCancelled;
        _toolbar.SetScrollCaptureActive(true);

        // Win32 region 挖洞 + 隐藏 adorner，让选区内透出桌面 live 内容
        _screenshotImage.IsVisible = false;
        _dimPath.Fill = SelectionDimBrush;
        var outer = new RectangleGeometry(new Rect(0, 0, _captureBounds.Width, _captureBounds.Height));
        var inner = new RectangleGeometry(_selection);
        _dimPath.Data = new CombinedGeometry(GeometryCombineMode.Xor, outer, inner);
        _dimPath.IsVisible = true;
        _dimPath.IsHitTestVisible = false;   // 遮罩透传鼠标事件
        _inputCatcher.IsHitTestVisible = false;
        _annoCanvas.IsHitTestVisible = false;
        _annoCanvas.IsVisible = false;
        _annoCanvas.Clip = null;
        _toolbar.IsHitTestVisible = true;
        _adorner.IsVisible = false;
        _adorner.UpdateLayout();
        _rootCanvas.Background = Brushes.Transparent;
        UpdateLayout();

        ApplyScrollCaptureHole();
        _scrollController.Begin();
    }

    /// <summary>
    /// 用 Win32 SetWindowRgn 在 overlay 上挖选区洞。坐标必须是 HWND 客户区物理像素。
    /// <para>
    /// 曾直接用 Avalonia 逻辑 DIP 设 region——125%/150% 缩放下洞与选区偏移，BitBlt 截到遮罩边缘。
    /// 现：hole = selection × dpiScale；client 优先 <see cref="Win32WindowRegion.TryGetClientSize"/>。
    /// </para>
    /// </summary>
    private void ApplyScrollCaptureHole()
    {
        var hwnd = TryGetHwnd();
        if (hwnd == IntPtr.Zero) return;

        // Win32 region 坐标系 = 客户区物理像素；_selection 为逻辑 DIP。
        int clientW = Math.Max(1, (int)Math.Round(_captureBounds.Width * _dpiScale));
        int clientH = Math.Max(1, (int)Math.Round(_captureBounds.Height * _dpiScale));
        if (Win32WindowRegion.TryGetClientSize(hwnd, out int cw, out int ch))
        {
            clientW = cw;
            clientH = ch;
        }

        int holeX = (int)Math.Round(_selection.X * _dpiScale);
        int holeY = (int)Math.Round(_selection.Y * _dpiScale);
        int holeW = Math.Max(1, (int)Math.Round(_selection.Width * _dpiScale));
        int holeH = Math.Max(1, (int)Math.Round(_selection.Height * _dpiScale));

        Win32WindowRegion.ApplySelectionHole(
            hwnd, clientW, clientH, holeX, holeY, holeW, holeH);
        System.Diagnostics.Debug.WriteLine(
            $"[scroll] hole physical: client={clientW}x{clientH} hole=({holeX},{holeY},{holeW}x{holeH}) dpi={_dpiScale:F2}");
    }

    private void ClearScrollCaptureHole()
    {
        var hwnd = TryGetHwnd();
        if (hwnd == IntPtr.Zero) return;
        Win32WindowRegion.Clear(hwnd);
    }

    /// <summary>End scroll mode; keep raw tiles in memory (no stitch).</summary>
    private async Task StopScrollCaptureAsync()
    {
        try
        {
            var ctl = _scrollController;
            if (ctl == null || !ctl.IsActive) return;
            await ctl.StopCapturingAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[scroll] StopScrollCaptureAsync: {ex}");
            EnsureScrollUiRecovered();
        }
    }

    /// <summary>Stop if needed, stitch stored tiles once, then confirm.</summary>
    private async Task FinishScrollCaptureAndConfirmAsync()
    {
        try
        {
            var ctl = _scrollController;
            if (ctl == null || (!ctl.IsActive && !ctl.HasTiles)) return;

            if (ctl.IsActive)
                await ctl.StopCapturingAsync();
            if (ctl.TileCount < 1)
            {
                System.Diagnostics.Debug.WriteLine("[scroll] confirm aborted — no tiles captured");
                return;
            }
            WriteableBitmap? stitched = null;
            try
            {
                await ShowStitchWaitAsync();
                stitched = await ctl.StitchAsync();
            }
            finally
            {
                HideStitchWait();
            }
            if (stitched.PixelSize.Width < 1 || stitched.PixelSize.Height < 1)
            {
                System.Diagnostics.Debug.WriteLine("[scroll] confirm aborted — stitched image empty");
                stitched.Dispose();
                return;
            }
            ApplyStitchedScrollResult(stitched, preview: false);
            _scrollController = null;
            await DoConfirmAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[scroll] FinishScrollCaptureAndConfirmAsync: {ex}");
            HideStitchWait();
            EnsureScrollUiRecovered();
        }
    }

    /// <summary>Stop if needed, stitch once, prepare bitmap for save/copy.</summary>
    private async Task<WriteableBitmap?> StitchScrollTilesForExportAsync()
    {
        try
        {
            var ctl = _scrollController;
            if (ctl == null || (!ctl.IsActive && !ctl.HasTiles))
                return null;
            if (ctl.IsActive)
                await ctl.StopCapturingAsync();
            if (ctl.TileCount < 1)
                return null;
            WriteableBitmap? stitched = null;
            try
            {
                await ShowStitchWaitAsync();
                stitched = await ctl.StitchAsync();
            }
            finally
            {
                HideStitchWait();
            }
            if (stitched.PixelSize.Width < 1 || stitched.PixelSize.Height < 1)
            {
                stitched.Dispose();
                return null;
            }
            ApplyStitchedScrollResult(stitched, preview: false);
            _scrollController = null;
            return stitched;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[scroll] StitchScrollTilesForExportAsync: {ex}");
            HideStitchWait();
            EnsureScrollUiRecovered();
            return null;
        }
    }

    private async Task ShowStitchWaitAsync()
    {
        _stitchWaitLayer.Width = _captureBounds.Width;
        _stitchWaitLayer.Height = _captureBounds.Height;
        _stitchWaitLayer.IsVisible = true;
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
        await Task.Delay(32);
    }

    private void HideStitchWait() => _stitchWaitLayer.IsVisible = false;

    private void OnScrollCapturingStopped(int tileCount)
    {
        try
        {
            _toolbar.SetScrollCaptureActive(false);
            RestorePassthroughUi();
            System.Diagnostics.Debug.WriteLine($"[scroll] UI: stopped with {tileCount} raw tile(s); stitch on confirm/save");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[scroll] OnScrollCapturingStopped: {ex}");
            EnsureScrollUiRecovered();
        }
    }

    private void ApplyStitchedScrollResult(WriteableBitmap stitched, bool preview)
    {
        var oldSource = _source;
        _source = stitched;

        double newW = _source.PixelSize.Width / _dpiScale;
        double newH = _source.PixelSize.Height / _dpiScale;
        _selection = new Rect(0, 0, newW, newH);
        _hasSelection = true;

        _screenshotImage.Source = _source;
        _screenshotImage.SourcePixelRect = new Rect(0, 0, _source.PixelSize.Width, _source.PixelSize.Height);
        _annoCanvas.SourceBitmap = _source;
        _annoCanvas.SourceDpiScale = _dpiScale;
        _annoCanvas.SourceOffset = new Point(0, 0);

        if (preview)
        {
            RestorePassthroughUi();
            _captureBounds = new Rect(0, 0, newW, newH);
            _screenshotImage.Width = _captureBounds.Width;
            _screenshotImage.Height = _captureBounds.Height;
            _inputCatcher.Width = _captureBounds.Width;
            _inputCatcher.Height = _captureBounds.Height;
            _annoCanvas.Width = _captureBounds.Width;
            _annoCanvas.Height = _captureBounds.Height;
            UpdateSelectionVisuals();
        }

        if (!ReferenceEquals(oldSource, stitched))
            oldSource.Dispose();
    }

    private void OnScrollCaptureCancelled()
    {
        try
        {
            RestorePassthroughUi();
            _toolbar.SetScrollCaptureActive(false);
            _scrollController = null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[scroll] OnScrollCaptureCancelled: {ex}");
            EnsureScrollUiRecovered();
            _scrollController = null;
        }
    }

    private void RestorePassthroughUi()
    {
        ClearScrollCaptureHole();
        _dimPath.IsHitTestVisible = true;
        if (_source != null)
        {
            _screenshotImage.Source = _source;
            _screenshotImage.SourcePixelRect = new Rect(0, 0, _source.PixelSize.Width, _source.PixelSize.Height);
            _annoCanvas.SourceBitmap = _source;
            _screenshotImage.IsVisible = true;
        }
        _inputCatcher.IsHitTestVisible = true;
        _annoCanvas.IsHitTestVisible = true;
        _toolbar.IsHitTestVisible = true;
        UpdateSelectionVisuals();
    }

    private void EnsureScrollUiRecovered()
    {
        try
        {
            HideStitchWait();
            _toolbar.SetScrollCaptureActive(false);
            ClearScrollCaptureHole();
            RestorePassthroughUi();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[scroll] EnsureScrollUiRecovered: {ex}");
        }
    }

    private IntPtr TryGetHwnd()
    {
        try
        {
            return TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        }
        catch
        {
            return IntPtr.Zero;
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
        // Selection is in logical (DIP) coords; the source bitmap is physical pixels.
        var physicalRect = new Rect(
            captureRect.X * _dpiScale,
            captureRect.Y * _dpiScale,
            captureRect.Width * _dpiScale,
            captureRect.Height * _dpiScale);
        int w = (int)physicalRect.Width;
        int h = (int)physicalRect.Height;
        // 1) Crop the source 1:1 to a fresh bitmap.
        var cropped = BitmapCropper.Crop(_source, new Rect(physicalRect.X, physicalRect.Y, w, h));

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
        var annoHost = new Canvas
        {
            Width = _captureBounds.Width,
            Height = _captureBounds.Height,
            RenderTransform = new ScaleTransform(_dpiScale, _dpiScale),
        };
        annoHost.Children.Add(_annoCanvas);
        _annoCanvas.Clip = null;
        _annoCanvas.Width = _captureBounds.Width;
        _annoCanvas.Height = _captureBounds.Height;
        Canvas.SetLeft(_annoCanvas, 0);
        Canvas.SetTop(_annoCanvas, 0);
        visual.Children.Add(annoHost);
        Canvas.SetLeft(annoHost, -captureRect.X * _dpiScale);
        Canvas.SetTop(annoHost, -captureRect.Y * _dpiScale);
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
            annoHost.Children.Remove(_annoCanvas);
            visual.Children.Remove(annoHost);
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

    private async Task DoConfirmAsync()
    {
        if (!_hasSelection) return;
        try
        {
            var bmp = BuildResultBitmap(_selection);
            var png = await Task.Run(() => PngEncoder.Encode(bmp));
            Result = new EditResult
            {
                Outcome = EditOutcome.Confirmed,
                Selection = _selection,
                Annotations = _annoCanvas.Items.ToArray(),
                Result = bmp,
                PngBytes = png,
            };
            await ClipboardService.CopyBitmapAsync(this, bmp);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"DoConfirmAsync failed: {ex}");
            Result ??= new EditResult { Outcome = EditOutcome.Cancelled };
        }
        Close();
    }

    private async Task DoCopyAsync()
    {
        if (!_hasSelection && _scrollController is not ({ IsActive: true } or { HasTiles: true }))
            return;
        if (_scrollController is { IsActive: true } or { HasTiles: true })
            await StitchScrollTilesForExportAsync();
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

    /// <summary>Save to a directory without a file dialog (no selection = full capture).</summary>
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
            RadiusX = new RelativeScalar(0.7, RelativeUnit.Relative),
            RadiusY = new RelativeScalar(0.7, RelativeUnit.Relative),
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

        for (var i = 0; i < 3; i++)
        {
            flash.Opacity = (i + 1) / 3.0;
            vignette.RadiusX = new RelativeScalar(0.62 + i * 0.14, RelativeUnit.Relative);
            vignette.RadiusY = new RelativeScalar(0.62 + i * 0.14, RelativeUnit.Relative);
            await Task.Delay(15);
        }

        await Task.Delay(18);

        for (var i = 0; i < 3; i++)
        {
            flash.Opacity = 1.0 - (i + 1) / 3.0;
            vignette.RadiusX = new RelativeScalar(0.9 + i * 0.1, RelativeUnit.Relative);
            vignette.RadiusY = new RelativeScalar(0.9 + i * 0.1, RelativeUnit.Relative);
            await Task.Delay(13);
        }

        Close();
    }

    private async Task DoSaveAsync()
    {
        if (!_hasSelection && _scrollController is not ({ IsActive: true } or { HasTiles: true }))
            return;
        if (_scrollController is { IsActive: true } or { HasTiles: true })
            await StitchScrollTilesForExportAsync();
        try
        {
            var bmp = BuildResultBitmap(_selection);
            var png = await Task.Run(() => PngEncoder.Encode(bmp));
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "保存截屏",
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

        _screenshotImage.Source = null;
        _annoCanvas.SourceBitmap = null;
        try
        {
            if (_scrollController is { IsActive: true })
                _scrollController.Cancel();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[scroll] CleanupResources cancel: {ex}");
        }
        _scrollController = null;
        try { _source.Dispose(); }
        catch (ObjectDisposedException) { }
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
