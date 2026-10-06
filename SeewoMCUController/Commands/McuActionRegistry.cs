using System.Globalization;
using System.Text;
using Cvte.Mcu;
using SeewoMCUController.Mcu;
using SeewoMCUController.Mcu.Protocol;
using SeewoMCUController.Ui;

namespace SeewoMCUController.Commands;

/// <summary>一个可执行动作。TUI 菜单与 CLI 都由本注册表驱动，保证两边行为完全一致。</summary>
public sealed class McuAction
{
    public required string Category { get; init; }
    public required string Name { get; init; }
    public string[] Aliases { get; init; } = Array.Empty<string>();
    public required string Description { get; init; }

    /// <summary>参数说明，出现在帮助里。</summary>
    public string Usage { get; init; } = string.Empty;

    /// <summary>该动作对应的 MCU 命令（用于帮助里标注帧）。</summary>
    public string Frame { get; init; } = string.Empty;

    /// <summary>不需要连接设备即可执行。</summary>
    public bool Offline { get; init; }

    public required Func<McuDevice, string[], CancellationToken, Task<string>> ExecuteAsync { get; init; }

    public bool Matches(string name) =>
        string.Equals(Name, name, StringComparison.OrdinalIgnoreCase)
        || Aliases.Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
}

/// <summary>命令分类（TUI 二级菜单的一级 / CLI 的第一段）。</summary>
public sealed class McuCategory
{
    public required string Key { get; init; }
    public required string Title { get; init; }
    public required string Hint { get; init; }
    public List<McuAction> Actions { get; } = new();
}

public static class McuActionRegistry
{
    public static List<McuCategory> Categories { get; } = Build();

    public static McuCategory? FindCategory(string key) =>
        Categories.FirstOrDefault(c => string.Equals(c.Key, key, StringComparison.OrdinalIgnoreCase));

    public static McuAction? FindAction(string categoryKey, string actionName)
    {
        var category = FindCategory(categoryKey);
        if (category is null) return null;
        return category.Actions.FirstOrDefault(a => a.Matches(actionName)) ?? SearchOther(actionName);
    }

    /// <summary>跨分类按名字查找，便于 CLI 省略分类前缀（如 <c>SeewoMCUController.exe mute</c>）。</summary>
    public static McuAction? SearchOther(string actionName) =>
        Categories.SelectMany(c => c.Actions).FirstOrDefault(a => a.Matches(actionName));

    public static IEnumerable<McuAction> AllActions => Categories.SelectMany(c => c.Actions);

    // ------------------------------------------------------------------ 参数解析

    public static int IntArg(string[] args, int index, int fallback) =>
        index < args.Length && int.TryParse(args[index], out int v) ? v : fallback;

    public static byte ByteArg(string[] args, int index, byte fallback) =>
        index < args.Length && byte.TryParse(args[index].Replace("0x", ""), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v)
            ? v : fallback;

    public static string TextArg(string[] args, int index, string fallback = "") =>
        index < args.Length ? args[index] : fallback;

    /// <summary>解析负载：偶数个十六进制数字按十六进制，否则按 UTF-8 文本。</summary>
    public static byte[] PayloadArg(string[] args, int fromIndex)
    {
        if (fromIndex >= args.Length) return Array.Empty<byte>();
        return ParseHexOrText(string.Join(" ", args.Skip(fromIndex)));
    }

    private static byte[] ParseHexOrText(string text)
    {
        text = text.Trim();
        if (text.Length == 0) return Array.Empty<byte>();
        string clean = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? text[2..].Trim() : text;
        clean = clean.Replace(" ", "");
        if (clean.Length % 2 == 0 && clean.All(Uri.IsHexDigit))
        {
            var result = new byte[clean.Length / 2];
            for (int i = 0; i < result.Length; i++)
                result[i] = byte.Parse(clean.AsSpan(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return result;
        }
        return Encoding.UTF8.GetBytes(text);
    }

    public static McuChannel ParseChannel(string name) => name.ToLowerInvariant() switch
    {
        "pc" or "5" or "ops" or "windows" => McuChannel.Pc,
        "tv" or "1" or "内置" => McuChannel.Tv,
        "hdmi" or "hdmi1" or "9" => McuChannel.Hdmi,
        "vga" or "10" => McuChannel.Vga,
        "android" or "14" or "安卓" => McuChannel.Android,
        _ => throw new ArgumentException($"未知通道 {name}（可选 pc/tv/hdmi/vga/android）"),
    };

    public static GraphicsMode ParseGraphicsMode(string name) => name.ToLowerInvariant() switch
    {
        "standard" or "0" or "标准" => GraphicsMode.Standard,
        "brightness" or "1" or "明亮" => GraphicsMode.Brightness,
        "gorgeous" or "2" or "鲜艳" => GraphicsMode.Gorgeous,
        "soft" or "3" or "柔和" => GraphicsMode.Soft,
        "custom" or "4" or "自定义" => GraphicsMode.Custom,
        _ => throw new ArgumentException($"未知显示模式 {name}（可选 standard/brightness/gorgeous/soft）"),
    };

    public static AudioMode ParseAudioMode(string name) => name.ToLowerInvariant() switch
    {
        "standard" or "0" or "标准" => AudioMode.Standard,
        "classroom" or "1" or "教室" => AudioMode.Classroom,
        "movie" or "2" or "影视" => AudioMode.Movie,
        "meeting" or "3" or "会议" => AudioMode.Meeting,
        "custom" or "4" or "自定义" => AudioMode.Custom,
        _ => throw new ArgumentException($"未知声音模式 {name}（可选 standard/classroom/movie/meeting）"),
    };

    public static bool IsOn(string? value) =>
        !string.IsNullOrEmpty(value) && value.ToLowerInvariant() is not ("off" or "0" or "false" or "no" or "关");

    // ------------------------------------------------------------------ 执行辅助

    private static Task<string> Send(McuDevice device, Func<McuFrame> build, string note = "")
    {
        if (!device.IsConnected) return Task.FromResult(NotConnected);
        var frame = build();
        bool ok = device.Send(frame);
        return Task.FromResult($"{(ok ? "[OK] 已发送" : "[ERR] 发送失败")}  {McuCommandNames.DescribeOut(frame)}  {frame}{note}");
    }

    private static Task<string> Steps(McuDevice device, Func<int, bool> run, int count, string frame, string verb)
    {
        if (!device.IsConnected) return Task.FromResult(NotConnected);
        int success = 0;
        for (int i = 0; i < count; i++)
        {
            if (run(i)) success++;
        }
        return Task.FromResult($"{(success == count ? "[OK]" : "[!]")} {verb} {frame} × {count}，成功 {success} 次");
    }

    private const string NotConnected = "[ERR] 尚未连接设备";

    private static async Task<string> Query(string label, Task<string?> query)
    {
        var value = await query.ConfigureAwait(false);
        return value is null ? $"[ERR] {label}：超时未收到回包" : $"[OK] {label}：{value}";
    }

    // ------------------------------------------------------------------ 注册表

    private static McuAction Act(
        string category,
        string name,
        string description,
        string frame,
        Func<McuDevice, string[], CancellationToken, Task<string>> exec,
        string usage = "",
        string[]? aliases = null,
        bool offline = false) =>
        new()
        {
            Category = category,
            Name = name,
            Aliases = aliases ?? Array.Empty<string>(),
            Description = description,
            Usage = usage,
            Frame = frame,
            Offline = offline,
            ExecuteAsync = exec,
        };

    private static List<McuCategory> Build()
    {
        var categories = new List<McuCategory>
        {
            new() { Key = "audio", Title = "音频设置", Hint = "音量、静音、声音模式（标准 / 教室 / 影视 / 会议）" },
            new() { Key = "video", Title = "视频设置", Hint = "显示／画面模式（标准 / 明亮 / 鲜艳 / 柔和）" },
            new() { Key = "channel", Title = "通道设置", Hint = "切换通道、默认通道模式、上报通道、请求全通道状态" },
            new() { Key = "lock", Title = "锁屏设置", Hint = "屏幕锁与 0x36 远程协助子命令组" },
            new() { Key = "info", Title = "设备信息", Hint = "板卡名 / SN / MAC / 机型 / 版本 / UID / IP 查询" },
            new() { Key = "system", Title = "系统与电源", Hint = "握手序列、时间同步、WOL、网络设置菜单、关机通知、线屏" },
            new() { Key = "device", Title = "外设与工具", Hint = "触控笔、U 盘解锁通知、任意原始帧" },
            new() { Key = "log", Title = "事件与报文", Hint = "查看上下行报文与 MCU→PC 事件字典" },
            new() { Key = "link", Title = "连接与链路", Hint = "设备枚举、帧模式、自动应答、链路参数" },
        };

        // ---------------- 音频
        var audio = categories[0];
        audio.Actions.Add(Act("audio", "volume", "设置绝对音量（0~100）", "(0x08,0x00) f[3]=1 f[5]=音量",
            (d, a, _) => Send(d, () => McuOutbound.SetVolume(Math.Clamp(IntArg(a, 0, 50), 0, 100)), "  [f[3]=1 是常量不是长度]"),
            "<0-100>", new[] { "vol", "setvol" }));
        audio.Actions.Add(Act("audio", "up", "音量增加 N 级（默认 1）", "(0x08,0x02)",
            (d, a, _) => Steps(d, _ => d.VolumeUp(), Math.Max(IntArg(a, 0, 1), 1), "(0x08,0x02)", "音量已加"),
            "[级数]", new[] { "volup" }));
        audio.Actions.Add(Act("audio", "down", "音量减少 N 级（默认 1）", "(0x08,0x01)",
            (d, a, _) => Steps(d, _ => d.VolumeDown(), Math.Max(IntArg(a, 0, 1), 1), "(0x08,0x01)", "音量已减"),
            "[级数]", new[] { "voldown" }));
        audio.Actions.Add(Act("audio", "mute", "静音", "(0x08,0x03)",
            (d, _, _) => Send(d, () => McuOutbound.SetMute(true))));
        audio.Actions.Add(Act("audio", "unmute", "取消静音", "(0x08,0x04)",
            (d, _, _) => Send(d, () => McuOutbound.SetMute(false))));
        audio.Actions.Add(Act("audio", "mode", "设置声音模式", "(0x0E, mode+3)",
            (d, a, _) =>
            {
                var mode = ParseAudioMode(TextArg(a, 0, "standard"));
                if (!d.IsConnected) return Task.FromResult(NotConnected + "  (" + McuCommandNames.DescribeAudioMode(mode) + ")");
                if (!McuOutbound.TryToModeSub((int)mode, out byte sub))
                    return Task.FromResult($"[ERR] {McuCommandNames.DescribeAudioMode(mode)} 在 PC 侧即被丢弃，不会下发");
                return Task.FromResult(d.Send(McuOutbound.SetAudioMode(mode))
                    ? $"[OK] 已发送 {McuCommandNames.DescribeAudioMode(mode)} → (0x0E,0x{sub:X2})"
                    : "[ERR] 发送失败");
            }, "<standard|classroom|movie|meeting|custom>", new[] { "audiomode", "sound" }));
        audio.Actions.Add(Act("audio", "modes", "查看声音模式映射表（enum+3）", "",
            (_, _, _) => Task.FromResult(ModeTable("音频", new[] { "STANDARD 标准", "CLASSROOM 教室", "MOVIE 影视", "MEETING 会议", "CUSTOM 自定义" })),
            "", Array.Empty<string>(), true));

        // ---------------- 视频
        var video = categories[1];
        video.Actions.Add(Act("video", "mode", "设置显示模式", "(0x09, mode+3)",
            (d, a, _) =>
            {
                var mode = ParseGraphicsMode(TextArg(a, 0, "standard"));
                if (!d.IsConnected) return Task.FromResult(NotConnected + "  (" + McuCommandNames.DescribeGraphicsMode(mode) + ")");
                if (!McuOutbound.TryToModeSub((int)mode, out byte sub))
                    return Task.FromResult($"[ERR] {McuCommandNames.DescribeGraphicsMode(mode)} 在 PC 侧即被丢弃，不会下发");
                return Task.FromResult(d.Send(McuOutbound.SetGraphicsMode(mode))
                    ? $"[OK] 已发送 {McuCommandNames.DescribeGraphicsMode(mode)} → (0x09,0x{sub:X2})"
                    : "[ERR] 发送失败");
            }, "<standard|brightness|gorgeous|soft|custom>", new[] { "graphics", "display" }));
        video.Actions.Add(Act("video", "modes", "查看显示模式映射表（enum+3）", "",
            (_, _, _) => Task.FromResult(ModeTable("显示", new[] { "STANDARD 标准", "BRIGHTNESS 明亮", "GORGEOUS 鲜艳", "SOFT 柔和", "CUSTOM 自定义" })),
            "", Array.Empty<string>(), true));

        // ---------------- 通道
        var channel = categories[2];
        channel.Actions.Add(Act("channel", "switch", "切换通道（HDMI/VGA 的 param 为 1 基序号）", "(0x07, ch)",
            (d, a, _) =>
            {
                var ch = ParseChannel(TextArg(a, 0, "pc"));
                ushort param = (ushort)Math.Max(IntArg(a, 1, 1), 0);
                if (!d.IsConnected) return Task.FromResult($"{NotConnected}  (0x07,0x{(byte)ch:X2}) param={param}");
                return Task.FromResult(d.Send(McuOutbound.SwitchChannel(ch, param))
                    ? $"[OK] 已发送 (0x07,0x{(byte)ch:X2}) {McuCommandNames.DescribeChannel((byte)ch)} param={param}"
                    : "[ERR] 发送失败");
            }, "<pc|tv|hdmi|vga|android> [param]", new[] { "sw" }));
        channel.Actions.Add(Act("channel", "default", "设置默认通道模式", "(0x13, ch)",
            (d, a, _) =>
            {
                var ch = ParseChannel(TextArg(a, 0, "pc"));
                ushort param = (ushort)Math.Max(IntArg(a, 1, 0), 0);
                if (!d.IsConnected) return Task.FromResult($"{NotConnected}  (0x13,0x{(byte)ch:X2}) param={param}");
                return Task.FromResult(d.Send(McuOutbound.SetDefaultChannelMode(ch, param))
                    ? $"[OK] 已发送 (0x13,0x{(byte)ch:X2}) param={param}"
                    : "[ERR] 发送失败");
            }, "<pc|tv|hdmi|vga|android> [param]", new[] { "defmode" }));
        channel.Actions.Add(Act("channel", "report", "上报当前通道", "(0x12, ch)",
            (d, a, _) =>
            {
                var ch = ParseChannel(TextArg(a, 0, "pc"));
                if (!d.IsConnected) return Task.FromResult($"{NotConnected}  (0x12,0x{(byte)ch:X2})");
                return Task.FromResult(d.Send(McuOutbound.ReportCurrentChannel(ch))
                    ? $"[OK] 已发送 (0x12,0x{(byte)ch:X2}) {McuCommandNames.DescribeChannel((byte)ch)}"
                    : "[ERR] 发送失败");
            }, "<pc|tv|hdmi|vga|android>"));
        channel.Actions.Add(Act("channel", "all", "请求全通道状态", "(0x28,0x00)",
            (d, _, _) => Send(d, () => McuOutbound.RequestAllChannel())));
        channel.Actions.Add(Act("channel", "list", "查看通道号映射与参数规则（ChannelType→ch）", "",
            (_, _, _) => Task.FromResult(string.Join(Environment.NewLine, new[]
            {
                "  通道类型        ch    含义",
                "  0  PC        5     PC（Windows / OPS）",
                "  1  TV        1     电视 / 内置输入",
                "  2  HDMI      9     HDMI 1",
                "  3  VGA       10    VGA",
                "  4  ANDROID   14    安卓板自身输入",
                "",
                "  参数规则：",
                "    (0x07,ch)  ch = 1        → f[3]=2, f[5]=param>>8, f[6]=param&0xFF",
                "               ch in {9,10}  → f[3]=1, f[5]=(byte)param-1（param 为 1 基序号）",
                "               其他          → f[3]=0",
                "    (0x13,ch)  布局相同，ch in {9,10} 时 f[5]=(byte)param（不做减一）",
            })), "", Array.Empty<string>(), true));

        // ---------------- 锁屏
        var lockMenu = categories[3];
        lockMenu.Actions.Add(Act("lock", "lock", "锁 Android 屏", "(0x36,0x10)",
            (d, _, _) => Send(d, () => McuOutbound.LockScreen()), "", new[] { "lockscreen" }));
        lockMenu.Actions.Add(Act("lock", "unlock", "解锁 Android 屏", "(0x36,0x12)",
            (d, _, _) => Send(d, () => McuOutbound.UnlockScreen()), "", new[] { "unlockscreen" }));
        lockMenu.Actions.Add(Act("lock", "pwd", "回 (0x36,0x31)=校验通过并自动解锁", "(0x36,0x31)",
            (d, a, _) =>
            {
                if (!d.IsConnected) return Task.FromResult(NotConnected);
                bool ok = d.CheckPassword(TextArg(a, 0, string.Empty));
                bool sent = d.Send(McuOutbound.PasswordResult(ok));
                string extra = string.Empty;
                if (ok && d.AutoUnlockOnPasswordAccepted && d.Send(McuOutbound.UnlockScreen()))
                    extra = "，并已发送 (0x36,0x12) 解锁";
                return Task.FromResult($"{(sent ? "[OK]" : "[ERR]")} 口令{(ok ? "正确" : "错误")}，已回 (0x36,0x31)={(ok ? "0x01" : "0x00")}{extra}");
            }, "<口令>", new[] { "password" }));
        lockMenu.Actions.Add(Act("lock", "set-password", "记录参考口令（仅本机，不发 MCU 命令；校验一律通过）", "",
            (d, a, _) =>
            {
                d.ScreenLockPassword = TextArg(a, 0, string.Empty);
                return Task.FromResult(string.IsNullOrEmpty(d.ScreenLockPassword)
                    ? "参考口令已清空（口令校验仍一律通过）"
                    : $"参考口令已记录（{d.ScreenLockPassword.Length} 位，口令校验仍一律通过）");
            }, "<口令>", new[] { "setpwd" }, true));
        lockMenu.Actions.Add(Act("lock", "file-back", "请求安卓板回传文件数据", "(0x36,0x33)",
            (d, _, _) => Send(d, () => McuOutbound.RequestFileBack())));
        lockMenu.Actions.Add(Act("lock", "sn-ack", "SN 接收确认", "(0x36,0x39)",
            (d, _, _) => Send(d, () => McuOutbound.AckSerialNumber())));
        lockMenu.Actions.Add(Act("lock", "notice", "无负载通知", "(0x36,0x37)",
            (d, a, _) => Send(d, () => McuOutbound.Notice(ByteArg(a, 0, 0x37)))));
        lockMenu.Actions.Add(Act("lock", "ack14", "状态／凭据应答 [tag][数据]", "(0x36,0x14)",
            (d, a, _) => Send(d, () => McuOutbound.StatusAck(ByteArg(a, 0, 0x00), PayloadArg(a, 1))), "[tag] [负载]"));
        lockMenu.Actions.Add(Act("lock", "ack16", "开关／弹窗状态应答 [tag][数据]", "(0x36,0x16)",
            (d, a, _) => Send(d, () => McuOutbound.SwitchStateAck(ByteArg(a, 0, 0x02), PayloadArg(a, 1))), "[tag] [负载]"));
        lockMenu.Actions.Add(Act("lock", "ack18", "二进制负载应答 [tag][数据]", "(0x36,0x18)",
            (d, a, _) => Send(d, () => McuOutbound.BinaryAck(ByteArg(a, 0, 0xFF), PayloadArg(a, 1))), "[tag] [负载]"));
        lockMenu.Actions.Add(Act("lock", "ack35", "状态应答（1 或 3 字节，取值 {0,1,2,3}）", "(0x36,0x35)",
            (d, a, _) => Send(d, () => McuOutbound.StateAck(a.Select((_, i) => ByteArg(a, i, 0)).ToArray())), "<v1> [v2 v3]"));

        // ---------------- 设备信息
        var info = categories[4];
        info.Actions.Add(Act("info", "show", "显示当前缓存的设备信息", "",
            (d, _, _) => Task.FromResult(RenderState(d)), "", new[] { "status" }, true));
        info.Actions.Add(Act("info", "board-name", "查询板卡名", "(0x34,0x0C)→(0x34,0x0D)",
            async (d, _, ct) => d.IsConnected ? await Query("板卡名", d.QueryBoardNameAsync(cancellationToken: ct)) : NotConnected));
        info.Actions.Add(Act("info", "sn", "请求 Android SN", "(0x2B,0x01)→(0x2B,0x02)",
            async (d, _, ct) => d.IsConnected ? await Query("Android SN", d.QueryAndroidSnAsync(cancellationToken: ct)) : NotConnected));
        info.Actions.Add(Act("info", "mac", "请求 Android MAC", "(0x2D,0x00)→(0x2D,0x01)",
            async (d, _, ct) => d.IsConnected ? await Query("Android MAC", d.QueryAndroidMacAsync(cancellationToken: ct)) : NotConnected));
        info.Actions.Add(Act("info", "machine", "查询机型字符串", "(0x50,0x00)→(0x50,0x01)",
            async (d, _, ct) => d.IsConnected ? await Query("机型", d.QueryMachineTypeAsync(cancellationToken: ct)) : NotConnected));
        info.Actions.Add(Act("info", "ip", "读取设备 IP（历史命令）", "(0x34,0x10)→(0x34,0x11)",
            async (d, _, ct) => d.IsConnected ? await Query("设备 IP", d.QueryIpAsync(cancellationToken: ct)) : NotConnected));
        info.Actions.Add(Act("info", "uid", "读取设备 UID（历史命令）", "(0x40,0x00)→(0xC1)",
            async (d, _, ct) => d.IsConnected ? await Query("设备 UID", d.QueryUidAsync(cancellationToken: ct)) : NotConnected));
        info.Actions.Add(Act("info", "version", "读取固件版本", "(0x16,0x02)→(0xC6,*)",
            async (d, _, ct) => d.IsConnected ? await Query("版本", d.QueryVersionAsync(cancellationToken: ct)) : NotConnected));
        info.Actions.Add(Act("info", "android-info", "仅发送 Android 详细信息请求（不等回包）", "(0x2B,0x01)",
            (d, _, _) => Send(d, () => McuOutbound.RequestAndroidInfo())));
        info.Actions.Add(Act("info", "base-info", "发送基础信息前段", "(0x01,0x00)",
            (d, _, _) => Send(d, () => McuOutbound.BaseInfo())));
        info.Actions.Add(Act("info", "all-channel", "请求全通道状态", "(0x28,0x00)",
            (d, _, _) => Send(d, () => McuOutbound.RequestAllChannel())));

        // ---------------- 系统与电源
        var system = categories[5];
        system.Actions.Add(Act("system", "handshake", "发送握手首包", "(0x50,0x00)",
            (d, _, _) => Send(d, () => McuOutbound.Handshake()), "", new[] { "hs0" }));
        system.Actions.Add(Act("system", "handshake-seq", "执行完整握手序列（12 步）", "",
            (d, _, _) =>
            {
                if (!d.IsConnected) return Task.FromResult(NotConnected);
                d.Handshake();
                return Task.FromResult("[OK] 握手序列已发送：(0x50,0) →(0x28,0) →(0x01,0) →(0x15,0x00)+时间 →(0x16,0..2) →(0x2B,0x01) →(0x2B,0x23) →(0x34,0x0C) →[1s]→(0x2D,0)");
            }, "", new[] { "hs" }));
        system.Actions.Add(Act("system", "time", "把 PC 当前时间同步给安卓板", "(0x15,0x00) f[3]=7",
            (d, _, _) =>
            {
                if (!d.IsConnected) return Task.FromResult(NotConnected);
                var now = DateTime.Now;
                bool ok = d.Send(McuOutbound.SyncTime(now));
                return Task.FromResult($"{(ok ? "[OK]" : "[ERR]")} 已发送 {now:yyyy-MM-dd HH:mm:ss}；"
                    + "f[5..11]=年高 年低 月 日 时 分 秒（SUB=0x00，0x07 是负载长度，协议不含时区）");
            }, "", new[] { "synctime" }));
        system.Actions.Add(Act("system", "poweroff", "通知安卓板关机", "(0x01,0x01)",
            (d, _, _) => Send(d, () => McuOutbound.PowerOffRequest(), "  （安卓板关机通知）")));
        system.Actions.Add(Act("system", "av-mode", "音视频模式声明 A/B/C", "(0x16,0/1/2)",
            (d, a, _) => Send(d, () => McuOutbound.AvMode(ByteArg(a, 0, 0))), "<0|1|2>"));
        system.Actions.Add(Act("system", "wol", "WOL 广播，让安卓板唤醒 OPS", "(0x34,0x12)",
            (d, _, _) => Send(d, () => McuOutbound.WolBroadcast())));
        system.Actions.Add(Act("system", "wol-register", "注册 WOL 服务器名（定长 25 字节）", "(0x2B,0x23)",
            (d, a, _) => Send(d, () => McuOutbound.RegisterWolServerName(TextArg(a, 0, McuOutbound.WolServerName))), "[名称]"));
        system.Actions.Add(Act("system", "network-menu", "让安卓板打开网络设置菜单", "(0x34,0x0A)",
            (d, _, _) => Send(d, () => McuOutbound.OpenNetworkMenu(), "  [f[3]=0x01 f[5]=0x01]")));
        system.Actions.Add(Act("system", "cable", "通知线屏已插入", "(0x32,0x00)",
            (d, _, _) => Send(d, () => McuOutbound.NotifyCableScreenPlugIn())));

        // ---------------- 外设与工具
        var device = categories[6];
        device.Actions.Add(Act("device", "pen", "触控笔开关", "(0x2A,0x00)",
            (d, a, _) =>
            {
                bool on = IsOn(TextArg(a, 0, "on"));
                return Send(d, () => McuOutbound.SetPen(on), on ? "  [启用]" : "  [禁用]");
            }, "<on|off>", new[] { "stylus" }));
        device.Actions.Add(Act("device", "usb-unlock", "通知安卓板「设备已被 U 盘解锁」", "(0x31,0x00)",
            (d, _, _) => Send(d, () => McuOutbound.NotifyUnlockedByUsbKey(),
                "  [!] 语义是「已用 U 盘解锁设备」，协议中不存在解锁 U 盘本身的命令")));
        device.Actions.Add(Act("device", "raw", "发送任意帧（自定义命令）", "",
            (d, a, _) =>
            {
                if (a.Length < 2) return Task.FromResult("用法: raw <main> <sub> [负载]，main/sub 支持 0x 前缀");
                return Send(d, () => McuOutbound.WithPayload(ByteArg(a, 0, 0), ByteArg(a, 1, 0), PayloadArg(a, 2)));
            }, "<main> <sub> [负载]", new[] { "send" }));

        // ---------------- 事件与报文
        var log = categories[7];
        log.Actions.Add(Act("log", "show", "打印最近的上下行报文", "",
            (d, a, _) =>
            {
                int take = Math.Max(IntArg(a, 0, 20), 1);
                var entries = d.Log;
                var slice = entries.Skip(Math.Max(0, entries.Count - take)).ToArray();
                return Task.FromResult(slice.Length == 0 ? "（暂无报文）" : string.Join(Environment.NewLine, slice.Select(RenderEntry)));
            }, "[条数]", new[] { "tail" }, true));
        log.Actions.Add(Act("log", "clear", "清空报文日志", "",
            (d, _, _) => { d.ClearLog(); return Task.FromResult("[OK] 已清空"); }, "", Array.Empty<string>(), true));
        log.Actions.Add(Act("log", "events", "打印 MCU→PC 事件字典", "",
            (_, _, _) => Task.FromResult(EventTable()), "", new[] { "table" }, true));

        // ---------------- 连接与链路
        var link = categories[8];
        link.Actions.Add(Act("link", "devices", "枚举检测到的 MCU 设备", "",
            (_, _, _) =>
            {
                var devices = McuHunter.FindAll();
                if (devices.Count == 0) return Task.FromResult("未找到任何 MCU 设备");
                var text = string.Join(Environment.NewLine, devices.Select((d, i) =>
                    $"  [{i + 1}] VID:0x{d.Vid:X4} PID:0x{d.Pid:X4} "
                    + $"{Ui.TextWidth.Pad(McuKnownDevices.Describe(McuKnownDevices.Detect(d)), 40)} {d.DeviceName}"));
                return Task.FromResult($"找到 {devices.Count} 个 MCU 设备:\n{text}");
            }, "", new[] { "list" }, true));
        link.Actions.Add(Act("link", "frame-mode", "查看/切换帧封装模式", "",
            (d, a, _) =>
            {
                if (a.Length > 0)
                {
                    d.FrameMode = a[0].ToLowerInvariant() switch
                    {
                        "auto" => FrameMode.Auto,
                        "raw" or "a" or "modea" => FrameMode.Raw,
                        "framed" or "b" or "modeb" => FrameMode.Framed,
                        _ => throw new ArgumentException("用法: frame-mode [auto|raw|framed]"),
                    };
                }
                string confirmed = d.Link.ConfirmedFrameMode is { } m ? McuDevice.DescribeMode(m) : "未确认";
                return Task.FromResult($"当前帧模式: {McuDevice.DescribeMode(d.FrameMode)}（上行确认: {confirmed}）");
            }, "[auto|raw|framed]", new[] { "mode" }, true));
        link.Actions.Add(Act("link", "auto-reply", "开关上行自动应答（对时/口令/SN 确认/回传文件/U 盘弹窗）", "",
            (d, a, _) =>
            {
                if (a.Length > 0) d.AutoReply = IsOn(a[0]);
                return Task.FromResult($"自动应答: {(d.AutoReply ? "开" : "关")}");
            }, "[on|off]", Array.Empty<string>(), true));
        link.Actions.Add(Act("link", "inter-frame", "设置每帧发送前的节流延时（默认 50ms）", "",
            (d, a, _) =>
            {
                if (a.Length > 0) d.Link.InterFrameDelayMs = Math.Clamp(IntArg(a, 0, 50), 0, 5000);
                return Task.FromResult($"每帧节流延时: {d.Link.InterFrameDelayMs} ms");
            }, "[毫秒]", new[] { "delay" }, true));
        link.Actions.Add(Act("link", "disconnect", "断开连接", "",
            (d, _, _) => { d.Disconnect(); return Task.FromResult("[OK] 已断开"); }, "", Array.Empty<string>(), true));
        link.Actions.Add(Act("link", "reconnect", "断开后重新连接并重新握手", "",
            async (d, _, ct) =>
            {
                d.Disconnect();
                if (!d.Connect()) return "[ERR] 重新连接失败";
                await d.ProbeFrameModeAsync(ct).ConfigureAwait(false);
                d.Handshake();
                return "[OK] 已重新连接并完成握手";
            }, "", Array.Empty<string>(), true));

        return categories;
    }

    private static string ModeTable(string title, string[] rows)
    {
        var lines = new List<string> { $"  {title}模式映射（SUB = enum + 3）:", "  enum  SUB   名称" };
        for (int i = 0; i < rows.Length; i++)
        {
            bool dropped = i == rows.Length - 1 && rows[i].StartsWith("CUSTOM", StringComparison.Ordinal);
            lines.Add($"  {i}     0x0{i + 3:X1}  {rows[i]}{(dropped ? "   [ERR] 不下发（PC 侧 if (mode > 3) return）" : string.Empty)}");
        }
        return string.Join(Environment.NewLine, lines);
    }

    public static string RenderEntry(McuLogEntry entry) =>
        $"{entry.Timestamp:HH:mm:ss.fff} {entry.Direction} {entry.Text}";

    public static string RenderState(McuDevice? device)
    {
        var sb = new System.Text.StringBuilder();
        void Line(string label, object? value) => sb.AppendLine("  " + Ui.TextWidth.Pad(label, 16) + value);

        sb.AppendLine("==============================================");
        sb.AppendLine("              MCU 设备信息");
        sb.AppendLine("==============================================");
        var state = device?.State;
        if (state is null)
        {
            sb.AppendLine("  （未连接）");
            return sb.ToString();
        }
        Line("机型(USB)", McuKnownDevices.Describe(state.MachineKind));
        Line("板卡名", state.BoardName ?? "未知");
        Line("机型", state.MachineType ?? "未知");
        Line("Android SN", state.AndroidSn ?? "未知");
        Line("Android MAC", state.AndroidMac ?? "未知");
        Line("设备 IP", state.Ip ?? "未知");
        Line("设备 UID", state.Uid ?? "未知");
        Line("Android 版本", state.AndroidVersion ?? "未知");
        Line("MCU 版本", state.McuVersion ?? "未知");
        Line("触摸固件", state.TouchVersion ?? "未知");
        Line("触摸屏尺寸", state.TouchSizeInch > 0 ? state.TouchSizeInch + " 英寸" : "未知");
        Line("支持全通道", state.SupportsAllChannel ? "是" : "否");
        Line("当前通道", state.CurrentChannel is { } ch ? McuCommandNames.DescribeChannel(ch) : "未知");
        Line("子通道索引", state.CurrentSubChannel?.ToString() ?? "未知");
        Line("最近对时", state.LastTimeSync?.ToString("yyyy-MM-dd HH:mm:ss") ?? "未同步");
        Line("最后拔出U盘", state.LastUsbDrive ?? "无");
        if (device!.UsbId is { } id)
        {
            Line("VID:PID", $"0x{id.Vid:X4}:0x{id.Pid:X4}");
            Line("设备路径", id.DeviceName);
        }
        Line("帧模式", McuDevice.DescribeMode(device.FrameMode));
        sb.AppendLine("==============================================");
        return sb.ToString();
    }

    private static string EventTable() => string.Join(Environment.NewLine, new[]
    {
        "  MAIN  名称                        处理要点",
        "  0x1E  打开会议或浏览器              启动会议或浏览器",
        "  0x1F  打开 EasiMeeting             同 0x80",
        "  0x28  全通道消息                   sub==5 → 支持全通道",
        "  0x29  U 盘锁动作                   sub 0→timer6 / 1→timer5（语义未命名）",
        "  0x2B  Android 信息                 sub 0x02=SN，sub 0x24=系统属性（key 定长 5 字节）",
        "  0x2D  Android MAC                  触发网络重连",
        "  0x34  安卓/全局                    0x0B 菜单状态 / 0x0D 板卡名 / 0x11 IP / 0x13 WOL 回执 / 0x14 通知",
        "  0x36  屏幕锁组                      0x19/0x40 U 盘动作、0x30 口令、0x32 文件、0x34 账号、0x36 二维码、0x38 SN",
        "  0x3D  应用状态通知                  sub<=1 有效",
        "  0x50  机型信息                      sub 0x01 机型串",
        "  0x80  打开 EasiMeeting",
        "  0x82  让 PC 待机",
        "  0x83  电视静音状态",
        "  0x84  PPT 切到屏幕同分辨率          无负载；收到后 PC 调整幻灯片页面比例",
        "  0x86  撤销／注销",
        "  0xA2  当前通道状态",
        "  0xAB  当前 TV 状态                  f[8]=通道号，f[9]=子通道索引",
        "  0xC0  请求 PC 时间                  → 回 (0x15,0x00)+7 字节时间",
        "  0xC1  设备 UID                      本项目历史命令",
        "  0xC4  最小化所有窗口",
        "  0xC5  触摸状态",
        "  0xC6  版本                          0=Android 1=MCU 2=触摸固件",
        "  0xC8  输入设备状态",
        "  0xD1  USB 切到 PC",
        "",
        "  注：0x2C（时间基准）无有效负载，收到后仅作记录。"
        + "22 条 MAIN 已按 byte+dword 双表逐项核对。",
    });
}
