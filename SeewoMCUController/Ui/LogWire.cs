using System.Globalization;

namespace SeewoMCUController.Ui;

/// <summary>
/// 操作台进程与独立日志窗口进程之间的行协议：制表符分隔的字段，纯 ASCII 结构 + UTF-8 文本。
/// <para>
/// 结构：<c>时间 ⟶ 方向 ⟶ 分类 ⟶ 事件 ⟶ 详情 ⟶ 颜色 ⟶ 合并计数</c>。
/// 前缀 <c>#!</c> 表示控制指令（如 <c>#!quit</c>，日志窗口据此让整个程序退出）。
/// </para>
/// </summary>
internal static class LogWire
{
    internal const char Field = '\t';
    internal const string CommandPrefix = "#!";

    /// <summary>让日志窗口与操作台一起退出的指令。</summary>
    public const string Quit = "quit";

    /// <summary>
    /// 把一条日志编码成一行。字段一律走 <see cref="TextWidth.Flatten"/>：
    /// 制表符和换行折成空格，控制字符换成 <c>·</c>，既不会拆行也无法注入转义序列。
    /// </summary>
    public static string Encode(ConsoleEventLine line) =>
        string.Join(Field, new[]
        {
            line.Timestamp.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            TextWidth.Flatten(line.Direction),
            TextWidth.Flatten(line.Category),
            TextWidth.Flatten(line.Event),
            TextWidth.Flatten(line.Detail),
            line.Color.ToString(),
            line.Count.ToString(CultureInfo.InvariantCulture),
        });

    /// <summary>解码一行日志。</summary>
    public static bool TryDecode(string payload, out ConsoleEventLine line)
    {
        line = default!;
        var parts = payload.Split(Field);
        if (parts.Length != 7) return false;
        if (!TimeSpan.TryParseExact(parts[0], @"hh\:mm\:ss", CultureInfo.InvariantCulture, out var t)) return false;
        var color = Enum.TryParse<ConsoleColor>(parts[5], ignoreCase: true, out var c) ? c : ConsoleColor.Gray;
        int count = int.TryParse(parts[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) && n > 1 ? n : 1;
        line = new ConsoleEventLine(
            DateTime.Today.Add(t),
            parts[1],
            parts[2],
            parts[3],
            parts[4],
            color)
        {
            Count = count,
        };
        return true;
    }

    /// <summary>判断是否为控制指令。</summary>
    public static bool IsCommand(string line, out string command)
    {
        command = string.Empty;
        if (!line.StartsWith(CommandPrefix, StringComparison.Ordinal)) return false;
        command = line[CommandPrefix.Length..];
        return true;
    }
}
