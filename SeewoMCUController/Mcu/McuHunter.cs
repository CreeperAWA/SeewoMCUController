using SeewoMCUController.Mcu;

namespace Cvte.Mcu;

/// <summary>
/// MCU 设备发现与连接。单进程内只有一个全局 <see cref="Usb"/> 实例，
/// <see cref="McuLink"/> 的读线程与写路径都复用它，避免同一条 HID 通道被两个读者抢占。
/// </summary>
public static class McuHunter
{
    /// <summary>共享的 HID 句柄（读写分离）。</summary>
    public static readonly UsbDevice Usb = new();

    /// <summary>当前选中的 MCU。</summary>
    public static UsbId? DetectedMcu { get; set; }

    /// <summary>枚举所有疑似 MCU 的 HID 设备，按预定义表排序。</summary>
    public static List<UsbId> FindAll()
    {
        var list = new List<UsbId>();
        foreach (DeviceInfo deviceInfo in HidDeviceDiscovery.GetDevices())
        {
            var usbId = new UsbId(deviceInfo.DevicePath, deviceInfo.Rev);
            if (usbId.Pid != -1 && usbId.Vid != -1)
            {
                if (CheckUsbDetectedMcu(usbId)) list.Add(usbId);
            }
            else if (CheckDetectedMcu(deviceInfo))
            {
                list.Add(new UsbId(deviceInfo.DevicePath, deviceInfo.Rev));
            }
        }

        list.Sort(new UsbIdSortComparer(McuKnownDevices.Defaults.ToList()));
        DetectedMcu ??= list.FirstOrDefault();
        return list;
    }

    /// <summary>依次尝试连接候选设备，返回是否至少成功连接一个。</summary>
    public static bool ConnectFirst()
    {
        foreach (UsbId usbId in FindAll())
        {
            if (!Usb.Connect(usbId)) continue;
            DetectedMcu = usbId;
            return true;
        }
        return false;
    }

    public static void CloseConnection()
    {
        try
        {
            Usb.Disconnect();
        }
        catch (Exception)
        {
            // 忽略：断开失败无需处理
        }
    }

    // ------------------------------------------------------------------ 判定

    private static bool CheckDetectedMcu(DeviceInfo device)
    {
        if (string.IsNullOrEmpty(device.DevicePath)) return false;
        return McuKnownDevices.Defaults.Any(usbId => Match(usbId, device.DevicePath));
    }

    private static bool Match(UsbId usbId, string deviceName) =>
        deviceName.IndexOf(usbId.ToString(), StringComparison.OrdinalIgnoreCase) >= 0;

    private static bool CheckUsbDetectedMcu(UsbId usbId)
    {
        if (DetectedMcu is not null && IsUsbIdEquals(DetectedMcu, usbId)) return true;
        foreach (var defined in McuKnownDevices.Defaults)
        {
            if (IsUsbIdEquals(defined, usbId)) return true;
        }

        // 通用 Seewo HID：VID 0x1FF7 + PID 0x0F20..0x0F3F + 路径关键字
        if (McuKnownDevices.Detect(usbId.Vid, usbId.Pid) == McuMachineType.GenericSeewo)
        {
            foreach (string key in PathKeys)
            {
                if (Contains(usbId.Key, key) || Contains(usbId.DeviceName, key)) return true;
            }
        }
        return false;
    }

    private static readonly string[] PathKeys = { "mi_00", "col03" };

    private static bool Contains(string? text, string pattern) =>
        !string.IsNullOrEmpty(text) && text.Contains(pattern, StringComparison.OrdinalIgnoreCase);

    private static bool IsUsbIdEquals(UsbId pattern, UsbId usbId) =>
        pattern is not null && usbId is not null
        && pattern.Pid == usbId.Pid
        && pattern.Vid == usbId.Vid
        && (pattern.Rev == 0 || usbId.Rev == 0 || pattern.Rev == usbId.Rev)
        && (string.IsNullOrEmpty(pattern.Key)
            || Contains(usbId.Key, pattern.Key)
            || Contains(usbId.DeviceName, pattern.Key));
}
