using System.Text;
using SeewoMCUController.Mcu;
using SeewoMCUController.Mcu.Protocol;

namespace SeewoMCUController.Cli;

/// <summary>
/// 无需设备的自检：打印每条命令构造出的 64 字节业务帧 + 两种封装形式的线上帧，
/// 并按协议规定的字节布局逐条断言，可用于回归校验实现。
/// </summary>
public static class SelfTest
{
    private sealed record Case(string Name, McuFrame Frame, Action<McuFrame>? Verify = null);

    public static int Run()
    {
        var cases = BuildCases();
        int failed = 0;

        Console.WriteLine("==============================================================");
        Console.WriteLine("  MCU 帧构造自检");
        Console.WriteLine("==============================================================");

        foreach (var c in cases)
        {
            string? error = null;
            try
            {
                c.Verify?.Invoke(c.Frame);
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }

            bool ok = error is null;
            if (!ok) failed++;
            Console.WriteLine($"{(ok ? "[OK]" : "[ERR]")} {c.Name,-34} {c.Frame}");
            if (!ok) Console.WriteLine($"    断言失败: {error}");
        }

        Console.WriteLine();
        Console.WriteLine("---- 封装对照（同一业务帧的两种线上形态）----");
        var sample = McuOutbound.Handshake();
        var rawWire = sample.ToTransport(FrameMode.Raw);
        var framedWire = sample.ToTransport(FrameMode.Framed);
        Console.WriteLine($"  业务帧 (64B)      : {sample.ToHex()}");
        Console.WriteLine($"  Mode A 无头 (64B) : {Hex(rawWire)}");
        Console.WriteLine($"  Mode B 带头 (64B) : {Hex(framedWire)}");
        Console.WriteLine("  说明：Mode B 先把业务帧右移 5 字节，再在线首写入 FC A5 05 3B 00；");
        Console.WriteLine("        SealFrame 写进 [63] 的校验和会被右移到 [58]，因此带头模式下不存在帧尾校验。");

        var framedOk = framedWire[0] == 0xFC && framedWire[1] == 0xA5 && framedWire[2] == 0x05 && framedWire[3] == 0x3B
            && framedWire[4] == 0x00 && framedWire[5] == 0xFB && framedWire[6] == 0x50 && framedWire[7] == 0x00;
        var rawOk = rawWire[0] == 0xFB && rawWire[1] == 0x50 && rawWire[63] == 0x4B;
        Console.WriteLine($"  {(framedOk ? "[OK]" : "[ERR]")} Mode B 偏移断言（线上 [5]=FB [6]=MAIN [7]=SUB [8]=len）");
        Console.WriteLine($"  {(rawOk ? "[OK]" : "[ERR]")} Mode A 断言（[0]=FB，帧尾校验和 = 0xFB+0x50 = 0x4B）");
        if (!framedOk || !rawOk) failed++;

        Console.WriteLine();
        Console.WriteLine("---- 时间同步帧 (0x15) 逐字节 ----");        var t = McuOutbound.SyncTime(new DateTime(2026, 10, 6, 14, 30, 45));
        Console.WriteLine($"  f[1..4] = {t.Buffer[1]:X2} {t.Buffer[2]:X2} {t.Buffer[3]:X2} {t.Buffer[4]:X2}   (MAIN=15 SUB=00 len=07)");
        Console.WriteLine($"  f[5..11]= {t.Buffer[5]:X2} {t.Buffer[6]:X2} {t.Buffer[7]:X2} {t.Buffer[8]:X2} {t.Buffer[9]:X2} {t.Buffer[10]:X2} {t.Buffer[11]:X2}   (年高 年低 月 日 时 分 秒)");
        Expect(t.Buffer[1] == 0x15 && t.Buffer[2] == 0x00 && t.Buffer[3] == 0x07, "时间帧头部必须是 15 00 07 00（SUB=0x00，0x07 是负载长度）");
        Expect(t.Buffer[5] == 0x07 && t.Buffer[6] == 0xEA, "2026 年应为 07 EA");
        Expect(t.Buffer[7] == 10 && t.Buffer[8] == 6 && t.Buffer[9] == 14 && t.Buffer[10] == 30 && t.Buffer[11] == 45, "月/日/时/分/秒 应为 10/06/14/30/45");
        Console.WriteLine($"  {(t.Buffer[1] == 0x15 && t.Buffer[2] == 0x00 && t.Buffer[3] == 0x07 ? "[OK]" : "[ERR]")} 头部断言通过");

        Console.WriteLine();
        Console.WriteLine("---- 上行拆帧与事件解析往返 ----");
        failed += UplinkRoundTrip();

        Console.WriteLine();
        Console.WriteLine("---- 机型识别（VID/PID 匹配表）----");
        failed += MachineTypeChecks();

        Console.WriteLine();
        Console.WriteLine(failed == 0
            ? $"全部断言通过。"
            : $"{failed} 项断言失败。");
        return failed == 0 ? 0 : 1;
    }

    private static int UplinkRoundTrip()
    {
        int failed = 0;
        void Check(string name, McuFrame up, FrameMode wireMode, Func<McuInboundEvent, bool> verify)
        {
            var wire = up.ToTransport(wireMode);
            bool parsed = McuFrameCodec.TryParse(wire, out var frame, out var detected);
            var evt = parsed ? McuInbound.Decode(frame) : null;
            bool ok = parsed && detected == wireMode && verify(evt!);
            if (!ok) failed++;
            Console.WriteLine($"  {(ok ? "[OK]" : "[ERR]")} {name,-40} {evt}");
            if (!ok && parsed) Console.WriteLine($"      解析为 {evt}");
        }

        var boardName = McuOutbound.Simple(0x34, 0x0D);
        Encoding.ASCII.GetBytes("CVTM638_1G_320").CopyTo(boardName.Buffer.AsSpan(5));
        Check("(0x34,0x0D) 板卡名 / Mode B", boardName, FrameMode.Framed, e => e.Detail == "CVTM638_1G_320");

        var mac = McuOutbound.WithText(0x2D, 0x01, "AA:BB:CC:DD:EE:FF");
        Check("(0x2D,0x01) MAC / Mode A", mac, FrameMode.Raw, e => e.Detail == "AA:BB:CC:DD:EE:FF");

        var mcuVer = McuOutbound.Simple(0xC6, 0x01);
        mcuVer.Buffer[6] = 0x12;
        mcuVer.Buffer[7] = 0x34;
        Check("(0xC6,0x01) MCU 版本 2 字节", mcuVer, FrameMode.Framed, e => e.Detail.StartsWith("1234", StringComparison.Ordinal));

        var mcuVer4 = McuOutbound.Simple(0xC6, 0x01);
        mcuVer4.Buffer[6] = 0xAB;
        mcuVer4.Buffer[7] = 0xCD;
        mcuVer4.Buffer[8] = 0xEF;
        mcuVer4.Buffer[9] = 0x01;
        Check("(0xC6,0x01) MCU 版本 4 字节", mcuVer4, FrameMode.Raw, e => e.Detail == "ABCDEF01");

        var prop = McuOutbound.Simple(0x2B, 0x24);
        var propText = "build.prop=SEEWO_638";
        Encoding.ASCII.GetBytes(propText).CopyTo(prop.Buffer.AsSpan(5));
        prop.Buffer[3] = (byte)propText.Length;
        Check("(0x2B,0x24) 系统属性 key 定长 5", prop, FrameMode.Framed, e => e.Detail == "build=.prop=SEEWO_638");

        var usb = McuOutbound.Simple(0x36, 0x40);
        usb.Buffer[3] = 0x06;
        usb.Buffer[5] = 0x01; // action = 插入
        Encoding.ASCII.GetBytes("F:\\").CopyTo(usb.Buffer.AsSpan(6));
        Check("(0x36,0x40) U 盘插入 action+串", usb, FrameMode.Framed, e => e.Detail == "F:\\" && e.Kind == InboundKind.Request);

        var pwd = McuOutbound.Simple(0x36, 0x30);
        pwd.Buffer[3] = 0x04;
        Encoding.ASCII.GetBytes("1234").CopyTo(pwd.Buffer.AsSpan(5));
        Check("(0x36,0x30) 锁屏口令", pwd, FrameMode.Framed, e => e.Detail == "1234" && e.Kind == InboundKind.Request);

        var tv = McuOutbound.Simple(0xAB, 0x00);
        tv.Buffer[8] = 0x09;
        tv.Buffer[9] = 0x00;
        Check("(0xAB) TV 状态 f[8]/f[9]", tv, FrameMode.Raw, e => e.Detail.Contains("HDMI(9)", StringComparison.Ordinal));

        return failed;
    }

    private static int MachineTypeChecks()
    {
        int failed = 0;
        void Check(int vid, int pid, McuMachineType expected)
        {
            var actual = McuKnownDevices.Detect(vid, pid);
            bool ok = actual == expected;
            if (!ok) failed++;
            Console.WriteLine($"  {(ok ? "[OK]" : "[ERR]")} VID:0x{vid:X4} PID:0x{pid:X4} → {McuKnownDevices.Describe(actual)}");
        }

        Check(0x1FF7, 0x0F15, McuMachineType.Type638);
        Check(0x10E0, 0xAA55, McuMachineType.Type309);
        Check(0x1FF7, 0x0001, McuMachineType.HuaxinTouch);
        Check(0x1FF7, 0x0F20, McuMachineType.GenericSeewo);
        Check(0x1FF7, 0x0F3F, McuMachineType.GenericSeewo);
        Check(0x1FE7, 0x0004, McuMachineType.None);
        return failed;
    }

    private static string Hex(byte[] data)
    {
        var sb = new StringBuilder(data.Length * 3);
        for (int i = 0; i < data.Length; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(data[i].ToString("X2"));
        }
        return sb.ToString();
    }

    private static void Expect(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static List<Case> BuildCases() => new()
    {
        // ---- 握手序列
        new("1  (0x50,0x00) 握手首包", McuOutbound.Handshake(), f =>
        {
            Expect(f.Buffer[1] == 0x50 && f.Buffer[2] == 0x00 && f.Buffer[3] == 0x00, "0x50 组包错误");
        }),
        new("2  (0x28,0x00) 请求全通道", McuOutbound.RequestAllChannel(), f =>
        {
            Expect(f.Buffer[1] == 0x28 && f.Buffer[2] == 0x00, "0x28 组包错误");
        }),
        new("3  (0x01,0x00) 基础信息前段", McuOutbound.BaseInfo(), f =>
        {
            Expect(f.Buffer[1] == 0x01 && f.Buffer[2] == 0x00, "0x01,0x00 组包错误");
        }),
        new("4  (0x15,0x00)+7B 时间同步", McuOutbound.SyncTime(new DateTime(2026, 10, 6, 14, 30, 45)), f =>
        {
            Expect(f.Buffer[1] == 0x15 && f.Buffer[2] == 0x00 && f.Buffer[3] == 0x07 && f.Buffer[4] == 0x00, "应为 15 00 07 00");
            Expect(f.Buffer[5] == 0x07 && f.Buffer[6] == 0xEA, "年份应为 07EA");
            Expect(f.Buffer[7] == 10, "月应为 10（tm_mon+1）");
        }),
        new("5  (0x16,0x00) 音视频模式 A", McuOutbound.AvMode(0x00)),
        new("6  (0x16,0x01) 音视频模式 B", McuOutbound.AvMode(0x01)),
        new("7  (0x16,0x02) 音视频模式 C", McuOutbound.AvMode(0x02)),
        new("8  (0x2B,0x01) 请求安卓信息", McuOutbound.RequestAndroidInfo(), f =>
        {
            Expect(f.Buffer[1] == 0x2B && f.Buffer[2] == 0x01, "0x2B,0x01 组包错误");
        }),
        new("9  (0x2B,0x23) 注册 WOL 名", McuOutbound.RegisterWolServerName(), f =>
        {
            Expect(f.Buffer[3] == 25, "f[3] 应为字符串长度 25");
            var name = Encoding.ASCII.GetString(f.Buffer, 5, 25);
            Expect(name == "persisit.remote.pc.wakeup", $"WOL 名错误: {name}（应为 25 字符且无尾部 \\0）");
        }),
        new("10 (0x34,0x0C) 查询板卡名", McuOutbound.QueryBoardName(), f =>
        {
            Expect(f.Buffer[1] == 0x34 && f.Buffer[2] == 0x0C, "0x34,0x0C 组包错误");
        }),
        new("11 (0x2D,0x00) 请求 MAC", McuOutbound.RequestAndroidMac(), f =>
        {
            Expect(f.Buffer[1] == 0x2D && f.Buffer[2] == 0x00, "0x2D,0x00 组包错误");
        }),
        new("12 (0x01,0x01) 通知关机", McuOutbound.PowerOffRequest(), f =>
        {
            Expect(f.Buffer[1] == 0x01 && f.Buffer[2] == 0x01, "0x01,0x01 组包错误");
        }),

        // ---- 通道
        new("13 (0x07,05) PC", McuOutbound.SwitchChannel(McuChannel.Pc), f =>
        {
            Expect(f.Buffer[1] == 0x07 && f.Buffer[2] == 0x05 && f.Buffer[3] == 0x00, "PC 通道 f[3] 应为 0");
        }),
        new("13 (0x07,01) TV + 分辨率", McuOutbound.SwitchChannel(McuChannel.Tv, 0x1080), f =>
        {
            Expect(f.Buffer[3] == 0x02, "TV 通道 f[3] 应为 2");
            Expect(f.Buffer[5] == 0x10 && f.Buffer[6] == 0x80, "TV 通道 f[5]=param>>8 f[6]=param&0xFF");
        }),
        new("13 (0x07,09) HDMI1 → param-1", McuOutbound.SwitchChannel(McuChannel.Hdmi, 1), f =>
        {
            Expect(f.Buffer[3] == 0x01, "HDMI f[3] 应为 1");
            Expect(f.Buffer[5] == 0x00, "HDMI 的 f[5] 应为 param-1");
        }),
        new("13 (0x07,0A) VGA2 → param-1", McuOutbound.SwitchChannel(McuChannel.Vga, 2), f =>
        {
            Expect(f.Buffer[5] == 0x01, "VGA 的 f[5] 应为 param-1");
        }),
        new("14 (0x13,09) 默认通道模式", McuOutbound.SetDefaultChannelMode(McuChannel.Hdmi, 1), f =>
        {
            Expect(f.Buffer[1] == 0x13 && f.Buffer[5] == 0x01, "(0x13) 的 f[5] 不应减 1");
        }),
        new("20 (0x12,05) 上报通道", McuOutbound.ReportCurrentChannel(McuChannel.Pc), f =>
        {
            Expect(f.Buffer[1] == 0x12 && f.Buffer[2] == 0x05, "0x12,ch 组包错误");
        }),

        // ---- 音频
        new("15 (0x08,00) 设置音量 50", McuOutbound.SetVolume(50), f =>
        {
            Expect(f.Buffer[1] == 0x08 && f.Buffer[2] == 0x00, "0x08,0x00 组包错误");
            Expect(f.Buffer[3] == 0x01, "f[3] 应为常量 1（不是长度）");
            Expect(f.Buffer[5] == 50, "音量应在 f[5]");
        }),
        new("16 (0x08,03) 静音", McuOutbound.SetMute(true), f =>
        {
            Expect(f.Buffer[2] == 0x03 && f.Buffer[3] == 0x00, "静音应为 (0x08,0x03) f[3]=0");
        }),
        new("17 (0x08,04) 取消静音", McuOutbound.SetMute(false), f =>
        {
            Expect(f.Buffer[2] == 0x04, "取消静音应为 (0x08,0x04)");
        }),
        new("-- (0x08,02) 音量+1（历史命令）", McuOutbound.VolumeUp(), f => Expect(f.Buffer[2] == 0x02, "应为 0x02")),
        new("-- (0x08,01) 音量-1（历史命令）", McuOutbound.VolumeDown(), f => Expect(f.Buffer[2] == 0x01, "应为 0x01")),

        // ---- 模式
        new("18 (0x09,03) 显示-标准", McuOutbound.SetGraphicsMode(GraphicsMode.Standard), f => Expect(f.Buffer[1] == 0x09 && f.Buffer[2] == 0x03, "0x09,0x03")),
        new("18 (0x09,06) 显示-柔和", McuOutbound.SetGraphicsMode(GraphicsMode.Soft), f => Expect(f.Buffer[2] == 0x06, "0x09,0x06")),
        new("19 (0x0E,04) 声音-教室", McuOutbound.SetAudioMode(AudioMode.Classroom), f => Expect(f.Buffer[1] == 0x0E && f.Buffer[2] == 0x04, "0x0E,0x04")),
        new("19 (0x0E,06) 声音-会议", McuOutbound.SetAudioMode(AudioMode.Meeting), f => Expect(f.Buffer[2] == 0x06, "0x0E,0x06")),

        // ---- 业务
        new("21 (0x32,00) 线屏插入", McuOutbound.NotifyCableScreenPlugIn(), f => Expect(f.Buffer[1] == 0x32, "0x32")),
        new("22 (0x34,0A) 网络设置菜单", McuOutbound.OpenNetworkMenu(), f =>
        {
            Expect(f.Buffer[1] == 0x34 && f.Buffer[2] == 0x0A, "0x34,0x0A");
            Expect(f.Buffer[3] == 0x01 && f.Buffer[5] == 0x01, "f[3]=0x01 f[5]=0x01");
        }),
        new("23 (0x34,12) WOL 广播", McuOutbound.WolBroadcast(), f => Expect(f.Buffer[2] == 0x12, "0x34,0x12")),
        new("24 (0x31,00) U盘解锁通知", McuOutbound.NotifyUnlockedByUsbKey(), f => Expect(f.Buffer[1] == 0x31, "0x31")),
        new("-- (0x34,10) 读取 IP（历史）", McuOutbound.RequestIp(), f => Expect(f.Buffer[2] == 0x10, "0x34,0x10")),
        new("-- (0x40,00) 读取 UID（历史）", McuOutbound.RequestUid(), f => Expect(f.Buffer[1] == 0x40, "0x40")),
        new("-- (0x2A,00) 触控笔开", McuOutbound.SetPen(true), f =>
        {
            Expect(f.Buffer[1] == 0x2A && f.Buffer[2] == 0x00 && f.Buffer[3] == 0x01 && f.Buffer[5] == 0x01,
                "触控笔应为 f[3]=1 f[5]=1");
        }),

        // ---- 0x36 屏幕锁组
        new("25 (0x36,10) 锁屏", McuOutbound.LockScreen(), f =>
        {
            Expect(f.Buffer[1] == 0x36 && f.Buffer[2] == 0x10 && f.Buffer[3] == 0x00, "0x36,0x10 空负载");
        }),
        new("26 (0x36,12) 解锁屏", McuOutbound.UnlockScreen(), f => Expect(f.Buffer[2] == 0x12, "0x36,0x12")),
        new("27 (0x36,14) 状态应答", McuOutbound.StatusAck(0x00, new byte[] { 0xAA, 0xBB }), f =>
        {
            Expect(f.Buffer[2] == 0x14 && f.Buffer[3] == 3 && f.Buffer[5] == 0x00, "[tag][数据…]，f[3]=1+len");
        }),
        new("28 (0x36,16) 弹窗应答", McuOutbound.SwitchStateAck(0x02), f =>
        {
            Expect(f.Buffer[2] == 0x16 && f.Buffer[3] == 1 && f.Buffer[5] == 0x02, "tag=0x02，f[3]=1");
        }),
        new("29 (0x36,18) 二进制应答", McuOutbound.BinaryAck(0xFF, new byte[] { 1, 2, 3 }), f =>
        {
            Expect(f.Buffer[2] == 0x18 && f.Buffer[3] == 4 && f.Buffer[5] == 0xFF, "tag=0xFF + 3 字节，f[3]=4");
        }),
        new("30 (0x36,31) 口令正确", McuOutbound.PasswordResult(true), f =>
        {
            Expect(f.Buffer[2] == 0x31 && f.Buffer[3] == 1 && f.Buffer[5] == 0x01, "1 字节 0x01");
        }),
        new("30 (0x36,31) 口令错误", McuOutbound.PasswordResult(false), f => Expect(f.Buffer[5] == 0x00, "1 字节 0x00")),
        new("31 (0x36,33) 请求回传文件", McuOutbound.RequestFileBack(), f => Expect(f.Buffer[2] == 0x33 && f.Buffer[3] == 0, "空负载")),
        new("32 (0x36,35) 状态应答×3", McuOutbound.StateAck(0x02, 0x02, 0x02), f =>
        {
            Expect(f.Buffer[2] == 0x35 && f.Buffer[3] == 3, "3 字节负载");
        }),
        new("33 (0x36,37) 无负载通知", McuOutbound.Notice(), f => Expect(f.Buffer[2] == 0x37, "0x36,0x37")),
        new("34 (0x36,39) SN 确认", McuOutbound.AckSerialNumber(), f => Expect(f.Buffer[2] == 0x39, "0x36,0x39")),
    };
}
