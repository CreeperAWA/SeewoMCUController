using System.Collections.Concurrent;
using SeewoMCUController.Commands;
using SeewoMCUController.Mcu;
using SeewoMCUController.Mcu.Protocol;
using SeewoMCUController.Ui;
using FrameMode = SeewoMCUController.Mcu.Protocol.FrameMode;

namespace SeewoMCUController.Cli;

/// <summary>命令行参数（不可变；<c>WithHoldSeconds</c> 用于派生驻留时限不同的副本）。</summary>
public sealed record CliOptions
{
    public string? DevicePath { get; private set; }
    public FrameMode? FrameModeOption { get; private set; }
    public bool NoHandshake { get; private set; }
    public bool NoAutoReply { get; private set; }
    public bool NoConnect { get; private set; }
    public bool DryRun { get; private set; }
    public bool Selftest { get; private set; }
    public bool Help { get; private set; }
    public bool ListCategories { get; private set; }
    public string? ListCommands { get; private set; }
    public bool ListDevices { get; private set; }
    public int? QueryTimeoutMs { get; private set; }

    /// <summary>可选状态快照：<c>HoldLoopAsync</c> 需要在不加时限的情况下复用同一份选项。</summary>
    internal CliOptions WithHoldSeconds(int seconds) => this with { HoldSeconds = seconds };

    /// <summary>命令执行完后保持进程驻留，以便继续校验上行请求。</summary>
    public bool Hold { get; private set; }

    /// <summary>
    /// 驻留时限（秒），<c>0</c> 表示不限时。
    /// <para>
    /// 锁屏类动作默认不限时：屏幕还锁着就不该自己退出。
    /// 用户显式传入 <c>--hold-seconds</c> 时才启用时限。
    /// </para>
    /// </summary>
    public int HoldSeconds { get; private set; }

    /// <summary>用户是否显式指定了驻留时限。</summary>
    public bool HoldSecondsExplicit { get; private set; }

    /// <summary>本次进程内使用的锁屏口令。设置后隐含 <see cref="Hold"/>。</summary>
    public string? Password { get; private set; }

    /// <summary>驻留期间把上行事件逐条打印到标准输出。</summary>
    public bool Verbose { get; private set; }

    /// <summary>以「独立日志窗口进程」模式启动（TUI 会自动拉起，一般无需手动使用）。</summary>
    public bool LogWindow { get; private set; }

    public List<string> Rest { get; } = new();

    public static bool TryParse(string[] args, out CliOptions options, out string? error)
    {
        options = new CliOptions();
        error = null;
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            string lower = arg.ToLowerInvariant();
            switch (lower)
            {
                case "-h":
                case "--help":
                case "-help":
                case "/?":
                case "help":
                    options.Help = true;
                    break;
                case "-d":
                case "--device-path":
                case "--device":
                    if (++i >= args.Length) return Fail("--device-path 需要指定设备路径", out options, out error);
                    options.DevicePath = args[i];
                    break;
                case "--frame":
                case "--frame-mode":
                    if (++i >= args.Length || !TryParseFrameMode(args[i], out var fm))
                        return Fail("--frame 需要 auto|raw|framed", out options, out error);
                    options.FrameModeOption = fm;
                    break;
                case "--no-handshake":
                    options.NoHandshake = true;
                    break;
                case "--no-auto-reply":
                    options.NoAutoReply = true;
                    break;
                case "--no-connect":
                    options.NoConnect = true;
                    break;
                case "--dry-run":
                    options.DryRun = true;
                    break;
                case "--selftest":
                    options.Selftest = true;
                    break;
                case "--log-window":
                    options.LogWindow = true;
                    break;
                case "--categories":
                    options.ListCategories = true;
                    break;
                case "--devices":
                case "--list":
                    options.ListDevices = true;
                    break;
                case "--commands":
                    options.ListCommands = i + 1 < args.Length ? args[++i] : string.Empty;
                    break;
                case "--timeout":
                    if (++i >= args.Length || !int.TryParse(args[i], out int ms))
                        return Fail("--timeout 需要毫秒数", out options, out error);
                    options.QueryTimeoutMs = ms;
                    break;
                case "--hold":
                case "--wait-unlock":
                    options.Hold = true;
                    break;
                case "--hold-seconds":
                    if (++i >= args.Length || !int.TryParse(args[i], out int hs) || hs < 0)
                        return Fail("--hold-seconds 需要非负秒数", out options, out error);
                    options.HoldSeconds = hs;
                    options.HoldSecondsExplicit = true;
                    options.Hold = true;
                    break;
                case "--password":
                case "--pwd":
                    // 口令只存在于本进程内存，所以设置口令必须与锁屏在同一次调用里完成，
                    // 并自动驻留等待校验；否则进程一退口令就没了。
                    if (++i >= args.Length) return Fail("--password 需要口令", out options, out error);
                    options.Password = args[i];
                    options.Hold = true;
                    options.Verbose = true;
                    break;
                case "--verbose":
                case "-v":
                    options.Verbose = true;
                    break;
                default:
                    if (lower.StartsWith("--device-path=", StringComparison.Ordinal))
                        options.DevicePath = arg["--device-path=".Length..];
                    else if (lower.StartsWith("--device=", StringComparison.Ordinal))
                        options.DevicePath = arg["--device=".Length..];
                    else if (lower.StartsWith("--frame=", StringComparison.Ordinal))
                    {
                        if (!TryParseFrameMode(arg["--frame=".Length..], out var fm2))
                            return Fail("--frame 需要 auto|raw|framed", out options, out error);
                        options.FrameModeOption = fm2;
                    }
                    else if (lower.StartsWith("--timeout=", StringComparison.Ordinal))
                    {
                        if (!int.TryParse(arg["--timeout=".Length..], out int t))
                            return Fail("--timeout 需要毫秒数", out options, out error);
                        options.QueryTimeoutMs = t;
                    }
                    else if (lower.StartsWith("--hold-seconds=", StringComparison.Ordinal))
                    {
                        if (!int.TryParse(arg["--hold-seconds=".Length..], out int hv) || hv < 0)
                            return Fail("--hold-seconds 需要非负秒数", out options, out error);
                        options.HoldSeconds = hv;
                        options.HoldSecondsExplicit = true;
                        options.Hold = true;
                    }
                    else if (lower.StartsWith("--password=", StringComparison.Ordinal))
                    {
                        options.Password = arg["--password=".Length..];
                        options.Hold = true;
                    }
                    else if (lower is "categories" or "commands" or "devices" or "selftest")
                    {
                        // 允许 `SeewoMCUController categories` 这种不带 -- 的写法
                        if (lower == "commands" && i + 1 < args.Length && !args[i + 1].StartsWith('-')) options.ListCommands = args[++i];
                        else if (lower == "categories") options.ListCategories = true;
                        else if (lower == "devices") options.ListDevices = true;
                        else options.Selftest = true;
                    }
                    else if (arg.StartsWith('-'))
                    {
                        return Fail($"未知选项 {arg}", out options, out error);
                    }
                    else
                    {
                        options.Rest.Add(arg);
                    }
                    break;
            }
        }
        return true;
    }

    private static bool TryParseFrameMode(string text, out FrameMode mode)
    {
        switch (text.ToLowerInvariant())
        {
            case "auto": mode = FrameMode.Auto; return true;
            case "raw":
            case "a": mode = FrameMode.Raw; return true;
            case "framed":
            case "b": mode = FrameMode.Framed; return true;
            default: mode = FrameMode.Framed; return false;
        }
    }

    private static bool Fail(string message, out CliOptions options, out string? error)
    {
        options = new CliOptions();
        error = message;
        return false;
    }

    public static void PrintUsage()
    {
        Console.WriteLine("Seewo MCU Controller —— 希沃 OPS <-> 安卓板 MCU 控制器");
        Console.WriteLine();
        Console.WriteLine("用法:");
        Console.WriteLine("  SeewoMCUController                                   进入交互式 TUI");
        Console.WriteLine("  SeewoMCUController <分类> <动作> [参数...]          执行单条命令后退出");
        Console.WriteLine("  SeewoMCUController <动作> [参数...]                省略分类（动作名唯一时可用）");
        Console.WriteLine();
        Console.WriteLine("选项:");
        Console.WriteLine("  -d, --device-path <路径>   指定 HID 设备路径");
        Console.WriteLine("      --frame <模式>         auto(默认) | raw(无头) | framed(带头 FC A5 05 3B 00)");
        Console.WriteLine("      --no-handshake         连接后不执行 12 步握手序列");
        Console.WriteLine("      --no-auto-reply        关闭上行请求的自动应答（不影响锁屏口令校验）");
        Console.WriteLine("      --no-connect           不连接设备（仅本地命令）");
        Console.WriteLine("      --timeout <毫秒>       查询类命令的回包等待时间（默认 2000）");
        Console.WriteLine("      --hold                命令执行完后驻留，持续应答上行请求（口令校验等）");
        Console.WriteLine("      --hold-seconds <秒>   驻留时限；锁屏类动作默认不限时，给 0 也是不限时");
        Console.WriteLine("      --password <口令>     本次进程内设置锁屏口令（隐含 --hold --verbose）");
        Console.WriteLine();
        Console.WriteLine("  注：lock lock / lock set-password 本身就要求驻留，无论是否加 --hold，");
        Console.WriteLine("      命令执行完后都不会退出，直到解锁为止；驻留中可继续输入命令。");
        Console.WriteLine("      驻留提示符下 exit 仅在屏幕未锁时可用。--no-auto-reply 不影响口令校验。");
        Console.WriteLine("  -v, --verbose             把上行事件逐条打印到标准输出");
        Console.WriteLine("      --dry-run              只回显命令，不连接设备");
        Console.WriteLine("      --selftest             打印并校验全部命令的构造帧");
        Console.WriteLine("      --log-window           以独立日志窗口进程启动（由 TUI 自动拉起，关闭任一窗口即退出全部）");
        Console.WriteLine("      --categories           列出全部分类");
        Console.WriteLine("      --commands [分类]      列出分类下的动作");
        Console.WriteLine("      --devices              枚举检测到的 MCU 设备");
        Console.WriteLine("  -h, --help                 显示本帮助");
        Console.WriteLine();
        Console.WriteLine("示例:");
        Console.WriteLine("  SeewoMCUController audio volume 60");
        Console.WriteLine("  SeewoMCUController channel switch hdmi 1");
        Console.WriteLine("  SeewoMCUController lock lock");
        Console.WriteLine("  SeewoMCUController lock lock --password 'Passw0rd'   # 设口令+锁屏+驻留等解锁");
        Console.WriteLine("  SeewoMCUController lock lock --hold                  # 不设口令，锁屏后驻留等解锁");
        Console.WriteLine("  SeewoMCUController info board-name --frame raw -d \"<设备路径>\"");
        Console.WriteLine("  SeewoMCUController --selftest");
        Console.WriteLine();
        Console.WriteLine("分类一览:");
        foreach (var c in McuActionRegistry.Categories)
            Console.WriteLine($"  {Ui.TextWidth.Pad(c.Key, 10)}{Ui.TextWidth.Pad(c.Title, 12)}{c.Hint}");
    }
}

/// <summary>命令行执行器。</summary>
public static class CliRunner
{
    public static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        if (!CliOptions.TryParse(args, out var options, out var error))
        {
            Console.Error.WriteLine($"错误: {error}");
            return 2;
        }

        if (options.Help)
        {
            CliOptions.PrintUsage();
            return 0;
        }
        if (options.LogWindow) return LogWindowCli.Run();
        if (options.Selftest) return SelfTest.Run();
        if (options.ListCategories) return ListCategories();
        if (options.ListCommands is not null) return ListCommands(options.ListCommands);
        if (options.ListDevices) return ListDevices();
        if (options.Rest.Count == 0) return await RunTuiAsync(options, cancellationToken).ConfigureAwait(false);
        return await RunSingleAsync(options, cancellationToken).ConfigureAwait(false);
    }

    private static int ListCategories()
    {
        foreach (var c in McuActionRegistry.Categories)
            Console.WriteLine($"{c.Key,-9}{c.Title,-10}{c.Actions.Count,3} 个动作   {c.Hint}");
        return 0;
    }

    private static int ListCommands(string categoryKey)
    {
        var categories = string.IsNullOrEmpty(categoryKey)
            ? McuActionRegistry.Categories
            : new[] { McuActionRegistry.FindCategory(categoryKey) }.Where(c => c is not null).Cast<Commands.McuCategory>().ToList();
        if (categories.Count == 0)
        {
            Console.Error.WriteLine($"错误: 未知分类 '{categoryKey}'");
            return 2;
        }
        foreach (var c in categories)
        {
            Console.WriteLine($"[{c.Key}] {c.Title}");
            foreach (var a in c.Actions)
            {
                string usage = string.IsNullOrEmpty(a.Usage) ? "" : "  " + a.Usage;
                string frame = string.IsNullOrEmpty(a.Frame) ? "" : "  " + a.Frame;
                Console.WriteLine($"  {a.Name,-15}{a.Description}{usage}{frame}");
            }
        }
        return 0;
    }

    private static int ListDevices()
    {
        var devices = Cvte.Mcu.McuHunter.FindAll();
        if (devices.Count == 0)
        {
            Console.WriteLine("未找到任何 MCU 设备");
            return 1;
        }
        Console.WriteLine($"找到 {devices.Count} 个 MCU 设备:");
        for (int i = 0; i < devices.Count; i++)
            Console.WriteLine($"  [{i + 1}] VID:0x{devices[i].Vid:X4} PID:0x{devices[i].Pid:X4} "
                + $"{Ui.TextWidth.Pad(McuKnownDevices.Describe(McuKnownDevices.Detect(devices[i])), 40)} {devices[i].DeviceName}");
        return 0;
    }

    private static async Task<int> RunTuiAsync(CliOptions options, CancellationToken cancellationToken)
    {
        using var device = CreateDevice(options);
        await device.ConnectAndHandshakeAsync(!options.NoHandshake, cancellationToken).ConfigureAwait(false);

        using var menu = new ConsoleMenu(device);
        await menu.RunAsync(cancellationToken).ConfigureAwait(false);
        device.Disconnect();
        return 0;
    }

    private static async Task<int> RunSingleAsync(CliOptions options, CancellationToken cancellationToken)
    {
        string head = options.Rest[0];
        McuAction? action = null;
        string[] args;

        if (McuActionRegistry.FindCategory(head) is { } category && options.Rest.Count >= 2)
        {
            action = McuActionRegistry.Categories
                .FirstOrDefault(c => string.Equals(c.Key, category.Key, StringComparison.OrdinalIgnoreCase))!
                .Actions.FirstOrDefault(a => a.Matches(options.Rest[1]));
            args = options.Rest.Skip(2).ToArray();
        }
        else
        {
            action = McuActionRegistry.SearchOther(head);
            args = options.Rest.Skip(1).ToArray();
        }

        if (action is null)
        {
            Console.Error.WriteLine($"错误: 未知命令 '{string.Join(' ', options.Rest)}'");
            Console.Error.WriteLine("提示: 第一段是分类时需要再跟一个动作名；用 --categories / --commands 查看可用项。");
            return 2;
        }

        if (options.DryRun)
        {
            // 只回显命令、不连设备，但仍执行一次以校验参数（参数非法时按退出码 2 报错）
            using var probe = CreateDevice(options);
            try
            {
                await action.ExecuteAsync(probe, args, cancellationToken).ConfigureAwait(false);
            }
            catch (ArgumentException ex)
            {
                Console.Error.WriteLine($"错误: {ex.Message}");
                return 2;
            }
            catch (Exception)
            {
                // 校验路径上的其它异常（多为「尚未连接设备」）在此忽略
            }
            Console.WriteLine($"[dry-run] {action.Category} {action.Name} {string.Join(' ', args)}");
            return 0;
        }

        using var device = CreateDevice(options);
        if (options.Verbose)
            device.Inbound += evt => Console.WriteLine($"  ← {DateTime.Now:HH:mm:ss} {evt}");

        // 口令只存在于本进程内存，所以必须在锁屏之前设好，且设了就要驻留。
        // 放在连接之前校验，参数写错时不必等设备枚举完。
        if (options.Password is { } pwd)
        {
            try
            {
                device.LockPassword.Set(pwd);
            }
            catch (ArgumentException ex)
            {
                Console.Error.WriteLine($"错误: {ex.Message}");
                return 2;
            }
            Console.WriteLine($"[口令] 已设置（{device.LockPassword.Length} 位）并进入驻留等待校验。");
            foreach (var risk in LockPasswordStore.DescribeRisks(pwd))
                Console.WriteLine($"[口令] {risk}");
        }

        if (!action.Offline && !await device.ConnectAndHandshakeAsync(!options.NoHandshake, cancellationToken).ConfigureAwait(false))
            return 1;

        try
        {
            var result = await action.ExecuteAsync(device, args, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(result)) Console.WriteLine(result);

            // set-password 本身是纯本地动作，单次调用退出后口令随即丢失、什么也保护不了。
            // 命令行里给它补一帧锁屏，让「设口令 + 锁屏」成为一次完整操作。
            if (action.ChainLockScreen && result.StartsWith("[OK]", StringComparison.Ordinal))
            {
                if (device.IsConnected)
                {
                    Console.WriteLine();
                    Console.WriteLine(SendLockTracked(device, LockScreenNote(device)));
                }
                else
                {
                    Console.WriteLine("[ERR] 未连接设备，已设置口令但没有锁屏；口令仅在本进程有效。");
                }
            }

            // 锁屏 / 设置口令这类动作必须驻留：安卓板是在收到 (0x36,0x10) 之后才弹锁屏页的，
            // 用户输完口令才会上报 (0x36,0x30)。进程一退就没人应答，屏幕再也解不开。
            // 所以动作自己声明 MustHold 时忽略 --hold 的缺省，直接驻留。
            if ((options.Hold || action.MustHold) && result.StartsWith("[OK]", StringComparison.Ordinal))
            {
                // 锁屏类动作默认不限时：屏幕还锁着就不该自己退出。
                // 只有用户显式给了 --hold-seconds 才启用时限。
                var opts = action.MustHold && !options.HoldSecondsExplicit
                    ? options.WithHoldSeconds(0)
                    : options;
                await HoldLoopAsync(device, opts, cancellationToken).ConfigureAwait(false);
            }

            return result.StartsWith("[ERR]", StringComparison.Ordinal) ? 1 : 0;
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"错误: {ex.Message}");
            return 2;
        }
        finally
        {
            if (device.IsConnected) device.Disconnect();
        }
    }

    /// <summary>
    /// 驻留等待解锁：持续应答上行请求（口令校验、SN 确认、文件回传…），
    /// 直到收到解锁时机（口令校验通过）或超时。
    /// <para>
    /// 提前退出：收到口令校验通过并发出 <c>(0x36,0x12)</c>，或 Ctrl+C，或到达时限。
    /// </para>
    /// </summary>
    /// <summary>等到解锁、到达时限或被取消为止，返回 true 表示是解锁导致的结束。</summary>
    internal static async Task<bool> WaitForUnlockAsync(
        McuDevice device,
        int holdSeconds,
        CancellationToken cancellationToken)
    {
        bool unlocked = false;
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnUnlocked()
        {
            unlocked = true;
            done.TrySetResult();
        }

        device.ScreenUnlocked += OnUnlocked;
        // holdSeconds == 0 表示不限时：不设定时器，只等解锁或 Ctrl+C。
        Timer? timer = holdSeconds > 0
            ? new Timer(_ => done.TrySetResult(), null, holdSeconds * 1000L, Timeout.Infinite)
            : null;
        using var cancel = cancellationToken.Register(() => done.TrySetResult());
        try
        {
            await done.Task.ConfigureAwait(false);
        }
        finally
        {
            timer?.Dispose();
            device.ScreenUnlocked -= OnUnlocked;
        }
        return unlocked && !cancellationToken.IsCancellationRequested;
    }

    /// <summary>
    /// 驻留循环：持续应答上行请求，同时接受命令行输入。
    /// <list type="bullet">
    /// <item>口令校验通过并发出 <c>(0x36,0x12)</c> → 自动结束（屏幕已解锁）。</item>
    /// <item>输入 <c>unlock</c> / <c>lock unlock</c> → 解锁后结束。</item>
    /// <item>输入 <c>exit</c> → 仅在屏幕<b>未</b>锁着时允许；锁着时会被拒绝。</item>
    /// <item>Ctrl+C → 强制结束（不做拦截，属于操作系统级中断）。</item>
    /// </list>
    /// </summary>
    /// <summary>是否为驻留模式下的退出指令（<c>exit</c> / <c>quit</c> / <c>q</c>）。</summary>
    internal static bool IsExitCommand(string line) =>
        line.ToLowerInvariant() is "exit" or "quit" or "q";

    /// <summary>自检路径下，脚本用尽后继续等待解锁信号的上限。</summary>
    private const int ResidentProbeGraceMs = 250;

    /// <summary>
    /// 驻留模式下能否退出。屏幕还锁着时必须拒绝：此时进程一退，
    /// 安卓板就再也等不到 <c>(0x36,0x31)</c> 应答，屏幕会永远锁着。
    /// </summary>
    internal static bool CanExitResident(McuDevice device) => !device.ScreenLocked;

    private static Task HoldLoopAsync(McuDevice device, CliOptions options, CancellationToken cancellationToken)
        => HoldLoopAsync(device, (IReadOnlyList<string>?)null, cancellationToken, options);

    private static async Task<bool> HoldLoopAsync(
        McuDevice device,
        IReadOnlyList<string>? script,
        CancellationToken cancellationToken,
        CliOptions? options = null)
    {
        var opts = options ?? new CliOptions();
        Console.WriteLine();
        Console.WriteLine($"[驻留] 进程保持运行以应答口令，直到解锁为止（时限：{DescribeHold(opts.HoldSeconds)}）。");
        Console.WriteLine(device.HasLockPassword
            ? $"[驻留] 口令已启用（{device.LockPassword.Length} 位）：在 Android 锁屏页输入口令即可解锁。"
            : "[驻留] 尚未设置口令：Android 端输入任何口令都会被判定为正确并解锁。");
        if (!device.AutoReply)
            Console.WriteLine("[驻留] --no-auto-reply 已关闭对时 / SN 确认 / 文件回传 / U 盘弹窗的自动应答；"
                + "锁屏口令校验不受影响，仍会正常应答。");
        Console.WriteLine("[驻留] 可继续输入命令：unlock 解锁并退出 · exit 退出（锁屏时不可用）· Ctrl+C 强制结束");
        Console.WriteLine();

        var unlocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnUnlocked() => unlocked.TrySetResult();
        device.ScreenUnlocked += OnUnlocked;

        // holdSeconds <= 0 表示不限时：不设定时器。
        Timer? timer = opts.HoldSeconds > 0
            ? new Timer(_ => unlocked.TrySetResult(), null, opts.HoldSeconds * 1000L, Timeout.Infinite)
            : null;
        using var cancel = cancellationToken.Register(() => unlocked.TrySetResult());
        using var reader = script is null ? new ResidentReader() : null;
        var queue = new Queue<string>(script ?? Array.Empty<string>());

        try
        {
            while (!unlocked.Task.IsCompleted)
            {
                string? line;
                if (script is not null)
                {
                    // 自检路径：脚本用尽即视为输入结束（EOF）。
                    if (queue.Count == 0) break;
                    line = queue.Dequeue();
                }
                else
                {
                    Console.Write("驻留> ");
                    Console.Out.Flush();
                    line = await reader!.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                    if (line is null) break; // 输入流结束（EOF）
                }

                line = line.Trim();
                if (line.Length == 0) continue;

                if (IsExitCommand(line))
                {
                    if (!CanExitResident(device))
                    {
                        // 锁着就退出 = 安卓板永远等不到 (0x36,0x31)，屏幕再也解不开。
                        Console.WriteLine("[ERR] 屏幕仍处于锁屏状态，不能退出。");
                        Console.WriteLine("      请先输入 unlock 解锁，或在 Android 上输入口令解锁后再退出。");
                        continue;
                    }
                    Console.WriteLine("[驻留] 已退出。");
                    return true;
                }

                if (line is "help" or "?")
                {
                    Console.WriteLine("  unlock / lock unlock   解锁 Android 屏并结束驻留");
                    Console.WriteLine("  exit                  退出（屏幕锁着时不可用）");
                    Console.WriteLine("  <其它命令>            同命令行用法，例如 info show / lock pwd-status");
                    continue;
                }

                await RunResidentCommandAsync(device, line, cancellationToken).ConfigureAwait(false);

                // 解锁是结束驻留的信号：屏幕已经不需要应答了。
                if (!device.ScreenLocked)
                {
                    Console.WriteLine("[驻留] 屏幕已解锁，结束驻留。");
                    return true;
                }
            }

            // 自检路径：脚本用尽后不再读输入，但仍在等解锁信号 / 取消，给一个短的上限，
            // 这样「exit 被拦截 → 循环没退出」与「解锁信号 → 循环结束」都能被稳定断言。
            if (script is not null)
            {
                await Task.WhenAny(
                    unlocked.Task,
                    Task.Delay(ResidentProbeGraceMs, cancellationToken)).ConfigureAwait(false);
                return !cancellationToken.IsCancellationRequested && unlocked.Task.IsCompleted;
            }

            if (!cancellationToken.IsCancellationRequested) await unlocked.Task.ConfigureAwait(false);
        }
        finally
        {
            timer?.Dispose();
            device.ScreenUnlocked -= OnUnlocked;
        }

        Console.WriteLine();
        if (cancellationToken.IsCancellationRequested) Console.WriteLine("[驻留] 已取消。");
        else if (!device.ScreenLocked) Console.WriteLine("[驻留] 已解锁，结束驻留。");
        else Console.WriteLine($"[驻留] 等待时限（{DescribeHold(opts.HoldSeconds)}）已到，结束驻留。");
        return true;
    }

    /// <summary>
    /// 把驻留循环的命令输入抽成可注入的序列，便于自检覆盖 <c>exit</c> 拦截等分支。
    /// 传 <c>null</c> 表示走真实 stdin。
    /// </summary>
    internal static async Task<bool> RunResidentScriptAsync(
        McuDevice device,
        IReadOnlyList<string>? script,
        CancellationToken cancellationToken)
    {
        return await HoldLoopAsync(device, script, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>驻留期间执行一条命令，复用与单次 CLI 完全相同的解析与执行路径。</summary>
    private static async Task RunResidentCommandAsync(McuDevice device, string line, CancellationToken cancellationToken)
    {
        var rest = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (rest.Length == 0) return;

        McuAction? action;
        string[] args;
        if (rest.Length >= 2 && McuActionRegistry.FindCategory(rest[0]) is { } cat)
        {
            action = cat.Actions.FirstOrDefault(a => a.Matches(rest[1]));
            args = rest.Skip(2).ToArray();
        }
        else
        {
            action = McuActionRegistry.SearchOther(rest[0]);
            args = rest.Skip(1).ToArray();
        }

        if (action is null)
        {
            Console.Error.WriteLine($"错误: 未知命令 '{line}'（输入 help 查看驻留模式可用命令）");
            return;
        }

        try
        {
            var result = await action.ExecuteAsync(device, args, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(result)) Console.WriteLine(result);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"错误: {ex.Message}");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"错误: 执行失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 阻塞读行放到后台线程，使 <see cref="CancellationToken"/> 能及时生效；
    /// 输入重定向到文件时 <see cref="Console.ReadLine"/> 返回 null（EOF），据此结束驻留。
    /// </summary>
    private sealed class ResidentReader : IDisposable
    {
        private readonly BlockingCollection<string?> _queue = new();
        private readonly Thread _thread;

        public ResidentReader()
        {
            _thread = new Thread(Pump)
            {
                IsBackground = true,
                Name = "CliResidentInput",
            };
            _thread.Start();
        }

        private void Pump()
        {
            try
            {
                while (true)
                {
                    var line = Console.ReadLine();
                    if (line is null)
                    {
                        _queue.Add(null);
                        return;
                    }
                    _queue.Add(line);
                }
            }
            catch (Exception)
            {
                _queue.TryAdd(null);
            }
        }

        public async Task<string?> ReadLineAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested) return null;
            var pending = Task.Run(() =>
            {
                try
                {
                    return _queue.Take();
                }
                catch (InvalidOperationException)
                {
                    return null; // 已 Dispose
                }
            });

            var finished = await Task.WhenAny(pending, WaitCancel(cancellationToken)).ConfigureAwait(false);
            if (finished != pending)
            {
                _queue.Dispose(); // 让阻塞的 Take 立刻返回，释放后台线程
                return null;
            }
            return await pending.ConfigureAwait(false);
        }

        private static async Task WaitCancel(CancellationToken token)
        {
            var tcs = new TaskCompletionSource();
            using (token.Register(() => tcs.TrySetResult()))
            {
                await tcs.Task.ConfigureAwait(false);
            }
        }

        /// <summary>幂等：驻留循环可能已在取消路径上 Dispose 过。</summary>
        public void Dispose()
        {
            try
            {
                _queue.Dispose();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }

    private static string DescribeHold(int seconds) => seconds <= 0 ? "不限时" : $"{seconds} 秒";

    /// <summary>
    /// 发送一帧并回显结果。驻留提示里也要用到，所以不能只依赖命令注册表里的那个。
    /// <para>
    /// 锁屏帧必须走 <see cref="McuDevice.LockScreen"/>（见 <c>SendTracked</c>），
    /// 否则 <c>ScreenLocked</c> 不会被置位，<c>exit</c> 拦截就形同虚设。
    /// </para>
    /// </summary>
    private static string Send(McuDevice device, Func<McuFrame> build, string note = "")
    {
        if (!device.IsConnected) return "[ERR] 尚未连接设备";
        var frame = build();
        bool ok = device.Send(frame);
        return $"{(ok ? "[OK] 已发送" : "[ERR] 发送失败")}  {McuCommandNames.DescribeOut(frame)}  {frame}{note}";
    }

    /// <summary>发锁屏帧并回显；走 <see cref="McuDevice.LockScreen"/> 以维护锁屏状态。</summary>
    private static string SendLockTracked(McuDevice device, string note = "")
    {
        if (!device.IsConnected) return "[ERR] 尚未连接设备";
        var frame = McuOutbound.LockScreen();
        bool ok = device.LockScreen();
        return $"{(ok ? "[OK] 已发送" : "[ERR] 发送失败")}  {McuCommandNames.DescribeOut(frame)}  {frame}{note}";
    }

    /// <summary>锁屏命令的附加提示：口令是否已启用。</summary>
    private static string LockScreenNote(McuDevice device) => device.HasLockPassword
        ? $"  [口令已启用 · {device.LockPassword.Length} 位]"
        : "  [!] 尚未设置锁屏口令：Android 端输入任何口令都能解锁（先执行 lock set-password）";

    private static McuDevice CreateDevice(CliOptions options)
    {
        var device = new McuDevice(options.DevicePath) { AutoReply = !options.NoAutoReply };
        if (options.FrameModeOption is { } mode) device.FrameMode = mode;
        if (options.QueryTimeoutMs is { } timeout) device.QueryTimeoutMs = timeout;
        // CLI 没有日志窗，锁屏口令的调试日志直接打到标准输出。
        device.PasswordLog += text => Console.WriteLine(text);
        return device;
    }
}
