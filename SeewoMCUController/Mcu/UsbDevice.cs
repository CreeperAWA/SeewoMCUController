using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace SeewoMCUController.Mcu
{
    public class UsbDevice
    {
        private SafeFileHandle? _hWriteFile;
        private SafeFileHandle? _hReadFile;

        private const uint GenericRead = WindowsApi.GENERIC_READ;
        private const uint GenericWrite = WindowsApi.GENERIC_WRITE;
        private const uint FileShareRead = WindowsApi.FILE_SHARE_READ;
        private const uint FileShareWrite = WindowsApi.FILE_SHARE_WRITE;
        private const uint FileAttributeNormal = WindowsApi.FILE_ATTRIBUTE_NORMAL;
        private const uint OpenExisting = WindowsApi.OPEN_EXISTING;

        public bool IsConnected
        {
            get
            {
                return _hReadFile != null && !_hReadFile.IsInvalid && _hWriteFile != null && !_hWriteFile.IsInvalid;
            }
        }

        public bool Find(UsbId usbId)
        {
            return CheckDeviceListContain(HidDeviceDiscovery.GetDevices(), usbId);
        }

        public bool Connect(string deviceName)
        {
            Disconnect();
            try
            {
                _hWriteFile = WindowsApi.CreateFile(deviceName, GenericWrite, FileShareRead | FileShareWrite, IntPtr.Zero, OpenExisting, FileAttributeNormal, IntPtr.Zero);
                _hReadFile = WindowsApi.CreateFile(deviceName, GenericRead, FileShareRead | FileShareWrite, IntPtr.Zero, OpenExisting, FileAttributeNormal, IntPtr.Zero);
                if (IsConnected) return true;
                Disconnect();
                return false;
            }
            catch
            {
                Disconnect();
                return false;
            }
        }

        public bool Connect(UsbId usbId)
        {
            if (!string.IsNullOrEmpty(usbId.DeviceName))
            {
                return Connect(usbId.DeviceName);
            }
            return Find(usbId) && Connect(usbId.DeviceName);
        }

        public bool Read(int length, out byte[] data)
        {
            data = new byte[length];
            VerifyConnection();
            uint bytesRead = 0;
            bool ok = WindowsApi.ReadFile(_hReadFile!, data, (uint)length, ref bytesRead, IntPtr.Zero);
            return ok;
        }

        /// <summary>
        /// 阻塞式读取一份 HID 报告，并回报实际读到的字节数。
        /// 供 <see cref="Mcu.McuLink"/> 的专属读线程使用：读取期间不与其它读者竞争句柄。
        /// </summary>
        public bool ReadReport(int length, out byte[] data, out int bytesRead)
        {
            data = new byte[length];
            bytesRead = 0;
            VerifyConnection();
            uint read = 0;
            bool ok = WindowsApi.ReadFile(_hReadFile!, data, (uint)length, ref read, IntPtr.Zero);
            bytesRead = ok ? (int)read : 0;
            return ok;
        }

        public bool Write(byte[] data)
        {
            VerifyConnection();
            uint bytesWritten = 0;
            bool ok = WindowsApi.WriteFile(_hWriteFile!, data, (uint)data.Length, ref bytesWritten, IntPtr.Zero);
            return ok;
        }

        public void Disconnect()
        {
            try { _hWriteFile?.Close(); } catch { }
            try { _hReadFile?.Close(); } catch { }
            _hWriteFile = null;
            _hReadFile = null;
        }

        private bool VerifyConnection()
        {
            if (!IsConnected)
                throw new InvalidOperationException("必须先调用 Connect 连接可以读写的 usb 才可以进行读写");
            return true;
        }

        internal static bool CheckDeviceListContain(List<DeviceInfo> deviceList, UsbId usbId)
        {
            if (!string.IsNullOrEmpty(usbId.DeviceName) && deviceList.Any(temp => string.Equals(temp.DevicePath, usbId.DeviceName, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
            string strSearch = $"vid_{usbId.Vid:x4}&pid_{usbId.Pid:x4}";
            DeviceInfo? deviceInfo = deviceList.FirstOrDefault(deviceItem =>
            {
                bool identityMatches = deviceItem.DevicePath.Contains(strSearch, StringComparison.OrdinalIgnoreCase)
                    && (string.IsNullOrEmpty(usbId.Key)
                        || deviceItem.DevicePath.Contains(usbId.Key, StringComparison.OrdinalIgnoreCase));
                bool revisionMatches = usbId.Rev == 0 || deviceItem.Rev == 0 || usbId.Rev == deviceItem.Rev;
                return identityMatches && revisionMatches;
            });
            if (deviceInfo != null)
            {
                usbId.DeviceName = deviceInfo.DevicePath;
                return true;
            }
            return false;
        }
    }
}
