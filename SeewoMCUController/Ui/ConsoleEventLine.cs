using SeewoMCUController.Mcu;
using SeewoMCUController.Mcu.Protocol;

namespace SeewoMCUController.Ui;

/// <summary>用于 TUI 事件面板的一条格式化日志行。</summary>
internal sealed record ConsoleEventLine(
    DateTime Timestamp,
    string Direction,
    string Category,
    string Event,
    string Detail,
    ConsoleColor Color)
{
    public static ConsoleEventLine FromInbound(McuInboundEvent evt) =>
        new(DateTime.Now, "←", CategoryOf(evt), evt.Name, DetailOf(evt), ColorOf(evt));

    public static ConsoleEventLine FromInbound(McuInboundEvent evt, DateTime timestamp) =>
        new(timestamp, "←", CategoryOf(evt), evt.Name, DetailOf(evt), ColorOf(evt));

    public static ConsoleEventLine FromLog(McuLogEntry entry)
    {
        if (!entry.Outgoing)
            return new(entry.Timestamp, "←", "MCU 事件", entry.Text, string.Empty, ConsoleColor.Cyan);

        string category = entry.Frame.Main == (byte)MainOut.Group36 ? "0x36 下发" : "PC 下发";
        return new(entry.Timestamp, "→", category, entry.Text, string.Empty, ConsoleColor.Green);
    }

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
        InboundKind.State => ConsoleColor.Green,
        _ => ConsoleColor.Cyan,
    };
}
