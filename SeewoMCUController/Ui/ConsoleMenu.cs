using SeewoMCUController.Commands;
using SeewoMCUController.Mcu;
using SeewoMCUController.Mcu.Protocol;
using System.Text;

namespace SeewoMCUController.Ui;

/// <summary>行式控制台菜单。输入既支持序号，也支持直接键入命令名或 <c>分类.动作 参数</c>。</summary>
public sealed class ConsoleMenu
{
    private const int AutoUnlockDelaySeconds = 10;
    private const int MaxEventLines = 200;

    private readonly McuDevice _device;
    private readonly List<ConsoleEventLine> _eventLines = new();
    private readonly object _eventGate = new();
    private bool _panelPaused;
    private ConsoleEventLine[]? _pausedEvents;
    private long _eventVersion;

    public ConsoleMenu(McuDevice device)
    {
        _device = device;
        _device.Inbound += OnInbound;
        _device.StatusChanged += OnStatus;
        _device.Link.LogAppended += OnLogAppended;

        foreach (var entry in _device.Link.SnapshotLog())
        {
            if (entry.Outgoing)
            {
                AddEvent(ConsoleEventLine.FromLog(entry));
                continue;
            }

            try
            {
                AddEvent(ConsoleEventLine.FromInbound(McuInbound.Decode(entry.Frame), entry.Timestamp));
            }
            catch
            {
                AddEvent(ConsoleEventLine.FromLog(entry));
            }
        }
    }

    private void OnInbound(McuInboundEvent evt) => AddEvent(ConsoleEventLine.FromInbound(evt));

    private void OnStatus(string text) => AddEvent(new ConsoleEventLine(
        DateTime.Now, "·", "链路", text, string.Empty, ConsoleColor.DarkGray));

    private void OnLogAppended(McuLogEntry entry)
    {
        if (entry.Outgoing) AddEvent(ConsoleEventLine.FromLog(entry));
    }

    private void AddEvent(ConsoleEventLine line)
    {
        lock (_eventGate)
        {
            _eventLines.Add(line);
            if (_eventLines.Count > MaxEventLines) _eventLines.RemoveRange(0, _eventLines.Count - MaxEventLines);
        }
        Interlocked.Increment(ref _eventVersion);
    }

    private void TogglePanelPause()
    {
        lock (_eventGate)
        {
            _panelPaused = !_panelPaused;
            _pausedEvents = _panelPaused ? _eventLines.ToArray() : null;
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
        while (!cancellationToken.IsCancellationRequested)
        {
            RenderMainScreen();
            McuActionRegistry.RenderState(_device);
            Console.WriteLine();
            Console.WriteLine("主菜单");
            for (int i = 0; i < McuActionRegistry.Categories.Count; i++)
            {
                var c = McuActionRegistry.Categories[i];
                Console.WriteLine($"  [{i + 1}] {TextWidth.Pad(c.Title, 14)}{c.Hint}");
            }
            Console.WriteLine("  [h] 帮助  [d] 握手  [p] 暂停面板  [c] 切换颜色  [q] 退出");
            var input = await ReadLineRealtimeAsync("请选择> ", RenderMainMenu).ConfigureAwait(false);
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
                case "p":
                case "pause":
                    TogglePanelPause();
                    continue;
                case "c":
                case "color":
                    ConsoleTheme.ToggleColors();
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
            RenderCategoryScreen(category);
            Console.WriteLine();
            Console.WriteLine($"== {category.Title} ==  {category.Hint}");
            for (int i = 0; i < category.Actions.Count; i++)
            {
                var a = category.Actions[i];
                string usage = string.IsNullOrEmpty(a.Usage) ? string.Empty : "  " + a.Usage;
                string frame = string.IsNullOrEmpty(a.Frame) ? string.Empty : $"  {a.Frame}";
                Console.WriteLine($"  [{i + 1}] {TextWidth.Pad(a.Name, 15)}{a.Description}{usage}{frame}");
            }
            Console.WriteLine("  [0] 返回  [p] 暂停面板  [c] 切换颜色");
            var input = await ReadLineRealtimeAsync("选择> ", () => RenderCategoryMenu(category)).ConfigureAwait(false);
            if (input is null) return;
            input = input.Trim();
            if (input.Length == 0) continue;
            if (input is "0" or "b" or "back" or "q") return;
            if (input is "p" or "pause")
            {
                TogglePanelPause();
                continue;
            }
            if (input is "c" or "color")
            {
                ConsoleTheme.ToggleColors();
                continue;
            }

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
        var line = await ReadLineRealtimeAsync(
            $"  参数（{action.Usage}，直接回车用默认值）> ",
            () => RenderCategoryScreen(McuActionRegistry.FindCategory(action.Category)!)).ConfigureAwait(false);
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

    /// <summary>需要二次确认的动作：锁 Android 屏（<c>lock lock</c>）。</summary>
    private static bool NeedsLockGuard(McuAction action) =>
        action.Category == "lock" && action.Matches("lock");

    /// <summary>
    /// 锁 Android 屏的二次确认。输入 <c>lock</c> 继续（锁屏并在 <see cref="AutoUnlockDelaySeconds"/> 秒后自动解锁）；
    /// 其余输入视为放弃，不发送任何命令。
    /// 返回 true 表示允许继续。
    /// </summary>
    private async Task<bool> ConfirmLockScreenAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine();
        Console.WriteLine("==============================================");
        Console.WriteLine("  此功能只应当搭配自动化使用，手动触发无异于吞下赛博灯泡！");
        Console.WriteLine("==============================================");
        Console.WriteLine($"  输入 lock    继续：锁定 Android 屏，{AutoUnlockDelaySeconds} 秒后自动解锁");
        Console.WriteLine("  其它输入     放弃，不发送任何命令");
        Console.WriteLine("  自动化场景请直接用命令行：SeewoMCUController.exe lock lock");
        var input = (await ReadLineRealtimeAsync("请输入> ", RenderMainScreen).ConfigureAwait(false))?.Trim();
        if (string.Equals(input, "lock", StringComparison.OrdinalIgnoreCase))
            return true;

        Warn("已放弃锁屏（未发送任何命令）");
        return false;
    }

    /// <summary>锁屏后等待 <see cref="AutoUnlockDelaySeconds"/> 秒，再发送一帧 <c>(0x36,0x12)</c> 解锁。</summary>
    private async Task AutoUnlockLaterAsync(CancellationToken cancellationToken)
    {
        for (int i = AutoUnlockDelaySeconds; i > 0; i--)
        {
            Console.Write($"\r  已锁定，{i} 秒后自动解锁 Android 屏…   ");
            try
            {
                await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
        Console.WriteLine("\r  自动解锁 Android 屏                    ");
        bool sent = _device.Send(McuOutbound.UnlockScreen());
        if (sent)
        {
            Console.WriteLine("[OK] 已发送 (0x36,0x12) 解锁 Android 屏");
        }
        else
        {
            Warn($"自动解锁发送失败：{NotConnectedHint()}");
        }
    }

    private string NotConnectedHint() => _device.IsConnected ? "写入失败" : "未连接设备";

    public async Task ExecuteAsync(McuAction action, string[] args, CancellationToken cancellationToken)
    {
        bool guardedLock = NeedsLockGuard(action);
        if (guardedLock && !await ConfirmLockScreenAsync(cancellationToken).ConfigureAwait(false))
            return;

        try
        {
            var result = await action.ExecuteAsync(_device, args, cancellationToken).ConfigureAwait(false);
            PrintResult(result);
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

        if (guardedLock) await AutoUnlockLaterAsync(cancellationToken).ConfigureAwait(false);
    }

    private void PrintResult(string result)
    {
        if (string.IsNullOrEmpty(result)) return;
        if (result.StartsWith("[ERR]", StringComparison.Ordinal))
        {
            AddEvent(new ConsoleEventLine(DateTime.Now, "!", "错误", result[5..].Trim(), string.Empty, ConsoleColor.Red));
            ConsoleTheme.WriteLine(result, ConsoleColor.Red, bold: true);
            return;
        }

        if (result.StartsWith("[OK]", StringComparison.Ordinal))
            ConsoleTheme.WriteLine(result, ConsoleColor.Green);
        else
            Console.WriteLine(result);
    }

    private void RenderMainScreen()
    {
        ClearScreen();
        Banner();
        RenderEventPanel();
    }

    private void RenderMainMenu()
    {
        RenderMainScreen();
        McuActionRegistry.RenderState(_device);
        Console.WriteLine();
        Console.WriteLine("主菜单");
        for (int i = 0; i < McuActionRegistry.Categories.Count; i++)
        {
            var c = McuActionRegistry.Categories[i];
            Console.WriteLine($"  [{i + 1}] {TextWidth.Pad(c.Title, 14)}{c.Hint}");
        }
        Console.WriteLine("  [h] 帮助  [d] 握手  [p] 暂停面板  [c] 切换颜色  [q] 退出");
    }

    private void RenderCategoryMenu(McuCategory category)
    {
        RenderCategoryScreen(category);
        Console.WriteLine();
        Console.WriteLine($"== {category.Title} ==  {category.Hint}");
        for (int i = 0; i < category.Actions.Count; i++)
        {
            var a = category.Actions[i];
            string usage = string.IsNullOrEmpty(a.Usage) ? string.Empty : "  " + a.Usage;
            string frame = string.IsNullOrEmpty(a.Frame) ? string.Empty : $"  {a.Frame}";
            Console.WriteLine($"  [{i + 1}] {TextWidth.Pad(a.Name, 15)}{a.Description}{usage}{frame}");
        }
        Console.WriteLine("  [0] 返回  [p] 暂停面板  [c] 切换颜色");
    }

    private async Task<string?> ReadLineRealtimeAsync(string prompt, Action redraw)
    {
        if (Console.IsInputRedirected)
        {
            Console.Write(prompt);
            return Console.ReadLine();
        }

        var buffer = new StringBuilder();
        long seenVersion = Interlocked.Read(ref _eventVersion);
        Console.Write(prompt);
        while (true)
        {
            while (Console.KeyAvailable)
            {
                var key = Console.ReadKey(intercept: true);
                if (key.Key is ConsoleKey.Enter)
                {
                    Console.WriteLine();
                    return buffer.ToString();
                }
                if (key.Key is ConsoleKey.Backspace)
                {
                    if (buffer.Length > 0)
                    {
                        buffer.Length--;
                        Console.Write("\b \b");
                    }
                    continue;
                }
                if (key.Key is ConsoleKey.Escape)
                {
                    Console.WriteLine();
                    return string.Empty;
                }
                if (!char.IsControl(key.KeyChar))
                {
                    buffer.Append(key.KeyChar);
                    Console.Write(key.KeyChar);
                }
            }

            long currentVersion = Interlocked.Read(ref _eventVersion);
            if (currentVersion != seenVersion)
            {
                seenVersion = currentVersion;
                redraw();
                Console.Write(prompt);
                Console.Write(buffer.ToString());
            }
            await Task.Delay(50).ConfigureAwait(false);
        }
    }

    private void RenderCategoryScreen(McuCategory category)
    {
        ClearScreen();
        Banner();
        RenderEventPanel();
    }

    private void RenderEventPanel()
    {
        Console.WriteLine();
        Console.WriteLine($"---- MCU 事件面板（最近 {PanelRowCount()} 条，{(_panelPaused ? "已暂停" : "实时")}，颜色：{ConsoleTheme.ColorState}） ----");
        Console.WriteLine("时间     方向 分类       事件                         详情");

        ConsoleEventLine[] lines;
        lock (_eventGate) lines = _panelPaused ? _pausedEvents ?? Array.Empty<ConsoleEventLine>() : _eventLines.ToArray();
        int skip = Math.Max(0, lines.Length - PanelRowCount());
        if (lines.Length == 0)
        {
            ConsoleTheme.WriteLine("（等待 MCU 事件；收到上行帧后会在这里显示解析结果）", ConsoleColor.DarkGray);
        }
        else if (_panelPaused)
        {
            skip = Math.Max(0, Math.Min(skip, lines.Length - PanelRowCount()));
            RenderLines(lines, skip);
        }
        else
        {
            RenderLines(lines, skip);
        }
        Console.WriteLine("-----------------------------------------------");
    }

    private static void RenderLines(ConsoleEventLine[] lines, int skip)
    {
        for (int i = skip; i < lines.Length; i++)
        {
            var line = lines[i];
            string prefix = $"{line.Timestamp:HH:mm:ss}  {line.Direction}  {TextWidth.Pad(line.Category, 10)}";
            string body = $"{TextWidth.Truncate(line.Event, 28),-28}  {TextWidth.Truncate(line.Detail, 70)}";
            ConsoleTheme.Write(prefix, ConsoleColor.DarkGray);
            ConsoleTheme.Write(body, line.Color, bold: line.Direction == "←");
            Console.WriteLine();
        }
    }

    private static int PanelRowCount()
    {
        try
        {
            return Math.Clamp((Console.WindowHeight - 16) / 2, 5, 12);
        }
        catch (IOException)
        {
            return 8;
        }
    }

    private static void ClearScreen()
    {
        if (Console.IsOutputRedirected) return;
        try
        {
            Console.Clear();
        }
        catch (IOException)
        {
            // Some redirected/embedded terminals reject Clear; keep the stream usable.
        }
    }

    private void Warn(string text)
    {
        AddEvent(new ConsoleEventLine(DateTime.Now, "!", "错误", text, string.Empty, ConsoleColor.Yellow));
        ConsoleTheme.WriteLine($"[ERR] {text}", ConsoleColor.Yellow, bold: true);
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
        Console.WriteLine("TUI 提示：事件面板显示解析后的 MCU→PC 事件与 PC→MCU 发送帧。");
        Console.WriteLine("  p / pause  暂停或恢复事件面板滚动");
        Console.WriteLine("  c / color  切换彩色/单色显示（NO_COLOR 环境变量默认关闭颜色）");
        Console.WriteLine("  面板行数随终端高度变化；扩大窗口可看到更多历史事件。");
        Console.WriteLine();
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
