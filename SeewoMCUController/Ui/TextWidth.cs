namespace SeewoMCUController.Ui;

/// <summary>
/// 控制台文本宽度工具。CJK 字符占两个显示列，直接用 <c>string.Format</c> 的 <c>{0,-n}</c>
/// 会导致中英文混排的表格错位。
/// </summary>
public static class TextWidth
{
    /// <summary>字符串的显示列宽。</summary>
    public static int Measure(string? text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        int width = 0;
        foreach (char c in text) width += IsWide(c) ? 2 : 1;
        return width;
    }

    /// <summary>按显示列宽左对齐补空格。</summary>
    public static string Pad(string? text, int width)
    {
        text ??= string.Empty;
        int current = Measure(text);
        return current >= width ? text : text + new string(' ', width - current);
    }

    /// <summary>按显示列宽截断（超出时尾部用 …）。</summary>
    public static string Truncate(string? text, int width)
    {
        if (text is null) return string.Empty;
        if (Measure(text) <= width) return text;
        var sb = new System.Text.StringBuilder();
        int used = 0;
        foreach (char c in text)
        {
            int w = IsWide(c) ? 2 : 1;
            if (used + w > width - 1) break;
            sb.Append(c);
            used += w;
        }
        return sb.Append('…').ToString();
    }

    private static bool IsWide(char c) =>
        c >= 0x1100
        && (c <= 0x115F || c == 0x2329 || c == 0x232A
            || (c >= 0x2E80 && c <= 0x303E) || (c >= 0x3041 && c <= 0x33FF)
            || (c >= 0x3400 && c <= 0x4DBF) || (c >= 0x4E00 && c <= 0x9FFF)
            || (c >= 0xA000 && c <= 0xA4CF) || (c >= 0xAC00 && c <= 0xD7A3)
            || (c >= 0xF900 && c <= 0xFAFF) || (c >= 0xFE30 && c <= 0xFE6F)
            || (c >= 0xFF00 && c <= 0xFF60) || (c >= 0xFFE0 && c <= 0xFFE6));
}
