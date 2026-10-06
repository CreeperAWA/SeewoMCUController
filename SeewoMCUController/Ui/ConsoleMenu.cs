using SeewoMCUController.Commands;
using SeewoMCUController.Mcu;
using SeewoMCUController.Mcu.Protocol;

namespace SeewoMCUController.Ui;

/// <summary>行式控制台菜单。输入既支持序号，也支持直接键入命令名或 <c>分类.动作 参数</c>。</summary>
public sealed class ConsoleMenu
{
    private readonly McuDevice _device;
    private readonly List<string> _notices = new();
    private readonly object _noticeGate = new();

    public ConsoleMenu(McuDevice device)
    {
        _device = device;
        _device.Inbound += OnInbound;
        _device.StatusChanged += OnStatus;
    }

    private void OnInbound(McuInboundEvent evt) => Push($"<- {evt}");

    private void OnStatus(string text) => Push($"· {text}");

    private void Push(string text)
    {
        lock (_noticeGate)
        {
            _notices.Add(text);
            if (_notices.Count > 50) _notices.RemoveAt(0);
        }
    }

    public void Banner()
    {
        Console.WriteLine("==============================================");
        Console.WriteLine("      Seewo MCU Controller · 交互式控制台");
        Console.WriteLine("==============================================");
        Console.WriteLine("  输入序号选择菜单，输入 help 查看全部命令，输入 exit 退出。");
    }

    /// <summary>主循环。</summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        Banner();
        while (!cancellationToken.IsCancellationRequested)
        {
            RenderNotices();
            McuActionRegistry.RenderState(_device);
            Console.WriteLine();
            Console.WriteLine("主菜单");
            for (int i = 0; i < McuActionRegistry.Categories.Count; i++)
            {
                var c = McuActionRegistry.Categories[i];
                Console.WriteLine($"  [{i + 1}] {TextWidth.Pad(c.Title, 14)}{c.Hint}");
            }
            Console.WriteLine("  [h] 帮助      [d] 重新握手    [q] 退出");
            Console.Write("请选择> ");

            var input = Console.ReadLine();
            if (input is null) return;
            input = input.Trim();
            if (input.Length == 0) continue;

            switch (input.ToLowerInvariant())
            {
                case "q":
                case "quit":
                case "exit":
                case "0":
                    return;
                case "h":
                case "help":
                case "?":
                    PrintHelp();
                    continue;
                case "d":
                    await RunCategoryAsync("system", "handshake-seq", cancellationToken).ConfigureAwait(false);
                    continue;
            }

            if (int.TryParse(input, out int choice) && choice >= 1 && choice <= McuActionRegistry.Categories.Count)
            {
                await RunCategoryMenuAsync(McuActionRegistry.Categories[choice - 1], cancellationToken).ConfigureAwait(false);
                continue;
            }

            // 直接键入命令：audio.mute / mute / channel switch pc
            await RunCommandLineAsync(input, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task RunCategoryMenuAsync(McuCategory category, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            RenderNotices();
            Console.WriteLine();
            Console.WriteLine($"== {category.Title} ==  {category.Hint}");
            for (int i = 0; i < category.Actions.Count; i++)
            {
                var a = category.Actions[i];
                string usage = string.IsNullOrEmpty(a.Usage) ? string.Empty : "  " + a.Usage;
                string frame = string.IsNullOrEmpty(a.Frame) ? string.Empty : $"  {a.Frame}";
                Console.WriteLine($"  [{i + 1}] {TextWidth.Pad(a.Name, 15)}{a.Description}{usage}{frame}");
            }
            Console.WriteLine("  [0] 返回主菜单");
            Console.Write("选择> ");

            var input = Console.ReadLine();
            if (input is null) return;
            input = input.Trim();
            if (input.Length == 0) continue;
            if (input is "0" or "b" or "back" or "q") return;

            if (int.TryParse(input, out int idx) && idx >= 1 && idx <= category.Actions.Count)
            {
                var action = category.Actions[idx - 1];
                var args = await PromptArgsAsync(action).ConfigureAwait(false);
                if (args is null) continue;
                await ExecuteAsync(action, args, cancellationToken).ConfigureAwait(false);
                continue;
            }

            await RunCommandLineAsync(input, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<string[]?> PromptArgsAsync(McuAction action)
    {
        if (string.IsNullOrEmpty(action.Usage)) return Array.Empty<string>();
        Console.Write($"  参数（{action.Usage}，直接回车用默认值）> ");
        var line = Console.ReadLine();
        if (line is null) return null;
        line = line.Trim();
        return line.Length == 0 ? Array.Empty<string>() : line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    private async Task RunCommandLineAsync(string input, CancellationToken cancellationToken)
    {
        var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var action = Resolve(parts[0], parts.Length > 1 ? parts[1] : null, out var actionName, out var restStart);
        if (action is null)
        {
            Warn($"未知命令: {input}（输入 help 查看全部命令）");
            return;
        }

        var args = parts.Skip(restStart).ToArray();
        await ExecuteAsync(action, args, cancellationToken).ConfigureAwait(false);
    }

    private static McuAction? Resolve(string first, string? second, out string name, out int restStart)
    {
        restStart = 1;
        if (second is not null && McuActionRegistry.FindCategory(first) is { } category)
        {
            var hit = category.Actions.FirstOrDefault(a => a.Matches(second));
            if (hit is not null)
            {
                name = $"{category.Key} {second}";
                restStart = 2;
                return hit;
            }
        }

        var direct = McuActionRegistry.SearchOther(first);
        if (direct is not null)
        {
            name = direct.Name;
            return direct;
        }

        name = first;
        return null;
    }

    private async Task RunCategoryAsync(string category, string action, CancellationToken cancellationToken)
    {
        var found = McuActionRegistry.FindAction(category, action);
        if (found is null)
        {
            Warn($"未知命令: {category} {action}");
            return;
        }
        await ExecuteAsync(found, Array.Empty<string>(), cancellationToken).ConfigureAwait(false);
    }

    public async Task ExecuteAsync(McuAction action, string[] args, CancellationToken cancellationToken)
    {
        try
        {
            var result = await action.ExecuteAsync(_device, args, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(result)) Console.WriteLine(result);
        }
        catch (ArgumentException ex)
        {
            Warn(ex.Message);
        }
        catch (OperationCanceledException)
        {
            Warn("已取消");
        }
        catch (Exception ex)
        {
            Warn($"执行失败: {ex.Message}");
        }
    }

    private void RenderNotices()
    {
        string[] lines;
        lock (_noticeGate) lines = _notices.ToArray();
        if (lines.Length == 0) return;
        Console.WriteLine("---- 最近事件 ----");
        foreach (var line in lines) Console.WriteLine("  " + line);
        Console.WriteLine("------------------");
        _notices.Clear();
    }

    public static void Warn(string text)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(text);
        Console.ResetColor();
    }

    public static void Info(string text)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(text);
        Console.ResetColor();
    }

    public static void Ok(string text)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(text);
        Console.ResetColor();
    }

    public static void PrintHelp()
    {
        Console.WriteLine("命令格式:  <分类> <动作> [参数]     例: audio volume 60");
        Console.WriteLine("          也可直接写动作名        例: mute / channel switch hdmi 1");
        Console.WriteLine();
        foreach (var category in McuActionRegistry.Categories)
        {
            Console.WriteLine($"[{category.Key}] {category.Title}");
            foreach (var a in category.Actions)
            {
                string usage = string.IsNullOrEmpty(a.Usage) ? "" : "  " + a.Usage;
                string frame = string.IsNullOrEmpty(a.Frame) ? "" : $"  {a.Frame}";
                Console.WriteLine($"    {TextWidth.Pad(a.Name, 15)}{a.Description}{usage}{frame}");
            }
            Console.WriteLine();
        }
    }
}
