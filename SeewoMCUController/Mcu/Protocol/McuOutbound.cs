using System.Text;

namespace SeewoMCUController.Mcu.Protocol;

/// <summary>
/// <summary>
/// 全部 PC → MCU 组包器。各方法的注释直接给出该命令的字节布局与各字段含义。
/// </summary>
public static class McuOutbound
{
    /// <summary>握手第 9 步注册的 WOL 服务器名，25 个字符。</summary>
    public const string WolServerName = "persisit.remote.pc.wakeup";

    public const int WolServerNameLength = 25;

    // ---------------------------------------------------------------- 握手序列

    /// <summary><c>(0x50,0x00)</c> 握手首包，用于拉取基础信息。</summary>
    public static McuFrame Handshake() => new((byte)MainOut.MachineType, 0x00);

    /// <summary><c>(0x28,0x00)</c> 请求全通道状态。</summary>
    public static McuFrame RequestAllChannel() => new((byte)MainOut.RequestAllChannel, 0x00);

    /// <summary><c>(0x01,0x00)</c> 基础信息前段（握手第 3 步）。</summary>
    public static McuFrame BaseInfo() => new(0x01, 0x00);

    /// <summary>
/// <summary>
/// <c>(0x01,0x01)</c> 通知安卓板关机。仅在 PC 侧执行关机流程且 MCU 通道
/// 可用时发送，握手序列中不包含它。
/// </summary>
    public static McuFrame PowerOffRequest() => new(0x01, 0x01);

    /// <summary><c>(0x16,0x00/0x01/0x02)</c> 音视频模式声明 A/B/C。</summary>
    public static McuFrame AvMode(byte index) => new((byte)MainOut.AvMode, index);

    /// <summary><c>(0x2B,0x01)</c> 请求 Android 详细信息。</summary>
    public static McuFrame RequestAndroidInfo() => new((byte)MainOut.AndroidInfo, (byte)Main2B.RequestInfo);

    /// <summary>
/// <summary>
/// <c>(0x2B,0x23)</c> 注册 WOL 服务器名，负载为 <b>25 个字符</b>。
/// <para>
/// 默认名称 <c>persisit.remote.pc.wakeup</c> 本身恰好 25 个字符，因此 <c>f[3] = 25</c>，负载为这 25 个
/// 字符、不包含尾部 <c>\0</c>；传入自定义名称时长度随之变化。
/// </para>
/// </summary>
/// </summary>
    public static McuFrame RegisterWolServerName(string name = WolServerName)
    {
        var bytes = Encoding.ASCII.GetBytes(name);
        int n = Math.Min(bytes.Length, McuFrame.MaxRawPayload);
        return new McuFrame((byte)MainOut.AndroidInfo, (byte)Main2B.WolServerName).SetPayload(bytes.AsSpan(0, n));
    }

    /// <summary><c>(0x34,0x0C)</c> 查询板卡名。</summary>
    public static McuFrame QueryBoardName() => new((byte)MainOut.AndroidGlobal, (byte)Main34.QueryBoardName);

    /// <summary><c>(0x2D,0x00)</c> 请求 Android MAC。</summary>
    public static McuFrame RequestAndroidMac() => new((byte)MainOut.RequestAndroidMac, 0x00);

    // ---------------------------------------------------------------- 时间同步

    /// <summary>
/// <summary>
/// <c>(0x15,0x00)</c>，<code>f[3]=7</c>，其后为 7 字节时间负载：<code>f[5]</code> 年高、<code>f[6]</code> 年低、
/// <code>f[7]</code> 月、<code>f[8]</code> 日、<code>f[9]</code> 时、<code>f[10]</code> 分、<code>f[11]</code> 秒。
/// <para>年份为 16 位大端；协议不携带时区，接收方按同一时区解释。</para>
/// </summary>
/// <para>年份为 16 位大端；月 = <c>tm_mon + 1</c>；协议不含时区。</para>
/// </summary>
    public static McuFrame SyncTime(DateTime localTime)
    {
        var frame = new McuFrame((byte)MainOut.SetTime, 0x00);
        frame.Buffer[3] = 0x07;
        int year = localTime.Year;
        frame.Buffer[5] = (byte)((year >> 8) & 0xFF);
        frame.Buffer[6] = (byte)(year & 0xFF);
        frame.Buffer[7] = (byte)localTime.Month;
        frame.Buffer[8] = (byte)localTime.Day;
        frame.Buffer[9] = (byte)localTime.Hour;
        frame.Buffer[10] = (byte)localTime.Minute;
        frame.Buffer[11] = (byte)localTime.Second;
        return frame;
    }

    public static McuFrame SyncTimeNow() => SyncTime(DateTime.Now);

    // ---------------------------------------------------------------- 通道

    /// <summary>
    /// <summary>
/// <c>(0x07,ch)</c> 切换通道。
/// <list type="bullet">
/// <item><c>ch==1</c>（TV）：<code>f[3]=2, f[5]=param&gt;&gt;8, f[6]=param&amp;0xFF</code></item>
/// <item><c>ch in {9,10}</c>（HDMI/VGA）：<code>f[3]=1, f[5]=(byte)param-1</code>，即 param 为 1 基序号</item>
/// <item>其他：<code>f[3]=0</code></item>
/// </list>
/// </summary>
    public static McuFrame SwitchChannel(McuChannel channel, ushort param = 0)
        => BuildChannelFrame((byte)MainOut.SwitchChannel, channel, param, decrementParam: true);

    /// <summary>
    /// <summary>
/// <c>(0x13,ch)</c> 设置默认通道模式。字段布局与 <see cref="SwitchChannel"/> 一致，
/// 区别在于 <c>ch in {9,10}</c> 时 <c>f[5]=(byte)param</c>（不做减一）。
/// </summary>
    public static McuFrame SetDefaultChannelMode(McuChannel channel, ushort param = 0)
        => BuildChannelFrame((byte)MainOut.DefaultChannelMode, channel, param, decrementParam: false);

    /// <summary><c>(0x12,ch)</c> 上报当前通道。</summary>
    public static McuFrame ReportCurrentChannel(McuChannel channel)
        => new((byte)MainOut.ReportChannel, (byte)channel);

    private static McuFrame BuildChannelFrame(byte main, McuChannel channel, ushort param, bool decrementParam)
    {
        byte ch = (byte)channel;
        var frame = new McuFrame(main, ch);
        if (ch == (byte)McuChannel.Tv)
        {
            frame.Buffer[3] = 0x02;
            frame.Buffer[5] = (byte)(param >> 8);
            frame.Buffer[6] = (byte)(param & 0xFF);
        }
        else if (ch is (byte)McuChannel.Hdmi or (byte)McuChannel.Vga)
        {
            frame.Buffer[3] = 0x01;
            byte value = (byte)(param & 0xFF);
            frame.Buffer[5] = decrementParam ? (byte)(value - 1) : value;
        }
        return frame;
    }

    // ---------------------------------------------------------------- 音频

    /// <summary><c>(0x08,0x00)</c> 设置音量：<code>f[3]</code> 固定为 1（该命令的约定常量），音量值放在 <c>f[5]</c>。</summary>
    public static McuFrame SetVolume(int volume)
    {
        var frame = new McuFrame((byte)MainOut.Volume, 0x00);
        frame.Buffer[3] = 0x01;
        frame.Buffer[5] = (byte)Math.Clamp(volume, 0, 255);
        return frame;
    }

    /// <summary><c>(0x08,0x03)</c> 静音 / <c>(0x08,0x04)</c> 取消静音，<code>f[3]=0</code>。</summary>
    public static McuFrame SetMute(bool mute)
        => new((byte)MainOut.Volume, mute ? (byte)0x03 : (byte)0x04);

    /// <summary><c>(0x08,0x02)</c> 音量加 1 级（本项目历史实现；与静音 SUB 同族）。</summary>
    public static McuFrame VolumeUp() => new((byte)MainOut.Volume, 0x02);

    /// <summary><c>(0x08,0x01)</c> 音量减 1 级（本项目历史实现）。</summary>
    public static McuFrame VolumeDown() => new((byte)MainOut.Volume, 0x01);

    // ---------------------------------------------------------------- 显示 / 声音模式

    /// <summary><c>(0x09, mode+3)</c> 显示模式，mode 取值 0..3。</summary>
    public static McuFrame SetGraphicsMode(GraphicsMode mode)
        => new((byte)MainOut.GraphicsMode, ToModeSub((int)mode));

    /// <summary><c>(0x0E, mode+3)</c> 声音模式，mode 取值 0..3。</summary>
    public static McuFrame SetAudioMode(AudioMode mode)
        => new((byte)MainOut.AudioMode, ToModeSub((int)mode));

    /// <summary>
    /// 模式枚举 → SUB：<code>0→3, 1→4, 2→5, 3→6</code>。
    /// <c>CUSTOM(4)</c> 在 PC 侧就被 <c>if (mode &gt; 3) return;</c> 丢弃，<b>永不下发</b>。
    /// </summary>
    public static byte ToModeSub(int mode)
    {
        if (!TryToModeSub(mode, out byte sub))
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "模式 4(自定义) 在 PC 侧即被丢弃，不会下发；请使用 0..3。");
        return sub;
    }

    /// <summary>模式枚举 → SUB 的非抛出版本。</summary>
    public static bool TryToModeSub(int mode, out byte sub)
    {
        if (mode is >= 0 and <= 3)
        {
            sub = (byte)(mode + 3);
            return true;
        }
        sub = 0;
        return false;
    }

    // ---------------------------------------------------------------- 业务

    /// <summary><c>(0x32,0x00)</c> 线屏已插入。</summary>
    public static McuFrame NotifyCableScreenPlugIn() => new((byte)MainOut.CableScreenPlugIn, 0x00);

    /// <summary>
    /// <summary>
/// <c>(0x34,0x0A)</c> 让安卓板打开网络设置菜单：<code>f[3]=0x01, f[5]=0x01</code>，
/// 即帧头 4 字节为 <c>34 0A 01 00</c>。
/// </summary>
    public static McuFrame OpenNetworkMenu()
    {
        var frame = new McuFrame((byte)MainOut.AndroidGlobal, (byte)Main34.NetworkMenu);
        frame.Buffer[3] = 0x01;
        frame.Buffer[5] = 0x01;
        return frame;
    }

    /// <summary><c>(0x34,0x10)</c> 读取设备 IP（本项目历史实现的命令，回包 <c>(0x34,0x11)</c>）。</summary>
    public static McuFrame RequestIp() => new((byte)MainOut.AndroidGlobal, 0x10);

    /// <summary><c>(0x34,0x12)</c> WOL 广播，让安卓板唤醒 OPS。</summary>
    public static McuFrame WolBroadcast() => new((byte)MainOut.AndroidGlobal, (byte)Main34.WolBroadcast);

    /// <summary>
/// <summary>
/// <c>(0x31,0x00)</c> 通知安卓板「设备已被 U 盘解锁」。
/// 协议中与 U 盘相关的命令只有这一条，作用是告知解锁结果，
/// 不涉及对 U 盘本身写保护或只读状态的任何操作。
/// </summary>
    public static McuFrame NotifyUnlockedByUsbKey() => new((byte)MainOut.UsbDiskUnlock, 0x00);

    /// <summary><c>(0x40,0x00)</c> 读取设备 UID（本项目历史实现）。</summary>
    public static McuFrame RequestUid() => new((byte)MainOut.Uid, 0x00);

    /// <summary>
    /// <c>(0x2A,0x00)</c> 触控笔开关（本项目历史实现）。
    /// 线上字节 <c>FC A5 05 3B 00 | FB 2A 00 01 00 | 00/01</c>，
    /// 即 <c>f[3]=1</c>（常量）、<c>f[5]=enabled</c>，与 <c>(0x08,0x00)</c> 音量命令同形。
    /// </summary>
    public static McuFrame SetPen(bool enabled)
    {
        var frame = new McuFrame((byte)MainOut.Pen, 0x00);
        frame.Buffer[3] = 0x01;
        frame.Buffer[5] = (byte)(enabled ? 1 : 0);
        return frame;
    }

    // ---------------------------------------------------------------- 0x36 屏幕锁组

    /// <summary><c>(0x36,0x10)</c> 锁 Android 屏。</summary>
    public static McuFrame LockScreen() => Build36((byte)Main36.LockScreen);

    /// <summary><c>(0x36,0x12)</c> 解锁 Android 屏。</summary>
    public static McuFrame UnlockScreen() => Build36((byte)Main36.UnlockScreen);

    /// <summary><c>(0x36,0x31)</c> 口令校验结果：<c>0x01</c> 正确 / <c>0x00</c> 错误（1 字节负载）。</summary>
    public static McuFrame PasswordResult(bool accepted)
        => Build36((byte)Main36.PasswordResult, new[] { (byte)(accepted ? 0x01 : 0x00) });

    /// <summary><c>(0x36,0x33)</c> 请求安卓板回传文件数据（空负载）。</summary>
    public static McuFrame RequestFileBack() => Build36((byte)Main36.RequestFileBack);

    /// <summary><c>(0x36,0x39)</c> SN 接收确认（空负载）。</summary>
    public static McuFrame AckSerialNumber() => Build36((byte)Main36.SnAck);

    /// <summary><c>(0x36,0x37)</c> 无负载通知。</summary>
    public static McuFrame Notice(byte sub = (byte)Main36.Notice) => Build36(sub);

    /// <summary><c>(0x36,0x14)</c> 状态／凭据应答 <c>[tag][数据…]</c>。</summary>
    public static McuFrame StatusAck(byte tag, ReadOnlySpan<byte> data = default)
        => Build36((byte)Main36.StatusAck, Concat(tag, data));

    /// <summary><c>(0x36,0x16)</c> 开关／弹窗状态应答 <c>[tag][数据…]</c>。</summary>
    public static McuFrame SwitchStateAck(byte tag, ReadOnlySpan<byte> data = default)
        => Build36((byte)Main36.SwitchStateAck, Concat(tag, data));

    /// <summary><c>(0x36,0x18)</c> 二进制负载应答 <c>[tag][二进制…]</c>。</summary>
    public static McuFrame BinaryAck(byte tag, ReadOnlySpan<byte> data = default)
        => Build36((byte)Main36.BinaryAck, Concat(tag, data));

    /// <summary><c>(0x36,0x35)</c> 状态应答，取值集合 <c>{0x00,0x01,0x02,0x03}</c>，长度 1 或 3。</summary>
    public static McuFrame StateAck(params byte[] values)
    {
        var payload = values.Length > 0 ? values : new byte[] { 0x00 };
        return Build36((byte)Main36.StateAck, payload);
    }

    /// <summary>
    /// <summary>
/// 通用 <c>0x36</c> 带负载组包：<code>f[3]</code> 自动取负载长度，负载写入 <c>f[5]</c> 起的连续字节。
/// </summary>
    public static McuFrame Build36(byte sub, ReadOnlySpan<byte> payload = default)
    {
        var frame = new McuFrame((byte)MainOut.ScreenLockGroup, sub);
        return frame.SetPayload(payload);
    }

    private static byte[] Concat(byte tag, ReadOnlySpan<byte> data)
    {
        var result = new byte[data.Length + 1];
        result[0] = tag;
        data.CopyTo(result.AsSpan(1));
        return result;
    }

    // ---------------------------------------------------------------- 通用

    /// <summary>任意 <c>(main,sub)</c> 空负载帧。</summary>
    public static McuFrame Simple(byte main, byte sub) => new(main, sub);

    /// <summary>任意 <c>(main,sub)</c> + 负载帧。</summary>
    public static McuFrame WithPayload(byte main, byte sub, ReadOnlySpan<byte> payload)
        => new McuFrame(main, sub).SetPayload(payload);

    /// <summary>任意 <c>(main,sub)</c> + 文本负载帧（按字节写入，不追加 <c>\0</c>）。</summary>
    public static McuFrame WithText(byte main, byte sub, string text, Encoding? encoding = null)
    {
        var bytes = (encoding ?? Encoding.UTF8).GetBytes(text);
        int n = Math.Min(bytes.Length, McuFrame.MaxRawPayload);
        return WithPayload(main, sub, bytes.AsSpan(0, n));
    }

    /// <summary>握手 12 步序列。第 1 步必须是 <c>(0x50,0x00)</c>，第 11 步为 1 秒等待。</summary>
    public static IEnumerable<McuFrame> HandshakeSequence()
    {
        yield return Handshake();                     // 1  (0x50,0x00)
        yield return RequestAllChannel();             // 2  (0x28,0x00)
        yield return BaseInfo();                      // 3  (0x01,0x00)
        yield return SyncTimeNow();                   // 4  (0x15,0x00)+7B
        yield return AvMode(0x00);                    // 5  (0x16,0x00)
        yield return AvMode(0x01);                    // 6  (0x16,0x01)
        yield return AvMode(0x02);                    // 7  (0x16,0x02)
        yield return RequestAndroidInfo();            // 8  (0x2B,0x01)
        yield return RegisterWolServerName();          // 9  (0x2B,0x23)+25B
        yield return QueryBoardName();                // 10 (0x34,0x0C)
        // 11 Sleep(1000)
        yield return RequestAndroidMac();             // 12 (0x2D,0x00)
    }
}
