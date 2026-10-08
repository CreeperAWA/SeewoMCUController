namespace SeewoMCUController.Ui;

/// <summary>
/// 控制台窗口尺寸与光标定位的薄封装。
/// 使用 .NET 控制台 API 管理尺寸、清屏和光标位置，不输出 ANSI 转义序列。
/// </summary>
internal static class Terminal
{
    /// <summary>支持上下双窗布局的最小宽度。</summary>
    public const int MinWidth = 44;

    /// <summary>支持上下双窗布局的最小高度。</summary>
    public const int MinHeight = 12;

    private static int _width = 80;
    private static int _height = 24;

    /// <summary>当前窗口宽度（显示列）。</summary>
    public static int Width => _width;

    /// <summary>当前窗口高度（行）。</summary>
    public static int Height => _height;

    /// <summary>是否可以启用「操作窗 + 日志窗」的上下分屏布局。输出被重定向或窗口过小时为 false。</summary>
    public static bool Split { get; private set; }

    /// <summary>重新读取窗口尺寸。返回尺寸或分屏可用性是否发生变化。</summary>
    public static bool RefreshSize()
    {
        int w = 0, h = 0;
        bool readable = false;
        try
        {
            // 输出被重定向时 WindowWidth 会抛异常，此时退回线性（非分屏）模式。
            if (!Console.IsOutputRedirected)
            {
                w = Console.WindowWidth;
                h = Console.WindowHeight;
                readable = true;
            }
        }
        catch (IOException)
        {
            // 嵌入式 / 重定向宿主拒绝查询尺寸，交给调用方降级处理。
        }
        catch (PlatformNotSupportedException)
        {
        }

        bool split = readable && w >= MinWidth && h >= MinHeight;
        bool changed = w != _width || h != _height || split != Split;
        _width = split ? Math.Min(w, 400) : Math.Max(Math.Min(w > 0 ? w : 80, 400), 20);
        _height = split ? Math.Min(h, 200) : Math.Max(Math.Min(h > 0 ? h : 24, 200), 5);
        Split = split;
        return changed;
    }

    /// <summary>检测到缓冲区滚动时清屏并将光标恢复到原点。</summary>
    public static bool AlignWindowToOrigin()
    {
        if (Console.IsOutputRedirected) return false;
        try
        {
            if (Console.WindowTop == 0 && Console.WindowLeft == 0) return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (PlatformNotSupportedException)
        {
            return false;
        }

        ClearScreen();
        try
        {
            Console.SetCursorPosition(0, 0);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (PlatformNotSupportedException)
        {
            return false;
        }
    }

    /// <summary>把光标移到指定位置；越界或宿主拒绝时返回 false，调用方应忽略失败。</summary>
    public static bool TrySetCursor(int col, int row)
    {
        try
        {
            Console.SetCursorPosition(col, row);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
        catch (IOException)
        {
            // 重定向 / 管道宿主不接受光标定位，调用方据此降级。
            return false;
        }
        catch (PlatformNotSupportedException)
        {
            return false;
        }
    }

    /// <summary>保存当前光标位置，供日志窗重绘后恢复。</summary>
    public static (int Top, int Left) SaveCursor()
    {
        try
        {
            return (Console.CursorTop, Console.CursorLeft);
        }
        catch (IOException)
        {
            return (-1, -1);
        }
        catch (PlatformNotSupportedException)
        {
            return (-1, -1);
        }
    }

    /// <summary>恢复光标位置。</summary>
    public static void RestoreCursor((int Top, int Left) saved)
    {
        if (saved.Top < 0) return;
        TrySetCursor(Math.Clamp(saved.Left, 0, Math.Max(0, Width - 1)), Math.Clamp(saved.Top, 0, Math.Max(0, Height - 1)));
    }

    /// <summary>清屏；宿主拒绝时保持流可用。</summary>
    public static void ClearScreen()
    {
        if (Console.IsOutputRedirected) return;
        try
        {
            Console.Clear();
        }
        catch (IOException)
        {
            // 嵌入式 / 重定向宿主可能拒绝 Clear，忽略即可，输出流仍然可用。
        }
        catch (PlatformNotSupportedException)
        {
        }
    }
}
