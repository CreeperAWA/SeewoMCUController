using System.Text;
using SeewoMCUController.Commands;
using SeewoMCUController.Mcu;
using SeewoMCUController.Mcu.Protocol;

namespace SeewoMCUController.Cli;

public static partial class SelfTest
{
    private sealed record Case(string Name, McuFrame Frame, Action<McuFrame>? Verify = null);

    public static int Run()
    {
        var cases = BuildCases();
        int failed = 0;

        Console.WriteLine("==============================================================");
        Console.WriteLine("  MCU 帧构造自检");
        Console.WriteLine("==============================================================");

        foreach (var c in cases)
        {
            string? error = null;
            try
            {
                c.Verify?.Invoke(c.Frame);
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }

            bool ok = error is null;
            if (!ok) failed++;
            Console.WriteLine($"{(ok ? "[OK]" : "[ERR]")} {c.Name,-34} {c.Frame}");
            if (!ok) Console.WriteLine($"    断言失败: {error}");
        }

        Console.WriteLine();
        Console.WriteLine("---- 封装对照（同一业务帧的两种线上形态）----");
        var sample = McuOutbound.Handshake();
        var rawWire = sample.ToTransport(FrameMode.Raw);
        var framedWire = sample.ToTransport(FrameMode.Framed);
        Console.WriteLine($"  业务帧 (64B)      : {sample.ToHex()}");
        Console.WriteLine($"  Mode A 无头 (64B) : {Hex(rawWire)}");
        Console.WriteLine($"  Mode B 带头 (64B) : {Hex(framedWire)}");
        Console.WriteLine("  说明：Mode B 先把业务帧右移 5 字节，再在线首写入 FC A5 05 3B 00；");
        Console.WriteLine("        SealFrame 写进 [63] 的校验和会被右移到 [58]，因此带头模式下不存在帧尾校验。");

        var framedOk = framedWire[0] == 0xFC && framedWire[1] == 0xA5 && framedWire[2] == 0x05 && framedWire[3] == 0x3B
            && framedWire[4] == 0x00 && framedWire[5] == 0xFB && framedWire[6] == 0x50 && framedWire[7] == 0x00;
        var rawOk = rawWire[0] == 0xFB && rawWire[1] == 0x50 && rawWire[63] == 0x4B;
        Console.WriteLine($"  {(framedOk ? "[OK]" : "[ERR]")} Mode B 偏移断言（线上 [5]=FB [6]=MAIN [7]=SUB [8]=len）");
        Console.WriteLine($"  {(rawOk ? "[OK]" : "[ERR]")} Mode A 断言（[0]=FB，帧尾校验和 = 0xFB+0x50 = 0x4B）");
        if (!framedOk || !rawOk) failed++;

        Console.WriteLine();
        Console.WriteLine("---- 时间同步帧 (0x15) 逐字节 ----");        var t = McuOutbound.SyncTime(new DateTime(2026, 10, 6, 14, 30, 45));
        Console.WriteLine($"  f[1..4] = {t.Buffer[1]:X2} {t.Buffer[2]:X2} {t.Buffer[3]:X2} {t.Buffer[4]:X2}   (MAIN=15 SUB=00 len=07)");
        Console.WriteLine($"  f[5..11]= {t.Buffer[5]:X2} {t.Buffer[6]:X2} {t.Buffer[7]:X2} {t.Buffer[8]:X2} {t.Buffer[9]:X2} {t.Buffer[10]:X2} {t.Buffer[11]:X2}   (年高 年低 月 日 时 分 秒)");
        Expect(t.Buffer[1] == 0x15 && t.Buffer[2] == 0x00 && t.Buffer[3] == 0x07, "时间帧头部必须是 15 00 07 00（SUB=0x00，0x07 是负载长度）");
        Expect(t.Buffer[5] == 0x07 && t.Buffer[6] == 0xEA, "2026 年应为 07 EA");
        Expect(t.Buffer[7] == 10 && t.Buffer[8] == 6 && t.Buffer[9] == 14 && t.Buffer[10] == 30 && t.Buffer[11] == 45, "月/日/时/分/秒 应为 10/06/14/30/45");
        Console.WriteLine($"  {(t.Buffer[1] == 0x15 && t.Buffer[2] == 0x00 && t.Buffer[3] == 0x07 ? "[OK]" : "[ERR]")} 头部断言通过");

        Console.WriteLine();
        Console.WriteLine("---- 上行拆帧与事件解析往返 ----");
        failed += UplinkRoundTrip();

        Console.WriteLine();
        Console.WriteLine("---- 机型识别（VID/PID 匹配表）----");
        failed += MachineTypeChecks();

        Console.WriteLine();
        Console.WriteLine("---- 锁屏口令仓库 ----");
        failed += LockPasswordChecks();

        Console.WriteLine();
        Console.WriteLine("---- 锁屏口令自动应答链路（不连设备）----");
        failed += PasswordAutoReplyChecks();

        Console.WriteLine();
        Console.WriteLine("---- 锁屏确认提示文案 ----");
        failed += LockConfirmTextChecks();

        Console.WriteLine();
        Console.WriteLine("---- 驻留时限与 exit 拦截 ----");
        failed += HoldExitGuardChecks();

        Console.WriteLine();
        Console.WriteLine("---- 锁屏口令动作的驻留要求 ----");
        failed += MustHoldChecks();

        Console.WriteLine();
        Console.WriteLine("---- CLI 驻留与解锁信号 ----");
        failed += HoldAndUnlockSignalChecks();

        Console.WriteLine();
        Console.WriteLine(failed == 0
            ? $"全部断言通过。"
            : $"{failed} 项断言失败。");
        return failed == 0 ? 0 : 1;
    }
}
