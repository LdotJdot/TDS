using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace TDS.ScreenShot.UI.Controls;

/// <summary>
/// Hint card during manual scroll-capture. Stitching happens on confirm/save only.
/// </summary>
public sealed class ScrollHintOverlay : Border
{
    private readonly TextBlock _title;
    private readonly TextBlock _sub;
    private string? _statsSubtitle;
    private DispatcherTimer? _restoreTimer;

    public static readonly StyledProperty<string> TitleProperty = AvaloniaProperty.Register<
        ScrollHintOverlay,
        string
    >(nameof(Title), "滚动截屏中…");

    public static readonly StyledProperty<string> SubtitleProperty = AvaloniaProperty.Register<
        ScrollHintOverlay,
        string
    >(nameof(Subtitle), "在选区内滚动滚轮截图 · 点按钮停止");

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Subtitle
    {
        get => GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public ScrollHintOverlay()
    {
        Background = new SolidColorBrush(Color.FromArgb(220, 18, 18, 22));
        BorderBrush = new SolidColorBrush(Color.FromArgb(70, 255, 255, 255));
        BorderThickness = new Thickness(0.5);
        CornerRadius = new CornerRadius(8);
        Padding = new Thickness(12, 8);
        BoxShadow = new BoxShadows(
            new BoxShadow
            {
                OffsetX = 0,
                OffsetY = 4,
                Blur = 12,
                Color = Color.FromArgb(110, 0, 0, 0),
            }
        );
        IsHitTestVisible = false;

        _title = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.FromRgb(245, 245, 250)),
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
        };
        _sub = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.FromArgb(190, 220, 220, 230)),
            FontSize = 11,
            Margin = new Thickness(0, 2, 0, 0),
        };

        var stack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 0 };
        stack.Children.Add(_title);
        stack.Children.Add(_sub);
        Child = stack;

        TitleProperty.Changed.AddClassHandler<ScrollHintOverlay>((b, _) => b._title.Text = b.Title);
        SubtitleProperty.Changed.AddClassHandler<ScrollHintOverlay>(
            (b, _) => b._sub.Text = b.Subtitle
        );
        _title.Text = Title;
        _sub.Text = Subtitle;
    }

    public void ShowManualCapturing(int tileCount)
    {
        _restoreTimer?.Stop();
        BorderBrush = new SolidColorBrush(Color.FromArgb(200, 100, 180, 255));
        Title = tileCount <= 1 ? "滚动截屏中…" : $"滚动截屏中…  已存 {tileCount} 张原图";
        _statsSubtitle = "滚一下：先截当前屏再滚动 · 停止后补最后一屏 · √/保存时一次性拼接";
        Subtitle = _statsSubtitle;
    }

    public void ShowCapturing(int nextTileIndex)
    {
        _restoreTimer?.Stop();
        BorderBrush = new SolidColorBrush(Color.FromArgb(200, 120, 200, 255));
        Title = $"截图中…  第 {nextTileIndex} 张";
        Subtitle = "截取当前画面";
    }

    public void ShowDuplicateFrame()
    {
        _restoreTimer?.Stop();
        BorderBrush = new SolidColorBrush(Color.FromArgb(200, 255, 180, 60));
        Title = "画面未变化";
        Subtitle = "请继续滚动后再截下一张";
        ScheduleRestoreSubtitle();
    }

    public void ShowCaptureFailed()
    {
        _restoreTimer?.Stop();
        BorderBrush = new SolidColorBrush(Color.FromArgb(200, 255, 120, 80));
        Title = "截图失败";
        Subtitle = "请再滚动一次重试";
        ScheduleRestoreSubtitle();
    }

    public void ShowCapturingStopped(int tileCount) => ShowManualStopped(tileCount);

    public void ShowManualStopped(int tileCount)
    {
        Title = tileCount <= 1 ? "滚动截屏已结束" : $"滚动截屏已结束 · 已存 {tileCount} 张原图";
        _statsSubtitle = "点 √ 确认或保存时再拼接长图";
        Subtitle = _statsSubtitle;
    }

    public void ShowTileCapReached(int tileCount)
    {
        Title = $"已达上限 · 已存 {tileCount} 张原图";
        _statsSubtitle = "点滚动按钮停止 · √ 或保存时再拼接";
        Subtitle = _statsSubtitle;
    }

    public void ShowStitchingInProgress()
    {
        _restoreTimer?.Stop();
        BorderBrush = new SolidColorBrush(Color.FromArgb(200, 120, 200, 255));
        Title = "拼接中…";
        Subtitle = "正在合成滚动长图，请稍候";
    }

    public void ShowScrollTooFast()
    {
        _restoreTimer?.Stop();
        BorderBrush = new SolidColorBrush(Color.FromArgb(200, 255, 160, 60));
        Title = "滚动过快";
        Subtitle = "上一张尚未保存，请稍候再滚";
        ScheduleRestoreSubtitle();
    }

    public void ShowDirectionBlocked(int lockedSign)
    {
        _restoreTimer?.Stop();
        BorderBrush = new SolidColorBrush(Color.FromArgb(200, 255, 180, 60));
        Subtitle = lockedSign > 0 ? "已锁定向上，不可反向 ↓" : "已锁定向下，不可反向 ↑";
        ScheduleRestoreSubtitle();
    }

    private void ScheduleRestoreSubtitle()
    {
        _restoreTimer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _restoreTimer.Tick -= OnRestoreSubtitleTick;
        _restoreTimer.Tick += OnRestoreSubtitleTick;
        _restoreTimer.Stop();
        _restoreTimer.Start();
    }

    private void OnRestoreSubtitleTick(object? sender, EventArgs e)
    {
        _restoreTimer?.Stop();
        BorderBrush = new SolidColorBrush(Color.FromArgb(70, 255, 255, 255));
        if (_statsSubtitle != null)
            Subtitle = _statsSubtitle;
    }
}
