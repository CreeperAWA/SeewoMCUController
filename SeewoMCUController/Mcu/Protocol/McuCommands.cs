namespace SeewoMCUController.Mcu.Protocol;

/// <summary>PC → MCU 主命令（<code>f[1]</code>）。</summary>
public enum MainOut : byte
{
    /// <summary><c>(0x01,0x00)</c> 握手第 3 步「基础信息前段」。</summary>
    BaseInfo = 0x01,

    /// <summary><c>(0x07,ch)</c> 切换通道。</summary>
    SwitchChannel = 0x07,

    /// <summary><c>(0x08,0x00)</c> 设置音量；<c>(0x08,0x01)</c> 降 1 级；<c>(0x08,0x02)</c> 升 1 级；<c>(0x08,0x03)</c> 静音；<c>(0x08,0x04)</c> 取消静音。</summary>
    Volume = 0x08,

    /// <summary><c>(0x09,3..6)</c> 显示/画面模式。</summary>
    GraphicsMode = 0x09,

    /// <summary><c>(0x0E,3..6)</c> 音频/声音模式。</summary>
    AudioMode = 0x0E,

    /// <summary><c>(0x12,ch)</c> 上报当前通道。</summary>
    ReportChannel = 0x12,

    /// <summary><c>(0x13,ch)</c> 设置默认通道模式。</summary>
    DefaultChannelMode = 0x13,

    /// <summary><c>(0x15,0x00)</c> PC 时间同步给安卓板（负载 7 字节，<c>f[3]=7</c>）。</summary>
    SetTime = 0x15,

    /// <summary><c>(0x16,0x00/0x01/0x02)</c> 音视频模式声明 A/B/C；本项目历史实现还用 <c>(0x16,0x02)</c> 读取固件版本。</summary>
    AvMode = 0x16,

    /// <summary><c>(0x28,0x00)</c> 请求全通道状态。</summary>
    RequestAllChannel = 0x28,

    /// <summary><c>(0x2A,0x00)</c> 触控笔开关（本项目历史实现，负载 3 字节）。</summary>
    Pen = 0x2A,

    /// <summary><c>(0x2B,0x01)</c> 请求 Android 详细信息；<c>(0x2B,0x23)</c> 注册 WOL 服务器名。</summary>
    AndroidInfo = 0x2B,

    /// <summary><c>(0x2D,0x00)</c> 请求 Android MAC。</summary>
    RequestAndroidMac = 0x2D,

    /// <summary><c>(0x31,0x00)</c> 通知安卓板「设备已被 U 盘解锁」。</summary>
    UsbDiskUnlock = 0x31,

    /// <summary><c>(0x32,0x00)</c> 线屏已插入。</summary>
    CableScreenPlugIn = 0x32,

    /// <summary><c>(0x33,sub)</c> 工具栏/主屏等（本项目历史实现）。</summary>
    Toolbar = 0x33,

    /// <summary><c>(0x34,sub)</c> 安卓/全局子命令组。</summary>
    AndroidGlobal = 0x34,

    /// <summary><c>(0x36,sub)</c> 子命令组，子命令分三类：屏幕锁 <c>0x10/0x12</c>、远程协助 <c>0x14/0x16/0x18/0x30/0x34/0x35/0x36/0x37</c>、
    /// U 盘动作 <c>0x19/0x32/0x33/0x38/0x39/0x40</c>。U 盘动作与屏幕锁是两条不同的业务，不要混为一谈。</summary>
    Group36 = 0x36,

    /// <summary><c>(0x40,0x00)</c> 读取设备 UID（本项目历史实现）。</summary>
    Uid = 0x40,

    /// <summary><c>(0x50,0x00)</c> 握手首包，拉基础信息。</summary>
    MachineType = 0x50,
}

/// <summary><c>0x34</c> 子命令。</summary>
public enum Main34 : byte
{
    /// <summary>→MCU 让安卓板打开网络设置菜单（<c>f[3]=0x01, f[5]=0x01</c>）。</summary>
    NetworkMenu = 0x0A,

    /// <summary>←PC 网络设置菜单状态回包（<c>f[3]</c> 是状态码）。</summary>
    NetworkMenuReply = 0x0B,

    /// <summary>→MCU 查询板卡名。</summary>
    QueryBoardName = 0x0C,

    /// <summary>←PC 板卡名回包。</summary>
    BoardNameReply = 0x0D,

    /// <summary>←PC 设备 IP 回包（本项目历史实现的命令）。</summary>
    IpReply = 0x11,

    /// <summary>→MCU WOL 广播（让安卓板唤醒 OPS）。</summary>
    WolBroadcast = 0x12,

    /// <summary>←PC WOL 广播回执。</summary>
    WolBroadcastReply = 0x13,

    /// <summary>←PC 未命名通知，<c>f[5]</c> 为 int8 值、<c>f[6..]</c> 为文本。</summary>
    PropertyNotify = 0x14,
}

/// <summary><c>0x36</c> 子命令（屏幕锁 / 远程协助 / U 盘动作）。</summary>
public enum Main36 : byte
{
    /// <summary>→MCU 锁 Android 屏。</summary>
    LockScreen = 0x10,

    /// <summary>→MCU 解锁 Android 屏。</summary>
    UnlockScreen = 0x12,

    /// <summary>→MCU 状态／凭据应答 <c>[tag][数据…]</c>。</summary>
    StatusAck = 0x14,

    /// <summary>→MCU 开关／弹窗状态应答 <c>[tag][数据…]</c>。
    /// <para>U 盘插入时也用本帧回程（<code>tag=0x02</code>），是该帧唯一一条被两条链路共用的回程帧。</para></summary>
    SwitchStateAck = 0x16,

    /// <summary>→MCU 二进制负载应答 <c>[tag][二进制…]</c>。</summary>
    BinaryAck = 0x18,

    /// <summary>←PC U 盘插拔事件：<c>f[5]</c>=action（<c>0</c>=拔出 / <c>1</c>=插入），<c>f[6..]</c>=U 盘标识串。</summary>
    UsbActionNotify = 0x19,

    /// <summary>←PC 远程协助锁屏口令。</summary>
    ScreenPassword = 0x30,

    /// <summary>→MCU 口令校验结果（1 字节：0x01 正确 / 0x00 错误）。</summary>
    PasswordResult = 0x31,

    /// <summary>←PC U 盘文件数据（<b>U 盘插在安卓板侧</b>时的文件下发）。
    /// <para><c>f[5]</c> 为 type：<code>0xFF</code> = 请求回传（对应应答 <c>(0x36,0x33)</c>），<code>0</code> = 重置缓存，其余为数据。</para></summary>
    UsbLockFileToPc = 0x32,

    /// <summary>→MCU 请求安卓板回传文件数据（空负载），U 盘镜像通道的回程帧。</summary>
    RequestFileBack = 0x33,

    /// <summary>←PC 希沃账号／口令。</summary>
    SeewoUser = 0x34,

    /// <summary>→MCU 状态应答（1 或 3 字节）。</summary>
    StateAck = 0x35,

    /// <summary>←PC 请求二维码信息。</summary>
    QrCodeRequest = 0x36,

    /// <summary>→MCU 无负载通知。</summary>
    Notice = 0x37,

    /// <summary>←PC 安卓板上报 SN（U 盘镜像通道）。</summary>
    SerialNumber = 0x38,

    /// <summary>→MCU SN 接收确认（空负载），U 盘镜像通道的回程帧。</summary>
    SnAck = 0x39,

    /// <summary>←PC U 盘插拔事件，与 <see cref="UsbActionNotify"/>（0x19）共用同一处理链与同一 payload 格式，
    /// 两者是同一条 U 盘镜像通道上的两个 SUB 别名。</summary>
    UsbAction = 0x40,
}

/// <summary><c>0x2B</c> 子命令。</summary>
public enum Main2B : byte
{
    /// <summary>→MCU 请求 Android 详细信息。</summary>
    RequestInfo = 0x01,

    /// <summary>←PC Android SN 回包。</summary>
    SerialNumberReply = 0x02,

    /// <summary>→MCU 注册 WOL 服务器名（负载定长 25 字节）。</summary>
    WolServerName = 0x23,

    /// <summary>←PC 系统属性回包（key 定长 5 字节 + value）。</summary>
    PropertyReply = 0x24,
}

/// <summary>MCU → PC 主命令（<code>f[1]</code>），共 22 条。</summary>
public enum MainIn : byte
{
    /// <summary>打开会议或浏览器。</summary>
    OpenMeetingOrIe = 0x1E,

    /// <summary>打开 EasiMeeting。</summary>
    OpenEasyMeeting = 0x1F,

    /// <summary>全通道状态；<c>sub==5</c> 表示安卓板支持全通道。</summary>
    AllChannelMessage = 0x28,

    /// <summary>安卓板请求 PC 执行 U 盘锁动作。</summary>
    UsbLock = 0x29,

    /// <summary>Android 信息（SN / 系统属性）。</summary>
    AndroidInfo = 0x2B,

    /// <summary>时间基准通知。</summary>
    NotifyTimeBase = 0x2C,

    /// <summary>Android MAC 回包。</summary>
    AndroidMacReply = 0x2D,

    /// <summary>应用状态通知（<c>sub&lt;=1</c> 有效）。</summary>
    AppStateNotification = 0x3D,

    /// <summary>设备 UID（本项目历史实现的命令）。</summary>
    UidReply = 0xC1,

    /// <summary>安卓／全局子命令组。</summary>
    AndroidGlobal = 0x34,

    /// <summary>子命令组：屏幕锁、远程协助与 U 盘动作三类（同 <c>0x36</c> 下的 <c>Main36</c> 枚举）。</summary>
    Group36 = 0x36,

    /// <summary>机型信息。</summary>
    MachineType = 0x50,

    /// <summary>命令 PC 打开 EasiMeeting。</summary>
    ControlPcOpenMeeting = 0x80,

    /// <summary>让 PC 待机。</summary>
    SetPcStandby = 0x82,

    /// <summary>电视静音状态。</summary>
    TvMuteState = 0x83,

    /// <summary>把当前 PPT 页面改成与屏幕同分辨率（无负载，纯触发）。</summary>
    SwitchPptResolution = 0x84,

    /// <summary>撤销／注销会话。</summary>
    PcDoRevocation = 0x86,

    /// <summary>当前通道状态。</summary>
    CurrentChannelStatus = 0xA2,

    /// <summary>当前 TV 状态统计（<c>f[8]</c>=通道号，<c>f[9]</c>=子通道索引）。</summary>
    CurrentTvStatus = 0xAB,

    /// <summary>请求 PC 当前时间。</summary>
    RequestPcTime = 0xC0,

    /// <summary>最小化所有窗口。</summary>
    MinimizeAllWindows = 0xC4,

    /// <summary>返回触摸状态。</summary>
    ReturnTouchStatus = 0xC5,

    /// <summary>返回版本（0=Android，1=MCU，2=触摸固件）。</summary>
    ReturnVersion = 0xC6,

    /// <summary>所有输入设备状态。</summary>
    AllInputDeviceStatus = 0xC8,

    /// <summary>把 USB 切到 PC。</summary>
    SwitchUsbToComputer = 0xD1,
}

/// <summary>通道号（业务侧通道类型 ↔ MCU 通道号）。</summary>
public enum McuChannel : byte
{
    /// <summary><c>ChannelType.TV</c> = 1。</summary>
    Tv = 1,

    /// <summary><c>ChannelType.PC</c> = 0（Windows / OPS）。</summary>
    Pc = 5,

    /// <summary><c>ChannelType.HDMI</c> = 2。</summary>
    Hdmi = 9,

    /// <summary><c>ChannelType.VGA</c> = 3。</summary>
    Vga = 10,

    /// <summary><c>ChannelType.ANDROID</c> = 4（安卓板自身输入）。</summary>
    Android = 14,
}

/// <summary>显示／画面模式（<c>GraphicsMode</c>），下发时 SUB = 值 + 3。</summary>
public enum GraphicsMode
{
    /// <summary>标准。</summary>
    Standard = 0,

    /// <summary>明亮。</summary>
    Brightness = 1,

    /// <summary>鲜艳。</summary>
    Gorgeous = 2,

    /// <summary>柔和。</summary>
    Soft = 3,

    /// <summary>自定义。仅 0..3 有对应命令，该值不会下发。</summary>
    Custom = 4,
}

/// <summary>音频／声音模式（<c>AudioMode</c>），下发时 SUB = 值 + 3。</summary>
public enum AudioMode
{
    /// <summary>标准。</summary>
    Standard = 0,

    /// <summary>教室。</summary>
    Classroom = 1,

    /// <summary>影视。</summary>
    Movie = 2,

    /// <summary>会议。</summary>
    Meeting = 3,

    /// <summary>自定义。仅 0..3 有对应命令，该值不会下发。</summary>
    Custom = 4,
}

/// <summary>命令名表，供 CLI / TUI / 日志复用。</summary>
public static class McuCommandNames
{
    private static readonly Dictionary<(byte Main, byte Sub), string> OutNames = new()
    {
        [(0x01, 0x00)] = "基础信息前段",
        [(0x01, 0x01)] = "通知安卓板关机",
        [(0x07, 0x09)] = "切换通道/HDMI",
        [(0x08, 0x00)] = "设置音量",
        [(0x08, 0x01)] = "音量减 1 级",
        [(0x08, 0x02)] = "音量加 1 级",
        [(0x08, 0x03)] = "静音",
        [(0x08, 0x04)] = "取消静音",
        [(0x09, 0x03)] = "显示模式-标准",
        [(0x09, 0x04)] = "显示模式-明亮",
        [(0x09, 0x05)] = "显示模式-鲜艳",
        [(0x09, 0x06)] = "显示模式-柔和",
        [(0x0E, 0x03)] = "声音模式-标准",
        [(0x0E, 0x04)] = "声音模式-教室",
        [(0x0E, 0x05)] = "声音模式-影视",
        [(0x0E, 0x06)] = "声音模式-会议",
        [(0x12, 0x00)] = "上报当前通道",
        [(0x13, 0x00)] = "设置默认通道模式",
        [(0x15, 0x00)] = "PC 时间同步",
        [(0x16, 0x00)] = "音视频模式声明 A",
        [(0x16, 0x01)] = "音视频模式声明 B",
        [(0x16, 0x02)] = "音视频模式声明 C / 读版本",
        [(0x28, 0x00)] = "请求全通道状态",
        [(0x2A, 0x00)] = "触控笔开关",
        [(0x2B, 0x01)] = "请求 Android 详细信息",
        [(0x2B, 0x23)] = "注册 WOL 服务器名",
        [(0x2D, 0x00)] = "请求 Android MAC",
        [(0x31, 0x00)] = "已用 U 盘解锁设备",
        [(0x32, 0x00)] = "线屏已插入",
        [(0x33, 0x00)] = "工具栏/主屏控制",
        [(0x34, 0x0A)] = "打开网络设置菜单",
        [(0x34, 0x0C)] = "查询板卡名",
        [(0x34, 0x10)] = "读取设备 IP",
        [(0x34, 0x12)] = "WOL 广播",
        [(0x36, 0x10)] = "锁 Android 屏",
        [(0x36, 0x12)] = "解锁 Android 屏",
        [(0x36, 0x14)] = "状态/凭据应答",
        [(0x36, 0x16)] = "开关/弹窗状态应答",
        [(0x36, 0x18)] = "二进制负载应答",
        [(0x36, 0x31)] = "口令校验结果",
        [(0x36, 0x33)] = "请求回传文件数据",
        [(0x36, 0x35)] = "状态应答",
        [(0x36, 0x37)] = "无负载通知",
        [(0x36, 0x39)] = "SN 接收确认",
        [(0x40, 0x00)] = "读取设备 UID",
        [(0x50, 0x00)] = "握手/拉基础信息",
    };

    private static readonly Dictionary<byte, string> InNames = new()
    {
        [0x1E] = "打开会议或浏览器",
        [0x1F] = "打开 EasiMeeting",
        [0x28] = "全通道状态",
        [0x29] = "U 盘锁动作",
        [0x2B] = "Android 信息",
        [0x2C] = "时间基准通知",
        [0x2D] = "Android MAC",
        [0x34] = "安卓/全局通知",
        [0x36] = "屏幕锁/远程协助/U 盘动作",
        [0x3D] = "应用状态通知",
        [0x50] = "机型信息",
        [0x80] = "打开 EasiMeeting",
        [0x82] = "PC 待机请求",
        [0x83] = "电视静音状态",
        [0x84] = "PPT 切到屏幕分辨率",
        [0x86] = "撤销/注销",
        [0xA2] = "当前通道状态",
        [0xAB] = "当前 TV 状态",
        [0xC0] = "请求 PC 时间",
        [0xC1] = "设备 UID",
        [0xC4] = "最小化所有窗口",
        [0xC5] = "触摸状态",
        [0xC6] = "版本信息",
        [0xC8] = "输入设备状态",
        [0xD1] = "USB 切到 PC",
    };

    public static string DescribeOut(McuFrame frame)
    {
        if (OutNames.TryGetValue((frame.Main, frame.Sub), out var exact)) return exact;
        if (frame.Main == (byte)MainOut.SwitchChannel || frame.Main == (byte)MainOut.DefaultChannelMode
            || frame.Main == (byte)MainOut.ReportChannel)
            return $"通道命令 ch={DescribeChannel(frame.Sub)}";
        if (frame.Main == (byte)MainOut.Volume || frame.Main == (byte)MainOut.Group36
            || frame.Main == (byte)MainOut.AndroidGlobal || frame.Main == (byte)MainOut.AndroidInfo)
            return $"0x{frame.Main:X2}/0x{frame.Sub:X2}";
        return $"0x{frame.Main:X2}/0x{frame.Sub:X2}";
    }

    public static string DescribeIn(McuFrame frame)
    {
        var head = InNames.TryGetValue(frame.Main, out var name) ? name : $"未知主命令 0x{frame.Main:X2}";
        return $"{head} (0x{frame.Main:X2}/0x{frame.Sub:X2})";
    }

    public static string DescribeChannel(byte ch) => ch switch
    {
        (byte)McuChannel.Tv => "TV/内置输入(1)",
        (byte)McuChannel.Pc => "PC/OPS(5)",
        (byte)McuChannel.Hdmi => "HDMI(9)",
        (byte)McuChannel.Vga => "VGA(10)",
        (byte)McuChannel.Android => "安卓板(14)",
        _ => $"未知({ch})",
    };

    public static string DescribeGraphicsMode(GraphicsMode mode) => mode switch
    {
        GraphicsMode.Standard => "标准",
        GraphicsMode.Brightness => "明亮",
        GraphicsMode.Gorgeous => "鲜艳",
        GraphicsMode.Soft => "柔和",
        GraphicsMode.Custom => "自定义（PC 侧不下发）",
        _ => "未知",
    };

    public static string DescribeAudioMode(AudioMode mode) => mode switch
    {
        AudioMode.Standard => "标准",
        AudioMode.Classroom => "教室",
        AudioMode.Movie => "影视",
        AudioMode.Meeting => "会议",
        AudioMode.Custom => "自定义（PC 侧不下发）",
        _ => "未知",
    };
}
