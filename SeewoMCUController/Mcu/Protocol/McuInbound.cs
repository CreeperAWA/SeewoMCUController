using System.Globalization;
using System.Text;

namespace SeewoMCUController.Mcu.Protocol;

/// <summary>入站事件类别，决定 <c>McuDevice</c> 是否需要自动应答。</summary>
public enum InboundKind
{
    /// <summary>纯通知，无需应答。</summary>
    Notice,

    /// <summary>携带状态更新（板卡名 / SN / MAC / 版本 / 通道…）。</summary>
    State,

    /// <summary>PC 需要应答（对时、口令校验、SN 确认、回传文件…）。</summary>
    Request,
}

/// <summary>一条 MCU → PC 事件的解码结果。</summary>
public sealed class McuInboundEvent
{
    public required McuFrame Frame { get; init; }
    public required MainIn Main { get; init; }
    public required string Name { get; init; }
    public required string Detail { get; init; }
    public InboundKind Kind { get; init; } = InboundKind.Notice;

    /// <summary>自动应答的简短说明（仅 <see cref="InboundKind.Request"/> 使用）。</summary>
    public string? AutoReply { get; init; }

    public override string ToString() =>
        Detail.Length == 0 ? Name : $"{Name} — {Detail}";
}

/// <summary>
/// MCU → PC 解析器。覆盖 22 条主命令以及 <c>0x34</c> / <c>0x36</c> 子命令组，
/// 以及本项目历史实现用到的 <c>(0x34,0x11)</c> IP、<c>(0xC1)</c> UID。
/// </summary>
public static class McuInbound
{
    public static McuInboundEvent Decode(McuFrame frame)
    {
        var main = (MainIn)frame.Main;
        return frame.Main switch
        {
            (byte)MainIn.AndroidInfo => Decode2B(frame),
            (byte)MainIn.AndroidGlobal => Decode34(frame),
            (byte)MainIn.ScreenLockGroup => Decode36(frame),
            (byte)MainIn.AndroidMacReply => new McuInboundEvent
            {
                Frame = frame,
                Main = main,
                Name = "Android MAC",
                Detail = frame.PayloadString(),
                Kind = InboundKind.State,
            },
            (byte)MainIn.UidReply => new McuInboundEvent
            {
                Frame = frame,
                Main = main,
                Name = "设备 UID",
                Detail = FormatUid(frame.PayloadBytes(0, Math.Min(frame.PayloadLength, 12))),
                Kind = InboundKind.State,
            },
            (byte)MainIn.AllChannelMessage => new McuInboundEvent
            {
                Frame = frame,
                Main = main,
                Name = frame.Sub == 5 ? "安卓板支持全通道" : "全通道状态",
                Detail = frame.Sub == 5 ? "bSupportAllChannel = true" : $"sub=0x{frame.Sub:X2}",
                Kind = InboundKind.State,
            },
            (byte)MainIn.MachineType => new McuInboundEvent
            {
                Frame = frame,
                Main = main,
                Name = "机型字符串",
                Detail = frame.PayloadString(),
                Kind = InboundKind.State,
            },
            (byte)MainIn.ReturnVersion => DecodeVersion(frame),
            (byte)MainIn.RequestPcTime => new McuInboundEvent
            {
                Frame = frame,
                Main = main,
                Name = "请求 PC 当前时间",
                Detail = string.Empty,
                Kind = InboundKind.Request,
                AutoReply = "自动回发 (0x15,0x00) + 7 字节时间",
            },
            (byte)MainIn.CurrentTvStatus => new McuInboundEvent
            {
                Frame = frame,
                Main = main,
                Name = "当前 TV 状态统计",
                Detail = $"通道={McuCommandNames.DescribeChannel(frame.Buffer[8])} 子通道索引={frame.Buffer[9]}",
                Kind = InboundKind.State,
            },
            (byte)MainIn.CurrentChannelStatus => new McuInboundEvent
            {
                Frame = frame,
                Main = main,
                Name = "当前通道状态",
                Detail = $"通道={McuCommandNames.DescribeChannel(frame.Buffer[8])} 子通道索引={frame.Buffer[9]}",
                Kind = InboundKind.State,
            },
            (byte)MainIn.UsbLock => new McuInboundEvent
            {
                Frame = frame,
                Main = main,
                Name = "U 盘锁动作请求",
                // sub==0 与 sub==1 是两类不同的动作，语义未命名
                Detail = frame.Sub == 0 ? "动作 A（内部 timer 6）" : frame.Sub == 1 ? "动作 B（内部 timer 5）" : $"sub=0x{frame.Sub:X2}",
                Kind = InboundKind.Request,
                AutoReply = "PC 侧仅投本地定时器，协议无回包",
            },
            (byte)MainIn.OpenMeetingOrIe => Simple(frame, main, "打开会议或浏览器"),
            (byte)MainIn.OpenEasyMeeting => Simple(frame, main, "打开 EasiMeeting"),
            (byte)MainIn.ControlPcOpenMeeting => Simple(frame, main, "命令 PC 打开 EasiMeeting"),
            (byte)MainIn.SetPcStandby => Simple(frame, main, "让 PC 待机"),
            (byte)MainIn.TvMuteState => Simple(frame, main, "电视静音状态"),
            (byte)MainIn.SwitchPptResolution => new McuInboundEvent
            {
                Frame = frame,
                Main = main,
                Name = "把当前 PPT 切到与屏幕同分辨率",
                Detail = "payload 为空，纯触发通知",
                Kind = InboundKind.Request,
                AutoReply = "PC 侧调整幻灯片页面比例（3 秒节流）",
            },
            (byte)MainIn.PcDoRevocation => new McuInboundEvent
            {
                Frame = frame,
                Main = main,
                Name = "撤销／注销会话",
                Detail = $"参数 {frame.PayloadBytes(0, 4).ToArray().Select(b => b.ToString("X2")).Aggregate((a, b) => a + " " + b)}",
                Kind = InboundKind.Request,
                AutoReply = "PC 侧撤销会话（语义未命名）",
            },
            (byte)MainIn.AppStateNotification => new McuInboundEvent
            {
                Frame = frame,
                Main = main,
                Name = "应用状态通知",
                Detail = frame.Sub <= 1 ? $"sub={frame.Sub}" : "Unknown subCommand",
                Kind = InboundKind.State,
            },
            (byte)MainIn.MinimizeAllWindows => Simple(frame, main, "最小化所有窗口"),
            (byte)MainIn.ReturnTouchStatus => Simple(frame, main, "返回触摸状态"),
            (byte)MainIn.AllInputDeviceStatus => Simple(frame, main, "所有输入设备状态"),
            (byte)MainIn.SwitchUsbToComputer => Simple(frame, main, "把 USB 切到 PC"),
            (byte)MainIn.NotifyTimeBase => Simple(frame, main, "时间基准通知"),
            _ => new McuInboundEvent
            {
                Frame = frame,
                Main = main,
                Name = $"未处理的主命令 0x{frame.Main:X2}",
                Detail = frame.ToHex(16),
            },
        };
    }

    private static McuInboundEvent Simple(McuFrame frame, MainIn main, string name) => new()
    {
        Frame = frame,
        Main = main,
        Name = name,
        Detail = frame.PayloadLength > 0 ? frame.ToHex(Math.Min(McuFrame.PayloadOffset + frame.PayloadLength, McuFrame.Size)) : string.Empty,
    };

    private static McuInboundEvent Decode2B(McuFrame frame)
    {
        switch ((Main2B)frame.Sub)
        {
            case Main2B.SerialNumberReply:
            {
                var sn = frame.PayloadString();
                return new McuInboundEvent
                {
                    Frame = frame,
                    Main = MainIn.AndroidInfo,
                    Name = "Android SN",
                    Detail = sn == "empty" ? "板卡未烧录 SN" : sn,
                    Kind = InboundKind.State,
                };
            }
            case Main2B.PropertyReply:
            {
                var key = frame.PayloadString(0, 5);
                var value = frame.PayloadString(5);
                return new McuInboundEvent
                {
                    Frame = frame,
                    Main = MainIn.AndroidInfo,
                    Name = "系统属性",
                    Detail = $"{key}={value}",
                    Kind = InboundKind.State,
                };
            }
            default:
                return new McuInboundEvent
                {
                    Frame = frame,
                    Main = MainIn.AndroidInfo,
                    Name = "Android 信息",
                    Detail = $"sub=0x{frame.Sub:X2} {frame.PayloadString()}",
                    Kind = InboundKind.Notice,
                };
        }
    }

    private static McuInboundEvent Decode34(McuFrame frame)
    {
        switch ((Main34)frame.Sub)
        {
            case Main34.NetworkMenuReply:
                return new McuInboundEvent
                {
                    Frame = frame,
                    Main = MainIn.AndroidGlobal,
                    Name = "网络设置菜单状态回包",
                    // 此命令 f[3] 是状态码而不是长度
                    Detail = $"状态码={frame.Raw}",
                    Kind = InboundKind.State,
                };
            case Main34.BoardNameReply:
            {
                // [!] 本命令不使用 f[3]，直接读 f[5] 起的 \0 串
                var name = frame.CString();
                return new McuInboundEvent
                {
                    Frame = frame,
                    Main = MainIn.AndroidGlobal,
                    Name = "板卡名",
                    Detail = name,
                    Kind = InboundKind.State,
                };
            }
            case Main34.IpReply:
                return new McuInboundEvent
                {
                    Frame = frame,
                    Main = MainIn.AndroidGlobal,
                    Name = "设备 IP",
                    Detail = frame.PayloadString(),
                    Kind = InboundKind.State,
                };
            case Main34.WolBroadcastReply:
                return new McuInboundEvent
                {
                    Frame = frame,
                    Main = MainIn.AndroidGlobal,
                    Name = "WOL 广播回执",
                    Detail = "仅日志",
                    Kind = InboundKind.State,
                };
            case Main34.PropertyNotify:
                return new McuInboundEvent
                {
                    Frame = frame,
                    Main = MainIn.AndroidGlobal,
                    Name = "未命名通知",
                    Detail = $"{frame.PayloadByte(0)} / {frame.CString(1)}",
                    Kind = InboundKind.State,
                };
            default:
                return new McuInboundEvent
                {
                    Frame = frame,
                    Main = MainIn.AndroidGlobal,
                    Name = "安卓/全局通知",
                    Detail = $"sub=0x{frame.Sub:X2} {frame.PayloadString()}",
                    Kind = InboundKind.Notice,
                };
        }
    }

    private static McuInboundEvent Decode36(McuFrame frame)
    {
        switch ((Main36)frame.Sub)
        {
            case Main36.UsbActionNotify:
            case Main36.UsbAction:
            {
                byte action = frame.PayloadByte(0);
                var drive = frame.PayloadString(1, Math.Max(frame.PayloadLength - 1, 0));
                return new McuInboundEvent
                {
                    Frame = frame,
                    Main = MainIn.ScreenLockGroup,
                    Name = action switch { 0 => "U 盘拔出", 1 => "U 盘插入", _ => "U 盘动作" },
                    Detail = string.IsNullOrEmpty(drive) ? $"action={action}" : drive,
                    Kind = action == 1 ? InboundKind.Request : InboundKind.State,
                    AutoReply = action == 1 ? "回发 (0x36,0x16) 弹窗状态" : null,
                };
            }
            case Main36.ScreenPassword:
                return new McuInboundEvent
            {
                Frame = frame,
                Main = MainIn.ScreenLockGroup,
                Name = "远程协助锁屏口令",
                Detail = frame.PayloadString(),
                Kind = InboundKind.Request,
                AutoReply = "校验后回 (0x36,0x31)；正确再发 (0x36,0x12)",
            };
            case Main36.UsbLockFileToPc:
            {
                byte type = frame.PayloadByte(0);
                return new McuInboundEvent
                {
                    Frame = frame,
                    Main = MainIn.ScreenLockGroup,
                    Name = type switch { 0 => "文件数据-重置缓存", 0xFF => "文件数据-请求回传", _ => "文件数据" },
                    Detail = type == 0xFF ? string.Empty : frame.ToHex(McuFrame.PayloadOffset + Math.Min(frame.PayloadLength, McuFrame.Size)),
                    Kind = type == 0xFF ? InboundKind.Request : InboundKind.State,
                    AutoReply = type == 0xFF ? "回发 (0x36,0x33)" : null,
                };
            }
            case Main36.SeewoUser:
                return new McuInboundEvent
                {
                    Frame = frame,
                    Main = MainIn.ScreenLockGroup,
                    Name = "希沃账号／口令",
                    Detail = frame.PayloadString(),
                    Kind = InboundKind.Request,
                    AutoReply = "校验通过则 UnlockScreen()",
                };
            case Main36.QrCodeRequest:
                return new McuInboundEvent
                {
                    Frame = frame,
                    Main = MainIn.ScreenLockGroup,
                    Name = "请求二维码信息",
                    Detail = string.Empty,
                    Kind = InboundKind.Request,
                };
            case Main36.SerialNumber:
                return new McuInboundEvent
                {
                    Frame = frame,
                    Main = MainIn.ScreenLockGroup,
                    Name = "安卓板上报 SN",
                    Detail = frame.PayloadString(),
                    Kind = InboundKind.Request,
                    AutoReply = "回发 (0x36,0x39)",
                };
            default:
                return new McuInboundEvent
                {
                    Frame = frame,
                    Main = MainIn.ScreenLockGroup,
                    Name = "0x36 组通知",
                    Detail = $"sub=0x{frame.Sub:X2} {frame.PayloadString()}",
                    Kind = InboundKind.Notice,
                };
        }
    }

    /// <summary><c>(0xC6,0/1/2)</c> 版本回包。三个 sub 的字节解析规则互不相同，见下方各自的实现。</summary>
    public static McuInboundEvent DecodeVersion(McuFrame frame)
    {
        var name = frame.Sub switch
        {
            0 => "Android 版本",
            1 => "MCU 版本",
            2 => "触摸固件版本",
            _ => $"版本(sub=0x{frame.Sub:X2})",
        };
        string value = frame.Sub switch
        {
            0 => ParseAndroidVersion(frame),
            1 => ParseMcuVersion(frame),
            2 => ParseTouchVersion(frame),
            _ => frame.PayloadString(),
        };
        return new McuInboundEvent
        {
            Frame = frame,
            Main = MainIn.ReturnVersion,
            Name = name,
            Detail = value,
            Kind = InboundKind.State,
        };
    }

    /// <summary>若 <c>f[8]==0 &amp;&amp; f[9]==0 &amp;&amp; f[10]==0</c> 则按 2 字节十六进制输出，否则原样 ASCII。</summary>
    public static string ParseAndroidVersion(McuFrame frame)
    {
        string value = frame.Buffer[8] == 0 && frame.Buffer[9] == 0 && frame.Buffer[10] == 0
            ? $"{frame.Buffer[6]:X2}{frame.Buffer[7]:X2}"
            : frame.PayloadString(1, Math.Max(frame.Raw - 1, 0));
        return $"{value} (版本码 {(sbyte)frame.Buffer[5]})";
    }

    /// <summary>若 <c>f[8]==0 &amp;&amp; f[9]==0</c> 则 2 字节，否则 4 字节十六进制。</summary>
    public static string ParseMcuVersion(McuFrame frame) =>
        frame.Buffer[8] == 0 && frame.Buffer[9] == 0
            ? $"{frame.Buffer[6]:X2}{frame.Buffer[7]:X2}"
            : $"{frame.Buffer[6]:X2}{frame.Buffer[7]:X2}{frame.Buffer[8]:X2}{frame.Buffer[9]:X2}";

    /// <summary>
    /// 触摸固件版本：源码循环上界用 <c>f[3]</c>（<b>非</b> <c>f[3]+5</c>），跳过前 5 字节，
    /// 过滤 <c>!</c> / <c>~</c>，只保留数字。
    /// </summary>
    public static string ParseTouchVersion(McuFrame frame)
    {
        var sb = new StringBuilder();
        for (int i = 6; i < frame.Raw && i < McuFrame.Size; i++)
        {
            byte c = frame.Buffer[i];
            if (c is (byte)'!' or (byte)'~') continue;
            if (c < '0' || c > '9') continue;
            sb.Append((char)c);
        }
        return sb.ToString();
    }

    private static string FormatUid(ReadOnlySpan<byte> bytes)
    {
        var sb = new StringBuilder();
        foreach (byte b in bytes)
        {
            if (sb.Length > 0) sb.Append(' ');
            sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
        }
        return sb.ToString();
    }
}
