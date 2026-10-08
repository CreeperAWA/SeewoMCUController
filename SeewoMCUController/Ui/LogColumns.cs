namespace SeewoMCUController.Ui;

/// <summary>
/// 日志表格的列宽分配与行格式化，格式为
/// <c>时间 | 方向 | 分类 | 事件 | 详情</c>（行尾带 <c>(×n)</c> 合并计数）。
/// 操作台内嵌面板与独立日志窗口共用，保证两处排版一致。
/// </summary>
internal static class LogColumns
{
    private const int TimeWidth = 8;
    private const int DirWidth = 2;
    private const int CatWidth = 10;
    private const int Sep = 3;   // " | "

    /// <summary>「事件」「详情」两列的列宽。</summary>
    public static (int Event, int Detail) Widths(int width)
    {
        int fixedCols = TimeWidth + DirWidth + CatWidth + Sep * 4;
        int rest = Math.Max(16, width - fixedCols);
        int ev = Math.Clamp(rest / 3, 8, 22);
        return (ev, Math.Max(8, rest - ev));
    }

    /// <summary>列头。</summary>
    public static string Header(int width)
    {
        var (ev, _) = Widths(width);
        return $"{TextWidth.Pad("时间", TimeWidth)} |{TextWidth.Pad("方向", DirWidth)} "
             + $"|{TextWidth.Pad("分类", CatWidth)} |{TextWidth.Pad("事件", ev)} | 详情";
    }

    /// <summary>一条日志的整行文本（末尾带合并计数）。</summary>
    public static string Format(ConsoleEventLine line, int width)
    {
        var (ev, detail) = Widths(width);
        string count = line.Merged ? $" (×{line.Count})" : string.Empty;
        return $"{line.Timestamp:HH:mm:ss} |{TextWidth.Fit(TextWidth.Clip(line.Direction, DirWidth), DirWidth)} "
             + $"|{TextWidth.Fit(TextWidth.Clip(line.Category, CatWidth), CatWidth)} "
             + $"|{TextWidth.Fit(TextWidth.Clip(line.Event, ev), ev)} "
             + $"| {TextWidth.Clip(line.Detail + count, detail)}";
    }
}
