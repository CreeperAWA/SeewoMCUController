namespace SeewoMCUController.Ui;

/// <summary>
/// 退化模式下的内嵌日志面板：独立日志窗口开不出来时，日志显示在操作台窗口底部。
/// <para>
/// 事件由 MCU 读线程 / 命令执行线程追加进来（<see cref="Append"/>），
/// 只有 <see cref="Draw"/> 会碰控制台；调用方在两次 Draw 之间随意刷新操作面板。
/// </para>
/// </summary>
internal sealed class ConsoleLogPane
{
    /// <summary>内存中保留的最大条数（与独立日志窗口一致）。</summary>
    private const int MaxLines = LogAggregator.Capacity;

    /// <summary>窗内标题 / 列头 / 分隔线占用的行数。</summary>
    private const int ChromeRows = 3;

    private readonly object _gate = new();
    private readonly List<ConsoleEventLine> _lines = new();
    private long _version;

    private ConsoleRow[] _drawn = System.Array.Empty<ConsoleRow>();
    private int _drawnTop = -1;
    private int _drawnWidth = -1;

    /// <summary>追加事件；追加后版本号自增。</summary>
    public void Append(ConsoleEventLine line)
    {
        lock (_gate)
        {
            _lines.Add(line);
            if (_lines.Count > MaxLines) _lines.RemoveRange(0, _lines.Count - MaxLines);
        }
        Interlocked.Increment(ref _version);
    }

    /// <summary>清空日志。</summary>
    public void Clear()
    {
        lock (_gate) _lines.Clear();
        Interlocked.Increment(ref _version);
    }

    /// <summary>已累计的事件行数。</summary>
    public int Count
    {
        get { lock (_gate) return _lines.Count; }
    }

    /// <summary>内容版本号，用于判断是否需要重画。</summary>
    public long Version => Interlocked.Read(ref _version);

    /// <summary>取一份当前事件快照。</summary>
    public ConsoleEventLine[] Snapshot()
    {
        lock (_gate) return _lines.ToArray();
    }

    /// <summary>
    /// 把日志面板画到从 <paramref name="top"/> 开始的 <paramref name="rows"/> 行。
    /// 内容没变时直接返回；否则只重画变化之后的行，并恢复调用前的光标位置。
    /// </summary>
    public void Draw(int top, int rows, int width, bool force)
    {
        if (rows < ChromeRows || width < 24) return;

        var frame = BuildFrame(rows, width);
        bool geometryChanged = _drawnTop != top || _drawnWidth != width || _drawn.Length != frame.Length;
        if (!force && !geometryChanged)
        {
            int first = 0;
            while (first < frame.Length && SameRow(_drawn[first], frame[first])) first++;
            if (first == frame.Length) return;   // 无变化，不碰控制台
            Paint(top, first, frame, width);
        }
        else
        {
            Paint(top, 0, frame, width);
        }

        _drawn = frame;
        _drawnTop = top;
        _drawnWidth = width;
    }

    /// <summary>几何尺寸变化时调用，丢弃缓存的帧以强制整窗重画。</summary>
    public void Invalidate()
    {
        _drawnTop = -1;
        _drawnWidth = -1;
    }

    private static bool SameRow(ConsoleRow a, ConsoleRow b) =>
        a.Color == b.Color && string.Equals(a.Text, b.Text, StringComparison.Ordinal);

    private void Paint(int top, int first, ConsoleRow[] frame, int width)
    {
        Terminal.AlignWindowToOrigin();

        var saved = Terminal.SaveCursor();
        for (int i = first; i < frame.Length; i++)
        {
            if (!Terminal.TrySetCursor(0, top + i)) break;
            ConsoleTheme.Write(TextWidth.FitLine(frame[i].Text, width), frame[i].Color);
        }
        Terminal.TrySetCursor(0, Math.Min(top + frame.Length - 1, Math.Max(0, Terminal.Height - 1)));
        Terminal.RestoreCursor(saved);
    }

    private ConsoleRow[] BuildFrame(int rows, int width)
    {
        ConsoleEventLine[] snapshot;
        lock (_gate) snapshot = _lines.ToArray();

        int bodyRows = Math.Max(1, rows - ChromeRows);
        var frame = new ConsoleRow[rows];
        for (int i = 0; i < rows; i++) frame[i] = ConsoleRow.Empty;

        frame[0] = new ConsoleRow(
            $"---- 实时日志 · 窗口 {bodyRows} 行 · 共 {snapshot.Length} 条 · 重复项合并为 (×n) · {ConsoleTheme.ColorState} ----",
            ConsoleColor.DarkGray);
        frame[1] = new ConsoleRow(LogColumns.Header(width), ConsoleColor.DarkGray);

        int start = Math.Max(0, snapshot.Length - bodyRows);
        for (int i = 0; i < bodyRows; i++)
        {
            if (start + i >= snapshot.Length) break;
            frame[2 + i] = new ConsoleRow(LogColumns.Format(snapshot[start + i], width), snapshot[start + i].Color);
        }
        if (snapshot.Length == 0)
        {
            frame[2] = new ConsoleRow("（等待 MCU 事件）", ConsoleColor.DarkGray);
        }

        frame[rows - 1] = new ConsoleRow(FooterText(rows), ConsoleColor.DarkGray);
        return frame;
    }

    private static string FooterText(int rows) =>
        rows >= 8 ? "操作窗：序号选择功能 · 日志窗：实时刷新（c 颜色 / h 帮助 / q 退出）" : "c 颜色 · h 帮助 · q 退出";
}
