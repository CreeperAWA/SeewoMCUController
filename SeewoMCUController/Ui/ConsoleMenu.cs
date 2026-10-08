using SeewoMCUController.Commands;
using SeewoMCUController.Mcu;
using SeewoMCUController.Mcu.Protocol;
using System.Text;

namespace SeewoMCUController.Ui;

/// <summary>
/// 操作台（主终端窗口）。输入既支持序号，也支持直接键入命令名或 <c>分类.动作 参数</c>。
/// <para>
/// <b>两个独立的终端窗口：</b>
/// <list type="bullet">
/// <item>本进程所在的窗口 = 操作台：只有菜单、连接状态和输入提示，整屏都是留给操作的，不含任何日志。</item>
/// <item><see cref="LogWindowProcess"/> 拉起的另一个进程 = 实时日志窗：<c>AllocConsole</c> 新建的独立控制台窗口，
/// MCU 上行事件 / PC 下发帧 / 链路状态 / 命令结果逐条滚动刷新。</item>
/// </list>
/// 两个窗是各自独立的操作系统窗口，可以分别缩放、滚动、关闭；日志刷新完全不碰操作台，
/// 所以操作面板不需要实时重画。开不出独立窗口时退回内嵌日志面板。
/// </para>
/// </summary>
public sealed class ConsoleMenu : IDisposable
{
    private const int AutoUnlockDelaySeconds = 10;

    /// <summary>日志窗最小行数（标题 + 列头 + 分隔线 + 至少 3 行事件）。</summary>
    private const int MinLogRows = 6;

    /// <summary>日志窗最大行数。</summary>
    private const int MaxLogRows = 18;

    private readonly McuDevice _device;

    /// <summary>独立的实时日志窗口进程。</summary>
    private readonly LogWindowProcess _logWindow = new();

    /// <summary>退化路径：独立窗口开不出来时，日志内嵌在操作台底部。</summary>
    private readonly ConsoleLogPane _fallbackPane = new();

    /// <summary>重复日志合并（上限 <see cref="LogAggregator.Capacity"/> 条）。</summary>
    private readonly LogAggregator _aggregator = new();

    /// <summary>是否曾经成功拉起过独立日志窗口。</summary>
    private bool _logWindowStarted;

    /// <summary>日志窗消失时置位，用于提示用户「关掉日志窗会退出整个程序」。</summary>
    private bool _logWindowGoneNoticed;

    /// <summary>二级菜单的动作列表滚动位置。</summary>
    private int _scroll;

    private int _promptRow;
    private int _promptCol;

    public ConsoleMenu(McuDevice device)
    {
        _device = device;
        _device.Inbound += OnInbound;
        _device.StatusChanged += OnStatus;
        _device.Link.LogAppended += OnLogAppended;
        _device.Link.LogCleared += OnLogCleared;
    }

    /// <summary>打开独立的实时日志窗口。</summary>
    public bool TryOpenLogWindow()
    {
        if (!_logWindow.TryOpen()) return false;
        _logWindowStarted = true;
        _logWindowGoneNoticed = false;
        // 连接 / 握手阶段的事件已经产生过了，补发给刚开的窗口。
        foreach (var entry in _device.Link.SnapshotLog()) Replay(entry);
        return true;
    }

    private void Replay(McuLogEntry entry)
    {
        ConsoleEventLine line;
        if (entry.Outgoing)
        {
            line = ConsoleEventLine.FromLog(entry);
        }
        else
        {
            try
            {
                line = ConsoleEventLine.FromInbound(McuInbound.Decode(entry.Frame), entry.Timestamp);
            }
            catch
            {
                line = ConsoleEventLine.FromLog(entry);
            }
        }
        Publish(line);
    }

    private void OnInbound(McuInboundEvent evt) => Publish(ConsoleEventLine.FromInbound(evt));

    private void OnStatus(string text) => Note("链路", "状态变化", text, ConsoleColor.DarkGray);

    private void OnLogAppended(McuLogEntry entry)
    {
        if (entry.Outgoing) Publish(ConsoleEventLine.FromLog(entry));
    }

    private void OnLogCleared()
    {
        _aggregator.Clear();
        _fallbackPane.Clear();
    }

    /// <summary>本机产生的消息（链路状态 / 命令结果 / 提示）：标签放「事件」列，正文放更宽的「详情」列。</summary>
    private void Note(string category, string eventName, string text, ConsoleColor color) =>
        Publish(new ConsoleEventLine(
            DateTime.Now, "·", TextWidth.Clip(category, 10), TextWidth.Clip(eventName, 22), TextWidth.Clip(text, 400), color));

    /// <summary>
    /// 把一条日志投出去：先经 <see cref="LogAggregator"/> 合并重复项，
    /// 再送到独立日志窗；日志窗不可用时落到内嵌面板。
    /// </summary>
    private void Publish(ConsoleEventLine line)
    {
        var (merged, _) = _aggregator.Append(line);
        if (SendToLogWindow(LogWire.Encode(merged))) return;
        _fallbackPane.Append(merged);
    }

    /// <summary>当前有效日志条数（合并后）。</summary>
    private int LogCount => LogWindowAlive ? _aggregator.Count : _fallbackPane.Count;

    /// <summary>日志窗是否已被用户关闭（关闭即退出整个程序）。</summary>
    private bool LogWindowClosed => _logWindowStarted && !_logWindow.IsAlive;

    // ------------------------------------------------------------------ 布局

    /// <summary>操作台的几何信息。独立日志窗可用时整屏都是操作面板，否则底部给日志面板留位。</summary>
    private readonly record struct Layout(int LogTop, int LogRows, int Room, int Width, int Height);

    /// <summary>按终端高度分配退化模式下内嵌日志面板的行数。</summary>
    private static int LogRowsFor(int height)
    {
        int ceiling = Math.Max(MinLogRows, Math.Min(MaxLogRows, height - 8));
        return Math.Clamp(height * 2 / 5, MinLogRows, ceiling);
    }

    private static Layout CurrentLayout(bool reserveLogPane)
    {
        int h = Terminal.Height;
        int w = Terminal.Width;
        if (!reserveLogPane)
        {
            // 日志在另一个窗口，这里整屏都是操作面板。
            return new Layout(h, 0, Math.Max(1, h - 1), w, h);
        }

        int logRows = Math.Clamp(LogRowsFor(h), MinLogRows, Math.Max(MinLogRows, h - 2));
        int logTop = Math.Max(2, h - logRows);
        return new Layout(logTop, Math.Max(MinLogRows, h - logTop), Math.Max(1, logTop - 1), w, h);
    }

    /// <summary>
    /// 画操作台（内容行 + 末行输入提示）。整屏都属于操作面板，日志在另一个窗口。
    /// <paramref name="force"/> 强制重画（清屏 / 尺寸变化后调用）。
    /// </summary>
    private void Present(IReadOnlyList<ConsoleRow> content, string prompt, Layout layout, bool force)
    {
        if (!Terminal.Split)
        {
            // 终端不可寻址（输出被重定向等）：退化为线性输出。
            Terminal.ClearScreen();
            foreach (var row in content) ConsoleTheme.WriteLine(TextWidth.PadLine(row.Text, layout.Width), row.Color);
            Console.Write(prompt);
            _promptRow = -1;
            _promptCol = TextWidth.Measure(prompt);
            return;
        }

        int w = layout.Width;
        int room = layout.Room;
        int count = Math.Min(content.Count, room);

        // 缓冲区滚动后，缓冲区坐标会与可视行错位；先恢复到原点。
        Terminal.AlignWindowToOrigin();

        // 每行显式定位，避免依赖控制台的自动换行状态。
        for (int i = 0; i < room; i++)
        {
            if (!Terminal.TrySetCursor(0, i)) break;
            var row = i < count ? content[i] : ConsoleRow.Empty;
            ConsoleTheme.Write(TextWidth.FitLine(row.Text, w), row.Color);
        }

        // 提示行也要铺满整宽，覆盖上一次输入留下的字符。
        string shown = TextWidth.Clip(prompt, w);
        _promptRow = Math.Min(layout.Height - 1, room);
        _promptCol = TextWidth.Measure(shown);
        Terminal.TrySetCursor(0, _promptRow);
        ConsoleTheme.Write(TextWidth.FitLine(shown, w), ConsoleColor.White);
        Terminal.TrySetCursor(Math.Min(_promptCol, Math.Max(0, w - 1)), _promptRow);

        // 只有退化模式（独立日志窗没开起来）才需要在本窗口里画日志面板。
        if (layout.LogRows > 0) _fallbackPane.Draw(layout.LogTop, layout.LogRows, w, force);
    }

    // ------------------------------------------------------------------ 主循环

    /// <summary>主循环。</summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        Terminal.RefreshSize();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        if (TryOpenLogWindow())
        {
            LogWindowNotice("已打开独立日志窗口：MCU 事件、收发帧与命令结果都在那个窗口里实时刷新。关闭任一窗口都会退出整个程序。");
            // 看门狗：日志窗一旦消失就取消整个操作台。
            // 必须独立于输入循环 —— 输入可能被阻塞在 ReadLine / ReadKey 上，
            // 只在重绘时检查会漏掉「用户在等待输入时关掉日志窗」这种最常见的情况。
            _ = WatchLogWindowAsync(linked);
        }
        else
        {
            LogWindowNotice("未能打开独立日志窗口，日志改为显示在本窗口底部。");
        }

        while (!linked.Token.IsCancellationRequested)
        {
            var input = await PromptAsync(BuildMainScreen, "请选择> ", null, linked.Token).ConfigureAwait(false);
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
                    ShowModal("命令帮助", HelpText(), cancellationToken);
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
                await RunCategoryLoopAsync(McuActionRegistry.Categories[choice - 1], cancellationToken).ConfigureAwait(false);
                continue;
            }

            // 直接键入命令：audio.mute / mute / channel switch pc
            await RunCommandLineAsync(input, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 日志窗是否还在。查子进程句柄的存活状态（<see cref="LogWindowProcess.IsAlive"/>），
    /// 而不是靠写管道是否失败 —— 管道有缓冲区，子进程没了之后写入仍可能短暂成功，
    /// 甚至在缓冲区填满前一直「成功」，所以靠写入结果判断会漏判。
    /// </summary>
    private bool LogWindowAlive => _logWindowStarted && _logWindow.IsAlive;

    /// <summary>
    /// 看门狗：轮询日志窗存活状态，一旦消失就取消操作台（两个窗同生共死）。
    /// <para>
    /// 之所以要独立于输入循环：主线程可能正阻塞在 <see cref="Console.ReadLine"/> 或
    /// 等待按键上，此时主循环里的检查点根本走不到。
    /// </para>
    /// </summary>
    private async Task WatchLogWindowAsync(CancellationTokenSource linked)
    {
        try
        {
            while (!linked.IsCancellationRequested)
            {
                await Task.Delay(200, linked.Token).ConfigureAwait(false);
                if (_logWindow.IsAlive) continue;

                if (!_logWindowGoneNoticed)
                {
                    _logWindowGoneNoticed = true;
                    Note("日志窗", "已关闭", "日志窗口已关闭，正在退出整个程序", ConsoleColor.Yellow);
                }
                linked.Cancel();
                return;
            }
        }
        catch (OperationCanceledException)
        {
            // 正常退出路径
        }
    }

    /// <summary>给日志窗发一行。</summary>
    private bool SendToLogWindow(string payload) => LogWindowAlive && _logWindow.Send(payload);

    /// <summary>设置操作台窗口标题，并把启动提示记进日志窗。</summary>
    private void LogWindowNotice(string text)
    {
        try
        {
            Console.Title = "Seewo MCU Controller · 操作台";
        }
        catch (IOException)
        {
            // 少数宿主不允许改标题，忽略
        }
        Note("操作台", "启动", text, ConsoleColor.DarkGray);
    }

    /// <summary>二级菜单循环：切分类后一直停在该分类，直到用户选 0 返回。</summary>
    private async Task RunCategoryLoopAsync(McuCategory category, CancellationToken cancellationToken)
    {
        _scroll = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            var input = await PromptAsync(
                () => BuildCategoryScreen(category), "选择> ", OnScrollKey, cancellationToken).ConfigureAwait(false);
            if (input is null) return;
            input = input.Trim();
            if (input.Length == 0) continue;
            if (input is "0" or "b" or "back" or "q") return;
            if (input is "c" or "color")
            {
                ConsoleTheme.ToggleColors();
                continue;
            }

            if (int.TryParse(input, out int idx) && idx >= 1 && idx <= category.Actions.Count)
            {
                var action = category.Actions[idx - 1];
                var args = await PromptArgsAsync(category, action, cancellationToken).ConfigureAwait(false);
                if (args is null) continue;
                await ExecuteAsync(action, args, cancellationToken).ConfigureAwait(false);
                continue;
            }

            await RunCommandLineAsync(input, cancellationToken).ConfigureAwait(false);
        }
    }

    private void OnScrollKey(ConsoleKey key)
    {
        switch (key)
        {
            case ConsoleKey.UpArrow: _scroll = Math.Max(0, _scroll - 1); break;
            case ConsoleKey.DownArrow: _scroll++; break;
            case ConsoleKey.PageUp: _scroll = Math.Max(0, _scroll - 10); break;
            case ConsoleKey.PageDown: _scroll += 10; break;
            case ConsoleKey.Home: _scroll = 0; break;
            case ConsoleKey.End: _scroll = int.MaxValue; break;
        }
    }

    // ------------------------------------------------------------------ 输入

    /// <summary>绘制一屏并读取一行输入。返回 null 表示取消 / 退出。</summary>
    private async Task<string?> PromptAsync(
        Func<IReadOnlyList<ConsoleRow>> build,
        string prompt,
        Action<ConsoleKey>? specialKey,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested) return null;

        // 日志窗被关掉 => 整个程序退出（两个窗同生共死）。
        if (LogWindowClosed) return null;

        bool force = Terminal.RefreshSize();
        if (force)
        {
            Terminal.ClearScreen();
            Terminal.TrySetCursor(0, 0);
            _fallbackPane.Invalidate();
        }

        Layout layout = CurrentLayout(!LogWindowAlive);
        Present(build(), prompt, layout, force);

        if (Console.IsInputRedirected)
        {
            return await ReadLineRedirectedAsync(cancellationToken).ConfigureAwait(false);
        }

        var buffer = new StringBuilder();
        int limit = Math.Max(1, layout.Width - TextWidth.Measure(prompt) - 1);
        long seen = _fallbackPane.Version;

        while (!cancellationToken.IsCancellationRequested)
        {
            bool rebuild = false;
            while (Console.KeyAvailable)
            {
                ConsoleKeyInfo key = Console.ReadKey(intercept: true);
                switch (key.Key)
                {
                    case ConsoleKey.Enter:
                        // 光标停在最后一行时写 '\n' 会把缓冲区整个滚上去，
                        // WindowTop 随之大于 0，之后按缓冲区行号定位就全部错位。
                        // 这里只移动光标、不输出换行：提示符由下一次 Present 重画。
                        Terminal.TrySetCursor(0, _promptRow);
                        return buffer.ToString();
                    case ConsoleKey.Escape:
                        ClearInputLine(prompt, buffer);
                        return string.Empty;
                    case ConsoleKey.Backspace:
                        EraseLast(buffer);
                        continue;
                    case ConsoleKey.UpArrow:
                    case ConsoleKey.DownArrow:
                    case ConsoleKey.PageUp:
                    case ConsoleKey.PageDown:
                    case ConsoleKey.Home:
                    case ConsoleKey.End:
                        if (specialKey is not null)
                        {
                            specialKey(key.Key);
                            rebuild = true;
                        }
                        continue;
                }

                if (key.KeyChar != '\0' && !char.IsControl(key.KeyChar) && buffer.Length < limit)
                {
                    buffer.Append(key.KeyChar);
                    Console.Write(key.KeyChar);
                }
            }

            if (rebuild)
            {
                layout = CurrentLayout(!LogWindowAlive);
                Present(build(), prompt, layout, force: false);
                Reecho(buffer);
                seen = _fallbackPane.Version;
            }
            else if (Terminal.RefreshSize())
            {
                // 操作台窗口被拉伸 / 缩放：重画操作面板。
                Terminal.ClearScreen();
                _fallbackPane.Invalidate();
                layout = CurrentLayout(!LogWindowAlive);
                Present(build(), prompt, layout, force: true);
                Reecho(buffer);
                seen = _fallbackPane.Version;
            }
            else if (!LogWindowAlive && _fallbackPane.Version != seen)
            {
                // 退化模式：日志就画在本窗口底部，就地重画那几行，输入光标保持原样。
                seen = _fallbackPane.Version;
                _fallbackPane.Draw(layout.LogTop, layout.LogRows, layout.Width, force: false);
            }

            try
            {
                await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        return null;
    }

    /// <summary>
    /// 输入被重定向时的读行。
    /// <para>
    /// <see cref="Console.ReadLine"/> 是阻塞调用，日志窗被关掉时看门狗虽然取消了令牌，
    /// 这里却还在等输入，整个程序就退不掉了。所以把阻塞读放到后台线程，
    /// 主线程只等「读完了」或「令牌取消了」两者之一。
    /// </para>
    /// </summary>
    private static async Task<string?> ReadLineRedirectedAsync(CancellationToken cancellationToken)
    {
        string? result = null;
        bool done = false;
        var finished = new ManualResetEventSlim(false);

        var reader = new Thread(() =>
        {
            try
            {
                result = Console.ReadLine();
            }
            catch (IOException)
            {
                result = null;
            }
            finally
            {
                done = true;
                finished.Set();
            }
        })
        {
            IsBackground = true,
            Name = "ConsoleRedirectedInput",
        };
        reader.Start();

        // 每 100ms 醒一次看令牌，避免阻塞读把退出卡住。
        while (!done && !cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }
        return cancellationToken.IsCancellationRequested ? null : result;
    }

    /// <summary>操作窗整屏重画后，把已输入的内容补回提示符后面。</summary>
    private void Reecho(StringBuilder buffer)
    {
        if (buffer.Length == 0 || !Terminal.Split) return;
        Console.Write(TextWidth.Clip(buffer.ToString(), Math.Max(0, Terminal.Width - 1)));
        Terminal.TrySetCursor(Math.Min(_promptCol + TextWidth.Measure(buffer.ToString()), Math.Max(0, Terminal.Width - 1)), _promptRow);
    }

    /// <summary>退格：删掉末尾一个字符（含代理对），并按其显示列宽擦除。</summary>
    private static void EraseLast(StringBuilder buffer)
    {
        if (buffer.Length == 0) return;
        int idx = buffer.Length - 1;
        while (idx > 0 && char.IsLowSurrogate(buffer[idx]) && char.IsHighSurrogate(buffer[idx - 1])) idx--;
        int columns = Math.Max(1, TextWidth.Measure(buffer.ToString(idx, buffer.Length - idx)));
        buffer.Length = idx;
        Console.Write("\b" + new string(' ', columns + 1) + new string('\b', columns + 1));
    }

    /// <summary>Esc 时抹掉已输入的内容，只留下提示符。</summary>
    private void ClearInputLine(string prompt, StringBuilder buffer)
    {
        var saved = Terminal.SaveCursor();
        if (_promptRow >= 0) Terminal.TrySetCursor(0, _promptRow);
        else Console.Write('\r');
        // 整行擦掉再写提示符：只按「提示符 + 输入长度」擦，遇到宽字符会算漏。
        ConsoleTheme.Write(TextWidth.FitLine(prompt, Terminal.Width), ConsoleColor.White);
        if (_promptRow >= 0) Terminal.TrySetCursor(Math.Min(_promptCol, Math.Max(0, Terminal.Width - 1)), _promptRow);
        else Terminal.RestoreCursor(saved);
        buffer.Clear();
    }

    private async Task<string[]?> PromptArgsAsync(McuCategory category, McuAction action, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(action.Usage)) return Array.Empty<string>();
        var line = await PromptAsync(
            () => BuildCategoryScreen(category),
            $"  参数（{action.Usage}，直接回车用默认值）> ",
            null,
            cancellationToken).ConfigureAwait(false);
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
        Terminal.ClearScreen();
        var lines = new[]
        {
            "  此功能只应当搭配自动化使用，手动触发无异于吞下赛博灯泡！",
            "",
            $"  输入 lock    继续：锁定 Android 屏，{AutoUnlockDelaySeconds} 秒后自动解锁",
            "  其它输入     放弃，不发送任何命令",
            "  自动化场景请直接用命令行：SeewoMCUController.exe lock lock",
        };
        var input = await PromptModalAsync("锁 Android 屏 · 二次确认", lines, "请输入> ", cancellationToken).ConfigureAwait(false);
        if (string.Equals(input?.Trim(), "lock", StringComparison.OrdinalIgnoreCase))
            return true;

        Warn("已放弃锁屏（未发送任何命令）");
        return false;
    }

    private string NotConnectedHint() => _device.IsConnected ? "写入失败" : "未连接设备";

    /// <summary>交互式锁屏后等待 <see cref="AutoUnlockDelaySeconds"/> 秒，再发送一帧 <c>(0x36,0x12)</c> 解锁。</summary>
    private async Task AutoUnlockLaterAsync(CancellationToken cancellationToken)
    {
        Terminal.ClearScreen();
        int width = Terminal.Width;
        int line = 0;
        Terminal.AlignWindowToOrigin();
        Terminal.TrySetCursor(0, 0);
        ConsoleTheme.Write(TextWidth.FitLine("==== 锁屏保护 ====", width), ConsoleColor.Yellow);

        void Say(string text, ConsoleColor color)
        {
            int row = 2 + line++;
            if (row < Terminal.Height)
            {
                Terminal.TrySetCursor(0, row);
                ConsoleTheme.Write(TextWidth.FitLine(text, width), color);
            }
        }

        for (int i = AutoUnlockDelaySeconds; i > 0; i--)
        {
            Say($"已锁定，{i} 秒后自动解锁 Android 屏…", ConsoleColor.Gray);
            try
            {
                await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        bool sent = _device.Send(McuOutbound.UnlockScreen());
            Say(sent ? "[OK] 已发送 (0x36,0x12) 解锁 Android 屏" : "[ERR] 自动解锁发送失败：" + NotConnectedHint(),
                sent ? ConsoleColor.Green : ConsoleColor.Red);

        try
        {
            await Task.Delay(1200, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        Terminal.ClearScreen();
        _fallbackPane.Invalidate();
    }

    public async Task ExecuteAsync(McuAction action, string[] args, CancellationToken cancellationToken)
    {
        bool guardedLock = NeedsLockGuard(action);
        if (guardedLock && !await ConfirmLockScreenAsync(cancellationToken).ConfigureAwait(false))
            return;

        try
        {
            var result = await action.ExecuteAsync(_device, args, cancellationToken).ConfigureAwait(false);
            Report(result, cancellationToken);
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

    // ------------------------------------------------------------------ 输出

    /// <summary>命令结果：单行走日志窗，长文本 / 多行走整屏分页。</summary>
    private void Report(string? result, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(result)) return;
        bool multiline = result.Contains('\n') || result.Contains('\r');
        if (multiline || TextWidth.Measure(result) > Math.Max(40, Terminal.Width - 2))
        {
            ShowModal($"命令输出 · {result.Length} 字符", result, cancellationToken);
            return;
        }

        bool error = result.StartsWith("[ERR]", StringComparison.Ordinal);
        bool ok = result.StartsWith("[OK]", StringComparison.Ordinal);
        string text = error || ok ? result[5..].Trim() : result;
        Note(error ? "错误" : "结果", error ? "失败" : ok ? "成功" : "输出", text,
            error ? ConsoleColor.Red : ok ? ConsoleColor.Green : ConsoleColor.Gray);
    }

    private void Warn(string text) => Note("错误", "提示", text, ConsoleColor.Yellow);

    // ------------------------------------------------------------------ 整屏模态

    /// <summary>整屏分页展示长文本（帮助、命令输出），按任意键翻页 / Esc 关闭。</summary>
    private void ShowModal(string title, string body, CancellationToken cancellationToken)
    {
        _modalClosed = false;
        int width = Terminal.Width;
        var lines = (body ?? string.Empty)
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n')
            .Select(l => TextWidth.Clip(l, width))
            .ToList();
        if (lines.Count == 0) lines.Add(string.Empty);

        int height = Terminal.Height;
        int pageRows = Math.Max(1, height - 2);
        int pages = Math.Max(1, (lines.Count + pageRows - 1) / pageRows);

        for (int page = 0; page < pages; page++)
        {
            Terminal.ClearScreen();
            Terminal.AlignWindowToOrigin();
            Terminal.TrySetCursor(0, 0);
            ConsoleTheme.Write(TextWidth.FitLine($"==== {title} · 第 {page + 1}/{pages} 页（空格/回车继续，Esc 关闭） ====", width), ConsoleColor.Cyan);
            for (int i = 0; i < pageRows; i++)
            {
                if (!Terminal.TrySetCursor(0, i + 1)) break;
                int idx = page * pageRows + i;
                ConsoleTheme.Write(TextWidth.FitLine(idx < lines.Count ? lines[idx] : string.Empty, width), ConsoleColor.Gray);
            }
            WaitAnyKey(cancellationToken);
            if (_modalClosed) break;
        }

        Terminal.ClearScreen();
        _fallbackPane.Invalidate();
    }

    private bool _modalClosed;

    /// <summary>等待任意键；输入被重定向或收到取消信号时立即返回。</summary>
    private void WaitAnyKey(CancellationToken cancellationToken)
    {
        if (Console.IsInputRedirected) return;
        while (!cancellationToken.IsCancellationRequested)
        {
            if (!Console.KeyAvailable) { Thread.Sleep(40); continue; }
            _modalClosed = Console.ReadKey(intercept: true).Key == ConsoleKey.Escape;
            return;
        }
    }

    /// <summary>整屏模态里读取一行输入（锁屏二次确认用）。</summary>
    private async Task<string?> PromptModalAsync(string title, IReadOnlyList<string> lines, string prompt, CancellationToken cancellationToken)
    {
        int width = Terminal.Width;
        var rows = new List<ConsoleRow>
        {
            new($"==== {title} ====", ConsoleColor.Yellow),
        };
        foreach (var line in lines) rows.Add(new(line, line.Length == 0 ? ConsoleColor.Gray : ConsoleColor.Yellow));

        Terminal.ClearScreen();
        Terminal.AlignWindowToOrigin();
        // 逐行定位，统一由 FitLine 负责裁剪和补齐。
        for (int i = 0; i < rows.Count; i++)
        {
            if (!Terminal.TrySetCursor(0, i)) break;
            ConsoleTheme.Write(TextWidth.FitLine(rows[i].Text, width), rows[i].Color);
        }
        Terminal.TrySetCursor(0, rows.Count);
        _promptRow = -1;
        // 同样铺满整宽，避免上一次输入的内容留在提示符右边。
        ConsoleTheme.Write(TextWidth.FitLine(prompt, width), ConsoleColor.White);

        if (Console.IsInputRedirected) return Console.ReadLine();

        var buffer = new StringBuilder();
        int limit = Math.Max(1, width - TextWidth.Measure(prompt) - 1);
        while (!cancellationToken.IsCancellationRequested)
        {
            while (Console.KeyAvailable)
            {
                var key = Console.ReadKey(intercept: true);
                if (key.Key == ConsoleKey.Enter)
                {
                    Console.WriteLine();
                    Terminal.ClearScreen();
                    _fallbackPane.Invalidate();
                    return buffer.ToString();
                }
                if (key.Key == ConsoleKey.Escape)
                {
                    ClearInputLine(prompt, buffer);
                    Terminal.ClearScreen();
                    _fallbackPane.Invalidate();
                    return string.Empty;
                }
                if (key.Key == ConsoleKey.Backspace)
                {
                    EraseLast(buffer);
                    continue;
                }
                if (key.KeyChar != '\0' && !char.IsControl(key.KeyChar) && buffer.Length < limit)
                {
                    buffer.Append(key.KeyChar);
                    Console.Write(key.KeyChar);
                }
            }

            try
            {
                await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        Terminal.ClearScreen();
        _fallbackPane.Invalidate();
        return null;
    }

    // ------------------------------------------------------------------ 画面内容

    // Build* 只产出原始文本，裁剪和补齐统一由 Present 处理。

    private List<ConsoleRow> BuildMainScreen()
    {
        var rows = new List<ConsoleRow>
        {
            new($"== 主菜单 ==  {McuActionRegistry.Categories.Count} 个分类 · {McuActionRegistry.AllActions.Count()} 个动作", ConsoleColor.Cyan),
            new("状态  " + StatusSummary(), ConsoleColor.DarkGray),
        };

        int room = Math.Max(1, CurrentLayout(!LogWindowAlive).Room - rows.Count - 1);
        var categories = McuActionRegistry.Categories;
        int show = Math.Min(room, categories.Count);
        for (int i = 0; i < show; i++)
        {
            var c = categories[i];
            rows.Add(new($"  [{i + 1}] {TextWidth.Pad(c.Title, 14)}{c.Hint}", ConsoleColor.Gray));
        }
        if (show < categories.Count)
        {
            rows.Add(new($"  另有 {categories.Count - show} 个分类，放大窗口或直接键入命令名", ConsoleColor.DarkGray));
        }

        rows.Add(new("  [h] 帮助  [d] 握手  [c] 颜色  [q] 退出（关闭任一窗口即退出程序）", ConsoleColor.DarkGray));
        return rows;
    }

    private List<ConsoleRow> BuildCategoryScreen(McuCategory category)
    {
        var rows = new List<ConsoleRow>
        {
            new($"== {category.Title} ==  {category.Hint}", ConsoleColor.Cyan),
            new("状态  " + StatusSummary(), ConsoleColor.DarkGray),
        };

        int room = Math.Max(1, CurrentLayout(!LogWindowAlive).Room - rows.Count - 1);
        int total = category.Actions.Count;
        _scroll = Math.Clamp(_scroll, 0, Math.Max(0, total - room));
        for (int i = _scroll; i < total && i < _scroll + room; i++)
        {
            var a = category.Actions[i];
            string usage = string.IsNullOrEmpty(a.Usage) ? string.Empty : "  " + a.Usage;
            string frame = string.IsNullOrEmpty(a.Frame) ? string.Empty : "  " + a.Frame;
            rows.Add(new($"  [{i + 1}] {TextWidth.Pad(a.Name, 15)}{a.Description}{usage}{frame}", ConsoleColor.Gray));
        }

        rows.Add(total > room
            ? new($"  ↑↓ PgUp PgDn 滚动（第 {_scroll + 1}-{Math.Min(total, _scroll + room)} 项 / 共 {total} 项）", ConsoleColor.DarkGray)
            : new("  [0] 返回  [c] 颜色  [q] 返回主菜单", ConsoleColor.DarkGray));
        return rows;
    }

    /// <summary>退出时关掉独立日志窗口（写端关闭 → 子进程读到 EOF 自行退出）。</summary>
    public void Dispose() => _logWindow.Dispose();

    private string StatusSummary()
    {
        var state = _device.State;
        string link = _device.IsConnected ? "已连接" : "未连接";
        if (_device.UsbId is { } id) link += $" 0x{id.Vid:X4}:0x{id.Pid:X4}";
        string machine = McuKnownDevices.Describe(state.MachineKind);
        string channel = state.CurrentChannel is { } c ? McuCommandNames.DescribeChannel(c) : "-";
        string mode = _device.Link.ConfirmedFrameMode is { } m ? McuDevice.DescribeMode(m) : _device.FrameMode.ToString();
        return $"{link} · {machine} · 通道 {channel} · {mode} · 事件 {LogCount} 条";
    }

    private string HelpText()
    {
        var sb = new StringBuilder();
        sb.AppendLine("TUI 提示：两个独立的终端窗口，同生共死。");
        sb.AppendLine("  本窗口 = 操作台，只管选功能和输入，不显示日志，因此不需要实时重画。");
        sb.AppendLine("  另一个窗口 = 实时日志窗，MCU 上行帧 / PC 下发帧 / 命令结果在那边逐条刷新。");
        sb.AppendLine("  两个窗口可分别缩放、滚动；关闭任意一个都会退出整个程序。");
        sb.AppendLine();
        sb.AppendLine("  日志最多保留 1024 条，超出丢弃最旧的。");
        sb.AppendLine("  方向 / 分类 / 事件 / 详情相同的日志合并为一条，行尾显示 (×n)，并移到末尾。");
        sb.AppendLine();
        sb.AppendLine("  c / color  切换彩色 / 单色（NO_COLOR 环境变量默认关闭颜色）");
        sb.AppendLine("  ↑↓ PgUp PgDn Home End  滚动过长的动作列表");
        sb.AppendLine("  Esc  清空当前输入    q / exit  退出");
        sb.AppendLine();
        sb.AppendLine("命令格式:  <分类> <动作> [参数]     例: audio volume 60");
        sb.AppendLine("          也可直接写动作名        例: mute / channel switch hdmi 1");
        sb.AppendLine();
        foreach (var category in McuActionRegistry.Categories)
        {
            sb.AppendLine($"[{category.Key}] {category.Title}");
            foreach (var a in category.Actions)
            {
                string usage = string.IsNullOrEmpty(a.Usage) ? "" : "  " + a.Usage;
                string frame = string.IsNullOrEmpty(a.Frame) ? "" : "  " + a.Frame;
                sb.AppendLine($"    {TextWidth.Pad(a.Name, 15)}{a.Description}{usage}{frame}");
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
