using System;
using System.Threading;
using System.Threading.Tasks;

public class Throttler
{
    private readonly int _milliseconds;
    private CancellationTokenSource _cancellationTokenSource;
    private readonly Lock _lockObject = new Lock();

    public Throttler(int milliseconds)
    {
        _milliseconds = milliseconds;
        _cancellationTokenSource = new CancellationTokenSource();
    }

    /// <summary>
    /// 节流执行一个操作。在指定的时间窗口内，只有最后一次调用会被执行。
    /// </summary>
    /// <param name="action">要执行的操作</param>
    public void Throttle(Action action)
    {
        // 取消之前的延迟任务（如果存在）
        CancelPendingTask();

        // 创建一个新的CTS用于当前调用
        var newCts = new CancellationTokenSource();
        CancellationTokenSource currentCts;

        // 在锁内安全地交换CTS引用
        lock (_lockObject)
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource = newCts;
            currentCts = newCts;
        }

        // 启动一个延迟任务，在指定时间后执行操作（除非被取消）
        _ = Task.Delay(_milliseconds, currentCts.Token)
            .ContinueWith(t =>
            {
                // 只有当延迟任务完成且未被取消时，才执行操作
                if (t.IsCompletedSuccessfully && !currentCts.Token.IsCancellationRequested)
                {
                    action();
                }
            }, TaskScheduler.Default);
    }

    /// <summary>
    /// 取消所有挂起的节流操作
    /// </summary>
    public void Cancel()
    {
        CancelPendingTask();
    }

    private void CancelPendingTask()
    {
        lock (_lockObject)
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        CancelPendingTask();
    }
}