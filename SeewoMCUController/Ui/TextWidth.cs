using System.Globalization;
using System.Text;

namespace SeewoMCUController.Ui;

/// <summary>
/// 控制台文本宽度工具。CJK 字符占两个显示列，直接用 <c>string.Format</c> 的 <c>{0,-n}</c>
/// 会导致中英文混排的表格错位。
/// <para>
/// 同时负责把文本里的控制字符换成可见占位符：MCU 负载按 UTF-8 直接解码，
/// 字节 <c>0x1B</c> 会解出真正的 ESC，若原样打印就是一段终端转义序列
/// （cmd.exe 下表现为 <c>□1m</c>、<c>□22m</c> 之类的方块乱码）。
/// </para>
/// </summary>
public static class TextWidth
{
    /// <summary>控制字符的可见占位符。</summary>
    private const char ControlPlaceholder = '·';

    /// <summary>字符串的显示列宽（忽略组合符号，代理对按其码位宽度计算）。</summary>
    public static int Measure(string? text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        int width = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            width += RuneWidth(rune);
        }
        return width;
    }

    /// <summary>按显示列宽左对齐补空格；不截断。</summary>
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
        if (width <= 0) return string.Empty;
        if (Measure(text) <= width) return text;
        var sb = new StringBuilder();
        int used = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            int w = RuneWidth(rune);
            if (used + w > width - 1) break;
            sb.Append(rune.ToString());
            used += w;
        }
        return sb.Append('…').ToString();
    }

    /// <summary>
    /// 单行化 + 截断到指定显示列宽：控制字符换成 <c>·</c>，换行折叠成空格，
    /// 超出时尾部用 <c>…</c>。所有写入控制台的表格文本都应先过这一道。
    /// </summary>
    public static string Clip(string? text, int width)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        if (width <= 0) return string.Empty;
        return Truncate(Flatten(text), width);
    }

    /// <summary>把 <paramref name="text"/> 处理成恰好 <paramref name="width"/> 个显示列宽的一行（不足补空格）。</summary>
    public static string Fit(string? text, int width)
    {
        if (width <= 0) return string.Empty;
        string clipped = Clip(text, width);
        return Pad(clipped, width);
    }

    /// <summary>
    /// 整屏铺满一行时用。
    /// <para>
    /// 两件事：单行化 + 裁到 <paramref name="width"/> 列，末尾补空格使总宽恰好等于
    /// <paramref name="width"/>，且<b>最后一格必定是空格</b>。
    /// </para>
    /// 最后一格保留为空格，避免整宽输出触发自动换行。
    /// </summary>
    public static string FitLine(string? text, int width)
    {
        if (width <= 0) return string.Empty;
        if (width == 1) return " ";
        return Pad(Clip(text, width - 1), width);
    }

    /// <summary>
    /// 单行铺满一行，<b>不产生省略号</b>：超出部分直接截掉，不留 <c>…</c>。
    /// 菜单 / 状态这类本来就可能超宽的行用它，避免每行末尾挂一个省略号。
    /// </summary>
    public static string PadLine(string? text, int width)
    {
        if (width <= 0) return string.Empty;
        return Pad(TruncateFlat(text, width), width);
    }

    /// <summary>单行化后按显示列硬截断，不加省略号。</summary>
    private static string TruncateFlat(string? text, int width)
    {
        string flat = Flatten(text);
        if (Measure(flat) <= width) return flat;
        var sb = new StringBuilder();
        int used = 0;
        foreach (var rune in flat.EnumerateRunes())
        {
            int w = RuneWidth(rune);
            if (used + w > width) break;
            sb.Append(rune.ToString());
            used += w;
        }
        return sb.ToString();
    }

    /// <summary>
    /// 去掉控制字符：<c>\n</c>/<c>\r</c>/<c>\t</c> 折叠成空格，
    /// 其余 C0/C1 控制符与 DEL 换成 <c>·</c>，从而无法注入终端转义序列。
    /// </summary>
    public static string Flatten(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        var sb = new StringBuilder(text.Length);
        foreach (var rune in text.EnumerateRunes())
        {
            int v = rune.Value;
            if (v is '\n' or '\r' or '\t' or '\v' or '\f')
            {
                sb.Append(' ');
            }
            else if (v < 0x20 || v == 0x7F || (v >= 0x80 && v <= 0x9F))
            {
                sb.Append(ControlPlaceholder);
            }
            else
            {
                sb.Append(rune.ToString());
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// 保留 <c>\n</c>/<c>\t</c>（多行结果要靠它们换行），把其余 C0/C1 控制符与 DEL
    /// 换成 <c>·</c>。用于可能含设备负载的多行文本，避免 <c>0x1B</c> 变成终端转义序列。
    /// </summary>
    public static string StripControls(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        // 绝大多数文本本来就干净，先扫一遍避免每次输出都重建字符串。
        bool dirty = false;
        foreach (var rune in text.EnumerateRunes())
        {
            int v = rune.Value;
            if (v == '\n' || v == '\t') continue;
            if (v < 0x20 || v == 0x7F || (v >= 0x80 && v <= 0x9F)) { dirty = true; break; }
        }
        if (!dirty) return text;

        var sb = new StringBuilder(text.Length);
        foreach (var rune in text.EnumerateRunes())
        {
            int v = rune.Value;
            if (v is '\n' or '\t')
            {
                sb.Append((char)v);
            }
            else if (v < 0x20 || v == 0x7F || (v >= 0x80 && v <= 0x9F))
            {
                sb.Append(ControlPlaceholder);
            }
            else
            {
                sb.Append(rune.ToString());
            }
        }
        return sb.ToString();
    }

    private static int RuneWidth(Rune rune)
    {
        var category = Rune.GetUnicodeCategory(rune);
        if (category is UnicodeCategory.NonSpacingMark
            or UnicodeCategory.EnclosingMark
            or UnicodeCategory.Format
            or UnicodeCategory.Control)
        {
            return 0;
        }

        int v = rune.Value;
        if (v < 0x10000) return IsWideBmp((char)v) ? 2 : 1;
        // 非 BMP：CJK 扩展 B 及常见 emoji 占两列，其余按一列算。
        return v is (>= 0x20000 and <= 0x3FFFD) or (>= 0x1F300 and <= 0x1FAFF) ? 2 : 1;
    }

    private static bool IsWideBmp(char c) =>
        c >= 0x1100
        && (c <= 0x115F || c == 0x2329 || c == 0x232A
            || (c >= 0x2E80 && c <= 0x303E) || (c >= 0x3041 && c <= 0x33FF)
            || (c >= 0x3400 && c <= 0x4DBF) || (c >= 0x4E00 && c <= 0x9FFF)
            || (c >= 0xA000 && c <= 0xA4CF) || (c >= 0xAC00 && c <= 0xD7A3)
            || (c >= 0xF900 && c <= 0xFAFF) || (c >= 0xFE30 && c <= 0xFE6F)
            || (c >= 0xFF00 && c <= 0xFF60) || (c >= 0xFFE0 && c <= 0xFFE6));
}
