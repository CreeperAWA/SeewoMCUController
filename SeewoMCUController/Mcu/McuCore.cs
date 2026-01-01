using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace SeewoMCUController.Mcu;

public class DeviceInfo
{
    public DeviceInfo(string devicePath, int rev)
    {
        DevicePath = devicePath;
        Rev = rev;
    }

    public string DevicePath { get; }
    public int Rev { get; }
}

public class UsbDevice
{
    private SafeFileHandle? _hReadFile;
    private SafeFileHandle? _hWriteFile;

    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint OpenExisting = 3;
    private const uint FileAttributeNormal = 0x00000080;

    public bool IsConnected => _hReadFile != null && !_hReadFile.IsInvalid && 
                               _hWriteFile != null && !_hWriteFile.IsInvalid;

    public bool Find(UsbId usbId)
    {
        return CheckDeviceListContain(HidDeviceDiscovery.GetDevices(), usbId);
    }

    public bool Connect(UsbId usbId)
    {
        if (!string.IsNullOrEmpty(usbId.DeviceName))
        {
            Console.WriteLine($"[调试] UsbDevice.Connect: DeviceName 已设置，直接连接");
            return Connect(usbId.DeviceName);
        }
        Console.WriteLine($"[调试] UsbDevice.Connect: DeviceName 为空，先 Find 再连接");
        return Find(usbId) && Connect(usbId.DeviceName);
    }

    public bool Connect(string devicePath)
    {
        Console.WriteLine($"[调试] UsbDevice.Connect: 尝试连接 {devicePath}");
        try
        {
            // 注意：原始代码使用的是 3U（FileShareRead | FileShareWrite）
            _hWriteFile = WindowsApi.CreateFile(
                devicePath,
                GenericWrite,
                FileShareRead | FileShareWrite,
                IntPtr.Zero,
                OpenExisting,
                FileAttributeNormal,
                IntPtr.Zero);

            _hReadFile = WindowsApi.CreateFile(
                devicePath,
                GenericRead,
                FileShareRead | FileShareWrite,
                IntPtr.Zero,
                OpenExisting,
                FileAttributeNormal,
                IntPtr.Zero);

            bool connected = IsConnected;
            Console.WriteLine($"[调试] UsbDevice.Connect: _hWriteFile.IsInvalid={_hWriteFile?.IsInvalid}");
            Console.WriteLine($"[调试] UsbDevice.Connect: _hReadFile.IsInvalid={_hReadFile?.IsInvalid}");
            Console.WriteLine($"[调试] UsbDevice.Connect: IsConnected={connected}");
            return connected;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[调试] UsbDevice.Connect 异常: {ex.Message}");
            return false;
        }
    }

    public bool Write(byte[] data)
    {
        if (!IsConnected || _hWriteFile == null)
            return false;

        try
        {
            uint bytesWritten = 0;
            return WindowsApi.WriteFile(_hWriteFile, data, (uint)data.Length, ref bytesWritten, IntPtr.Zero);
        }
        catch
        {
            return false;
        }
    }

    public bool Read(int length, out byte[] data)
    {
        data = new byte[length];
        if (!IsConnected || _hReadFile == null)
            return false;

        try
        {
            uint bytesRead = 0;
            return WindowsApi.ReadFile(_hReadFile, data, (uint)length, ref bytesRead, IntPtr.Zero);
        }
        catch
        {
            return false;
        }
    }

    public void Disconnect()
    {
        _hWriteFile?.Dispose();
        _hReadFile?.Dispose();
        _hWriteFile = null;
        _hReadFile = null;
    }

    internal static bool CheckDeviceListContain(List<DeviceInfo> deviceList, UsbId usbId)
    {
        if (!string.IsNullOrEmpty(usbId.DeviceName) && deviceList.Any(temp => string.Equals(temp.DevicePath, usbId.DeviceName, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }
        string strSearch = string.Format("vid_{0:x4}&pid_{1:x4}", usbId.Vid, usbId.Pid);
        DeviceInfo? deviceInfo = deviceList.FirstOrDefault(deviceItem => 
            (deviceItem.DevicePath.IndexOf(strSearch, StringComparison.Ordinal) >= 0 && 
             deviceItem.DevicePath.IndexOf(usbId.Key, StringComparison.Ordinal) >= 0 && 
             usbId.Rev == 0) || 
            deviceItem.Rev == 0 || 
            usbId.Rev == deviceItem.Rev);
        if (deviceInfo != null)
        {
            usbId.DeviceName = deviceInfo.DevicePath;
            return true;
        }
        return false;
    }
}

public static class HidDeviceDiscovery
{
    private static Guid _hidClassGuid = Guid.Empty;
    
    private static Guid HidClassGuid
    {
        get
        {
            if (_hidClassGuid.Equals(Guid.Empty))
            {
                NativeMethods.HidD_GetHidGuid(ref _hidClassGuid);
            }
            return _hidClassGuid;
        }
    }

    public static List<DeviceInfo> GetDevices()
    {
        var devices = new List<DeviceInfo>();
        Guid hidClassGuid = HidClassGuid;
        
        IntPtr deviceInfoSet = NativeMethods.SetupDiGetClassDevs(
            ref hidClassGuid,
            null!,
            0,
            18); // DIGCF_PRESENT | DIGCF_DEVICEINTERFACE

        if (deviceInfoSet.ToInt64() != -1L)
        {
            var deviceInfoData = CreateDeviceInfoData();
            int deviceIndex = 0;
            
            while (NativeMethods.SetupDiEnumDeviceInfo(deviceInfoSet, deviceIndex, ref deviceInfoData))
            {
                deviceIndex++;
                
                var deviceInterfaceData = new NativeMethods.SP_DEVICE_INTERFACE_DATA
                {
                    cbSize = Marshal.SizeOf<NativeMethods.SP_DEVICE_INTERFACE_DATA>()
                };
                
                int interfaceIndex = 0;
                while (NativeMethods.SetupDiEnumDeviceInterfaces(
                    deviceInfoSet,
                    ref deviceInfoData,
                    ref hidClassGuid,
                    interfaceIndex,
                    ref deviceInterfaceData))
                {
                    interfaceIndex++;
                    
                    string? devicePath = GetDevicePath(deviceInfoSet, deviceInterfaceData);
                    if (string.IsNullOrEmpty(devicePath))
                        continue;
                        
                    int rev = GetRevFromHardwareId(deviceInfoSet, ref deviceInfoData);
                    
                    devices.Add(new DeviceInfo(devicePath, rev));
                }
            }
            
            NativeMethods.SetupDiDestroyDeviceInfoList(deviceInfoSet);
        }
        else
        {
            Console.WriteLine("[调试] SetupDiGetClassDevs 返回 -1，失败");
        }
        
        return devices;
    }

    private static NativeMethods.SP_DEVINFO_DATA CreateDeviceInfoData()
    {
        var deviceInfoData = new NativeMethods.SP_DEVINFO_DATA();
        deviceInfoData.cbSize = Marshal.SizeOf<NativeMethods.SP_DEVINFO_DATA>();
        deviceInfoData.DevInst = 0;
        deviceInfoData.ClassGuid = Guid.Empty;
        deviceInfoData.Reserved = IntPtr.Zero;
        return deviceInfoData;
    }

    private static string? GetDevicePath(IntPtr deviceInfoSet, NativeMethods.SP_DEVICE_INTERFACE_DATA deviceInterfaceData)
    {
        int requiredSize = 0;
        var detailData = new NativeMethods.SP_DEVICE_INTERFACE_DETAIL_DATA
        {
            Size = IntPtr.Size == 4 ? (4 + Marshal.SystemDefaultCharSize) : 8
        };

        NativeMethods.SetupDiGetDeviceInterfaceDetailBuffer(
            deviceInfoSet,
            ref deviceInterfaceData,
            IntPtr.Zero,
            0,
            ref requiredSize,
            IntPtr.Zero);

        if (!NativeMethods.SetupDiGetDeviceInterfaceDetail(
            deviceInfoSet,
            ref deviceInterfaceData,
            ref detailData,
            requiredSize,
            ref requiredSize,
            IntPtr.Zero))
        {
            return null;
        }

        return detailData.DevicePath;
    }

    private static unsafe int GetRevFromHardwareId(IntPtr deviceInfoSet, ref NativeMethods.SP_DEVINFO_DATA deviceInfoData)
    {
        int requiredSize;
        NativeMethods.SetupDiGetDeviceRegistryProperty(
            deviceInfoSet,
            ref deviceInfoData,
            1, // SPDRP_HARDWAREID
            out _,
            IntPtr.Zero,
            0,
            out requiredSize);

        if (Marshal.GetLastWin32Error() == 122) // ERROR_INSUFFICIENT_BUFFER
        {
            byte* buffer = stackalloc byte[requiredSize];
            if (NativeMethods.SetupDiGetDeviceRegistryProperty(
                deviceInfoSet,
                ref deviceInfoData,
                1, // SPDRP_HARDWAREID
                out _,
                (IntPtr)buffer,
                requiredSize,
                out _))
            {
                char* endPtr = (char*)(buffer + requiredSize);
                char* ptr = (char*)buffer;
                
                while (ptr + 8 < endPtr)
                {
                    if (*ptr == 'R' &&
                        *(++ptr) == 'E' &&
                        *(++ptr) == 'V' &&
                        *(++ptr) == '_')
                    {
                        // Found REV_, return next 4 chars as hex string
                        string revStr = new string(ptr + 1, 0, 4);
                        if (int.TryParse(revStr, System.Globalization.NumberStyles.HexNumber, null, out int rev))
                        {
                            return rev;
                        }
                    }
                    ptr++;
                }
            }
        }

        return 0;
    }
}

public class UsbId
{
    private string _deviceName = string.Empty;

    public UsbId(int pid, int vid, string key, int rev = 0)
    {
        Pid = pid;
        Vid = vid;
        Key = key;
        Rev = rev;
    }

    public UsbId(string deviceName, int rev = 0)
    {
        DeviceName = deviceName;
        Rev = rev;
    }

    public int Pid { get; private set; } = -1;
    public int Vid { get; private set; } = -1;
    public string Key { get; private set; } = string.Empty;
    public int Rev { get; }

    public string DeviceName
    {
        get => _deviceName;
        set
        {
            _deviceName = value;
            if (!TryParseDeviceName(_deviceName))
            {
                Vid = -1;
                Pid = -1;
                Key = "";
            }
        }
    }

    private bool TryParseDeviceName(string deviceName)
    {
        if (string.IsNullOrEmpty(deviceName))
            return false;

        string[] parts = deviceName.Split('#');
        if (parts.Length == 4)
        {
            string[] idParts = parts[1].Split('&');
            try
            {
                // Parse VID
                string[] vidParts = idParts[0].Split('_');
                if (vidParts.Length != 2)
                    return false;

                if (!int.TryParse(vidParts[1], System.Globalization.NumberStyles.AllowHexSpecifier, System.Globalization.CultureInfo.InvariantCulture, out int vid))
                    return false;

                // Parse PID
                string[] pidParts = idParts[1].Split('_');
                if (pidParts.Length != 2)
                    return false;

                if (!int.TryParse(pidParts[1], System.Globalization.NumberStyles.AllowHexSpecifier, System.Globalization.CultureInfo.InvariantCulture, out int pid))
                    return false;

                Vid = vid;
                Pid = pid;

                // Extract Key - exactly as original code
                int keyStartIndex = parts[1].IndexOf(idParts[1], StringComparison.Ordinal) + idParts[1].Length + 1;
                Key = keyStartIndex < parts[1].Length ? parts[1].Substring(keyStartIndex, parts[1].Length - keyStartIndex) : "";
                
                return true;
            }
            catch
            {
                return false;
            }
        }
        return false;
    }

    public override string ToString()
    {
        return $"Pid:0x{Pid:X4},Vid:0x{Vid:X4},Key:{Key}";
    }
}

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

    public static UsbId? DetectedMcu { get; internal set; }

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
        if (DetectedMcu == null)
        {
            DetectedMcu = list.FirstOrDefault();
        }
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
        var cmd = new byte[64];
        cmd[0] = 0xFC;
        cmd[1] = 0xA5;
        cmd[2] = 0x05;
        cmd[3] = 0x3B;
        cmd[4] = 0x00;
        cmd[5] = 0xFB;
        cmd[6] = 0x33; // 51
        cmd[8] = 0x01;
        cmd[9] = 0x00;
        bool result = Usb.Write(cmd);
        Console.WriteLine($"[调试] CanWrite 测试结果: {result}");
        return result;
    }

    private static UsbDevice Usb = new UsbDevice();

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

internal class UsbIdSortComparer : IComparer<UsbId?>
{
    private readonly List<UsbIdIndex> UsbIdSortList;

    public UsbIdSortComparer(IEnumerable<UsbId> usbIdSortList)
    {
        var list = usbIdSortList.Select((temp, i) => new UsbIdIndex(temp, i)).ToList();
        this.UsbIdSortList = list;
    }

    public int Compare(UsbId? x, UsbId? y)
    {
        if (x == y)
        {
            return 0;
        }
        if (y == null)
        {
            return -1;
        }
        if (x == null)
        {
            return 1;
        }
        UsbIdIndex usbIdIndex = GetUsbIdIndex(x);
        UsbIdIndex usbIdIndex2 = GetUsbIdIndex(y);
        if (usbIdIndex.Index == usbIdIndex2.Index)
        {
            return 0;
        }
        return usbIdIndex.Index.CompareTo(usbIdIndex2.Index);
    }

    private UsbIdIndex GetUsbIdIndex(UsbId? usbId)
    {
        foreach (UsbIdIndex usbIdIndex in this.UsbIdSortList)
        {
            if (IsUsbIdEquals(usbIdIndex.UsbId, usbId))
            {
                return usbIdIndex;
            }
        }
        // 如果是 podium 类型设备，返回特殊索引
        if (IsUsbIdEquals(McuPodium, usbId))
        {
            return new UsbIdIndex(usbId, 10001);
        }
        return new UsbIdIndex(usbId, 10000);
    }

    private static bool IsUsbIdEquals(UsbId? patternUsbId, UsbId? usbId)
    {
        return patternUsbId != null && usbId != null && patternUsbId.Pid == usbId.Pid && patternUsbId.Vid == usbId.Vid && (patternUsbId.Rev == 0 || usbId.Rev == 0 || patternUsbId.Rev == usbId.Rev) && (string.IsNullOrEmpty(patternUsbId.Key) || IsStringContainIgnoreCase(usbId.Key, patternUsbId.Key) || IsStringContainIgnoreCase(usbId.DeviceName, patternUsbId.Key));
    }

    private static bool IsStringContainIgnoreCase(string? str, string? pattern)
    {
        return str != null && (string.IsNullOrEmpty(pattern) || str.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) > -1);
    }

    private static UsbId McuPodium = new UsbId(0x0F31, 0x1FF7, "mi_00", 0); // 3889, 8183

    private readonly struct UsbIdIndex
    {
        public UsbIdIndex(UsbId? usbId, int index)
        {
            UsbId = usbId;
            Index = index;
        }

        public UsbId? UsbId { get; }
        public int Index { get; }
    }
}

public static class McuCommands
{
    private const byte HEADER_0 = 0xFC;
    private const byte HEADER_1 = 0xA5;
    private const byte HEADER_2 = 0x05;
    private const byte HEADER_3 = 0x3B;
    private const byte HEADER_4 = 0x00;
    private const byte DEVICE_ID = 0xFB;
    private const int DATA_LENGTH = 64;

    public static byte[] GetMcuVersionCommand()
    {
        var cmd = new byte[DATA_LENGTH];
        cmd[0] = HEADER_0;
        cmd[1] = HEADER_1;
        cmd[2] = HEADER_2;
        cmd[3] = HEADER_3;
        cmd[4] = HEADER_4;
        cmd[5] = DEVICE_ID;
        cmd[6] = 0x16; // Main command
        cmd[7] = 0x02; // Sub command
        return cmd;
    }

    public static byte[] GetBoardNameCommand()
    {
        var cmd = new byte[DATA_LENGTH];
        cmd[0] = HEADER_0;
        cmd[1] = HEADER_1;
        cmd[2] = HEADER_2;
        cmd[3] = HEADER_3;
        cmd[4] = HEADER_4;
        cmd[5] = DEVICE_ID;
        cmd[6] = 0x34; // Main command: 52
        cmd[7] = 0x0C; // Sub command: 12
        return cmd;
    }

    public static byte[] GetIPCommand()
    {
        var cmd = new byte[DATA_LENGTH];
        cmd[0] = HEADER_0;
        cmd[1] = HEADER_1;
        cmd[2] = HEADER_2;
        cmd[3] = HEADER_3;
        cmd[4] = HEADER_4;
        cmd[5] = DEVICE_ID;
        cmd[6] = 0x34; // Main command: 52
        cmd[7] = 0x10; // Sub command: 16
        return cmd;
    }

    public static byte[] GetUidCommand()
    {
        var cmd = new byte[DATA_LENGTH];
        cmd[0] = HEADER_0;
        cmd[1] = HEADER_1;
        cmd[2] = HEADER_2;
        cmd[3] = HEADER_3;
        cmd[4] = HEADER_4;
        cmd[5] = DEVICE_ID;
        cmd[6] = 0x40; // Main command: 64
        cmd[7] = 0x00; // Sub command
        return cmd;
    }

    public static byte[] AddVolumeCommand()
    {
        var cmd = new byte[DATA_LENGTH];
        cmd[0] = HEADER_0;
        cmd[1] = HEADER_1;
        cmd[2] = HEADER_2;
        cmd[3] = HEADER_3;
        cmd[4] = HEADER_4;
        cmd[5] = DEVICE_ID;
        cmd[6] = 0x08; // Main command: 8
        cmd[7] = 0x02; // Sub command: 2 (increase)
        return cmd;
    }

    public static byte[] DecreaseVolumeCommand()
    {
        var cmd = new byte[DATA_LENGTH];
        cmd[0] = HEADER_0;
        cmd[1] = HEADER_1;
        cmd[2] = HEADER_2;
        cmd[3] = HEADER_3;
        cmd[4] = HEADER_4;
        cmd[5] = DEVICE_ID;
        cmd[6] = 0x08; // Main command: 8
        cmd[7] = 0x01; // Sub command: 1 (decrease)
        return cmd;
    }

    public static byte[] SwitchToHdmi1Command()
    {
        var cmd = new byte[DATA_LENGTH];
        cmd[0] = HEADER_0;
        cmd[1] = HEADER_1;
        cmd[2] = HEADER_2;
        cmd[3] = HEADER_3;
        cmd[4] = HEADER_4;
        cmd[5] = DEVICE_ID;
        cmd[6] = 0x07; // Main command: 7
        cmd[7] = 0x09; // Sub command: 9
        cmd[8] = 0x00;
        return cmd;
    }

    public static byte[] OpenAndroidPenCommand()
    {
        var cmd = new byte[DATA_LENGTH];
        cmd[0] = HEADER_0;
        cmd[1] = HEADER_1;
        cmd[2] = HEADER_2;
        cmd[3] = HEADER_3;
        cmd[4] = HEADER_4;
        cmd[5] = DEVICE_ID;
        cmd[6] = 0x2A; // Main command: 42
        cmd[7] = 0x00;
        cmd[8] = 0x01;
        cmd[9] = 0x00;
        cmd[10] = 0x01; // Enable
        return cmd;
    }

    public static byte[] ForbiddenAndroidPenCommand()
    {
        var cmd = new byte[DATA_LENGTH];
        cmd[0] = HEADER_0;
        cmd[1] = HEADER_1;
        cmd[2] = HEADER_2;
        cmd[3] = HEADER_3;
        cmd[4] = HEADER_4;
        cmd[5] = DEVICE_ID;
        cmd[6] = 0x2A; // Main command: 42
        cmd[7] = 0x00;
        cmd[8] = 0x01;
        cmd[9] = 0x00;
        cmd[10] = 0x00; // Disable
        return cmd;
    }
}

public class McuController
{
    private UsbDevice? _device;
    private UsbId? _usbId;
    private const int DATA_LENGTH = 64;

    public bool IsConnected => _device?.IsConnected == true;

    public bool FindAndConnect()
    {
        try
        {
            // 使用完整的 FindAndConnection 逻辑，遍历所有设备直到找到可写的
            bool result = McuFinder.FindAndConnection();
            if (result)
            {
                // 成功连接后，获取已检测到的设备并创建连接
                _usbId = McuFinder.GetOrFindMcu();
                if (_usbId != null)
                {
                    _device = new UsbDevice();
                    // 直接使用已找到的设备路径进行连接
                    bool connected = _device.Connect(_usbId.DeviceName);
                    Console.WriteLine($"[调试] McuController 连接结果: {connected}");
                    return connected;
                }
            }
            
            Console.WriteLine("[调试] McuFinder.FindAndConnection() 返回 false");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[调试] 连接异常: {ex.Message}");
            return false;
        }
    }

    public bool FindAndConnect(UsbId usbId)
    {
        try
        {
            Console.WriteLine($"[调试] 尝试连接指定设备: {usbId.DeviceName}");
            _device = new UsbDevice();
            bool connected = _device.Connect(usbId.DeviceName);
            Console.WriteLine($"[调试] 连接结果: {connected}");
            if (connected)
            {
                _usbId = usbId;
                McuFinder.DetectedMcu = usbId;
            }
            return connected;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[调试] 连接异常: {ex.Message}");
            return false;
        }
    }

    public string GetBoardName()
    {
        if (!IsConnected) return string.Empty;

        try
        {
            if (!_device!.Write(McuCommands.GetBoardNameCommand()))
                return string.Empty;

            for (int i = 0; i < 3; i++)
            {
                if (!_device.Read(DATA_LENGTH, out byte[] data))
                    continue;

                if (data[6] == 52 && data[7] == 13) // Command response
                {
                    short length = BitConverter.ToInt16(data, 8);
                    if (length > 0 && length < data.Length - 10)
                    {
                        return Encoding.UTF8.GetString(data, 10, length);
                    }
                }
            }
        }
        catch { }

        return string.Empty;
    }

    public string GetIP()
    {
        if (!IsConnected) return string.Empty;

        try
        {
            if (!_device!.Write(McuCommands.GetIPCommand()))
                return string.Empty;

            for (int i = 0; i < 3; i++)
            {
                if (!_device.Read(DATA_LENGTH, out byte[] data))
                    continue;

                if (data[6] == 52 && data[7] == 17) // Command response
                {
                    short length = BitConverter.ToInt16(data, 8);
                    if (length > 0 && length < data.Length - 10)
                    {
                        return Encoding.UTF8.GetString(data, 10, length);
                    }
                }
            }
        }
        catch { }

        return string.Empty;
    }

    public string GetUid()
    {
        if (!IsConnected) return string.Empty;

        try
        {
            if (!_device!.Write(McuCommands.GetUidCommand()))
                return string.Empty;

            if (!_device.Read(DATA_LENGTH, out byte[] data))
                return string.Empty;

            if (data[6] == 0xC1) // Command response: 193
            {
                var sb = new StringBuilder();
                for (int i = 0; i < 12; i++)
                {
                    sb.Append(data[10 + i].ToString("x2"));
                    if (i < 11) sb.Append(" ");
                }
                return sb.ToString();
            }
        }
        catch { }

        return string.Empty;
    }

    public int GetCVTouchSize()
    {
        if (!IsConnected) return -1;

        try
        {
            if (!_device!.Write(McuCommands.GetMcuVersionCommand()))
                return -1;

            for (int i = 0; i < 3; i++)
            {
                if (!_device.Read(DATA_LENGTH, out byte[] data))
                    continue;

                // Try to parse touch size from version data
                // This is a simplified version
                if (data[6] == 0x16) // Version command response
                {
                    // Try to extract size info (simplified)
                    return 86; // Default size for most Seewo devices
                }
            }
        }
        catch { }

        return -1;
    }

    public bool AddVolume()
    {
        if (!IsConnected) return false;

        try
        {
            if (!_device!.Write(McuCommands.AddVolumeCommand()))
                return false;

            if (!_device.Read(DATA_LENGTH, out byte[] data))
                return false;

            return data[6] == 0xA1; // Success response: 161
        }
        catch
        {
            return false;
        }
    }

    public bool DecreaseVolume()
    {
        if (!IsConnected) return false;

        try
        {
            if (!_device!.Write(McuCommands.DecreaseVolumeCommand()))
                return false;

            if (!_device.Read(DATA_LENGTH, out byte[] data))
                return false;

            return data[6] == 0xA1; // Success response: 161
        }
        catch
        {
            return false;
        }
    }

    public bool SwitchToHdmi1()
    {
        if (!IsConnected) return false;

        try
        {
            return _device!.Write(McuCommands.SwitchToHdmi1Command());
        }
        catch
        {
            return false;
        }
    }

    public bool OpenAndroidPen()
    {
        if (!IsConnected) return false;

        try
        {
            return _device!.Write(McuCommands.OpenAndroidPenCommand());
        }
        catch
        {
            return false;
        }
    }

    public bool ForbiddenAndroidPen()
    {
        if (!IsConnected) return false;

        try
        {
            return _device!.Write(McuCommands.ForbiddenAndroidPenCommand());
        }
        catch
        {
            return false;
        }
    }

    public void Disconnect()
    {
        _device?.Disconnect();
    }

    public int GetDeviceVid()
    {
        if (_usbId != null)
        {
            return _usbId.Vid;
        }
        return -1;
    }

    public int GetDevicePid()
    {
        if (_usbId != null)
        {
            return _usbId.Pid;
        }
        return -1;
    }

    public string GetDevicePath()
    {
        if (_usbId != null)
        {
            return _usbId.DeviceName ?? "未知";
        }
        return "未知";
    }
}