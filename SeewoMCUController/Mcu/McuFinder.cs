using System.Text;
using System.Collections.Generic;
using System.Linq;

namespace SeewoMCUController.Mcu;

public static class McuFinder
{
    // 预定义的MCU设备类型，仿照Example项目中的McuHunter.DefineDeviceMcu
    public static List<UsbId> DefineDeviceMcu { get; set; } = new List<UsbId>
    {
        CommonMcu,
        Mcu638,
        Mcu309,
        Mcu551,
        McuTouch,
        Mcu551B2,
        McuV,
        McuFourSidesInfraredBlackboard,
        McuFlatFrogTouchFrame,
        McuVI,
        McuVIAbroad,
        McuVII,
        McuVIIFourSidesInfraredBlackboard,
        McuVIIPlusFourSidesInfraredBlackboard,
        McuVIIMultiScreensLeft,
        McuVIIMultiScreensMiddle,
        McuVIIMultiScreensRight,
        McuVIII,
        McuVIIIMultiScreensLeft,
        McuVIIIMultiScreensMiddle,
        McuVIIIMultiScreensRight
    };

    // DetectedMcu is a single global for the whole process — there is only one MCU.
    public static UsbId? DetectedMcu;

    public static bool FindMcu()
    {
        try
        {
            Console.WriteLine("[调试] FindMcu() 开始");
            var devices = FindAll();
            Console.WriteLine($"[调试] FindAll() 返回 {devices.Count} 个设备");
            return devices.Any();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[调试] FindMcu() 异常: {ex.Message}");
            Console.WriteLine($"[调试] 堆栈: {ex.StackTrace}");
        }
        return false;
    }

    public static List<UsbId> FindAll()
    {
        Console.WriteLine("[调试] FindAll() 开始");
        List<UsbId> list = new List<UsbId>();
        
        try
        {
            Console.WriteLine("[调试] 调用 HidDeviceDiscovery.GetDevices()");
            var devices = HidDeviceDiscovery.GetDevices();
            Console.WriteLine($"[调试] HidDeviceDiscovery.GetDevices() 返回 {devices.Count} 个设备");
            
            foreach (DeviceInfo deviceInfo in devices)
            {
                string devicePath = deviceInfo.DevicePath;
                int rev = deviceInfo.Rev;
                UsbId usbId = new UsbId(devicePath, rev);
                
                Console.WriteLine($"[调试] 检查设备: {devicePath}");
                Console.WriteLine($"[调试]   VID={usbId.Vid:X4}, PID={usbId.Pid:X4}, Key={usbId.Key}, REV={rev:X4}");
                
                if (usbId.Pid != -1 && usbId.Vid != -1)
                {
                    if (CheckUsbDetectedMcu(usbId))
                    {
                        Console.WriteLine($"[调试]   ✓ 匹配 MCU 设备!");
                        list.Add(usbId);
                    }
                    else
                    {
                        Console.WriteLine($"[调试]   ✗ 不匹配 MCU 设备");
                    }
                }
                else if (CheckDetectedMcu(deviceInfo))
                {
                    Console.WriteLine($"[调试]   ✓ 通过 CheckDetectedMcu 匹配!");
                    list.Add(new UsbId(devicePath, rev));
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[调试] FindAll() 异常: {ex.Message}");
            Console.WriteLine($"[调试] 堆栈: {ex.StackTrace}");
        }
        
        list = SortUsbIdList(list);
         Console.WriteLine($"[调试] FindAll() 完成，找到 {list.Count} 个 MCU 设备");
         return list;
    }

    private static bool CheckDetectedMcu(DeviceInfo device)
    {
        if (string.IsNullOrEmpty(device.DevicePath))
        {
            return false;
        }
        string deviceName = device.DevicePath;
        return GetUsbId().Any(usbId => Match(usbId, deviceName));
    }

    private static bool Match(UsbId usbId, string deviceName)
    {
        return deviceName.IndexOf(usbId.ToString(), StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool CheckUsbDetectedMcu(UsbId usbId)
    {
        if (IsUsbIdEquals(DetectedMcu, usbId))
        {
            return true;
        }
        foreach (var definedUsbId in DefineDeviceMcu)
        {
            if (IsUsbIdEquals(definedUsbId, usbId))
            {
                return true;
            }
        }

        foreach (var (minPid, maxPid) in PidSubsections)
        {
            int item = minPid;
            int item2 = maxPid;
            foreach (int num in Vids)
            {
                foreach (string text in PchKeys)
                {
                    if (usbId.Vid == num && usbId.Pid >= item && usbId.Pid <= item2)
                    {
                        if (IsStringContainIgnoreCase(usbId.Key, text))
                        {
                            return true;
                        }
                        if (IsStringContainIgnoreCase(usbId.DeviceName, text))
                        {
                            return true;
                        }
                    }
                }
            }
        }
        return false;
    }

    private static List<UsbId> SortUsbIdList(List<UsbId> usbIdList)
    {
        if (usbIdList.Count == 0)
        {
            return usbIdList;
        }
        usbIdList.Sort(new UsbIdSortComparer(DefineDeviceMcu));
        return usbIdList;
    }

    private static bool IsUsbIdEquals(UsbId? patternUsbId, UsbId? usbId)
    {
        return patternUsbId != null && usbId != null && patternUsbId.Pid == usbId.Pid && patternUsbId.Vid == usbId.Vid && (patternUsbId.Rev == 0 || usbId.Rev == 0 || patternUsbId.Rev == usbId.Rev) && (string.IsNullOrEmpty(patternUsbId.Key) || IsStringContainIgnoreCase(usbId.Key, patternUsbId.Key) || IsStringContainIgnoreCase(usbId.DeviceName, patternUsbId.Key));
    }

    private static bool IsStringContainIgnoreCase(string? str, string? pattern)
    {
        return str != null && (string.IsNullOrEmpty(pattern) || str.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) > -1);
    }

    private static IEnumerable<UsbId> GetUsbId()
    {
        return DefineDeviceMcu;
    }

    public static UsbId? GetOrFindMcu()
    {
        if (DetectedMcu != null)
        {
            return DetectedMcu;
        }
        FindMcu();
        return DetectedMcu;
    }

    public static bool FindAndConnection()
    {
        try
        {
            foreach (UsbId usbId in FindAll())
            {
                Console.WriteLine($"[调试] 尝试连接设备: {usbId.DeviceName}");
                bool connectResult = Usb.Connect(usbId);
                Console.WriteLine($"[调试] 连接结果: {connectResult}");
                
                if (connectResult && CanWrite())
                {
                    Console.WriteLine($"[调试] 设备可写，设置为 DetectedMcu");
                    DetectedMcu = usbId;
                    return true;
                }
                else
                {
                    Console.WriteLine($"[调试] 设备不可写或连接失败，尝试下一个");
                    // 断开连接，尝试下一个设备
                    Usb.Disconnect();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[调试] FindAndConnection 异常: {ex.Message}");
        }
        return false;
    }

    private static bool CanWrite()
    {
        // 使用与原始项目相同的测试命令
        bool result = Usb.Write(McuCommand.CommandBase);
        Console.WriteLine($"[调试] CanWrite 测试结果: {result}");
        return result;
    }

    public static UsbDevice Usb = new UsbDevice();

    // 设备定义，仿照Example项目中的McuHunter
    private const int VID = 0x1FF7; // 8183
    private const int PID = 0x0F15; // 3861
    private const string KEY = "mi_00&col02";
    private static readonly UsbId CommonMcu = new UsbId(PID, VID, KEY, 0);

    private const int VID_638 = 0x1FF7; // 8183
    private const int PID_638 = 0x0F15; // 3861
    private const string KEY_638 = "mi_00";
    private static readonly UsbId Mcu638 = new UsbId(PID_638, VID_638, KEY_638, 0);

    private const int VID_TOUCH = 0x1FF7; // 8183
    private const int PID_TOUCH = 0x0001; // 1
    private const string KEY_TOUCH = "col03";
    private static readonly UsbId McuTouch = new UsbId(PID_TOUCH, VID_TOUCH, KEY_TOUCH, 0);

    private const int VID_309 = 0x10E0; // 4320
    private const int PID_309 = 0xAA55; // 43605
    private const string KEY_309 = "col01";
    private static readonly UsbId Mcu309 = new UsbId(PID_309, VID_309, KEY_309, 0);

    private const int VID_551 = 0x1FF7; // 8183
    private const int PID_551 = 0x0F15; // 3861
    private const string KEY_551 = "mi_00";
    private static readonly UsbId Mcu551 = new UsbId(PID_551, VID_551, KEY_551, 0);

    private const int VID_551B2 = 0x1FF7; // 8183
    private const int PID_551B2 = 0x0F21; // 3873
    private const string KEY_551B2 = "mi_00&col02";
    private static readonly UsbId Mcu551B2 = new UsbId(PID_551B2, VID_551B2, KEY_551B2, 0);

    public const int VID_CVTE = 0x1FF7; // 8183
    public const int PID_V = 0x0F26; // 3878
    public const int VID_V = 0x1FF7; // 8183
    public static readonly UsbId McuV = new UsbId(PID_V, VID_V, "mi_00", 0);

    public const int PID_FourSidesInfraredBlackboard = 0x0F50; // 3920
    public const int VID_FourSidesInfraredBlackboard = 0x1FF7; // 8183
    public static readonly UsbId McuFourSidesInfraredBlackboard = new UsbId(PID_FourSidesInfraredBlackboard, VID_FourSidesInfraredBlackboard, "mi_00", 0);

    public const int PID_FlatFrogTouchFrame = 0x0F28; // 3880
    public const int VID_FlatFrogTouchFrame = 0x1FF7; // 8183
    public static readonly UsbId McuFlatFrogTouchFrame = new UsbId(PID_FlatFrogTouchFrame, VID_FlatFrogTouchFrame, "mi_00", 0);

    public const int PID_VI = 0x0F33; // 3891
    public const int VID_VI = 0x1FF7; // 8183
    public const int REV_VI = 0x0300; // 768
    public static readonly UsbId McuVI = new UsbId(PID_VI, VID_VI, "mi_00", REV_VI);

    public const int PID_VIAbroad = 0x0F27; // 3879
    public const int VID_VIAbroad = 0x1FF7; // 8183
    public static readonly UsbId McuVIAbroad = new UsbId(PID_VIAbroad, VID_VIAbroad, "mi_00", 0);

    public const int PID_VII = 0x0F33; // 3891
    public const int VID_VII = 0x1FF7; // 8183
    public const int REV_VII = 0x0450; // 1104
    public const int REV_VIIFutureLeft = 0x0452; // 1106
    public const int REV_VIIFutureRight = 0x0454; // 1108
    public static readonly UsbId McuVII = new UsbId(PID_VII, VID_VII, string.Empty, REV_VII);

    public const int PID_VIIFourSidesInfraredBlackboard = 0x0F33; // 3891
    public const int VID_VIIFourSidesInfraredBlackboard = 0x1FF7; // 8183
    public const int REV_VIIFourSidesInfraredBlackboard = 0x0451; // 1105
    public static readonly UsbId McuVIIFourSidesInfraredBlackboard = new UsbId(PID_VIIFourSidesInfraredBlackboard, VID_VIIFourSidesInfraredBlackboard, "mi_00", REV_VIIFourSidesInfraredBlackboard);

    public const int REV_VIIPlusFourSidesInfraredBlackboard = 0x045B; // 1115
    public static readonly UsbId McuVIIPlusFourSidesInfraredBlackboard = new UsbId(PID_VII, VID_VII, "mi_00", REV_VIIPlusFourSidesInfraredBlackboard);

    public const int PID_VIIMultiScreens = 0x0F33; // 3891
    public const int VID_VIIMultiScreens = 0x1FF7; // 8183
    public const int REV_VIIMultiScreensLeft = 0x045D; // 1117
    public const int REV_VIIMultiScreensMiddle = 0x045E; // 1118
    public const int REV_VIIMultiScreensRight = 0x045F; // 1119
    public static readonly UsbId McuVIIMultiScreensLeft = new UsbId(PID_VIIMultiScreens, VID_VIIMultiScreens, "", REV_VIIMultiScreensLeft);
    public static readonly UsbId McuVIIMultiScreensMiddle = new UsbId(PID_VIIMultiScreens, VID_VIIMultiScreens, "", REV_VIIMultiScreensMiddle);
    public static readonly UsbId McuVIIMultiScreensRight = new UsbId(PID_VIIMultiScreens, VID_VIIMultiScreens, "", REV_VIIMultiScreensRight);

    public const int PID_VIII = 0x0F33; // 3891
    public const int VID_VIII = 0x1FF7; // 8183
    public const int REV_VIII = 0x0550; // 1360
    public static readonly UsbId McuVIII = new UsbId(PID_VIII, VID_VIII, string.Empty, REV_VIII);

    public const int PID_VIIIMultiScreens = 0x0F33; // 3891
    public const int VID_VIIIMultiScreens = 0x1FF7; // 8183
    public const int REV_VIIIMultiScreensLeft = 0x0555; // 1365
    public const int REV_VIIIMultiScreensMiddle = 0x0556; // 1366
    public const int REV_VIIIMultiScreensRight = 0x0557; // 1367
    public static readonly UsbId McuVIIIMultiScreensLeft = new UsbId(PID_VIIIMultiScreens, VID_VIIIMultiScreens, "", REV_VIIIMultiScreensLeft);
    public static readonly UsbId McuVIIIMultiScreensMiddle = new UsbId(PID_VIIIMultiScreens, VID_VIIIMultiScreens, "", REV_VIIIMultiScreensMiddle);
    public static readonly UsbId McuVIIIMultiScreensRight = new UsbId(PID_VIIIMultiScreens, VID_VIIIMultiScreens, "", REV_VIIIMultiScreensRight);

    // 仿照Example项目中的定义
    public static readonly List<int> Vids = new() { 0x1FF7, 0x222A }; // 8183, 8746
    public static readonly List<(int minPid, int maxPid)> PidSubsections = new()
    {
        (1, 32),
        (0x0F20, 0x0F30), // 3872, 3888
        (0x0F32, 0x0F3F)  // 3890, 3903
    };
    public static readonly List<string> PchKeys = new() { "mi_00", "col03" };
}