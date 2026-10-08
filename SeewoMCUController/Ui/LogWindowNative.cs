using System.Runtime.InteropServices;
using System.Text;

namespace SeewoMCUController.Ui;

/// <summary>
/// 「日志独立窗口」两侧所需的 Win32 调用。
/// <para>
/// 结构：一个 Windows 控制台进程只能附着到一个控制台，因此日志窗由
/// <see cref="LogWindowProcess"/> 以 <c>CREATE_NEW_CONSOLE</c> 启动一个自身副本 ——
/// conhost 会为子进程直接开出一个新的控制台窗口，它与操作台窗口互不相干。
/// 父子之间的日志通过匿名管道（子进程的 stdin）传递；
/// 因为 <c>STARTF_USESTDHANDLES</c> 占用了标准句柄，子进程再用 <c>CONOUT$</c>
/// 打开属于自己的控制台输出（<c>CREATE_NEW_CONSOLE</c> 下 conhost 已经分配好控制台）。
/// </para>
/// </summary>
internal static class LogWindowNative
{
    internal const int STARTF_USESTDHANDLES = 0x00000100;
    internal const int CREATE_NEW_CONSOLE = 0x00000010;
    internal const int CREATE_UNICODE_ENVIRONMENT = 0x00000400;
    internal const uint HANDLE_FLAG_INHERIT = 0x00000001;
    internal const uint STILL_ACTIVE = 259;
    internal const uint WAIT_TIMEOUT = 258;

    [StructLayout(LayoutKind.Sequential)]
    internal struct SECURITY_ATTRIBUTES
    {
        internal int nLength;
        internal IntPtr lpSecurityDescriptor;
        internal int bInheritHandle;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct STARTUPINFO
    {
        internal int cb;
        internal string? lpReserved;
        internal string? lpDesktop;
        internal string? lpTitle;
        internal int dwX;
        internal int dwY;
        internal int dwXSize;
        internal int dwYSize;
        internal int dwXChars;
        internal int dwYChars;
        internal int dwFillAttribute;
        internal int dwFlags;
        internal short wShowWindow;
        internal short cbReserved2;
        internal IntPtr lpReserved2;
        internal IntPtr hStdInput;
        internal IntPtr hStdOutput;
        internal IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PROCESS_INFORMATION
    {
        internal IntPtr hProcess;
        internal IntPtr hThread;
        internal int dwProcessId;
        internal int dwThreadId;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CreatePipe(
        out IntPtr hReadPipe, out IntPtr hWritePipe, IntPtr lpPipeAttributes, int nSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetHandleInformation(IntPtr hObject, uint dwMask, uint dwFlags);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CreateProcessW(
        string? lpApplicationName,
        StringBuilder lpCommandLine,
        IntPtr lpProcessAttributes,
        IntPtr lpThreadAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool bInheritHandles,
        int dwCreationFlags,
        IntPtr lpEnvironment,
        string? lpCurrentDirectory,
        ref STARTUPINFO lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern uint WaitForSingleObject(IntPtr hObject, int dwMilliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetExitCodeProcess(IntPtr hProcess, out uint lpExitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool AllocConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool FreeConsole();

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetConsoleTitleW(string lpConsoleTitle);

    [DllImport("kernel32.dll")]
    internal static extern IntPtr GetConsoleWindow();

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern IntPtr CreateFileW(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetStdHandle(int nStdHandle, IntPtr hHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern int GetFileType(IntPtr hFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern int GetFileSize(IntPtr hFile, out int lpFileSizeHigh);

    /// <summary>窗口被关闭（点 X）时收到的事件码。</summary>
    internal const uint CTRL_CLOSE_EVENT = 0x00000001;

    /// <summary>
    /// 注册控制台控制事件回调。<paramref name="callback"/> 返回 true 表示事件已被处理、
    /// 系统不会直接终止进程，便于自己走优雅退出路径。
    /// </summary>
    internal static void SetConsoleCtrlHandlerFor(uint ctrlType, Func<bool> callback) =>
        _ = SetConsoleCtrlHandler((type, _) => type == ctrlType && callback(), true);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetConsoleCtrlHandler(HandlerRoutine handler, [MarshalAs(UnmanagedType.Bool)] bool add);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate bool HandlerRoutine(uint ctrlType, IntPtr reserved);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetConsoleOutputCP(uint wCodePageID);

    /// <summary>UTF-8 代码页。</summary>
    internal const uint CP_UTF8 = 65001;

    internal const uint GENERIC_READ = 0x80000000;
    internal const uint GENERIC_WRITE = 0x40000000;
    internal const uint FILE_SHARE_READ = 0x00000001;
    internal const uint FILE_SHARE_WRITE = 0x00000002;
    internal const uint OPEN_EXISTING = 3;
    internal const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;
    internal const int FILE_TYPE_DISK = 0x0001;
    internal const int INVALID_HANDLE_VALUE = -1;
    internal const int STD_INPUT_HANDLE = -10;
    internal const int STD_OUTPUT_HANDLE = -11;
    internal const int STD_ERROR_HANDLE = -12;

    /// <summary>句柄是否是一个真正可用的磁盘文件 / 管道句柄。</summary>
    internal static bool IsValidFileHandle(IntPtr handle) =>
        handle != IntPtr.Zero
        && handle != new IntPtr(INVALID_HANDLE_VALUE)
        && GetFileType(handle) == FILE_TYPE_DISK;
}
