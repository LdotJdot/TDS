using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using TDS.ScreenShot.Core;
using TDS.ScreenShot.Core.Capture;
using TDS.ScreenShot.UI.Controls;

namespace TDS.ScreenShot.UI.Services;

/// <summary>
/// 自动滚动截屏编排：定时 → 截图 → SendMessage 滚轮到选区中心 → 等待 → 循环。
/// </summary>
public sealed class ScrollCaptureController
{
    private readonly ICaptureService _capture;
    private readonly Panel _overlayHost;
    private readonly ScrollHintOverlay _overlay;
    private readonly PixelRect _captureRect;
    private readonly double _dpiScale;
    private readonly IntPtr _overlayHwnd;
    private readonly int _maxTiles;
    private readonly int _scrollIntervalMs;
    private readonly int _stepsPerTick;
    private readonly bool _scrollUp;

    private readonly List<WriteableBitmap> _tiles = new();
    private CancellationTokenSource? _cts;
    private bool _active;
    private int _captureBusy;
    private bool _stitchingOverlayActive;

    public event Action<int>? CapturingStopped;
    public event Action? Cancelled;

    public bool IsActive => _active;
    public int TileCount => _tiles.Count;
    public bool HasTiles => _tiles.Count > 0;

    private const int WHEEL_DELTA = 120;

    public ScrollCaptureController(
        ICaptureService capture,
        Panel overlayHost,
        PixelRect captureRect,
        double dpiScale,
        IntPtr overlayHwnd = default,
        int scrollIntervalMs = 200,
        int stepsPerTick = 1,
        bool scrollUp = false,
        int maxTiles = 200
    )
    {
        _capture = capture;
        _overlayHost = overlayHost;
        _captureRect = captureRect;
        _dpiScale = dpiScale;
        _overlayHwnd = overlayHwnd;
        _scrollIntervalMs = scrollIntervalMs;
        _stepsPerTick = stepsPerTick;
        _scrollUp = scrollUp;
        _maxTiles = maxTiles;
        _overlay = new ScrollHintOverlay();
    }

    /// <summary>
    /// 开始自动滚动截图。调用方须在此之前挖洞并隐藏 adorner。
    /// 首帧立即截图一次，随后进入定时循环：截图 → SendInput 滚轮 → 等待。
    /// </summary>
    public void Begin()
    {
        if (_active)
            return;
        _active = true;
        Interlocked.Exchange(ref _captureBusy, 0);
        _tiles.Clear();

        ShowOverlay();
        PostToOverlay(() => _overlay.ShowManualCapturing(0));
        Debug.WriteLine("[scroll] auto-scroll started");

        _cts = new CancellationTokenSource();
        _ = AutoScrollLoopAsync(_cts.Token);
    }

    private async Task AutoScrollLoopAsync(CancellationToken ct)
    {
        // 首次捕获（滚动前画面）
        var first = await CaptureOnceSafeAsync();
        if (first != null && _active && !ct.IsCancellationRequested)
            _tiles.Add(first);

        while (!ct.IsCancellationRequested && _active && _tiles.Count <= _maxTiles)
        {
            PostToOverlay(() => _overlay.ShowCapturing(_tiles.Count + 1));

            // 步骤 1：SendInput 滚轮到选区中心
            SimulateWheelAtCenter();

            // 步骤 2：等待目标窗口渲染
            try
            {
                await Task.Delay(_scrollIntervalMs, ct);
            }
            catch (TaskCanceledException)
            {
                break;
            }

            // 步骤 3：截图
            var bmp = await CaptureOnceSafeAsync();
            if (bmp == null || ct.IsCancellationRequested || !_active)
            {
                bmp?.Dispose();
                break;
            }

            _tiles.Add(bmp);
            PostToOverlay(() => _overlay.ShowManualCapturing(_tiles.Count));
            Debug.WriteLine($"[scroll] tile #{_tiles.Count} captured");
        }

        // 循环结束，通知外部
        _active = false;
        HideOverlay();
        Debug.WriteLine($"[scroll] loop ended — {_tiles.Count} tile(s)");
        await InvokeCapturingStoppedAsync(_tiles.Count);
    }

    /// <summary>
    /// 向选区中心屏幕坐标下的目标 HWND 投递 WM_MOUSEWHEEL，与光标位置无关。
    /// </summary>
    private void SimulateWheelAtCenter()
    {
        int cx = _captureRect.X + _captureRect.Width / 2;
        int cy = _captureRect.Y + _captureRect.Height / 2;
        int delta = (_scrollUp ? 1 : -1) * WHEEL_DELTA * _stepsPerTick;

        if (!Win32CaptureService.TrySendWheelAt(cx, cy, delta, _overlayHwnd))
            Debug.WriteLine($"[scroll] SimulateWheel failed at ({cx},{cy})");
    }

    public async Task StopCapturingAsync()
    {
        if (!_active && _cts == null)
            return;
        try
        {
            _cts?.Cancel();
            _active = false;
            HideOverlay();
            Debug.WriteLine($"[scroll] stopped — {_tiles.Count} raw tile(s) kept");
            await InvokeCapturingStoppedAsync(_tiles.Count);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[scroll] stop failed: {ex}");
            _active = false;
            try
            {
                HideOverlay();
            }
            catch { }
            await InvokeCapturingStoppedAsync(_tiles.Count);
        }
    }

    /// <summary>
    /// 确认/保存时一次性拼接。
    /// </summary>
    public async Task<WriteableBitmap> StitchAsync()
    {
        _stitchingOverlayActive = true;
        WriteableBitmap? resultBitmap = null;
        try
        {
            ShowOverlay();
            await ShowStitchingUiAsync();

            if (_tiles.Count == 0)
            {
                Debug.WriteLine("[scroll] stitch skipped — no tiles");
                return EmptyTile();
            }
            if (_tiles.Count == 1)
            {
                resultBitmap = CloneTile(_tiles[0]);
            }
            else
            {
                var tilesCopy = _tiles.ToArray();
                Debug.WriteLine(
                    $"[scroll] stitching {tilesCopy.Length} raw tile(s) — one-time merge"
                );
                var stitchResult = await Task.Run(() => ScrollStitcher.Stitch(tilesCopy));
                resultBitmap = stitchResult.Bitmap;
                Debug.WriteLine(
                    $"[scroll] stitch complete — {tilesCopy.Length} tiles → h={resultBitmap.PixelSize.Height}"
                );
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[scroll] stitch failed: {ex}");
            resultBitmap = _tiles.Count > 0 ? CloneTile(_tiles[0]) : EmptyTile();
        }
        finally
        {
            _stitchingOverlayActive = false;
            HideOverlay();
        }
        DisposeTilesExcept(null);
        return resultBitmap ?? EmptyTile();
    }

    /// <summary>
    /// 取消滚动——释放所有资源，不保留 tiles。
    /// </summary>
    public void Cancel() => _ = CancelAsync();

    public async Task CancelAsync()
    {
        if (!_active && _tiles.Count == 0)
            return;
        try
        {
            _cts?.Cancel();
            _active = false;
            HideOverlay();
            DisposeTiles();
            await InvokeCancelledAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[scroll] cancel failed: {ex}");
            _active = false;
            try
            {
                HideOverlay();
                DisposeTiles();
            }
            catch { }
            await InvokeCancelledAsync();
        }
    }

    // ── 内部辅助 ──

    private async Task<WriteableBitmap?> CaptureOnceSafeAsync()
    {
        try
        {
            Interlocked.Exchange(ref _captureBusy, 1);
            return await Task.Run(
                () =>
                    _capture.CaptureScreenRect(
                        _captureRect.X,
                        _captureRect.Y,
                        _captureRect.Width,
                        _captureRect.Height
                    )
            );
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[scroll] capture failed: {ex.Message}");
            return null;
        }
        finally
        {
            Interlocked.Exchange(ref _captureBusy, 0);
        }
    }

    private WriteableBitmap EmptyTile()
    {
        return new WriteableBitmap(
            new PixelSize(_captureRect.Width, _captureRect.Height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Opaque
        );
    }

    private static WriteableBitmap CloneTile(WriteableBitmap src)
    {
        int w = src.PixelSize.Width;
        int h = src.PixelSize.Height;
        var clone = new WriteableBitmap(
            new PixelSize(w, h),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Opaque
        );
        using var sLock = src.Lock();
        using var dLock = clone.Lock();
        unsafe
        {
            int rowBytes = w * 4;
            byte* s = (byte*)sLock.Address;
            byte* d = (byte*)dLock.Address;
            for (int y = 0; y < h; y++)
                Buffer.MemoryCopy(
                    s + (long)y * sLock.RowBytes,
                    d + (long)y * dLock.RowBytes,
                    rowBytes,
                    rowBytes
                );
        }
        return clone;
    }

    private void DisposeTilesExcept(WriteableBitmap? keep)
    {
        for (int i = 0; i < _tiles.Count; i++)
        {
            if (ReferenceEquals(_tiles[i], keep))
                continue;
            try
            {
                _tiles[i]?.Dispose();
            }
            catch (ObjectDisposedException) { }
        }
        _tiles.Clear();
    }

    private void DisposeTiles() => DisposeTilesExcept(null);

    private async Task ShowStitchingUiAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            _overlay.ShowStitchingInProgress();
            LayoutOverlay();
        });
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
        await Task.Delay(32);
    }

    private void ShowOverlay()
    {
        try
        {
            if (_overlay.Parent is null)
                _overlayHost.Children.Add(_overlay);
            _overlay.IsVisible = true;
            LayoutOverlay();
            if (!_stitchingOverlayActive && _active)
                PostToOverlay(() => _overlay.ShowManualCapturing(_tiles.Count));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[scroll] ShowOverlay failed: {ex.Message}");
        }
    }

    private void HideOverlay()
    {
        try
        {
            _overlay.IsVisible = false;
            if (_overlay.Parent is Panel p)
                p.Children.Remove(_overlay);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[scroll] HideOverlay failed: {ex.Message}");
        }
    }

    private void PostToOverlay(Action action)
    {
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                if (!_active && !_stitchingOverlayActive)
                    return;
                action();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[scroll] overlay update skipped: {ex.Message}");
            }
        });
    }

    private async Task InvokeCapturingStoppedAsync(int count)
    {
        try
        {
            await Dispatcher.UIThread.InvokeAsync(() => CapturingStopped?.Invoke(count));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[scroll] CapturingStopped callback failed: {ex.Message}");
        }
    }

    private async Task InvokeCancelledAsync()
    {
        try
        {
            await Dispatcher.UIThread.InvokeAsync(() => Cancelled?.Invoke());
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[scroll] Cancelled callback failed: {ex.Message}");
        }
    }

    private void LayoutOverlay()
    {
        try
        {
            var hostBounds = _overlayHost.Bounds;
            _overlay.Measure(Size.Infinity);
            var sz = _overlay.DesiredSize;
            if (hostBounds.Height > 0)
            {
                Canvas.SetLeft(_overlay, Math.Max(8, hostBounds.Width / 2 - sz.Width / 2));
                Canvas.SetTop(_overlay, 24);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[scroll] LayoutOverlay failed: {ex.Message}");
        }
    }
}
