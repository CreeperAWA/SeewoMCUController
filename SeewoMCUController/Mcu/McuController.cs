using System.Text;

namespace SeewoMCUController.Mcu;

public class McuController
{
    private const int DATA_LENGTH = 64;

    public bool IsConnected => McuFinder.Usb.IsConnected;

    // Use only the already-set DetectedMcu. Do NOT call FindAll or rescan on failure.
    private bool TrySendAndRead(byte[] cmd, out byte[] data, int timeoutMs = 10000)
    {
        data = new byte[DATA_LENGTH];
        try
        {
            var detected = McuFinder.DetectedMcu;
            if (detected == null)
            {
                Console.WriteLine("[调试] TrySendAndRead: 未设置 DetectedMcu，取消读取");
                return false;
            }

            Console.WriteLine($"[调试] TrySendAndRead: 使用已检测接口 {detected.DeviceName}");

            // Ensure connected to detected interface
            if (!McuFinder.Usb.Connect(detected))
            {
                Console.WriteLine($"[调试] TrySendAndRead: 连接 DetectedMcu 失败: {detected.DeviceName}");
                return false;
            }

            // Use a short read timeout of 100ms per attempt as requested
            int readTimeout = 100;

            // Try up to 3 times: write then read with 100ms timeout
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                if (!McuFinder.Usb.Write(cmd))
                {
                    Console.WriteLine($"[调试] TrySendAndRead: Write 失败，尝试 #{attempt}");
                    continue;
                }

                if (McuFinder.Usb.ReadWithTimeout(DATA_LENGTH, out data, readTimeout))
                {
                    Console.WriteLine($"[调试] TrySendAndRead: 读取成功 on attempt #{attempt}");
                    return true;
                }

                Console.WriteLine($"[调试] TrySendAndRead: 读取超时/失败 on attempt #{attempt}");
            }

            // Do NOT attempt 0x00-leading variant at all
            Console.WriteLine("[调试] TrySendAndRead: 所有三次常规尝试失败（未尝试 0x00 变体）");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[调试] TrySendAndRead 异常: {ex.Message}");
            return false;
        }
    }

    public bool FindAndConnect()
    {
        try
        {
            // 使用完整的 FindAndConnection 逻辑，遍历所有设备直到找到可写的
            bool result = McuFinder.FindAndConnection();
            if (result)
            {
                Console.WriteLine($"[调试] McuController 连接结果: {McuFinder.Usb.IsConnected}");
                return McuFinder.Usb.IsConnected;
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
            bool connected = McuFinder.Usb.Connect(usbId.DeviceName);
            Console.WriteLine($"[调试] 连接结果: {connected}");
            if (connected)
            {
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
            var cmd = McuCommand.GetBoardNameCommand;
            if (!TrySendAndRead(cmd, out byte[] data, 10000))
            {
                Console.WriteLine("[调试] GetBoardName Write/Read 失败");
                return string.Empty;
            }

            Console.WriteLine($"[调试] 读取到主板名称响应数据: {BitConverter.ToString(data, 0, Math.Min(10, data.Length))}...");
            if (data[6] == 52 && data[7] == 13) // Command response
            {
                short length = BitConverter.ToInt16(data, 8);
                byte[] array2 = new byte[(int)length];
                Buffer.BlockCopy(data, 10, array2, 0, (int)length);
                string result = Encoding.UTF8.GetString(array2);
                Console.WriteLine($"[调试] 成功获取主板名称: {result}");
                return result;
            }

            Console.WriteLine($"[调试] GetBoardName 读取到的数据错误");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[调试] ExceptionError: {ex.Message}");
            Console.WriteLine(ex);
        }

        return string.Empty;
    }

    public string GetIP()
    {
        if (!IsConnected) return string.Empty;

        try
        {
            var cmd = McuCommand.GetIPCommand;
            for (int i = 0; i < 3; i++)
            {
                if (!TrySendAndRead(cmd, out byte[] data, 10000))
                {
                    Console.WriteLine($"[调试] 读取IP响应尝试 #{i+1} 失败");
                    continue;
                }

                Console.WriteLine($"[调试] 读取到IP响应数据: {BitConverter.ToString(data, 0, Math.Min(10, data.Length))}...");
                if (data[6] == 52 && data[7] == 17) // Command response
                {
                    short length = BitConverter.ToInt16(data, 8);
                    byte[] array2 = new byte[(int)length];
                    Buffer.BlockCopy(data, 10, array2, 0, (int)length);
                    string result = Encoding.UTF8.GetString(array2);
                    Console.WriteLine($"[调试] 成功获取IP: {result}");
                    return result;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }

        return string.Empty;
    }

    public string GetUid()
    {
        if (!IsConnected) return string.Empty;

        try
        {
            var cmd = McuCommand.GetUidCommand;
            if (!TrySendAndRead(cmd, out byte[] data, 10000))
            {
                Console.WriteLine("[调试] GetUid Write/Read 失败");
                return string.Empty;
            }

            Console.WriteLine($"[调试] 读取到UID响应数据: {BitConverter.ToString(data, 0, Math.Min(10, data.Length))}...");
            var sb = new StringBuilder();
            if (data[6] == 0xC1) // Command response: 193
            {
                for (int i = 0; i < 12; i++)
                {
                    sb.Append(data[10 + i].ToString("x2") + " ");
                }
                string result = sb.ToString();
                Console.WriteLine($"[调试] 成功获取UID: {result}");
                return result;
            }

            Console.WriteLine("[调试] GetUid 数据错误");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[调试] GetUID 异常: {ex.Message}");
        }

        return string.Empty;
    }

    public int GetCVTouchSize()
    {
        if (!IsConnected) return -1;

        try
        {
            var cmd = McuCommand.GetMcuVersionCommand;
            for (int i = 0; i < 3; i++)
            {
                Console.WriteLine($"[调试] 尝试读取触摸屏尺寸响应 #{i+1}");
                if (!TrySendAndRead(cmd, out byte[] data, 10000))
                {
                    Console.WriteLine($"[调试] GetCVTouchSize ReadError, 尝试 #{i+1}");
                    continue;
                }

                Console.WriteLine($"[调试] 读取到触摸屏尺寸响应数据: {BitConverter.ToString(data, 0, Math.Min(10, data.Length))}...");
                if (CVTouchAnalyzer.IsMatchCVTouchVersion(data))
                {
                    Console.WriteLine($"[调试] GetCVTouchSize: 读取到屏幕尺寸");
                    return CVTouchAnalyzer.GetCVTouchSize(data);
                }

                Console.WriteLine($"[调试] GetCVTouchSize: 读取到数据格式错误");
            }
            Console.WriteLine($"[调试] GetCVTouchSize 读取到的数据错误");
        }
        catch (Exception)
        {
            return -1;
        }

        return -1;
    }

    public string GetMcuVersion()
    {
        if (!IsConnected) return string.Empty;

        try
        {
            var cmd = McuCommand.GetMcuVersionCommand;
            for (int i = 0; i < 3; i++)
            {
                Console.WriteLine($"[调试] 尝试读取MCU版本响应 #{i+1}");
                if (!TrySendAndRead(cmd, out byte[] data, 10000))
                {
                    Console.WriteLine($"[调试] 读取MCU版本响应超时或失败，尝试 #{i+1}");
                    continue;
                }

                Console.WriteLine($"[调试] 读取到MCU版本响应数据: {BitConverter.ToString(data, 0, Math.Min(10, data.Length))}...");
                // MCU版本信息通常在响应的特定位置
                if (data[6] == 0xC6) // 198
                {
                    // 解析版本信息
                    var sb = new StringBuilder();
                    for (int j = 16; j < data.Length && data[j] != 0; j++)
                    {
                        if (data[j] >= 32 && data[j] <= 126) // 可打印字符
                        {
                            sb.Append((char)data[j]);
                        }
                        else
                        {
                            break;
                        }
                    }
                    string result = sb.ToString();
                    Console.WriteLine($"[调试] 成功获取MCU版本: {result}");
                    return result;
                }
            }
            Console.WriteLine($"[调试] GetMcuVersion 读取到的数据错误");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[调试] ExceptionError: {ex.Message}");
            Console.WriteLine(ex);
        }

        return string.Empty;
    }

    public bool AddVolume()
    {
        if (!IsConnected) return false;

        try
        {
            if (!McuFinder.Usb.Write(McuCommand.AddVolumeCommand))
                return false;

            if (!McuFinder.Usb.Read(DATA_LENGTH, out byte[] data))
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
            if (!McuFinder.Usb.Write(McuCommand.DecreaseVolume))
                return false;

            if (!McuFinder.Usb.Read(DATA_LENGTH, out byte[] data))
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
            return McuFinder.Usb.Write(McuCommand.SwitchToHdmi1Command);
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
            return McuFinder.Usb.Write(McuCommand.OpenAndroidPenCommand);
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
            return McuFinder.Usb.Write(McuCommand.ForbiddenAndroidPenCommand);
        }
        catch
        {
            return false;
        }
    }

    public void Disconnect()
    {
        McuFinder.Usb.Disconnect();
    }

    public int GetDeviceVid()
    {
        var detectedMcu = McuFinder.GetOrFindMcu();
        if (detectedMcu != null)
        {
            return detectedMcu.Vid;
        }
        return -1;
    }

    public int GetDevicePid()
    {
        var detectedMcu = McuFinder.GetOrFindMcu();
        if (detectedMcu != null)
        {
            return detectedMcu.Pid;
        }
        return -1;
    }

    public string GetDevicePath()
    {
        var detectedMcu = McuFinder.GetOrFindMcu();
        if (detectedMcu != null)
        {
            return detectedMcu.DeviceName ?? "未知";
        }
        return "未知";
    }
}