using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WindowsTypeAnywhere;

/// <summary>
/// 通过 EVENT_SYSTEM_FOREGROUND 记住「中转框出现之前」的那个前台窗口，省去手动选定目标；
/// 同时为焦点切换提供一次事件驱动的等待（无轮询、无定时器）。
/// </summary>
internal sealed class ForegroundTracker : IDisposable
{
    private readonly object _gate = new object();
    private readonly HashSet<IntPtr> _ownWindows = new HashSet<IntPtr>();
    private readonly uint _ownPid = Native.GetCurrentProcessId();
    private readonly Native.WinEventDelegate _callback;
    private readonly IntPtr _hook;

    private IntPtr _lastTarget = IntPtr.Zero;
    private TaskCompletionSource<bool> _waiter;
    private IntPtr _waitTarget = IntPtr.Zero;

    public IntPtr LastTarget
    {
        get { lock (_gate) return _lastTarget; }
    }

    public ForegroundTracker()
    {
        _callback = OnWinEvent;
        _hook = Native.SetWinEventHook(
            Native.EVENT_SYSTEM_FOREGROUND,
            Native.EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero,
            _callback,
            0, 0,
            Native.WINEVENT_OUTOFCONTEXT);

        // 程序启动前目标窗口可能已经在前台，此时不会收到 FOREGROUND 事件，先补一次初值
        IntPtr current = Native.GetForegroundWindow();
        if (!IsOwn(current)) _lastTarget = current;
    }

    public void RegisterOwnWindow(IntPtr hwnd)
    {
        if (hwnd != IntPtr.Zero)
            lock (_gate) _ownWindows.Add(hwnd);
    }

    private bool IsOwn(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return true;
        lock (_gate)
            if (_ownWindows.Contains(hwnd)) return true;
        Native.GetWindowThreadProcessId(hwnd, out uint pid);
        return pid == _ownPid;
    }

    private void OnWinEvent(IntPtr hHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        // 焦点切换过程中可能短暂出现无效/已销毁窗口，别让它污染目标
        if (hwnd == IntPtr.Zero || !Native.IsWindow(hwnd)) return;
        if (IsOwn(hwnd)) return;
        lock (_gate)
        {
            _lastTarget = hwnd;
            if (_waiter != null && _waitTarget == hwnd)
            {
                var t = _waiter;
                _waiter = null;
                _waitTarget = IntPtr.Zero;
                t.TrySetResult(true);
            }
        }
    }

    /// <summary>事件驱动地等待 hwnd 成为前台；全程无轮询、无定时器。</summary>
    public async Task<bool> WaitForForegroundAsync(IntPtr hwnd, int timeoutMs)
    {
        if (hwnd == IntPtr.Zero) return false;
        if (Native.GetForegroundWindow() == hwnd) return true;

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            _waiter = tcs;
            _waitTarget = hwnd;
        }

        // 事件可能在登记等待者之前就已到达，补一次状态检查
        if (Native.GetForegroundWindow() == hwnd)
            tcs.TrySetResult(true);

        Task timeout = Task.Delay(Math.Max(0, timeoutMs));
        Task winner = await Task.WhenAny(tcs.Task, timeout).ConfigureAwait(true);

        lock (_gate)
        {
            if (_waiter == tcs)
            {
                _waiter = null;
                _waitTarget = IntPtr.Zero;
            }
        }
        return winner == tcs.Task;
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
            Native.UnhookWinEvent(_hook);
    }
}
