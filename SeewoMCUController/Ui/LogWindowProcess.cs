using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace SeewoMCUController.Ui;

/// <summary>
/// 独立实时日志窗口（父进程侧）。
/// <para>
/// 日志窗是本程序自身的一个副本进程：父进程用 <c>CREATE_NEW_CONSOLE</c> 创建它，
/// conhost 于是为它单独开出一个控制台窗口 —— 与操作台窗口互不相干，
/// 可以分别缩放、滚动、关闭。日志行通过匿名管道（子进程的 stdin）喂进去。
/// </para>
/// </summary>
internal sealed class LogWindowProcess : IDisposable
{
    private readonly object _gate = new();
    private readonly object _writeGate = new();
    private IntPtr _writePipe = IntPtr.Zero;
    private IntPtr _process = IntPtr.Zero;
    private int _pid;
    private StreamWriter? _writer;
    private bool _disposed;

    /// <summary>
    /// 日志窗口进程是否仍在运行。
    /// </summary>
    public bool IsAlive
    {
        get
        {
            lock (_gate)
            {
                if (_disposed || _process == IntPtr.Zero) return false;
                return LogWindowNative.WaitForSingleObject(_process, 0) == LogWindowNative.WAIT_TIMEOUT;
            }
        }
    }

    /// <summary>
    /// 尝试打开一个独立的日志窗口。失败返回 false，调用方应退回内嵌日志面板。
    /// </summary>
    public bool TryOpen()
    {
        lock (_gate)
        {
            if (_disposed) return false;
            if (IsAlive) return true;

            string exe = Environment.ProcessPath ?? string.Empty;
            if (exe.Length == 0 || !File.Exists(exe)) return false;

            IntPtr readPipe = IntPtr.Zero;
            IntPtr writePipe = IntPtr.Zero;
            try
            {
                var sa = new LogWindowNative.SECURITY_ATTRIBUTES
                {
                    nLength = Marshal.SizeOf<LogWindowNative.SECURITY_ATTRIBUTES>(),
                    bInheritHandle = 1,
                };
                int size = Marshal.SizeOf<LogWindowNative.SECURITY_ATTRIBUTES>();
                IntPtr saPtr = Marshal.AllocHGlobal(size);
                try
                {
                    Marshal.StructureToPtr(sa, saPtr, false);
                    if (!LogWindowNative.CreatePipe(out readPipe, out writePipe, saPtr, 0)) return false;
                }
                finally
                {
                    Marshal.FreeHGlobal(saPtr);
                }

                // 写端必须不可继承，否则子进程自己也持有写端，永远等不到 EOF。
                LogWindowNative.SetHandleInformation(writePipe, LogWindowNative.HANDLE_FLAG_INHERIT, 0);

                var startup = new LogWindowNative.STARTUPINFO
                {
                    cb = Marshal.SizeOf<LogWindowNative.STARTUPINFO>(),
                    dwFlags = LogWindowNative.STARTF_USESTDHANDLES,
                    hStdInput = readPipe,
                    hStdOutput = IntPtr.Zero,
                    hStdError = IntPtr.Zero,
                };

                // 引号包住 exe 路径，兼容 Program Files 这类带空格的目录。
                var cmd = new StringBuilder();
                cmd.Append('"').Append(exe).Append('"').Append(" --log-window");

                // CREATE_NEW_CONSOLE：conhost 直接为子进程开出独立控制台窗口。
                // 它和 CREATE_NO_WINDOW + AllocConsole 的区别是窗口由创建时就分配好，
                // 不依赖子进程能否再分配控制台（后者在受限环境下会 ACCESS_DENIED）。
                if (!LogWindowNative.CreateProcessW(
                        exe, cmd, IntPtr.Zero, IntPtr.Zero, true,
                        LogWindowNative.CREATE_NEW_CONSOLE | LogWindowNative.CREATE_UNICODE_ENVIRONMENT,
                        IntPtr.Zero, null, ref startup, out var pi))
                {
                    return false;
                }

                _process = pi.hProcess;
                _pid = pi.dwProcessId;
                LogWindowNative.CloseHandle(pi.hThread);
                _writePipe = writePipe;
                writePipe = IntPtr.Zero;

                // FileStream 不能直接接管 Win32 管道句柄，用 SafeFileHandle 交接所有权。
                var safe = new SafeFileHandle(_writePipe, ownsHandle: true);
                _writePipe = IntPtr.Zero;
                _writer = new StreamWriter(new FileStream(safe, FileAccess.Write, 4096, isAsync: false), new UTF8Encoding(false))
                {
                    AutoFlush = true,
                };
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (readPipe != IntPtr.Zero) LogWindowNative.CloseHandle(readPipe);
                if (writePipe != IntPtr.Zero) LogWindowNative.CloseHandle(writePipe);
            }
        }
    }

    /// <summary>写一行日志；写失败（窗口被关掉）返回 false。</summary>
    public bool Send(string payload)
    {
        StreamWriter? writer;
        lock (_gate)
        {
            if (_disposed || _writer is null) return false;
            writer = _writer;
        }

        try
        {
            lock (_writeGate) writer.WriteLine(payload);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }

    /// <summary>向日志窗发一条控制指令（如 <see cref="LogWire.Quit"/>）。</summary>
    public bool Command(string command)
    {
        StreamWriter? writer;
        lock (_gate)
        {
            if (_disposed || _writer is null) return false;
            writer = _writer;
        }

        try
        {
            lock (_writeGate) writer.WriteLine(LogWire.CommandPrefix + command);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        StreamWriter? writer;
        IntPtr process;
        int pid;

        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            writer = _writer;
            _writer = null;
            process = _process;
            _process = IntPtr.Zero;
            pid = _pid;
            _writePipe = IntPtr.Zero;
        }

        lock (_writeGate)
        {
            try
            {
                writer?.Dispose();
            }
            catch (IOException)
            {
            }
        }

        if (process != IntPtr.Zero)
        {
            // 关闭 stdin 已让子进程读到 EOF；这里只做兜底。
            if (LogWindowNative.WaitForSingleObject(process, 300) == LogWindowNative.WAIT_TIMEOUT)
            {
                try
                {
                    Process.GetProcessById(pid).Kill();
                }
                catch (Exception)
                {
                }
            }
            LogWindowNative.CloseHandle(process);
        }
    }
}
