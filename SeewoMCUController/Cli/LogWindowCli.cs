using System.Runtime.InteropServices;
using System.Text;
using SeewoMCUController.Ui;

namespace SeewoMCUController.Cli;

/// <summary>
/// 独立日志窗口进程（子进程侧）：以 <c>--log-window</c> 启动。
/// <para>
/// 父进程用 <c>CREATE_NEW_CONSOLE</c> 创建本进程，conhost 会直接开出一个新的控制台窗口，
/// 这个窗口完全独立于操作台窗口。日志内容从 stdin 管道逐行读入（UTF-8），边读边滚动打印。
/// <para>
/// <b>联动退出：</b>本窗口被关闭（用户点 X 或按 Ctrl+C）时，通过管道回一条
/// <c>#!closed</c> 通知操作台，操作台随即退出 —— 两个窗同生共死。
/// 父进程关闭管道时读到 EOF，本窗口也自行关闭。
/// </para>
/// <para>
/// <b>编码：</b>日志行按 UTF-8 写出，因此把本控制台的输出代码页也设成 UTF-8，
/// 否则控制台会按系统 ANSI 代码页（本机多为 GBK/936）解读这些字节，中文会显示成乱码。
/// </para>
/// </summary>
internal static class LogWindowCli
{
    /// <summary>日志窗被用户关闭时回传给操作台的指令。</summary>
    private const string ClosedNotice = "closed";

    public static int Run()
    {
        // 父进程以 CREATE_NEW_CONSOLE 创建本进程，控制台窗口在创建时就分配好了；
        // 但 STARTF_USESTDHANDLES 接管了标准句柄（stdin 是日志管道），
        // 因此输出要用 CONOUT$ 接回本进程自己的控制台。
        if (LogWindowNative.GetConsoleWindow() == IntPtr.Zero && !LogWindowNative.AllocConsole())
        {
            // 无交互式桌面 / 权限受限，无法呈现日志窗。
            return 1;
        }

        LogWindowNative.SetConsoleTitleW("Seewo MCU Controller · 实时日志窗");
        EnsureOutputHandle();

        // 编码：日志由 Console.Out 按控制台输出代码页写出，所以两边保持一致即可。
        // 控制台默认是系统 ANSI 代码页（本机多为 GBK/936），已能正确显示中文；
        // 切到 UTF-8 只是为了和操作台的管道编码对齐，失败也不影响显示。
        if (LogWindowNative.SetConsoleOutputCP(LogWindowNative.CP_UTF8))
        {
            try
            {
                Console.OutputEncoding = new UTF8Encoding(false);
            }
            catch (IOException)
            {
                // 代码页已切换，这里失败无碍
            }
        }

        // 可视区对齐到缓冲区原点：SetCursorPosition 用的是缓冲区坐标，
        // 输出把缓冲区滚下去后行号会与视觉行错位。
        Terminal.AlignWindowToOrigin();

        var view = new LogWindowView();
        view.RedrawIfResized();   // 先画出列头

        var reader = new LogWindowPipeReader();

        // Ctrl+C / 关闭窗口 / 按下 q：都要让整个程序一起退出。
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            reader.RequestShutdown();
        };
        LogWindowNative.SetConsoleCtrlHandlerFor(LogWindowNative.CTRL_CLOSE_EVENT, () =>
        {
            reader.RequestShutdown();
            return true;
        });

        while (true)
        {
            string? line = reader.ReadLine(TimeSpan.FromMilliseconds(100));
            if (line is null)
            {
                if (reader.ShutdownRequested || reader.PipeClosed) break;
                view.RedrawIfResized();
                continue;
            }

            if (LogWire.IsCommand(line, out var command))
            {
                if (command == LogWire.Quit) break;
                continue;
            }
            if (LogWire.TryDecode(line, out var entry)) view.Append(entry);
        }

        // 本窗退出的原因不需要回传：操作台通过子进程句柄的存活状态判断（LogWindowProcess.IsAlive），
        // 发现日志窗没了就会跟着退出。
        view.WriteFooter();
        return 0;
    }

    /// <summary>
    /// 父进程用 <c>STARTF_USESTDHANDLES</c> 把 stdout/stderr 留空，只接了 stdin 管道。
    /// 这里把 stdout/stderr 接到本进程自己的控制台（<c>CONOUT$</c>），日志才写得到日志窗上。
    /// </summary>
    private static void EnsureOutputHandle()
    {
        if (!LogWindowNative.IsValidFileHandle(LogWindowNative.GetStdHandle(LogWindowNative.STD_OUTPUT_HANDLE)))
        {
            var h = LogWindowNative.CreateFileW(
                "CONOUT$",
                LogWindowNative.GENERIC_WRITE | LogWindowNative.GENERIC_READ,
                LogWindowNative.FILE_SHARE_READ | LogWindowNative.FILE_SHARE_WRITE,
                IntPtr.Zero,
                LogWindowNative.OPEN_EXISTING,
                LogWindowNative.FILE_ATTRIBUTE_NORMAL,
                IntPtr.Zero);
            if (LogWindowNative.IsValidFileHandle(h)) LogWindowNative.SetStdHandle(LogWindowNative.STD_OUTPUT_HANDLE, h);
        }

        if (!LogWindowNative.IsValidFileHandle(LogWindowNative.GetStdHandle(LogWindowNative.STD_ERROR_HANDLE)))
        {
            var h = LogWindowNative.CreateFileW(
                "CONOUT$",
                LogWindowNative.GENERIC_WRITE,
                LogWindowNative.FILE_SHARE_READ | LogWindowNative.FILE_SHARE_WRITE,
                IntPtr.Zero,
                LogWindowNative.OPEN_EXISTING,
                LogWindowNative.FILE_ATTRIBUTE_NORMAL,
                IntPtr.Zero);
            if (LogWindowNative.IsValidFileHandle(h)) LogWindowNative.SetStdHandle(LogWindowNative.STD_ERROR_HANDLE, h);
        }
    }

    /// <summary>
    /// 管道读取端。用后台线程做阻塞读取，主循环只取已完成的结果，
    /// 这样 Ctrl+C / 关闭事件才能在等待期间被处理（否则阻塞在 ReadLine 里收不到信号）。
    /// </summary>
    private sealed class LogWindowPipeReader
    {
        private readonly StreamReader _reader;
        private readonly System.Collections.Concurrent.ConcurrentQueue<string> _lines = new();
        private readonly ManualResetEventSlim _ready = new(false);
        private volatile bool _pipeClosed;
        private volatile bool _shutdown;

        public LogWindowPipeReader()
        {
            var stdin = Console.OpenStandardInput();
            _reader = new StreamReader(stdin, new UTF8Encoding(false));
            var thread = new Thread(ReadLoop) { IsBackground = true, Name = "LogWindowPipe" };
            thread.Start();
        }

        /// <summary>收到退出信号（Ctrl+C / 关闭窗口）。</summary>
        public void RequestShutdown() => _shutdown = true;

        /// <summary>父进程已关闭管道。</summary>
        public bool PipeClosed => _pipeClosed;

        /// <summary>请求退出。</summary>
        public bool ShutdownRequested => _shutdown;

        /// <summary>取一行；暂时没数据则等一小段时间再返回 null。</summary>
        public string? ReadLine(TimeSpan wait)
        {
            if (_lines.TryDequeue(out var line)) return line;
            _ready.Reset();
            if (_lines.TryDequeue(out line)) return line;
            _ready.Wait(wait);
            _lines.TryDequeue(out line);
            return line;
        }

        private void ReadLoop()
        {
            try
            {
                while (!_shutdown)
                {
                    string? line = _reader.ReadLine();
                    if (line is null) break;
                    _lines.Enqueue(line);
                    _ready.Set();
                }
            }
            catch (IOException)
            {
                // 管道断开
            }
            finally
            {
                _pipeClosed = true;
                _ready.Set();
            }
        }
    }

    /// <summary>
    /// 日志窗口本体：整屏表格视图。
    /// <para>
    /// 重复日志由 <see cref="LogTableView"/> 内的聚合器合并成一行 ——
    /// 时间刷新为最新一条、行尾追加 <c>(×n)</c>、位置移到末尾。
    /// 因为要体现「位置变化」，这里是整屏重画而不是往末尾追加。
    /// </para>
    /// </summary>
    private sealed class LogWindowView
    {
        private readonly LogTableView _table = new();

        /// <summary>追加一条日志，随后整屏重画（合并项会就地更新并移位）。</summary>
        public void Append(ConsoleEventLine line)
        {
            _table.Append(line);
            Redraw(force: false);
        }

        /// <summary>窗口尺寸变化后强制重画一次。</summary>
        public void RedrawIfResized()
        {
            int w, h;
            try
            {
                w = Console.WindowWidth;
                h = Console.WindowHeight;
            }
            catch (IOException)
            {
                return;
            }
            if (w < 24 || h < 2) return;
            if (w == _lastWidth && h == _lastHeight) return;
            _table.Invalidate();
            Redraw(force: true);
        }

        private int _lastWidth = -1;
        private int _lastHeight = -1;

        private void Redraw(bool force)
        {
            int w, h;
            try
            {
                w = Console.WindowWidth;
                h = Console.WindowHeight;
            }
            catch (IOException)
            {
                return;
            }
            if (w < 24 || h < 2) return;

            // LogTableView 内部会保存 / 恢复光标，重画不影响外部光标位置。
            _table.Draw(0, h, w, force);
            _lastWidth = w;
            _lastHeight = h;
        }

        /// <summary>退出前清屏，避免表格残影与收尾提示交错。</summary>
        public void WriteFooter()
        {
            try
            {
                Terminal.ClearScreen();
                Console.WriteLine(TextWidth.FitLine("程序已退出，日志窗关闭", Console.WindowWidth));
            }
            catch (IOException)
            {
            }
        }
    }
}
