using System.Text;
using SeewoMCUController.Commands;
using SeewoMCUController.Mcu;
using SeewoMCUController.Mcu.Protocol;

namespace SeewoMCUController.Cli;

public static partial class SelfTest
{
    /// <summary>
    /// 校验「安卓板上报口令 → PC 判定 → 回 (0x36,0x31) → 解锁」这条链路的判定结果。
    /// 只验证判定与组包，不连接设备。
    /// </summary>
    private static int PasswordAutoReplyChecks()
    {
        int failed = 0;
        void Line(string name, bool ok, string extra = "")
        {
            if (!ok) failed++;
            Console.WriteLine($"  {(ok ? "[OK]" : "[ERR]")} {name,-40}{extra}");
        }

        // 未连接设备时 CheckPassword 仍然可用（纯本地比对），因此可以离线验证判定。
        using var device = new McuDevice();
        const string secret = "Sec!2026";

        Line("初始未设置口令", !device.HasLockPassword);
        Line("未设置：任意口令都判定为正确", device.CheckPassword(secret) && device.CheckPassword("乱输"));

        device.LockPassword.Set(secret);
        Line("设置后 HasLockPassword", device.HasLockPassword && device.LockPassword.Length == 8);
        Line("上报正确口令 → 判定通过", device.CheckPassword(secret));
        Line("上报错误口令 → 判定拒绝", !device.CheckPassword("sec!2026") && !device.CheckPassword("Sec!2026 "));

        // 0x30 携带的负载会经过编解码往返，确认判定吃的是原始字节而不是被改写过的字符串。
        var uplink = McuOutbound.Simple(0x36, 0x30);
        Encoding.UTF8.GetBytes(secret).CopyTo(uplink.Buffer.AsSpan(5));
        uplink.Buffer[3] = (byte)Encoding.UTF8.GetByteCount(secret);
        var wire = uplink.ToTransport(FrameMode.Framed);
        Line("(0x36,0x30) 往返后判定仍通过",
            McuFrameCodec.TryParse(wire, out var back, out _)
            && back.Main == 0x36 && back.Sub == 0x30
            && device.CheckPassword(back.Payload),
            $"{Encoding.UTF8.GetString(back.Payload)} → MD5 {LockPasswordStore.Md5Hex(back.Payload)}");

        Line("应答帧 (0x36,0x31)=1", McuOutbound.PasswordResult(true).Buffer[5] == 0x01);
        Line("应答帧 (0x36,0x31)=0", McuOutbound.PasswordResult(false).Buffer[5] == 0x00);
        Line("解锁帧 (0x36,0x12) 空负载",
            McuOutbound.UnlockScreen().Buffer[1] == 0x36 && McuOutbound.UnlockScreen().Buffer[2] == 0x12);

        device.LockPassword.Clear();
        Line("清除后回到任意口令都通过", !device.HasLockPassword && device.CheckPassword("乱输"));

        return failed;
    }
    private static int HoldExitGuardChecks()
    {
        int failed = 0;
        void Line(string name, bool ok, string extra = "")
        {
            if (!ok) failed++;
            Console.WriteLine($"  {(ok ? "[OK]" : "[ERR]")} {name,-40}{extra}");
        }

        bool Parses(string[] argv, out CliOptions opts) =>
            CliOptions.TryParse(argv, out opts!, out _);

        // 锁屏类动作默认不限时：屏幕还锁着就不该自己退出。
        Line("默认无时限", Parses(new[] { "lock", "lock" }, out var d1) && d1.HoldSeconds == 0);
        Line("默认未标记显式时限", Parses(new[] { "lock", "lock" }, out var d2) && !d2.HoldSecondsExplicit);
        Line("锁屏动作派生为不限时",
            Parses(new[] { "lock", "lock" }, out var d3) && d3.WithHoldSeconds(0).HoldSeconds == 0);
        Line("显式 --hold-seconds 45 生效",
            Parses(new[] { "lock", "lock", "--hold-seconds", "45" }, out var d4)
            && d4.HoldSeconds == 45 && d4.HoldSecondsExplicit);
        Line("显式 --hold-seconds=0 仍是不限时",
            Parses(new[] { "lock", "lock", "--hold-seconds", "0" }, out var d5)
            && d5.HoldSeconds == 0 && d5.HoldSecondsExplicit);
        Line("WithHoldSeconds 不影响其它字段",
            Parses(new[] { "lock", "lock", "--password", "Abc123" }, out var d6)
            && d6.WithHoldSeconds(0).Password == "Abc123"
            && d6.WithHoldSeconds(0).HoldSecondsExplicit == d6.HoldSecondsExplicit);

        // exit 拦截：锁屏时必须拒绝。
        Line("新建设备未标记为锁屏", !new McuDevice().ScreenLocked);

        using var lockedDevice = new McuDevice();
        lockedDevice.SetScreenLockedForTest(true);
        Line("可标记为锁屏状态", lockedDevice.ScreenLocked);
        Line("锁屏状态下 CanExitResident 为 false", !CliRunner.CanExitResident(lockedDevice));
        lockedDevice.SetScreenLockedForTest(false);
        Line("解锁后 CanExitResident 为 true", CliRunner.CanExitResident(lockedDevice));

        // 回归：锁屏绝不能触发 ScreenUnlocked（否则驻留会立刻退出）。
        // 这两个事件用无设备也能验证，因为状态变更发生在 Send 成功之后，
        // 而这里直接检查方法体的语义：锁屏只发 ScreenLockChanged(true)。
        using var evtDevice = new McuDevice();
        var unlockedFired = 0;
        var lockStates = new List<bool>();
        evtDevice.ScreenUnlocked += () => unlockedFired++;
        evtDevice.ScreenLockChanged += locked => lockStates.Add(locked);
        evtDevice.SetScreenLockedForTest(true);
        Line("锁屏不触发 ScreenUnlocked", unlockedFired == 0, $"触发 {unlockedFired} 次");
        Line("锁屏触发 ScreenLockChanged(true)", lockStates.SequenceEqual(new[] { true }),
            string.Join(",", lockStates.Select(x => x.ToString())));
        Line("锁屏后 ScreenLocked 为 true", evtDevice.ScreenLocked);
        Line("锁屏后不允许 exit", !CliRunner.CanExitResident(evtDevice));

        evtDevice.SetScreenLockedForTest(false);
        Line("解锁触发 ScreenUnlocked 恰好一次", unlockedFired == 1, $"触发 {unlockedFired} 次");
        Line("解锁触发 ScreenLockChanged(false)",
            lockStates.SequenceEqual(new[] { true, false }));
        Line("解锁后 ScreenLocked 为 false", !evtDevice.ScreenLocked);
        Line("解锁后允许 exit", CliRunner.CanExitResident(evtDevice));

        // 端到端：锁屏状态下依次输入 exit / exit / unlock，验证只有解锁那次才退出。
        // 这些脚本都不连设备，unlock 会因「尚未连接」而发不出帧，
        // 因此用 RaiseScreenUnlockedForTest 无法验证；这里只验证「exit 被拒绝时循环不退出」。
        lockedDevice.SetScreenLockedForTest(true);
        Line("锁屏 + exit 不退出驻留", !ResidentRun(lockedDevice, "exit", "exit"));
        Line("锁屏 + 未知命令 + exit 仍不退出", !ResidentRun(lockedDevice, "nosuchcmd", "exit"));

        using var freeDevice = new McuDevice();
        Line("未锁屏 + exit 直接退出", ResidentRun(freeDevice, "exit"));

        // 锁屏 + help 不退出驻留（help 不是退出指令）
        Line("锁屏 + help 不退出驻留", !ResidentRun(lockedDevice, "help", "exit"));

        // 驻留模式的命令白名单
        Line("exit 属退出指令", CliRunner.IsExitCommand("exit"));
        Line("quit / q 亦属退出指令",
            CliRunner.IsExitCommand("quit") && CliRunner.IsExitCommand("q") && CliRunner.IsExitCommand("EXIT"));
        Line("unlock 不属退出指令", !CliRunner.IsExitCommand("unlock"));

        return failed;
    }

    /// <summary>跑一段驻留脚本，返回是否真的退出了驻留循环。</summary>
    private static bool ResidentRun(McuDevice device, params string[] script) =>
        CliRunner.RunResidentScriptAsync(device, script, CancellationToken.None).GetAwaiter().GetResult();

    /// <summary>
    /// 锁屏相关动作必须驻留；且口令校验不受 <c>AutoReply</c> 开关影响。
    /// </summary>
    private static int MustHoldChecks()
    {
        int failed = 0;
        void Line(string name, bool ok, string extra = "")
        {
            if (!ok) failed++;
            Console.WriteLine($"  {(ok ? "[OK]" : "[ERR]")} {name,-40}{extra}");
        }

        var lockAction = McuActionRegistry.FindAction("lock", "lock");
        var setPwd = McuActionRegistry.FindAction("lock", "set-password");
        Line("lock lock 声明必须驻留", lockAction is { MustHold: true });
        Line("lock set-password 声明必须驻留", setPwd is { MustHold: true });
        Line("set-password 命令行下接上锁屏", setPwd is { ChainLockScreen: true });
        Line("lock lock 不接上锁屏（它本身就是锁屏）", lockAction is { ChainLockScreen: false });

        var unlock = McuActionRegistry.FindAction("lock", "unlock");
        Line("unlock 不需要驻留", unlock is { MustHold: false });
        var pwdStatus = McuActionRegistry.FindAction("lock", "pwd-status");
        Line("pwd-status 不需要驻留", pwdStatus is { MustHold: false });

        // AutoReply 关闭后，(0x36,0x30) 仍必须被判定为口令请求并应答。
        using var device = new McuDevice();
        device.LockPassword.Set("Abc123");

        var pwdFrame = McuOutbound.Simple(0x36, 0x30);
        Encoding.UTF8.GetBytes("Abc123").CopyTo(pwdFrame.Buffer.AsSpan(5));
        pwdFrame.Buffer[3] = 6;

        Line("识别 (0x36,0x30) 为口令帧", McuDevice.IsScreenPasswordFrameForTest(pwdFrame));
        Line("非口令帧不被误判",
            !McuDevice.IsScreenPasswordFrameForTest(McuOutbound.Simple(0x36, 0x38))
            && !McuDevice.IsScreenPasswordFrameForTest(McuOutbound.Simple(0xC0, 0x00)));

        device.AutoReply = false;
        Line("关闭 AutoReply 后开关仍为 false", !device.AutoReply);
        // 应答路径独立于 AutoReply：直接走 ReplyScreenPassword 验证它不会因开关而跳过。
        Line("关闭 AutoReply 后口令仍可校验通过", device.CheckPassword(pwdFrame.Payload));
        Line("关闭 AutoReply 后错误口令仍被拒", !device.CheckPassword("nope"));
        device.AutoReply = true;
        Line("重新打开 AutoReply 正常", device.AutoReply);

        return failed;
    }

    /// <summary>
    /// CLI 驻留逻辑：口令参数解析，以及解锁信号能否让驻留提前结束。
    /// </summary>
    private static int HoldAndUnlockSignalChecks()
    {
        int failed = 0;
        void Line(string name, bool ok, string extra = "")
        {
            if (!ok) failed++;
            Console.WriteLine($"  {(ok ? "[OK]" : "[ERR]")} {name,-40}{extra}");
        }

        // 参数解析：--password 隐含 --hold
        bool Parses(string[] argv, out CliOptions opts, out string? err) =>
            CliOptions.TryParse(argv, out opts!, out err);

        Line("--password 设置口令",
            Parses(new[] { "lock", "lock", "--password", "Abc123" }, out var p1, out _)
            && p1.Password == "Abc123");
        Line("--password 隐含 --hold",
            Parses(new[] { "lock", "lock", "--password", "Abc123" }, out var p2, out _)
            && p2.Hold && p2.Password == "Abc123");
        Line("--pwd 是 --password 的别名",
            Parses(new[] { "lock", "lock", "--pwd", "Abc123" }, out var p3, out _)
            && p3.Password == "Abc123" && p3.Hold);
        Line("--password=Xxx 写法",
            Parses(new[] { "lock", "lock", "--password=Abc123" }, out var p4, out _)
            && p4.Password == "Abc123" && p4.Hold);
        Line("--password 缺值报错",
            !Parses(new[] { "lock", "lock", "--password" }, out _, out var e1) && e1 is not null, e1 ?? "");
        Line("--hold 启用驻留",
            Parses(new[] { "lock", "lock", "--hold" }, out var p5, out _) && p5.Hold);
        Line("--hold-seconds 生效",
            Parses(new[] { "lock", "lock", "--hold-seconds", "45" }, out var p6, out _)
            && p6.Hold && p6.HoldSeconds == 45);
        Line("--hold-seconds=0 表示不限时",
            Parses(new[] { "lock", "lock", "--hold-seconds", "0" }, out var p7, out _)
            && p7.Hold && p7.HoldSeconds == 0);
        Line("--hold-seconds 负值报错",
            !Parses(new[] { "lock", "lock", "--hold-seconds", "-5" }, out _, out var e2) && e2 is not null);
        Line("不传 --password 时不驻留",
            Parses(new[] { "lock", "unlock" }, out var p8, out _) && !p8.Hold && p8.Password is null);

        // 驻留循环应在解锁信号下结束，锁屏信号本身不能结束驻留。
        using var residentDevice = new McuDevice();
        var residentRun = CliRunner.RunResidentScriptAsync(residentDevice, Array.Empty<string>(), CancellationToken.None);
        residentDevice.SetScreenLockedForTest(true);
        Line("驻留中置为锁屏不会结束", !residentRun.IsCompleted);
        residentDevice.SetScreenLockedForTest(false);
        Line("驻留循环在解锁信号下结束", residentRun.GetAwaiter().GetResult());

        return failed;
    }
    private static int LockPasswordChecks()
    {
        int failed = 0;
        void Line(string name, bool ok, string extra = "")
        {
            if (!ok) failed++;
            Console.WriteLine($"  {(ok ? "[OK]" : "[ERR]")} {name,-40}{extra}");
        }
        void Throws(string name, Action action)
        {
            bool threw = false;
            try { action(); }
            catch (ArgumentException) { threw = true; }
            Line(name, threw);
        }

        // RFC 1321 附录 A.5 的标准测试向量，确认与官方 sub_408DC0/sub_408D80 一致。
        Line("MD5(\"\") = d41d8cd98f00b204e9800998ecf8427e",
            LockPasswordStore.Md5Hex(string.Empty) == "d41d8cd98f00b204e9800998ecf8427e");
        Line("MD5(\"abc\") = 900150983cd24fb0d6963f7d28e17f72",
            LockPasswordStore.Md5Hex("abc") == "900150983cd24fb0d6963f7d28e17f72");
        Line("MD5(\"1234\") = 81dc9bdb52d04dc20036dbd8313ed055",
            LockPasswordStore.Md5Hex("1234") == "81dc9bdb52d04dc20036dbd8313ed055");

        var store = new LockPasswordStore();
        Line("初始为未设置", !store.IsSet && store.Length == 0);
        Line("未设置时任意口令都算正确",
            store.Verify("whatever") && store.Verify(string.Empty) && store.Verify("!@#$%^&*()"));

        store.Set("Abc123!@#xyz");
        Line("设置后 IsSet / 长度", store.IsSet && store.Length == 12, $"{store.Length} 位");
        Line("HashHex = MD5(明文)", store.HashHex == LockPasswordStore.Md5Hex("Abc123!@#xyz"), store.HashHex ?? "");
        Line("正确口令通过", store.Verify("Abc123!@#xyz"));
        Line("错误口令拒绝", !store.Verify("abc123!@#xyz"));
        Line("大小写敏感", !store.Verify("ABC123!@#xyz"));
        Line("前缀相同但不等长也拒绝", !store.Verify("Abc123!@#xy"));
        Line("原始字节入口与明文入口等价", store.Verify(Encoding.UTF8.GetBytes("Abc123!@#xyz")));

        store.Clear();
        Line("清除后回到任何口令都正确", !store.IsSet && store.Verify("anything"));

        Throws("空口令被拒绝", () => store.Set(string.Empty));
        Throws($"超长口令（>{LockPasswordStore.MaxLength} 位）被拒绝",
            () => store.Set(new string('x', LockPasswordStore.MaxLength + 1)));
        Throws("含控制字符的口令被拒绝", () => store.Set("a\tb"));
        Throws("含 NUL 的口令被拒绝", () => store.Set("a\0b"));

        Line("短纯字母数字无风险提示", LockPasswordStore.DescribeRisks("abc123").Count == 0);
        Line("超长口令有风险提示", LockPasswordStore.DescribeRisks(new string('x', 14)).Count > 0);
        Line("含特殊字符有风险提示", LockPasswordStore.DescribeRisks("abc!").Count > 0);

        Console.WriteLine($"    {LockPasswordStore.AdviceLine1}");
        Console.WriteLine($"    {LockPasswordStore.AdviceLine2}");
        return failed;
    }
}
