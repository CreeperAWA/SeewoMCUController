using SeewoMCUController.Mcu;
using SeewoMCUController.Ui;

namespace SeewoMCUController.Cli;

public static partial class SelfTest
{
    private static int LockConfirmTextChecks()
    {
        int failed = 0;
        void Line(string name, bool ok, string extra = "")
        {
            if (!ok) failed++;
            Console.WriteLine($"  {(ok ? "[OK]" : "[ERR]")} {name,-40}{extra}");
        }

        using var device = new McuDevice();
        var unset = string.Join("\n", ConsoleMenu.BuildLockConfirmLines(device));
        Line("未设置口令 → 文案警示任何口令都能解锁",
            unset.Contains("尚未设置锁屏口令", StringComparison.Ordinal)
            && unset.Contains("输入任何口令都能解锁", StringComparison.Ordinal));
        Line("未设置口令 → 提示先设置口令",
            unset.Contains("set-password", StringComparison.Ordinal));
        Line("未设置口令 → 不出现「口令已启用」",
            !unset.Contains("口令已启用", StringComparison.Ordinal));
        Line("始终要求输入 lock 才继续",
            unset.Contains("输入 lock", StringComparison.Ordinal));

        Line("未设置口令 → 提示 10 秒后自动解锁",
            unset.Contains("秒后自动解锁", StringComparison.Ordinal));

        device.LockPassword.Set("Abc123");
        var set = string.Join("\n", ConsoleMenu.BuildLockConfirmLines(device));
        Line("已设置口令 → 文案显示口令已启用与位数",
            set.Contains("口令已启用", StringComparison.Ordinal) && set.Contains("6 位", StringComparison.Ordinal));
        Line("已设置口令 → 不再警示任何口令都能解锁",
            !set.Contains("尚未设置锁屏口令", StringComparison.Ordinal));
        Line("已设置口令 → 不再承诺自动解锁",
            !set.Contains("秒后自动解锁", StringComparison.Ordinal)
            && set.Contains("需在 Android 上输入口令解锁", StringComparison.Ordinal));

        foreach (var l in ConsoleMenu.BuildLockConfirmLines(device))
            Console.WriteLine($"    {l}");
        return failed;
    }
}
