namespace SeewoMCUController.Ui;

/// <summary>
/// 日志聚合器：合并重复日志并限制内存占用。
/// <para>
/// <b>合并规则：</b>方向 / 分类 / 事件 / 详情四项完全相同（时间不参与比较）的日志
/// 视为同一条，计数 +1、时间更新为最新一条，行尾显示 <c>(×n)</c>。
/// 每次合并都会把这条日志移到列表末尾，因此反复出现的日志始终停留在最新位置。
/// </para>
/// <para>
/// <b>容量：</b>最多保留 <see cref="Capacity"/> 条，超过后丢弃最旧的一条
/// （连带它在索引里的位置），避免长时间运行时内存无限增长。
/// </para>
/// </summary>
internal sealed class LogAggregator
{
    /// <summary>保留的最大条数。</summary>
    public const int Capacity = 1024;

    private readonly object _gate = new();

    /// <summary>按合并键索引到列表节点，便于 O(1) 定位、移除并重新追加到末尾。</summary>
    private readonly Dictionary<string, LinkedListNode<ConsoleEventLine>> _index = new(StringComparer.Ordinal);

    /// <summary>顺序即显示顺序，队首最旧、队尾最新。</summary>
    private readonly LinkedList<ConsoleEventLine> _order = new();

    /// <summary>记录被合并的次数，用于行尾 <c>(×n)</c>。</summary>
    private readonly Dictionary<string, int> _counts = new(StringComparer.Ordinal);

    /// <summary>当前保留的条数。</summary>
    public int Count
    {
        get { lock (_gate) return _order.Count; }
    }

    private long _version;

    /// <summary>内容版本号，每次追加 / 合并后自增，供视图判断是否需要重画。</summary>
    public long Version => Interlocked.Read(ref _version);

    /// <summary>
    /// 追加一条日志，返回合并后的结果：<c>result.Line</c> 是最终显示内容，
    /// <c>result.IsNew</c> 表示这是首次出现（无需刷新既有行）。
    /// </summary>
    public (ConsoleEventLine Line, bool IsNew) Append(ConsoleEventLine line)
    {
        lock (_gate)
        {
            string key = KeyOf(line);

            if (_index.TryGetValue(key, out var existing))
            {
                // 同一条又来了：计数 +1、时间刷新、移到末尾。
                _counts[key] = _counts.TryGetValue(key, out int n) ? n + 1 : 2;
                _order.Remove(existing);
                var merged = line with { Count = _counts[key] };
                _index[key] = _order.AddLast(merged);
                Interlocked.Increment(ref _version);
                return (merged, false);
            }

            if (_order.Count >= Capacity)
            {
                // 满了就淘汰最旧的一条（连带它的计数与索引）。
                var oldest = _order.First;
                if (oldest is not null)
                {
                    _order.RemoveFirst();
                    string oldestKey = KeyOf(oldest.Value);
                    _index.Remove(oldestKey);
                    _counts.Remove(oldestKey);
                }
            }

            _counts[key] = 1;
            _index[key] = _order.AddLast(line);
            Interlocked.Increment(ref _version);
            return (line, true);
        }
    }

    /// <summary>取一份当前保留的日志快照（按显示顺序，最旧在前）。</summary>
    public ConsoleEventLine[] Snapshot()
    {
        lock (_gate) return _order.ToArray();
    }

    /// <summary>清空所有缓冲。</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _order.Clear();
            _index.Clear();
            _counts.Clear();
        }
        Interlocked.Increment(ref _version);
    }

    /// <summary>合并键：方向 / 分类 / 事件 / 详情（时间不参与，所以不同时刻的重复日志也会合并）。</summary>
    private static string KeyOf(ConsoleEventLine line) =>
        string.Join('\u0001', line.Direction, line.Category, line.Event, line.Detail);
}
