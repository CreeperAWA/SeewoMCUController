namespace SeewoMCUController.Mcu;

/// <summary>
/// MCU 机型识别。按 VID/PID 匹配表判定，匹配依据是 <c>HidD_GetAttributes</c>
/// 读回的设备属性。
/// </summary>
public enum McuMachineType
{
    /// <summary>非希沃 MCU。</summary>
    None = 0,

    /// <summary>309 型（<c>0x10E0:0xAA55</c>）。</summary>
    Type309 = 1,

    /// <summary>华信触摸适配器（<c>0x1FF7:0x0001</c>）。</summary>
    HuaxinTouch = 2,

    /// <summary>638 型（<c>0x1FF7:0x0F15</c>）。</summary>
    Type638 = 3,

    /// <summary>通用 Seewo HID（<c>0x1FF7:0x0F20</c>–<c>0x0F3F</c>，共 32 项）。</summary>
    GenericSeewo = 6,
}

/// <summary>VID/PID 匹配表与机型判定。</summary>
public static class McuKnownDevices
{
    public const ushort VidSeewo = 0x1FF7;
    public const ushort Vid309 = 0x10E0;

    /// <summary>判定 MCU 机型；不匹配返回 <see cref="McuMachineType.None"/>。</summary>
    public static McuMachineType Detect(int vid, int pid)
    {
        if (vid == Vid309 && pid == 0xAA55) return McuMachineType.Type309;
        if (vid == VidSeewo)
        {
            if (pid == 0x0F15) return McuMachineType.Type638;
            if (pid == 0x0001) return McuMachineType.HuaxinTouch;
            if (pid is >= 0x0F20 and <= 0x0F3F) return McuMachineType.GenericSeewo;
        }
        return McuMachineType.None;
    }

    public static McuMachineType Detect(UsbId? usbId) =>
        usbId is null ? McuMachineType.None : Detect(usbId.Vid, usbId.Pid);

    public static string Describe(McuMachineType type) => type switch
    {
        McuMachineType.Type309 => "309 型 (0x10E0:0xAA55)",
        McuMachineType.HuaxinTouch => "华信触摸适配器 (0x1FF7:0x0001)",
        McuMachineType.Type638 => "638 型 (0x1FF7:0x0F15)",
        McuMachineType.GenericSeewo => "通用 Seewo HID (0x1FF7:0x0F20-0x0F3F)",
        _ => "非希沃 MCU / 未识别",
    };

    /// <summary>预定义的 MCU 设备（VID/PID + 路径关键字），沿用历史实现的排序优先级。</summary>
    public static IReadOnlyList<UsbId> Defaults { get; } = new List<UsbId>
    {
        new(0x0F15, VidSeewo, "mi_00", 0),          // 638 型
        new(0xAA55, Vid309, "col01", 0),            // 309 型
        new(0x0001, VidSeewo, "col03", 0),          // 华信触摸适配器
        new(0x0F21, VidSeewo, "mi_00&col02", 0),
        new(0x0F22, VidSeewo, "mi_00", 0),
        new(0x0F25, VidSeewo, "mi_00", 0),
        new(0x0F28, VidSeewo, "mi_00", 0),
        new(0x0F29, VidSeewo, "mi_00", 0),
        new(0x0F2A, VidSeewo, "mi_00", 0),
        new(0x0F30, VidSeewo, "mi_00", 0),
        new(0x0F31, VidSeewo, "mi_00", 0),
        new(0x0F38, VidSeewo, "mi_00", 0),
        new(0x0F39, VidSeewo, "mi_00", 0),
    };
}
