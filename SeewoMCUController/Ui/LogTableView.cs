namespace SeewoMCUController.Ui;

/// <summary>
/// 日志表格视图：把 <see cref="LogAggregator"/> 的内容整屏重画到指定区域。
/// <para>
/// 重复项由聚合器合并成一行（时间刷新、行尾 <c>(×n)</c>、位置移到末尾），
/// 所以这里每次都按聚合结果整屏重画，而不是往屏幕末尾追加 —— 追加无法体现
/// 「已有条目被合并后移动到最新位置」。
/// </para>
/// <para>
/// 重画时会算出与上一帧的差异，只重写变化之后的行，避免整屏闪烁。
/// </para>
/// </summary>
internal sealed class LogTableView
{
    private readonly LogAggregator _aggregator;

    private ConsoleRow[] _drawn = System.Array.Empty<ConsoleRow>();
    private int _drawnTop = -1;
    private int _drawnWidth = -1;

    public LogTableView() : this(new LogAggregator())
    {
    }

    public LogTableView(LogAggregator aggregator) => _aggregator = aggregator;

    /// <summary>追加一条日志（内部先合并重复项）。</summary>
    public void Append(ConsoleEventLine line) => _aggregator.Append(line);

    /// <summary>内容版本号。</summary>
    public long Version => _aggregator.Version;

    /// <summary>当前条数。</summary>
    public int Count => _aggregator.Count;

    /// <summary>清空。</summary>
    public void Clear() => _aggregator.Clear();

    /// <summary>丢弃缓存帧，强制整屏重画。</summary>
    public void Invalidate()
    {
        _drawnTop = -1;
        _drawnWidth = -1;
    }

    /// <summary>
    /// 画到从 <paramref name="top"/> 开始的 <paramref name="rows"/> 行。
    /// 内容没变时直接返回，不碰控制台。
    /// </summary>
    public void Draw(int top, int rows, int width, bool force)
    {
        if (rows < 1 || width < 24) return;

        var frame = BuildFrame(rows, width);
        bool geometryChanged = _drawnTop != top || _drawnWidth != width || _drawn.Length != frame.Length;

        int first = 0;
        if (!force && !geometryChanged)
        {
            while (first < frame.Length && SameRow(_drawn[first], frame[first])) first++;
            if (first == frame.Length) return;   // 无变化
        }

        Paint(top, first, frame, width);
        _drawn = frame;
        _drawnTop = top;
        _drawnWidth = width;
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

    /// <summary>第 0 行是列头，其余行是日志（最新的一条在最末尾）。</summary>
    private ConsoleRow[] BuildFrame(int rows, int width)
    {
        var snapshot = _aggregator.Snapshot();
        var frame = new ConsoleRow[rows];

        frame[0] = new ConsoleRow(LogColumns.Header(width), ConsoleColor.DarkGray);

        int bodyRows = rows - 1;
        int start = Math.Max(0, snapshot.Length - bodyRows);
        for (int i = 0; i < bodyRows; i++)
        {
            int idx = start + i;
            frame[i + 1] = idx < snapshot.Length
                ? new ConsoleRow(LogColumns.Format(snapshot[idx], width), snapshot[idx].Color)
                : ConsoleRow.Empty;
        }
        return frame;
    }

    /// <summary>把所有行裁剪到窗口宽度并补满，供截屏 / 自检核对排版。</summary>
    public string[] RenderLines(int rows, int width)
    {
        var frame = BuildFrame(rows, width);
        var lines = new string[frame.Length];
        for (int i = 0; i < frame.Length; i++) lines[i] = TextWidth.PadLine(frame[i].Text, width);
        return lines;
    }
}
