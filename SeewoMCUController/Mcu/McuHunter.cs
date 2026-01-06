using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SeewoMCUController.Mcu;

namespace Cvte.Mcu
{
    public class McuHunter
    {
        // 预定义的MCU设备类型，与Example项目中的McuHunter.DefineDeviceMcu完全一致
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
            McuVIIIMultiScreensRight,
            McuPodium // 新增的设备类型
        };

        public static bool FindMcu()
        {
            try
            {
                return FindAll().Any();
            }
            catch (Exception)
            {
            }
            return false;
        }

        public static List<UsbId> FindAll()
        {
            List<UsbId> list = new List<UsbId>();
            foreach (DeviceInfo deviceInfo in HidDeviceDiscovery.GetDevices())
            {
                string devicePath = deviceInfo.DevicePath;
                int rev = deviceInfo.Rev;
                UsbId usbId = new UsbId(devicePath, rev);
                if (usbId.Pid != -1 && usbId.Vid != -1)
                {
                    if (CheckUsbDetectedMcu(usbId))
                    {
                        list.Add(usbId);
                    }
                }
                else if (CheckDetectedMcu(deviceInfo))
                {
                    list.Add(new UsbId(devicePath, rev));
                }
            }
            list = SortUsbIdList(list);
            if (DetectedMcu == null)
            {
                DetectedMcu = list.FirstOrDefault();
            }
            return list;
        }

        public static bool Write(byte[] array)
        {
            bool flag;
            try
            {
                if (DetectedMcu == null && !FindAndConnection())
                {
                    flag = false;
                }
                else if (!Usb.Connect(DetectedMcu) && !FindAndConnection())
                {
                    flag = false;
                }
                else
                {
                    // 尝试写入，如果失败则重新查找连接并再次尝试
                    flag = Usb.Write(array);
                    if (!flag)
                    {
                        // 如果写入失败，尝试重新查找连接并再次写入
                        if (FindAndConnection())
                        {
                            flag = Usb.Write(array);
                        }
                    }
                }
            }
            catch (Exception)
            {
                flag = false;
            }
            return flag;
        }

        public static bool Write(UsbId? usbId, byte[] array)
        {
            if (usbId == null) return false;
            UsbDevice usb = new UsbDevice();
            return usb.Connect(usbId) && usb.Write(array);
        }

        public static bool Read(int dataLength, out byte[] data)
        {
            data = new byte[dataLength];
            bool flag;
            try
            {
                flag = Usb.Read(dataLength, out data);
            }
            catch (Exception)
            {
                flag = false;
            }
            return flag;
        }

        public static bool ReadWithShortTimeout(int dataLength, out byte[] data)
        {
            data = new byte[dataLength];
            bool flag;
            try
            {
                flag = Usb.ReadWithShortTimeout(dataLength, out data);
            }
            catch (Exception)
            {
                flag = false;
            }
            return flag;
        }

        public static void CloseConnection()
        {
            try
            {
                UsbDevice usb = Usb;
                if (usb != null)
                {
                    usb.Disconnect();
                }
            }
            catch (Exception)
            {
            }
        }

        public static UsbId? DetectedMcu { get; set; }

        public static UsbId GetOrFindMcu()
        {
            if (DetectedMcu != null)
            {
                return DetectedMcu;
            }
            FindMcu();
            return DetectedMcu;
        }

        private static bool FindAndConnectionInternal()
        {
            try
            {
                foreach (UsbId usbId in FindAll())
                {
                    if (Usb.Connect(usbId) && CanWrite())
                    {
                        DetectedMcu = usbId;
                        return true;
                    }
                }
            }
            catch (Exception)
            {
            }
            return false;
        }
        
        public static bool FindAndConnection()
        {
            try
            {
                foreach (UsbId usbId in FindAll())
                {
                    if (Usb.Connect(usbId) && CanWrite())
                    {
                        DetectedMcu = usbId;
                        return true;
                    }
                }
            }
            catch (Exception)
            {
            }
            return false;
        }

        private static IEnumerable<UsbId> GetUsbId()
        {
            return DefineDeviceMcu;
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
            if (DetectedMcu != null && IsUsbIdEquals(DetectedMcu, usbId))
            {
                return true;
            }
            foreach (var definedUsbId in DefineDeviceMcu)
            {
                if (definedUsbId != null && IsUsbIdEquals(definedUsbId, usbId))
                {
                    return true;
                }
            }

            foreach (var (minPid, maxPid) in PidSubsection)
            {
                int item = minPid;
                int item2 = maxPid;
                foreach (int num in Vids)
                {
                    foreach (string text in PchKey)
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

        private static bool CanWrite()
        {
            return Usb.Write(McuCommand.CommandBase);
        }

        private static bool IsUsbIdEquals(UsbId patternUsbId, UsbId usbId)
        {
            return patternUsbId != null && usbId != null && 
                   patternUsbId.Pid == usbId.Pid && 
                   patternUsbId.Vid == usbId.Vid && 
                   (patternUsbId.Rev == 0 || usbId.Rev == 0 || patternUsbId.Rev == usbId.Rev) && 
                   (string.IsNullOrEmpty(patternUsbId.Key) || 
                    IsStringContainIgnoreCase(usbId.Key, patternUsbId.Key) || 
                    IsStringContainIgnoreCase(usbId.DeviceName, patternUsbId.Key));
        }

        private static bool IsStringContainIgnoreCase(string str, string pattern)
        {
            return str != null && (string.IsNullOrEmpty(pattern) || str.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) > -1);
        }

        // 设备定义，与Example项目中的McuHunter完全一致
        public const int DefaultDataLength = 64;

        public static readonly List<int> Vids = new List<int> { 8183, 8746 };

        public static readonly List<(int minPid, int maxPid)> PidSubsection = new List<(int minPid, int maxPid)>
        {
            (1, 32),
            (3872, 3888),
            (3890, 3903)
        };

        public static readonly List<string> PchKey = new List<string> { "mi_00", "col03" };

        public static readonly UsbDevice Usb = new UsbDevice();

        // Example-McuHunter.cs 兼容方法
        public static bool WriteToDevice(byte[] array)
        {
            bool flag;
            try
            {
                if (DetectedMcu == null && !FindAndConnection())
                {
                    flag = false;
                }
                else if (!Usb.Connect(DetectedMcu) && !FindAndConnection())
                {
                    flag = false;
                }
                else
                {
                    // 尝试写入，如果失败则重新查找连接并再次尝试
                    flag = Usb.Write(array);
                    if (!flag)
                    {
                        // 如果写入失败，尝试重新查找连接并再次写入
                        if (FindAndConnection())
                        {
                            flag = Usb.Write(array);
                        }
                    }
                }
            }
            catch (Exception)
            {
                flag = false;
            }
            return flag;
        }

        public static bool WriteToDevice(UsbId? usbId, byte[] array)
        {
            if (usbId == null) return false;
            UsbDevice usb = new UsbDevice();
            return usb.Connect(usbId) && usb.Write(array);
        }

        public static bool ReadFromDevice(int dataLength, out byte[] data)
        {
            data = new byte[dataLength];
            bool flag;
            try
            {
                flag = Usb.Read(dataLength, out data);
            }
            catch (Exception)
            {
                flag = false;
            }
            return flag;
        }

        public static void CloseDeviceConnection()
        {
            try
            {
                UsbDevice usb = Usb;
                if (usb != null)
                {
                    usb.Disconnect();
                }
            }
            catch (Exception)
            {
            }
        }

        public static AsyncOperation<ValueTuple<bool, byte[]>> TryReadFromDeviceAsync(int dataLength, int timeout = 3, string methodName = "")
        {
            var asyncOperation = AsyncOperation<ValueTuple<bool, byte[]>>.Create(out Action<ValueTuple<bool, byte[]>, Exception> resultCallback);
            Thread thread = new Thread((ThreadStart)delegate
            {
                try
                {
                    byte[] array;
                    Usb.Read(dataLength, out array);
                    resultCallback(new ValueTuple<bool, byte[]>(true, array), null);
                }
                catch (ThreadAbortException)
                {
                    // Thread.ResetAbort() 已过时，不再调用
                }
            });
            thread.Start();
            return asyncOperation;
        }

        private const string DefaultSeewoPchKey = "mi_00";
        private const string DefaultPchKeyCom = "col03";
        private const int VID = 8183;
        private const int PID = 3861;
        private const string KEY = "mi_00&col02";
        public static readonly UsbId CommonMcu = new UsbId(8183, 3861, "mi_00&col02", 0);

        private const int VID_638 = 8183;
        private const int PID_638 = 3861;
        private const string KEY_638 = "mi_00";
        public static readonly UsbId Mcu638 = new UsbId(3861, 8183, "mi_00", 0);

        private const int PID_TOUCH = 1;
        private const int VID_TOUCH = 8183;
        private const string KEY_TOUCH = "col03";
        public static readonly UsbId McuTouch = new UsbId(1, 8183, "col03", 0);

        private const int VID_309 = 4320;
        private const int PID_309 = 43605;
        private const string KEY_309 = "col01";
        public static readonly UsbId Mcu309 = new UsbId(43605, 4320, "col01", 0);

        private const int PID_551 = 3861;
        private const int VID_551 = 8183;
        private const string KEY_551 = "mi_00";
        public static readonly UsbId Mcu551 = new UsbId(3861, 8183, "mi_00", 0);

        private const int PID_551B2 = 3873;
        private const int VID_551B2 = 8183;
        private const string KEY_551B2 = "mi_00&col02";
        public static readonly UsbId Mcu551B2 = new UsbId(3873, 8183, "mi_00&col02", 0);

        public const int VID_CVTE = 8183;
        public const int PID_V = 3878;
        public const int VID_V = 8183;
        public static readonly UsbId McuV = new UsbId(3878, 8183, "mi_00", 0);

        public const int PID_FourSidesInfraredBlackboard = 3920;
        public const int VID_FourSidesInfraredBlackboard = 8183;
        public static readonly UsbId McuFourSidesInfraredBlackboard = new UsbId(3920, 8183, "mi_00", 0);

        public const int PID_FlatFrogTouchFrame = 3880;
        public const int VID_FlatFrogTouchFrame = 8183;
        public static readonly UsbId McuFlatFrogTouchFrame = new UsbId(3880, 8183, "mi_00", 0);

        public const int PID_VI = 3891;
        public const int VID_VI = 8183;
        public const int REV_VI = 768;
        public static readonly UsbId McuVI = new UsbId(3891, 8183, "mi_00", 768);

        public const int PID_VIAbroad = 3879;
        public const int VID_VIAbroad = 8183;
        public static readonly UsbId McuVIAbroad = new UsbId(3879, 8183, "mi_00", 0);

        public const int PID_VII = 3891;
        public const int VID_VII = 8183;
        public const int REV_VII = 1104;
        public const int REV_VIIFutureLeft = 1106;
        public const int REV_VIIFutureRight = 1108;
        public static readonly UsbId McuVII = new UsbId(3891, 8183, string.Empty, 1104);

        public const int PID_VIIFourSidesInfraredBlackboard = 3891;
        public const int VID_VIIFourSidesInfraredBlackboard = 8183;
        public const int REV_VIIFourSidesInfraredBlackboard = 1105;
        public static readonly UsbId McuVIIFourSidesInfraredBlackboard = new UsbId(3891, 8183, "mi_00", 1105);

        public const int REV_VIIPlusFourSidesInfraredBlackboard = 1107;
        public static readonly UsbId McuVIIPlusFourSidesInfraredBlackboard = new UsbId(3891, 8183, "mi_00", 1107);

        public const int PID_VIIMultiScreens = 3891;
        public const int VID_VIIMultiScreens = 8183;
        public const int REV_VIIMultiScreensLeft = 1109;
        public const int REV_VIIMultiScreensMiddle = 1110;
        public const int REV_VIIMultiScreensRight = 1111;
        public static readonly UsbId McuVIIMultiScreensLeft = new UsbId(3891, 8183, "", 1109);
        public static readonly UsbId McuVIIMultiScreensMiddle = new UsbId(3891, 8183, "", 1110);
        public static readonly UsbId McuVIIMultiScreensRight = new UsbId(3891, 8183, "", 1111);

        public const int PID_VIII = 3891;
        public const int VID_VIII = 8183;
        public const int REV_VIII = 1360;
        public static readonly UsbId McuVIII = new UsbId(3891, 8183, string.Empty, 1360);

        public const int PID_VIIIMultiScreens = 3891;
        public const int VID_VIIIMultiScreens = 8183;
        public const int REV_VIIIMultiScreensLeft = 1365;
        public const int REV_VIIIMultiScreensMiddle = 1366;
        public const int REV_VIIIMultiScreensRight = 1367;
        public static readonly UsbId McuVIIIMultiScreensLeft = new UsbId(3891, 8183, "", 1365);
        public static readonly UsbId McuVIIIMultiScreensMiddle = new UsbId(3891, 8183, "", 1366);
        public static readonly UsbId McuVIIIMultiScreensRight = new UsbId(3891, 8183, "", 1367);

        // 新增的Podium设备
        public const int PID_Podium = 3889;
        public const int VID_Podium = 8183;
        public static readonly UsbId McuPodium = new UsbId(3889, 8183, "mi_00", 0);
    }
}