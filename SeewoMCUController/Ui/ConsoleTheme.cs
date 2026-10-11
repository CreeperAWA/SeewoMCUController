using System.Text;

namespace SeewoMCUController.Ui;

/// <summary>
/// 控制台显示主题。设置 <c>NO_COLOR</c> 或重定向输出时自动关闭颜色。
/// <para>
/// 使用控制台颜色 API，避免直接写入 ANSI 转义序列。
/// <c>ENABLE_VIRTUAL_TERMINAL_PROCESSING</c>，<c>ESC [ 1 m</c> 会被原样打印成
/// <c>□1m</c>、<c>ESC [ 2 2 m</c> 打印成 <c>□22m</c>，在表格单元格里堆出一片方块。
/// 颜色一律走 <see cref="Console.ForegroundColor"/>（Windows 上映射到
/// <c>SetConsoleTextAttribute</c>），在 cmd.exe、Windows Terminal 与重定向输出下都安全。
/// </para>
/// </summary>
public static class ConsoleTheme
{
    private static readonly object Gate = new();

    public static bool Enabled { get; private set; } = !Console.IsOutputRedirected
        && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"));

    public static void ToggleColors()
    {
        lock (Gate) Enabled = !Enabled;
    }

    /// <summary>按颜色输出一段文本（保留换行，剔除终端转义注入用的控制字符）。</summary>
    public static void Write(string? text, ConsoleColor color)
    {
        text = TextWidth.StripControls(text);
        if (!Enabled || text.Length == 0)
        {
            Console.Write(text);
            return;
        }

        var old = Console.ForegroundColor;
        try
        {
            Console.ForegroundColor = color;
            Console.Write(text);
        }
        finally
        {
            Console.ForegroundColor = old;
        }
    }

    /// <summary>按颜色输出一整行（允许文本自带换行）。</summary>
    public static void WriteLine(string? text, ConsoleColor color)
    {
        Write(text, color);
        Console.WriteLine();
    }

    /// <summary>重置到终端默认前景色。</summary>
    public static void Reset() => Console.ResetColor();

    /// <summary>
    /// 把文本按颜色写到指定 writer。独立日志窗口进程的输出流不走 Console.Out，
    /// 需要显式传 writer；颜色仍然通过 ForegroundColor 设置（Windows 上映射到
    /// SetConsoleTextAttribute，cmd.exe 下同样安全）。
    /// </summary>
    public static void WriteTo(TextWriter? writer, string? text, ConsoleColor color)
    {
        if (writer is null) return;
        text = TextWidth.Flatten(text);
        if (text.Length == 0 || !Enabled)
        {
            writer.Write(text);
            return;
        }

        var old = Console.ForegroundColor;
        try
        {
            Console.ForegroundColor = color;
            writer.Write(text);
        }
        finally
        {
            Console.ForegroundColor = old;
        }
    }

    public static string ColorState => Enabled ? "彩色" : "单色";
}
