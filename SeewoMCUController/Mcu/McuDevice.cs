using System.Text;
using Cvte.Mcu;
using SeewoMCUController.Mcu.Protocol;

namespace SeewoMCUController.Mcu;

/// <summary>设备连接状态快照。</summary>
public sealed class McuDeviceState
{
    public McuMachineType MachineKind { get; set; } = McuMachineType.None;
    public string? BoardName { get; set; }
    public string? MachineType { get; set; }
    public string? AndroidSn { get; set; }
    public string? AndroidMac { get; set; }
    public string? Uid { get; set; }
    public string? Ip { get; set; }
    public string? McuVersion { get; set; }
    public string? AndroidVersion { get; set; }
    public string? TouchVersion { get; set; }
    public bool SupportsAllChannel { get; set; }
    public byte? CurrentChannel { get; set; }
    public byte? CurrentSubChannel { get; set; }
    public int TouchSizeInch { get; set; } = -1;
    public DateTime? LastTimeSync { get; set; }
    public string? LastUsbDrive { get; set; }
}

/// <summary>
/// MCU 高层控制接口：连接、握手、全部 PC → MCU 命令、入站事件处理与状态缓存。
/// </summary>
public sealed class McuDevice : IDisposable
{
    private readonly McuLink _link;
    private readonly McuDeviceState _state = new();
    private readonly List<string> _notices = new();
    private readonly object _noticeGate = new();
    private UsbId? _usbId;
    private bool _autoReply = true;
    private bool _probed;

    public McuDevice(string? devicePath = null)
    {
        DevicePath = devicePath;
        _link = new McuLink(McuHunter.Usb) { AutoDetectFrameMode = true };
        _link.FrameReceived += OnFrameReceived;
        _link.LinkLost += reason => StatusChanged?.Invoke($"链路断开: {reason}");
    }

    public McuLink Link => _link;

    public McuDeviceState State => _state;

    public string? DevicePath { get; private set; }

    public UsbId? UsbId => _usbId;

    public bool IsConnected => _usbId is not null && McuHunter.Usb.IsConnected;

    /// <summary>帧封装模式；可由用户在 TUI / CLI 中手动切换。</summary>
    public FrameMode FrameMode
    {
        get => _link.FrameMode;
        set
        {
            _link.FrameMode = value;
            _link.AutoDetectFrameMode = value == FrameMode.Auto;
            StatusChanged?.Invoke($"帧模式切换为 {DescribeMode(value)}");
        }
    }

    /// <summary>是否对上行请求自动应答（对时 / 口令校验 / SN 确认 / 回传文件 / U 盘弹窗）。</summary>
    public bool AutoReply
    {
        get => _autoReply;
        set => _autoReply = value;
    }

    /// <summary>参考口令。本工具面向调试与自动化，不做本地口令比对，该值仅供记录与展示。</summary>
    public string ScreenLockPassword { get; set; } = string.Empty;

    /// <summary>口令校验通过时是否自动追加一帧 <c>(0x36,0x12)</c> 解锁 Android 屏。</summary>
    public bool AutoUnlockOnPasswordAccepted { get; set; } = true;

    /// <summary>查询类命令等待回包的超时时间（毫秒）。</summary>
    public int QueryTimeoutMs { get; set; } = 2000;

    public event Action<McuInboundEvent>? Inbound;

    public event Action<string>? StatusChanged;

    public event Action<McuDeviceState>? StateChanged;

    public static string DescribeMode(FrameMode mode) => mode switch
    {
        FrameMode.Auto => "自动探测",
        FrameMode.Raw => "无头 (Mode A)",
        FrameMode.Framed => "带头 FC A5 05 3B 00 (Mode B)",
        _ => mode.ToString(),
    };

    // ------------------------------------------------------------------ 连接

    public bool Connect()
    {
        if (!string.IsNullOrEmpty(DevicePath) && DevicePath != "auto")
        {
            _usbId = new UsbId(DevicePath, 0);
            if (!McuHunter.Usb.Connect(_usbId)) return Fail("无法打开指定设备");
            McuHunter.DetectedMcu = _usbId;
        }
        else
        {
            var devices = McuHunter.FindAll();
            if (devices.Count == 0) return Fail("未找到任何 MCU 设备");
            UsbId? chosen = devices.Count == 1 ? devices[0] : PickDevice(devices);
            if (chosen is null) return Fail("未选择设备");
            if (!McuHunter.Usb.Connect(chosen)) return Fail("打开设备失败");
            _usbId = chosen;
            McuHunter.DetectedMcu = chosen;
        }

        _state.MachineKind = McuKnownDevices.Detect(_usbId);
        _link.StartReader();
        StatusChanged?.Invoke($"已连接 {_usbId}（{McuKnownDevices.Describe(_state.MachineKind)}）");
        return true;
    }

    private UsbId? PickDevice(List<UsbId> devices)
    {
        Console.WriteLine($"发现 {devices.Count} 个 MCU 设备:");
        for (int i = 0; i < devices.Count; i++)
            Console.WriteLine($"  [{i + 1}] VID:0x{devices[i].Vid:X4} PID:0x{devices[i].Pid:X4} {devices[i].DeviceName}");
        Console.Write($"请选择 (1-{devices.Count})，直接回车用第 1 个: ");
        var input = Console.ReadLine();
        return int.TryParse(input, out int n) && n >= 1 && n <= devices.Count ? devices[n - 1] : devices[0];
    }

    private bool Fail(string message)
    {
        StatusChanged?.Invoke(message);
        Console.Error.WriteLine($"错误: {message}");
        return false;
    }

    /// <summary>
    /// 连接 → 帧模式探测 → 握手 12 步。
    /// 探测必须先于握手：探测本身就是发一包握手首包，先握手会在错误的封装模式下白发。
    /// </summary>
    public async Task<bool> ConnectAndHandshakeAsync(bool handshake = true, CancellationToken cancellationToken = default)
    {
        if (!Connect()) return false;
        if (FrameMode == FrameMode.Auto) await ProbeFrameModeAsync(cancellationToken).ConfigureAwait(false);
        if (handshake) Handshake();
        return true;
    }

    /// <summary>
    /// 帧模式自动探测：先用项目历史验证过的 Mode B 发握手首包，收到回包即锁定；
    /// 超时未收到则切到 Mode A 重试一次。
    /// </summary>
    public async Task<bool> ProbeFrameModeAsync(CancellationToken cancellationToken = default)
    {
        if (!_link.AutoDetectFrameMode || _probed) return true;
        _probed = true;

        var candidates = new[] { FrameMode.Framed, FrameMode.Raw };
        foreach (var candidate in candidates)
        {
            _link.FrameMode = candidate;
            _link.ResetConfirmedFrameMode();
            StatusChanged?.Invoke($"探测帧模式: {DescribeMode(candidate)} …");
            var reply = await _link.RequestAsync(McuOutbound.Handshake(), _ => true, _link.ProbeTimeoutMs, cancellationToken)
                .ConfigureAwait(false);
            if (reply is not null)
            {
                StatusChanged?.Invoke($"帧模式已确认: {DescribeMode(candidate)}（收到 {reply.Name}）");
                return true;
            }
        }

        StatusChanged?.Invoke($"未探测到上行回包，保持 {DescribeMode(_link.FrameMode)}");
        return false;
    }

    /// <summary>
    /// 握手序列：第 1 包必须为 <c>(0x50,0x00)</c>（安卓板收到它之前不会回任何包），第 11 步 <c>Sleep(1000)</c>。
    /// </summary>
    public void Handshake()
    {
        StatusChanged?.Invoke("开始握手序列");
        foreach (var frame in McuOutbound.HandshakeSequence())
        {
            _link.Send(frame);
        }
        _state.LastTimeSync = DateTime.Now;
        StatusChanged?.Invoke("握手序列发送完毕");
    }

    public void Disconnect()
    {
        _probed = false;
        _link.StopReader();
        McuHunter.CloseConnection();
        _usbId = null;
        StatusChanged?.Invoke("已断开连接");
    }

    // ------------------------------------------------------------------ 发送（原语）

    public bool Send(McuFrame frame) => _link.Send(frame);

    public bool SendRaw(byte main, byte sub, ReadOnlySpan<byte> payload) => Send(McuOutbound.WithPayload(main, sub, payload));

    public bool SendRawText(byte main, byte sub, string text) => Send(McuOutbound.WithText(main, sub, text));

    // ------------------------------------------------------------------ 握手 / 系统

    public bool HandshakeRequest() => Send(McuOutbound.Handshake());

    public bool RequestAllChannel() => Send(McuOutbound.RequestAllChannel());

    public bool SendBaseInfo() => Send(McuOutbound.BaseInfo());

    public bool SendPowerOffRequest() => Send(McuOutbound.PowerOffRequest());

    public bool SendAvMode(byte index) => Send(McuOutbound.AvMode(index));

    public bool RequestAndroidInfo() => Send(McuOutbound.RequestAndroidInfo());

    public bool RegisterWolServerName(string? name = null) => Send(McuOutbound.RegisterWolServerName(name ?? McuOutbound.WolServerName));

    public bool WolBroadcast() => Send(McuOutbound.WolBroadcast());

    public bool NotifyCableScreenPlugIn() => Send(McuOutbound.NotifyCableScreenPlugIn());

    public bool NotifyUnlockedByUsbKey() => Send(McuOutbound.NotifyUnlockedByUsbKey());

    public bool OpenNetworkMenu() => Send(McuOutbound.OpenNetworkMenu());

    public bool SyncTime(DateTime? localTime = null)
    {
        bool ok = Send(McuOutbound.SyncTime(localTime ?? DateTime.Now));
        if (ok)
        {
            _state.LastTimeSync = localTime ?? DateTime.Now;
            StateChanged?.Invoke(_state);
        }
        return ok;
    }

    // ------------------------------------------------------------------ 音频

    /// <summary><c>(0x08,0x00)</c> 绝对音量，<c>f[3]=1</c> 为常量、<c>f[5]</c> 为音量。</summary>
    public bool SetVolume(int volume) => Send(McuOutbound.SetVolume(volume));

    /// <summary><c>(0x08,0x02)</c> 音量 +1 级（本项目历史实现）。</summary>
    public bool VolumeUp() => Send(McuOutbound.VolumeUp());

    /// <summary><c>(0x08,0x01)</c> 音量 -1 级（本项目历史实现）。</summary>
    public bool VolumeDown() => Send(McuOutbound.VolumeDown());

    public bool VolumeUp(int steps)
    {
        bool ok = true;
        for (int i = 0; i < steps; i++)
        {
            ok &= VolumeUp();
        }
        return ok;
    }

    public bool VolumeDown(int steps)
    {
        bool ok = true;
        for (int i = 0; i < steps; i++)
        {
            ok &= VolumeDown();
        }
        return ok;
    }

    /// <summary><c>(0x08,0x03)</c> 静音。</summary>
    public bool Mute() => Send(McuOutbound.SetMute(true));

    /// <summary><c>(0x08,0x04)</c> 取消静音。</summary>
    public bool Unmute() => Send(McuOutbound.SetMute(false));

    public bool SetMute(bool mute) => Send(McuOutbound.SetMute(mute));

    /// <summary><c>(0x0E, mode+3)</c> 声音模式；<c>Custom</c> 会被拒绝（PC 侧原实现即不下发）。</summary>
    public bool SetAudioMode(AudioMode mode)
    {
        try
        {
            return Send(McuOutbound.SetAudioMode(mode));
        }
        catch (ArgumentOutOfRangeException ex)
        {
            StatusChanged?.Invoke(ex.Message);
            return false;
        }
    }

    /// <summary><c>(0x2A,0x00)</c> 触控笔开关。</summary>
    public bool SetPen(bool enabled) => Send(McuOutbound.SetPen(enabled));

    // ------------------------------------------------------------------ 视频

    /// <summary><c>(0x09, mode+3)</c> 显示模式。</summary>
    public bool SetGraphicsMode(GraphicsMode mode)
    {
        try
        {
            return Send(McuOutbound.SetGraphicsMode(mode));
        }
        catch (ArgumentOutOfRangeException ex)
        {
            StatusChanged?.Invoke(ex.Message);
            return false;
        }
    }

    // ------------------------------------------------------------------ 通道

    /// <summary><c>(0x07,ch)</c> 切换通道。HDMI/VGA 的 <paramref name="param"/> 为 1 基序号。</summary>
    public bool SwitchChannel(McuChannel channel, ushort param = 0) => Send(McuOutbound.SwitchChannel(channel, param));

    /// <summary><c>(0x13,ch)</c> 设置默认通道模式。</summary>
    public bool SetDefaultChannelMode(McuChannel channel, ushort param = 0) => Send(McuOutbound.SetDefaultChannelMode(channel, param));

    /// <summary><c>(0x12,ch)</c> 上报当前通道。</summary>
    public bool ReportCurrentChannel(McuChannel channel) => Send(McuOutbound.ReportCurrentChannel(channel));

    // ------------------------------------------------------------------ 锁屏 / 远程协助

    /// <summary><c>(0x36,0x10)</c> 锁 Android 屏。</summary>
    public bool LockScreen() => Send(McuOutbound.LockScreen());

    /// <summary><c>(0x36,0x12)</c> 解锁 Android 屏。</summary>
    public bool UnlockScreen() => Send(McuOutbound.UnlockScreen());

    /// <summary><c>(0x36,0x31)</c> 口令校验结果。</summary>
    public bool ReplyPasswordResult(bool accepted) => Send(McuOutbound.PasswordResult(accepted));

    /// <summary><c>(0x36,0x33)</c> 请求安卓板回传文件数据。</summary>
    public bool RequestFileBack() => Send(McuOutbound.RequestFileBack());

    /// <summary><c>(0x36,0x39)</c> SN 接收确认。</summary>
    public bool AckSerialNumber() => Send(McuOutbound.AckSerialNumber());

    /// <summary><c>(0x36,0x37)</c> 无负载通知。</summary>
    public bool SendNotice(byte sub = (byte)Main36.Notice) => Send(McuOutbound.Notice(sub));

    /// <summary><c>(0x36,0x14)</c> 状态／凭据应答。</summary>
    public bool SendStatusAck(byte tag, byte[]? data = null) => Send(McuOutbound.StatusAck(tag, data));

    /// <summary><c>(0x36,0x16)</c> 开关／弹窗状态应答。</summary>
    public bool SendSwitchStateAck(byte tag, byte[]? data = null) => Send(McuOutbound.SwitchStateAck(tag, data));

    /// <summary><c>(0x36,0x18)</c> 二进制负载应答。</summary>
    public bool SendBinaryAck(byte tag, byte[]? data = null) => Send(McuOutbound.BinaryAck(tag, data));

    /// <summary><c>(0x36,0x35)</c> 状态应答。</summary>
    public bool SendStateAck(params byte[] values) => Send(McuOutbound.StateAck(values));

    // ------------------------------------------------------------------ 查询

    public Task<string?> QueryBoardNameAsync(int? timeoutMs = null, CancellationToken cancellationToken = default)
        => QueryStringAsync(McuOutbound.QueryBoardName(), e => e.Frame.Main == (byte)MainIn.AndroidGlobal
            && e.Frame.Sub == (byte)Main34.BoardNameReply, timeoutMs, cancellationToken);

    public Task<string?> QueryAndroidSnAsync(int? timeoutMs = null, CancellationToken cancellationToken = default)
        => QueryStringAsync(McuOutbound.RequestAndroidInfo(), e => e.Frame.Main == (byte)MainIn.AndroidInfo
            && e.Frame.Sub == (byte)Main2B.SerialNumberReply, timeoutMs, cancellationToken);

    public Task<string?> QueryAndroidMacAsync(int? timeoutMs = null, CancellationToken cancellationToken = default)
        => QueryStringAsync(McuOutbound.RequestAndroidMac(), e => e.Frame.Main == (byte)MainIn.AndroidMacReply,
            timeoutMs, cancellationToken);

    public Task<string?> QueryMachineTypeAsync(int? timeoutMs = null, CancellationToken cancellationToken = default)
        => QueryStringAsync(McuOutbound.Handshake(), e => e.Frame.Main == (byte)MainIn.MachineType
            && e.Frame.Sub == (byte)0x01, timeoutMs, cancellationToken);

    public Task<string?> QueryIpAsync(int? timeoutMs = null, CancellationToken cancellationToken = default)
        => QueryStringAsync(McuOutbound.RequestIp(), e => e.Frame.Main == (byte)MainIn.AndroidGlobal
            && e.Frame.Sub == (byte)Main34.IpReply, timeoutMs, cancellationToken);

    public Task<string?> QueryUidAsync(int? timeoutMs = null, CancellationToken cancellationToken = default)
        => QueryStringAsync(McuOutbound.RequestUid(), e => e.Frame.Main == (byte)MainIn.UidReply, timeoutMs, cancellationToken);

    public Task<string?> QueryVersionAsync(int? timeoutMs = null, CancellationToken cancellationToken = default)
        => QueryStringAsync(McuOutbound.AvMode(0x02), e => e.Frame.Main == (byte)MainIn.ReturnVersion,
            timeoutMs, cancellationToken);

    private async Task<string?> QueryStringAsync(
        McuFrame frame,
        Func<McuInboundEvent, bool> matcher,
        int? timeoutMs,
        CancellationToken cancellationToken)
    {
        var reply = await _link.RequestAsync(frame, matcher, timeoutMs ?? QueryTimeoutMs, cancellationToken).ConfigureAwait(false);
        return reply?.Detail;
    }

    // ------------------------------------------------------------------ 入站处理

    private void OnFrameReceived(McuInboundEvent evt)
    {
        var frame = evt.Frame;
        ApplyState(evt);
        Inbound?.Invoke(evt);
        if (_autoReply) TryAutoReply(evt);
    }

    private void ApplyState(McuInboundEvent evt)
    {
        var f = evt.Frame;
        bool changed = false;
        switch (f.Main)
        {
            case (byte)MainIn.AndroidGlobal when f.Sub == (byte)Main34.BoardNameReply:
                _state.BoardName = evt.Detail;
                changed = true;
                break;
            case (byte)MainIn.AndroidGlobal when f.Sub == (byte)Main34.IpReply:
                _state.Ip = evt.Detail;
                changed = true;
                break;
            case (byte)MainIn.AndroidInfo when f.Sub == (byte)Main2B.SerialNumberReply:
                if (!string.IsNullOrEmpty(evt.Detail) && evt.Detail != "板卡未烧录 SN") _state.AndroidSn = evt.Detail;
                changed = true;
                break;
            case (byte)MainIn.AndroidMacReply:
                _state.AndroidMac = evt.Detail;
                changed = true;
                break;
            case (byte)MainIn.UidReply:
                _state.Uid = evt.Detail;
                changed = true;
                break;
            case (byte)MainIn.MachineType:
                _state.MachineType = evt.Detail;
                changed = true;
                break;
            case (byte)MainIn.AllChannelMessage when f.Sub == 5:
                _state.SupportsAllChannel = true;
                changed = true;
                break;
            case (byte)MainIn.CurrentChannelStatus:
            case (byte)MainIn.CurrentTvStatus:
                _state.CurrentChannel = f.Buffer[8];
                _state.CurrentSubChannel = f.Buffer[9];
                changed = true;
                break;
            case (byte)MainIn.ReturnVersion:
                switch (f.Sub)
                {
                    case 0: _state.AndroidVersion = evt.Detail; break;
                    case 1: _state.McuVersion = evt.Detail; break;
                    case 2:
                        _state.TouchVersion = evt.Detail;
                        if (int.TryParse(evt.Detail, out int inch) && inch > 0) _state.TouchSizeInch = inch;
                        break;
                }
                changed = true;
                break;
        }
        if (changed) StateChanged?.Invoke(_state);
    }

    private void TryAutoReply(McuInboundEvent evt)
    {
        var f = evt.Frame;
        try
        {
            switch (f.Main)
            {
                case (byte)MainIn.RequestPcTime:
                    Send(McuOutbound.SyncTimeNow());
                    break;
                case (byte)MainIn.Group36:
                    switch ((Main36)f.Sub)
                    {
                        case Main36.ScreenPassword:
                        {
                            bool ok = CheckPassword(evt.Detail);
                            Send(McuOutbound.PasswordResult(ok));
                            if (ok && AutoUnlockOnPasswordAccepted) Send(McuOutbound.UnlockScreen());
                            break;
                        }
                        case Main36.UsbLockFileToPc when f.PayloadByte(0) == 0xFF:
                            Send(McuOutbound.RequestFileBack());
                            break;
                        case Main36.SerialNumber:
                            Send(McuOutbound.AckSerialNumber());
                            break;
                        case Main36.UsbActionNotify:
                        case Main36.UsbAction:
                            if (f.PayloadByte(0) == 1) Send(McuOutbound.SwitchStateAck(0x02));
                            else if (f.PayloadByte(0) == 0) _state.LastUsbDrive = evt.Detail;
                            break;
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke($"自动应答失败: {ex.Message}");
        }
    }

    /// <summary>口令校验。本工具面向调试与自动化，不做本地口令比对，一律认定为校验通过。</summary>
    public bool CheckPassword(string password)
    {
        _ = password;
        return true;
    }

    // ------------------------------------------------------------------ 日志

    public IReadOnlyList<McuLogEntry> Log => _link.SnapshotLog();

    public void ClearLog() => _link.ClearLog();

    public IReadOnlyList<string> Notices
    {
        get
        {
            lock (_noticeGate) return _notices.ToArray();
        }
    }

    public void AddNotice(string text)
    {
        lock (_noticeGate)
        {
            _notices.Add($"{DateTime.Now:HH:mm:ss} {text}");
            if (_notices.Count > 200) _notices.RemoveAt(0);
        }
    }

    public void Dispose()
    {
        _link.FrameReceived -= OnFrameReceived;
        _link.Dispose();
    }
}
