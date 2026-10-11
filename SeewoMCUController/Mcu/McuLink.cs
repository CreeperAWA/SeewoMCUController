using SeewoMCUController.Mcu.Protocol;

namespace SeewoMCUController.Mcu;

/// <summary>一条链路日志（发送或接收）。</summary>
public sealed class McuLogEntry
{
    public required DateTime Timestamp { get; init; }
    public required bool Outgoing { get; init; }
    public required McuFrame Frame { get; init; }
    public required string Text { get; init; }

    public string Direction => Outgoing ? "→" : "←";
}

/// <summary>
/// MCU 链路：唯一持有读线程，负责 64 字节报告的收帧、拆头、事件分发与请求/响应匹配。
/// <para>
/// 句柄按读写分离（<c>UsbDevice</c> 内 <c>CreateFile</c> 两次），
/// 因此阻塞式 <c>ReadFile</c> 不会阻塞 <c>WriteFile</c>。
/// </para>
/// </summary>
public sealed class McuLink : IDisposable
{
    private readonly UsbDevice _usb;
    private readonly object _writeGate = new();
    private readonly List<PendingRequest> _pending = new();
    private readonly object _pendingGate = new();
    private readonly List<McuLogEntry> _log = new();
    private readonly object _logGate = new();

    private Thread? _reader;
    private volatile bool _running;
    private long _sequence;
    private int _consecutiveReadErrors;

    public McuLink(UsbDevice usb)
    {
        _usb = usb;
    }

    /// <summary>上行帧模式。<see cref="FrameMode.Auto"/> 时由握手探测锁定。</summary>
    public FrameMode FrameMode { get; set; } = FrameMode.Auto;

    /// <summary>是否自动探测帧模式（<see cref="FrameMode.Auto"/>）。</summary>
    public bool AutoDetectFrameMode { get; set; } = true;

    /// <summary>已被上行帧证实的模式；<c>null</c> 表示尚未确认。</summary>
    public FrameMode? ConfirmedFrameMode { get; private set; }

    /// <summary>清除「已确认」标记，重新探测时使用。</summary>
    public void ResetConfirmedFrameMode() => ConfirmedFrameMode = null;

    /// <summary>每帧发送前的节流延时（毫秒），默认 50。</summary>
    public int InterFrameDelayMs { get; set; } = 50;

    public bool IsRunning => _running;

    /// <summary>握手探测超时（毫秒）。</summary>
    public int ProbeTimeoutMs { get; set; } = 1200;

    /// <summary>读线程因连续失败而退出时触发。</summary>
    public event Action<string>? LinkLost;

    /// <summary>收到并成功拆帧的上行帧（已解码）。</summary>
    public event Action<McuInboundEvent>? FrameReceived;

    /// <summary>任意收发帧（用于日志与十六进制查看）。</summary>
    public event Action<McuLogEntry>? LogAppended;

    /// <summary>报文日志被清空时触发（TUI 的日志窗据此同步清屏）。</summary>
    public event Action? LogCleared;

    public long Sequence => Interlocked.Read(ref _sequence);

    public IReadOnlyList<McuLogEntry> SnapshotLog()
    {
        lock (_logGate) return _log.ToArray();
    }

    public void ClearLog()
    {
        lock (_logGate) _log.Clear();
        LogCleared?.Invoke();
    }

    // ------------------------------------------------------------------ 生命周期

    public void StartReader()
    {
        if (_running) return;
        _running = true;
        _consecutiveReadErrors = 0;
        _reader = new Thread(ReadLoop)
        {
            IsBackground = true,
            Name = "McuLinkReader",
        };
        _reader.Start();
    }

    public void StopReader()
    {
        _running = false;
        FailPending("链路已关闭");
        if (_reader is not { IsAlive: true } reader || ReferenceEquals(Thread.CurrentThread, reader)) return;

        if (reader.Join(500)) return;

        // 关闭句柄以解除阻塞式 ReadFile。
        _usb.Disconnect();
        reader.Join(500);
    }

    private void ReadLoop()
    {
        var raw = new byte[McuFrame.Size];
        while (_running)
        {
            bool ok;
            int read;
            try
            {
                ok = _usb.ReadReport(McuFrame.Size, out raw, out read);
            }
            catch (Exception ex)
            {
                ok = false;
                read = 0;
                if (_running) LinkLost?.Invoke($"读取失败: {ex.Message}");
            }

            if (!_running) break;

            if (!ok || read <= 0)
            {
                if (++_consecutiveReadErrors >= 8)
                {
                    _running = false;
                    FailPending("链路断开");
                    LinkLost?.Invoke("连续读取失败，链路已断开（设备可能被拔出）");
                }
                Thread.Sleep(20);
                continue;
            }

            _consecutiveReadErrors = 0;
            if (read != McuFrame.Size)
                continue; // 非 64 字节报告，丢弃（HID 报告长度不符）

            if (!McuFrameCodec.TryParse(raw, out var frame, out var mode)) continue;
            ConfirmFrameMode(mode);
            Interlocked.Increment(ref _sequence);
            Dispatch(frame);
        }
    }

    private void ConfirmFrameMode(FrameMode mode)
    {
        if (ConfirmedFrameMode == mode) return;
        ConfirmedFrameMode = mode;
        if (AutoDetectFrameMode && FrameMode == FrameMode.Auto) FrameMode = mode;
    }

    private void Dispatch(McuFrame frame)
    {
        McuInboundEvent inbound;
        try
        {
            inbound = McuInbound.Decode(frame);
        }
        catch (Exception)
        {
            inbound = new McuInboundEvent
            {
                Frame = frame,
                Main = (MainIn)frame.Main,
                Name = "解析失败",
                Detail = frame.ToHex(24),
            };
        }

        AppendLog(new McuLogEntry
        {
            Timestamp = DateTime.Now,
            Outgoing = false,
            Frame = frame,
            Text = inbound.ToString(),
        });

        FrameReceived?.Invoke(inbound);

        lock (_pendingGate)
        {
            foreach (var request in _pending)
            {
                if (request.TryComplete(inbound)) break;
            }
        }
    }

    // ------------------------------------------------------------------ 发送

    /// <summary>发送一帧（自动封帧、限速、记录日志）。</summary>
    public bool Send(McuFrame frame)
    {
        if (frame is null) throw new ArgumentNullException(nameof(frame));
        var wire = frame.ToTransport(FrameMode);
        bool ok;
        lock (_writeGate)
        {
            // 每帧写前先等待，给固件留出处理上一帧的时间
            if (InterFrameDelayMs > 0) Thread.Sleep(InterFrameDelayMs);
            ok = WriteCore(wire);
        }

        AppendLog(new McuLogEntry
        {
            Timestamp = DateTime.Now,
            Outgoing = true,
            Frame = frame,
            Text = McuCommandNames.DescribeOut(frame) + (ok ? string.Empty : "  [写入失败]"),
        });
        return ok;
    }

    private bool WriteCore(byte[] wire)
    {
        try
        {
            return _usb.Write(wire);
        }
        catch (Exception)
        {
            return false;
        }
    }

    // ------------------------------------------------------------------ 请求 / 响应

    /// <summary>
    /// 发送一帧并等待满足 <paramref name="matcher"/> 的上行帧。
    /// </summary>
    public async Task<McuInboundEvent?> RequestAsync(
        McuFrame frame,
        Func<McuInboundEvent, bool> matcher,
        int timeoutMs = 3000,
        CancellationToken cancellationToken = default)
    {
        var pending = new PendingRequest(matcher);
        lock (_pendingGate) _pending.Add(pending);
        try
        {
            if (!Send(frame)) return null;
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeoutMs);
            var finished = await Task.WhenAny(pending.Task, WaitForCancellation(cts.Token)).ConfigureAwait(false);
            if (finished != pending.Task) return null;
            return await pending.Task.ConfigureAwait(false);
        }
        finally
        {
            lock (_pendingGate) _pending.Remove(pending);
        }
    }

    /// <summary>发送一帧并等待任意上行帧。</summary>
    public Task<McuInboundEvent?> RequestAsync(McuFrame frame, int timeoutMs = 3000, CancellationToken cancellationToken = default)
        => RequestAsync(frame, _ => true, timeoutMs, cancellationToken);

    private static async Task WaitForCancellation(CancellationToken token)
    {
        var tcs = new TaskCompletionSource();
        using (token.Register(() => tcs.TrySetResult()))
        {
            await tcs.Task.ConfigureAwait(false);
        }
    }

    private void FailPending(string reason)
    {
        lock (_pendingGate)
        {
            foreach (var request in _pending)
            {
                request.TryComplete(null);
            }
            _pending.Clear();
        }
    }

    private void AppendLog(McuLogEntry entry)
    {
        lock (_logGate)
        {
            _log.Add(entry);
            if (_log.Count > 2000) _log.RemoveRange(0, _log.Count - 2000);
        }
        LogAppended?.Invoke(entry);
    }

    public void Dispose()
    {
        StopReader();
        _usb.Disconnect();
    }

    private sealed class PendingRequest
    {
        private readonly Func<McuInboundEvent, bool> _matcher;
        private readonly TaskCompletionSource<McuInboundEvent?> _tcs =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public PendingRequest(Func<McuInboundEvent, bool> matcher) => _matcher = matcher;

        public Task<McuInboundEvent?> Task => _tcs.Task;

        public bool TryComplete(McuInboundEvent? evt)
        {
            if (evt is not null && !_matcher(evt)) return false;
            return _tcs.TrySetResult(evt);
        }
    }
}
