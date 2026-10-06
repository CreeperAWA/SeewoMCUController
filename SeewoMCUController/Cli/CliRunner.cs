using SeewoMCUController.Commands;
using SeewoMCUController.Mcu;
using SeewoMCUController.Ui;
using FrameMode = SeewoMCUController.Mcu.Protocol.FrameMode;

namespace SeewoMCUController.Cli;

/// <summary>命令行参数。</summary>
public sealed class CliOptions
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
        Console.WriteLine("      --no-auto-reply        关闭上行请求的自动应答");
        Console.WriteLine("      --no-connect           不连接设备（仅本地命令）");
        Console.WriteLine("      --timeout <毫秒>       查询类命令的回包等待时间（默认 2000）");
        Console.WriteLine("      --dry-run              只回显命令，不连接设备");
        Console.WriteLine("      --selftest             打印并校验全部命令的构造帧");
        Console.WriteLine("      --categories           列出全部分类");
        Console.WriteLine("      --commands [分类]      列出分类下的动作");
        Console.WriteLine("      --devices              枚举检测到的 MCU 设备");
        Console.WriteLine("  -h, --help                 显示本帮助");
        Console.WriteLine();
        Console.WriteLine("示例:");
        Console.WriteLine("  SeewoMCUController audio volume 60");
        Console.WriteLine("  SeewoMCUController channel switch hdmi 1");
        Console.WriteLine("  SeewoMCUController lock lock");
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
                Console.WriteLine($"  {a.Name,-14}{a.Description}{usage}{frame}");
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

        var menu = new ConsoleMenu(device);
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
            using var probe = new McuDevice(options.DevicePath);
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
        if (!action.Offline && !await device.ConnectAndHandshakeAsync(!options.NoHandshake, cancellationToken).ConfigureAwait(false))
            return 1;

        try
        {
            var result = await action.ExecuteAsync(device, args, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(result)) Console.WriteLine(result);
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

    private static McuDevice CreateDevice(CliOptions options)
    {
        var device = new McuDevice(options.DevicePath) { AutoReply = !options.NoAutoReply };
        if (options.FrameModeOption is { } mode) device.FrameMode = mode;
        if (options.QueryTimeoutMs is { } timeout) device.QueryTimeoutMs = timeout;
        return device;
    }
}
