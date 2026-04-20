using System;
using System.Threading;

namespace TDS.PeekDesktop;

/// <summary>
/// Runs Peek Desktop on a dedicated STA thread with a Win32 message pump (same model as standalone PeekDesktop).
/// The thread is created only while Peek is enabled in settings (or during the brief startup when enabled).
/// Foreground WinEvents are coalesced and delivered on this thread.
/// </summary>
public static class PeekDesktopHost
{
    private static readonly object Gate = new();
    private static Thread? _thread;
    private static Win32MessageLoop? _loop;
    private static DesktopPeek? _peek;
    private static readonly ManualResetEventSlim Ready = new(false);

    private static IntPtr _pendingForegroundHwnd;
    private static int _foregroundFlushScheduled;

    public static bool IsUiReady => _loop is not null && _peek is not null;

    /// <summary>Starts the peek thread if not already running (e.g. user just turned Peek on).</summary>
    public static void EnsureStarted()
    {
        if (!OperatingSystem.IsWindows())
            return;

        StartPeekThreadIfNotRunning();
    }

    /// <summary>Starts the peek thread only when persisted settings have Peek enabled.</summary>
    public static void EnsureStartedIfEnabledInSettings()
    {
        if (!OperatingSystem.IsWindows())
            return;

        if (!PeekDesktopSettings.Load().Enabled)
            return;

        StartPeekThreadIfNotRunning();
    }

    private static void StartPeekThreadIfNotRunning()
    {
        lock (Gate)
        {
            if (_thread is not null)
                return;

            Ready.Reset();
            _thread = new Thread(PeekThreadProc)
            {
                IsBackground = true,
                Name = "TDS.PeekDesktop"
            };
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();

            if (!Ready.Wait(TimeSpan.FromSeconds(10)))
                AppDiagnostics.Log("PeekDesktopHost: initialization wait timed out");
        }
    }

    private static bool IsPeekThreadRunning()
    {
        lock (Gate)
            return _thread is not null;
    }

    /// <summary>
    /// Called from the WinEvent hook (possibly an arbitrary thread). Updates the pending HWND and
    /// schedules at most one flush onto the peek message loop, merging rapid-fire events.
    /// </summary>
    internal static void ScheduleForegroundCoalesced(IntPtr hwnd)
    {
        Volatile.Write(ref _pendingForegroundHwnd, hwnd);

        Win32MessageLoop? loop;
        lock (Gate)
            loop = _loop;

        if (loop is null)
            return;

        if (Interlocked.CompareExchange(ref _foregroundFlushScheduled, 1, 0) != 0)
            return;

        loop.BeginInvoke(FlushPendingForeground);
    }

    private static void FlushPendingForeground()
    {
        Interlocked.Exchange(ref _foregroundFlushScheduled, 0);
        IntPtr hwnd = Volatile.Read(ref _pendingForegroundHwnd);

        DesktopPeek? peek;
        lock (Gate)
            peek = _peek;

        peek?.DeliverForegroundChanged(hwnd);
    }

    public static void Shutdown()
    {
        Win32MessageLoop? loop;
        Thread? thread;
        lock (Gate)
        {
            loop = _loop;
            thread = _thread;
        }

        if (loop is null)
            return;

        loop.BeginInvoke(loop.Quit);

        if (thread is not null && !thread.Join(TimeSpan.FromSeconds(3)))
            AppDiagnostics.Log("PeekDesktopHost: peek thread did not exit cleanly");
    }

    private static void PeekThreadProc()
    {
        try
        {
            Interlocked.Exchange(ref _foregroundFlushScheduled, 0);
            Volatile.Write(ref _pendingForegroundHwnd, IntPtr.Zero);

            using var loop = new Win32MessageLoop();
            lock (Gate)
            {
                _loop = loop;
            }

            loop.PostDeferredAction(1, () =>
            {
                try
                {
                    var settings = PeekDesktopSettings.Load();
                    _peek = new DesktopPeek(settings);
                    _peek.IsEnabled = settings.Enabled;
                    if (settings.Enabled)
                        _peek.Start();
                }
                catch (Exception ex)
                {
                    AppDiagnostics.Log($"PeekDesktopHost init failed: {ex}");
                }
                finally
                {
                    Ready.Set();
                }
            });

            loop.Run();
        }
        finally
        {
            lock (Gate)
            {
                _peek?.Dispose();
                _peek = null;
                _loop = null;
                _thread = null;
            }
        }
    }

    public static void QueueInvoke(Action action)
    {
        Win32MessageLoop? loop = _loop;
        if (loop is null)
            return;
        loop.BeginInvoke(action);
    }

    public static void ToggleEnabled()
    {
        if (!OperatingSystem.IsWindows())
            return;

        if (!IsPeekThreadRunning())
        {
            var settings = PeekDesktopSettings.Load();
            settings.Enabled = !settings.Enabled;
            settings.Save();
            if (settings.Enabled)
                StartPeekThreadIfNotRunning();
            return;
        }

        QueueInvoke(() =>
        {
            if (_peek is null)
                return;

            var settings = PeekDesktopSettings.Load();
            settings.Enabled = !settings.Enabled;
            _peek.IsEnabled = settings.Enabled;
            if (settings.Enabled)
                _peek.Start();
            else
                _peek.Stop();

            settings.Save();

            if (!settings.Enabled)
            {
                Win32MessageLoop? loop;
                lock (Gate)
                    loop = _loop;
                loop?.Quit();
            }
        });
    }

    public static void SetEnabled(bool enabled)
    {
        if (!OperatingSystem.IsWindows())
            return;

        if (!IsPeekThreadRunning())
        {
            var settings = PeekDesktopSettings.Load();
            if (settings.Enabled == enabled)
                return;
            settings.Enabled = enabled;
            settings.Save();
            if (enabled)
                StartPeekThreadIfNotRunning();
            return;
        }

        QueueInvoke(() =>
        {
            if (_peek is null)
                return;

            var settings = PeekDesktopSettings.Load();
            if (settings.Enabled == enabled)
                return;

            settings.Enabled = enabled;
            _peek.IsEnabled = enabled;
            if (enabled)
                _peek.Start();
            else
                _peek.Stop();

            settings.Save();

            if (!enabled)
            {
                Win32MessageLoop? loop;
                lock (Gate)
                    loop = _loop;
                loop?.Quit();
            }
        });
    }

    public static void ToggleRequireDoubleClick()
    {
        if (!OperatingSystem.IsWindows())
            return;

        if (!IsPeekThreadRunning())
        {
            var settings = PeekDesktopSettings.Load();
            settings.RequireDoubleClick = !settings.RequireDoubleClick;
            settings.Save();
            return;
        }

        QueueInvoke(() =>
        {
            if (_peek is null)
                return;
            var settings = PeekDesktopSettings.Load();
            settings.RequireDoubleClick = !settings.RequireDoubleClick;
            _peek.SetRequireDoubleClick(settings.RequireDoubleClick);
            settings.Save();
        });
    }

    public static void TogglePeekOnTaskbarClick()
    {
        if (!OperatingSystem.IsWindows())
            return;

        if (!IsPeekThreadRunning())
        {
            var settings = PeekDesktopSettings.Load();
            settings.PeekOnTaskbarClick = !settings.PeekOnTaskbarClick;
            settings.Save();
            return;
        }

        QueueInvoke(() =>
        {
            if (_peek is null)
                return;
            var settings = PeekDesktopSettings.Load();
            settings.PeekOnTaskbarClick = !settings.PeekOnTaskbarClick;
            _peek.SetPeekOnTaskbarClick(settings.PeekOnTaskbarClick);
            settings.Save();
        });
    }

    public static void TogglePauseWhileFullscreen()
    {
        if (!OperatingSystem.IsWindows())
            return;

        if (!IsPeekThreadRunning())
        {
            var settings = PeekDesktopSettings.Load();
            settings.PauseWhileFullscreenAppActive = !settings.PauseWhileFullscreenAppActive;
            settings.Save();
            return;
        }

        QueueInvoke(() =>
        {
            if (_peek is null)
                return;
            var settings = PeekDesktopSettings.Load();
            settings.PauseWhileFullscreenAppActive = !settings.PauseWhileFullscreenAppActive;
            _peek.SetPauseWhileFullscreenAppActive(settings.PauseWhileFullscreenAppActive);
            settings.Save();
        });
    }

    public static void SetPeekMode(PeekMode mode)
    {
        if (!OperatingSystem.IsWindows())
            return;

        if (!IsPeekThreadRunning())
        {
            var settings = PeekDesktopSettings.Load();
            settings.PeekMode = mode;
            settings.Save();
            return;
        }

        QueueInvoke(() =>
        {
            if (_peek is null)
                return;
            var settings = PeekDesktopSettings.Load();
            settings.PeekMode = mode;
            _peek.SetPeekMode(mode);
            settings.Save();
        });
    }

    /// <summary>Reads current JSON so the tray menu can refresh checkmarks (approximate if host is still starting).</summary>
    public static PeekDesktopSettings SnapshotSettingsForMenu()
        => PeekDesktopSettings.Load();
}
