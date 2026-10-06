namespace SeewoMCUController.Ui;

/// <summary>控制台显示主题。设置 <c>NO_COLOR</c> 或重定向输出时自动关闭颜色。</summary>
public static class ConsoleTheme
{
    public static bool Enabled { get; private set; } = !Console.IsOutputRedirected
        && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"));

    public static void ToggleColors() => Enabled = !Enabled;

    public static void Write(string text, ConsoleColor color, bool bold = false)
    {
        if (!Enabled)
        {
            Console.Write(text);
            return;
        }

        var old = Console.ForegroundColor;
        try
        {
            Console.ForegroundColor = color;
            if (bold) Console.Write("\x1b[1m");
            Console.Write(text);
            if (bold) Console.Write("\x1b[22m");
        }
        finally
        {
            Console.ForegroundColor = old;
        }
    }

    public static void WriteLine(string text, ConsoleColor color, bool bold = false)
    {
        Write(text, color, bold);
        Console.WriteLine();
    }

    public static string ColorState => Enabled ? "彩色" : "单色";
}
