using System.Security.Cryptography;

namespace SeewoMCUController.Mcu;

/// <summary>
/// 锁 Android 屏的口令仓库。
/// <para>
/// 校验方式与官方 <c>SeewoUSBLockMonitor.exe</c> 完全一致：安卓板把用户在锁屏页输入的
/// <b>明文</b>口令放进 <c>(0x36,0x30)</c> 的负载，PC 侧对<b>原始负载字节</b>做 MD5，
/// 得到 <b>32 位小写十六进制</b>后与本地缓存的 <c>lockpwd</c> 逐字符比较，然后回
/// <c>(0x36,0x31)</c> + 1 字节（<c>1</c> 通过 / <c>0</c> 不通过），为 <c>1</c> 时再发
/// <c>(0x36,0x12)</c> 解锁。
/// 依据：<c>MCUService::HandleScreenPWD</c> @ <c>0x0044C350</c>、
/// <c>DevLockService::VerifyLockPwdByLocalCache</c> @ <c>0x0042DA0</c>
/// （见 <c>opencode/screenlock/REPORT_android_screen_lock.md</c> 第 4.1 节）。
/// </para>
/// <para>
/// <b>未设置口令时一律判定为正确</b>，保持「任何口令都能解锁」的历史行为；
/// 只有设置过口令才会真正比对。
/// </para>
/// <para>
/// 口令只保存在进程内存里，退出即失效；本程序不写盘、不修改系统配置。
/// </para>
/// </summary>
public sealed class LockPasswordStore
{
    /// <summary>建议的口令长度上限，仅用于兼容性提示，不限制输入。</summary>
    public const int RecommendedLength = 48;

    /// <summary>菜单里的兼容性提示（第 1 行）。</summary>
    public const string AdviceLine1 = "口令可用大小写字母、数字和符号，建议不超过 48 位且少用特殊字符；";

    /// <summary>菜单里的兼容性提示（第 2 行）。</summary>
    public const string AdviceLine2 = "过长或含大量特殊字符的口令，部分 Android 输入法无法正确输入，会导致锁屏后解不开。";

    /// <summary>完整的兼容性说明，供 CLI 帮助 / 多行展示使用。</summary>
    public static string Advice => AdviceLine1 + AdviceLine2;

    private const string HexDigits = "0123456789abcdef";

    private readonly object _gate = new();
    private string? _plain;
    private string? _hash;

    /// <summary>是否已设置口令。</summary>
    public bool IsSet
    {
        get { lock (_gate) return _hash is not null; }
    }

    /// <summary>口令的 MD5 小写十六进制，即官方本地缓存里的 <c>lockpwd</c>。</summary>
    public string? HashHex
    {
        get { lock (_gate) return _hash; }
    }

    /// <summary>口令长度；未设置时为 0。</summary>
    public int Length
    {
        get { lock (_gate) return _plain?.Length ?? 0; }
    }

    /// <summary>口令状态的一行描述，不包含口令内容。</summary>
    public string Status
    {
        get
        {
            lock (_gate)
            {
                return _hash is null
                    ? "未设置（任何口令都能解锁）"
                    : "已设置";
            }
        }
    }

    // ------------------------------------------------------------------ MD5

    /// <summary>把任意字节串按官方实现转成 32 位小写十六进制 MD5。</summary>
    public static string Md5Hex(ReadOnlySpan<byte> data)
    {
        Span<byte> digest = stackalloc byte[16];
        MD5.HashData(data, digest);
        Span<char> chars = stackalloc char[32];
        for (int i = 0; i < 16; i++)
        {
            chars[i * 2] = HexDigits[digest[i] >> 4];
            chars[i * 2 + 1] = HexDigits[digest[i] & 0x0F];
        }
        return new string(chars);
    }

    /// <summary>把明文口令按 UTF-8 编码后转成 32 位小写十六进制 MD5。</summary>
    public static string Md5Hex(string text) => Md5Hex(System.Text.Encoding.UTF8.GetBytes(text));

    // ------------------------------------------------------------------ 设置 / 清除

    /// <summary>
    /// 设置口令。非法输入（空、含控制字符）抛 <see cref="ArgumentException"/>。
    /// </summary>
    public void Set(string password)
    {
        Validate(password);
        string hash = Md5Hex(password);
        lock (_gate)
        {
            _plain = password;
            _hash = hash;
        }
    }

    /// <summary>清除口令，回到「任何口令都能解锁」。</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _plain = null;
            _hash = null;
        }
    }

    /// <summary>校验输入是否合法。空、超长、含控制字符都不合法。</summary>
    public static void Validate(string password)
    {
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("口令不能为空；如需取消设置请直接放弃", nameof(password));
        if (password.Contains('\0'))
            throw new ArgumentException("口令不能包含 NUL", nameof(password));
        foreach (char c in password)
        {
            if (char.IsControl(c))
                throw new ArgumentException("口令不能包含控制字符（回车、制表符等）", nameof(password));
        }
    }

    /// <summary>
    /// 口令风险提示。返回空列表表示没有需要提醒的地方；非空时每项都应以 <c>[!]</c> 开头。
    /// <para>
    /// 特殊字符本身是允许的，这里只提示 Android 输入法的兼容性风险，
    /// 避免锁屏之后口令输不进去、屏幕再也解不开。
    /// </para>
    /// </summary>
    public static IReadOnlyList<string> DescribeRisks(string password)
    {
        var risks = new List<string>(2);
        int symbols = password.Count(c => !char.IsLetterOrDigit(c));
        if (password.Length > RecommendedLength)
            risks.Add($"[!] 口令较长，建议不超过 {RecommendedLength} 位：Android 输入法可能输入不全");
        if (symbols > 0)
            risks.Add($"[!] 含 {symbols} 个非字母数字字符：特殊字符在部分 Android 输入法上可能无法正确输入，锁屏后有解不开的风险");
        return risks;
    }

    // ------------------------------------------------------------------ 校验

    /// <summary>
    /// 校验安卓板上行的口令负载。<b>未设置口令时一律返回 <c>true</c></b>。
    /// </summary>
    public bool Verify(ReadOnlySpan<byte> rawPayload)
    {
        string hash = Md5Hex(rawPayload);
        string? expected;
        bool ok;
        lock (_gate)
        {
            expected = _hash;
            ok = expected is null || FixedTimeEquals(expected, hash);
        }
        return ok;
    }

    /// <summary>校验明文口令（等价于对 UTF-8 字节做同样的比对）。</summary>
    public bool Verify(string input) => Verify(System.Text.Encoding.UTF8.GetBytes(input));

    private static bool FixedTimeEquals(string a, string b)
    {
        Span<byte> x = stackalloc byte[32];
        Span<byte> y = stackalloc byte[32];
        System.Text.Encoding.ASCII.GetBytes(a.AsSpan(), x);
        System.Text.Encoding.ASCII.GetBytes(b.AsSpan(), y);
        return CryptographicOperations.FixedTimeEquals(x, y);
    }

}
