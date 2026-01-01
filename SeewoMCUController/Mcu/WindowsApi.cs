using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace SeewoMCUController.Mcu;

internal static class WindowsApi
{
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern SafeFileHandle CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool ReadFile(
        SafeFileHandle hFile,
        byte[] lpBuffer,
        uint nNumberOfBytesToRead,
        ref uint lpNumberOfBytesRead,
        IntPtr lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool WriteFile(
        SafeFileHandle hFile,
        byte[] lpBuffer,
        uint nNumberOfBytesToWrite,
        ref uint lpNumberOfBytesWritten,
        IntPtr lpOverlapped);

    internal const uint GENERIC_READ = 0x80000000;
    internal const uint GENERIC_WRITE = 0x40000000;
    internal const uint FILE_SHARE_READ = 0x00000001;
    internal const uint FILE_SHARE_WRITE = 0x00000002;
    internal const uint OPEN_EXISTING = 3;
    internal const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;
}
