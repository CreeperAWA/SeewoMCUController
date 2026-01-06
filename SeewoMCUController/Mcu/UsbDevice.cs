using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using System.Diagnostics;

namespace SeewoMCUController.Mcu
{
    public class UsbDevice
    {
        private SafeFileHandle? _hWriteFile;
        private SafeFileHandle? _hReadFile;

        private const uint GenericRead = 0x80000000;
        private const uint GenericWrite = 0x40000000;
        private const uint FileShareRead = 0x00000001;
        private const uint FileShareWrite = 0x00000002;
        private const uint FileAttributeNormal = 0x00000080;
        private const uint OpenExisting = 3;

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
            try
            {
                _hWriteFile = WindowsApi.CreateFile(deviceName, GenericWrite, FileShareRead | FileShareWrite, IntPtr.Zero, OpenExisting, FileAttributeNormal, IntPtr.Zero);
                _hReadFile = WindowsApi.CreateFile(deviceName, GenericRead, FileShareRead | FileShareWrite, IntPtr.Zero, OpenExisting, FileAttributeNormal, IntPtr.Zero);
                return IsConnected;
            }
            catch
            {
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

        public bool ReadWithTimeout(int length, out byte[] data, int timeoutMs = 5000)
        {
            data = new byte[length];
            if (!IsConnected || _hReadFile == null)
                return false;

            try
            {
                var sw = Stopwatch.StartNew();
                var buffer = new List<byte>();

                while (sw.ElapsedMilliseconds < timeoutMs)
                {
                    int remaining = Math.Max(1, timeoutMs - (int)sw.ElapsedMilliseconds);

                    var task = Task.Run(() =>
                    {
                        byte[] local = new byte[length];
                        uint bytesRead = 0u;
                        bool ok = WindowsApi.ReadFile(_hReadFile, local, (uint)length, ref bytesRead, IntPtr.Zero);
                        return (ok, local, (int)bytesRead);
                    });

                    bool finished = task.Wait(TimeSpan.FromMilliseconds(remaining));
                    if (!finished)
                    {

                        continue;
                    }

                    var (okRead, localData, bytesReadCount) = task.Result;
                    if (!okRead || bytesReadCount <= 0)
                    {
                        continue;
                    }

                    // append received bytes
                    if (bytesReadCount < localData.Length)
                        buffer.AddRange(localData.Take(bytesReadCount));
                    else
                        buffer.AddRange(localData);

                    // Debug: show first bytes collected
                    try
                    {

                    }
                    catch { }

                    
                    // find header: pattern 0xFC,0xA5,ANY,0x3B,0x00
                    int headerIndex = -1;
                    for (int i = 0; i <= buffer.Count - 5; i++)
                    {
                        if (buffer[i] == 0xFC && buffer[i + 1] == 0xA5 && buffer[i + 3] == 0x3B && buffer[i + 4] == 0x00)
                        {
                            headerIndex = i;
                            break;
                        }
                    }

                    if (headerIndex >= 0)
                    {
                        if (buffer.Count >= headerIndex + 10)
                        {
                            int payloadLen = BitConverter.ToInt16(buffer.ToArray(), headerIndex + 8);
                            int totalLen = 10 + payloadLen;
                            if (buffer.Count >= headerIndex + totalLen)
                            {
                                var packet = buffer.Skip(headerIndex).Take(totalLen).ToArray();
                                int copyLen = Math.Min(length, packet.Length);
                                Array.Clear(data, 0, data.Length);
                                Array.Copy(packet, 0, data, 0, copyLen);
                                return true;
                            }
                        }
                    }

                    if (headerIndex < 0 && buffer.Count >= length)
                    {
                        Array.Clear(data, 0, data.Length);
                        Array.Copy(buffer.Take(length).ToArray(), data, length);
                        return true;
                    }

                    // continue until timeout
                }


                return false;
            }
            catch (Exception)
            {

                return false;
            }
        }

        public bool ReadWithShortTimeout(int length, out byte[] data)
        {
            return ReadWithTimeout(length, out data, 100); // 100ms 超时
        }

        public bool Write(byte[] data)
        {
            VerifyConnection();
            uint bytesWritten = 0;
            bool ok = WindowsApi.WriteFile(_hWriteFile!, data, (uint)data.Length, ref bytesWritten, IntPtr.Zero);
            try
            {

            }
            catch { }
            if (ok)
            {
                Thread.Sleep(80);
            }
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
            string strSearch = string.Format("vid_{0:x4}&pid_{1:x4}", usbId.Vid, usbId.Pid);
            DeviceInfo? deviceInfo = deviceList.FirstOrDefault(deviceItem => (deviceItem.DevicePath.IndexOf(strSearch, StringComparison.Ordinal) >= 0 && deviceItem.DevicePath.IndexOf(usbId.Key, StringComparison.Ordinal) >= 0 && usbId.Rev == 0) || deviceItem.Rev == 0 || usbId.Rev == deviceItem.Rev);
            if (deviceInfo != null)
            {
                usbId.DeviceName = deviceInfo.DevicePath;
                return true;
            }
            return false;
        }
    }
}