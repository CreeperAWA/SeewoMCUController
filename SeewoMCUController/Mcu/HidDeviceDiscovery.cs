using System.Runtime.InteropServices;

namespace SeewoMCUController.Mcu;

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