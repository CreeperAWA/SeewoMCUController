using SeewoMCUController.Mcu;
using SeewoMCUController.Mcu.Protocol;

namespace SeewoMCUController.Ui;

/// <summary>用于 TUI 实时日志的一条格式化日志行。</summary>
/// <param name="Timestamp">最后一次出现的时间（合并计数 > 1 时随最新一条更新）。</param>
/// <param name="Direction">← 上行 / → 下发 / · 本机消息。</param>
/// <param name="Category">分类。</param>
/// <param name="Event">事件名。</param>
/// <param name="Detail">详情。</param>
/// <param name="Color">显示颜色。</param>
internal sealed record ConsoleEventLine(
    DateTime Timestamp,
    string Direction,
    string Category,
    string Event,
    string Detail,
    ConsoleColor Color)
{
    /// <summary>
    /// 合并计数。方向 / 分类 / 事件 / 详情完全相同的日志会合并成一条，
    /// 每多一条计数 +1，行尾显示 <c>(×n)</c>。
    /// </summary>
    public int Count { get; init; } = 1;

    /// <summary>是否被合并过（用于决定行尾要不要带计数）。</summary>
    public bool Merged => Count > 1;

    public static ConsoleEventLine FromInbound(McuInboundEvent evt) =>
        new(DateTime.Now, "←", CategoryOf(evt), Safe(evt.Name), Safe(DetailOf(evt)), ColorOf(evt));

    public static ConsoleEventLine FromInbound(McuInboundEvent evt, DateTime timestamp) =>
        new(timestamp, "←", CategoryOf(evt), Safe(evt.Name), Safe(DetailOf(evt)), ColorOf(evt));

    public static ConsoleEventLine FromLog(McuLogEntry entry)
    {
        if (!entry.Outgoing)
            return new(entry.Timestamp, "←", "MCU 事件", Safe(entry.Text), string.Empty, ConsoleColor.Cyan);

        string category = entry.Frame.Main == (byte)MainOut.Group36 ? "0x36 下发" : "PC 下发";
        return new(entry.Timestamp, "→", category, Safe(entry.Text), string.Empty, ConsoleColor.Green);
    }

    /// <summary>MCU 负载按 UTF-8 直接解码，字节 0x1B 会解出 ESC；这里先压成单行，杜绝转义序列注入控制台。</summary>
    private static string Safe(string? text) => TextWidth.Clip(text, 400);

    private static string CategoryOf(McuInboundEvent evt) => evt.Main switch
    {
        MainIn.UsbLock => "U 盘锁",
        MainIn.Group36 when evt.Frame.Sub is 0x19 or 0x32 or 0x38 or 0x40 => "U 盘锁",
        MainIn.Group36 => "远程协助",
        MainIn.AndroidInfo or MainIn.AndroidMacReply or MainIn.MachineType => "设备信息",
        MainIn.CurrentChannelStatus or MainIn.CurrentTvStatus or MainIn.AllChannelMessage => "通道",
        MainIn.RequestPcTime => "系统",
        _ => "MCU 事件",
    };

    private static string DetailOf(McuInboundEvent evt)
    {
        var detail = evt.Detail;
        if (!string.IsNullOrWhiteSpace(evt.AutoReply))
            detail = string.IsNullOrWhiteSpace(detail) ? $"→ {evt.AutoReply}" : $"{detail}；→ {evt.AutoReply}";
        return detail;
    }

    private static ConsoleColor ColorOf(McuInboundEvent evt) => evt.Kind switch
    {
        InboundKind.Request => ConsoleColor.Yellow,
        InboundKind.State => ConsoleColor.White,
        _ => ConsoleColor.Cyan,
    };
}
