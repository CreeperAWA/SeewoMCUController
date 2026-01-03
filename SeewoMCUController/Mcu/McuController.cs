using System.Text;

namespace SeewoMCUController.Mcu;

public class McuController
{
    private const int DATA_LENGTH = 64;

    public bool IsConnected => Cvte.Mcu.McuHunter.Usb.IsConnected;
    
    // Use only the already-set DetectedMcu. Do NOT call FindAll or rescan on failure.
    private bool TrySendAndRead(byte[] cmd, out byte[] data, int timeoutMs = 10000)
    {
        data = new byte[DATA_LENGTH];
        try
        {
            if (!Cvte.Mcu.McuHunter.Write(cmd))
            {
                Console.WriteLine("[调试] TrySendAndRead: Write 失败");
                return false;
            }
            
            Console.WriteLine($"[调试] Write: sent {cmd.Length} bytes: {BitConverter.ToString(cmd)}");
                    
            if (Cvte.Mcu.McuHunter.ReadWithShortTimeout(DATA_LENGTH, out byte[] readData))
            {
                data = readData;
                Console.WriteLine($"[调试] Read: received {data.Length} bytes: {BitConverter.ToString(data)}");
                Console.WriteLine("[调试] TrySendAndRead: 读取成功");
                return true;
            }
            else
            {
                Console.WriteLine("[调试] TrySendAndRead: 读取失败");
                return false;
            }
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
            bool result = Cvte.Mcu.McuHunter.FindAndConnection();
            if (result)
            {
                Console.WriteLine($"[调试] McuController 连接结果: {Cvte.Mcu.McuHunter.Usb.IsConnected}");
                return Cvte.Mcu.McuHunter.Usb.IsConnected;
            }

            Console.WriteLine("[调试] McuHunter.FindAndConnection() 返回 false");
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
            bool connected = Cvte.Mcu.McuHunter.Usb.Connect(usbId.DeviceName);
            Console.WriteLine($"[调试] 连接结果: {connected}");
            if (connected)
            {
                Cvte.Mcu.McuHunter.DetectedMcu = usbId;
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
            return Cvte.Mcu.Mcu.GetBoardName();
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
            return Cvte.Mcu.Mcu.GetIP();
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
            return Cvte.Mcu.Mcu.GetUid();
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
            return Cvte.Mcu.Mcu.GetCVTouchSize();
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
            // 由于 Cvte.Mcu.Mcu 类中没有直接的 GetMcuVersion 方法，
            // 我们使用 TrySendAndRead 方法发送 GetMcuVersionCommand
            var cmd = SeewoMCUController.Mcu.McuCommand.GetMcuVersionCommand;
            if (!TrySendAndRead(cmd, out byte[] data, 100))
            {
                Console.WriteLine("[调试] GetMcuVersion Write/Read 失败");
                return string.Empty;
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
            return Cvte.Mcu.Mcu.AddVolume();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[调试] AddVolume 异常: {ex.Message}");
            return false;
        }
    }

    public bool DecreaseVolume()
    {
        if (!IsConnected) return false;

        try
        {
            return Cvte.Mcu.Mcu.DecreaseVolume();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[调试] DecreaseVolume 异常: {ex.Message}");
            return false;
        }
    }

    public bool SwitchToHdmi1()
    {
        if (!IsConnected) return false;

        try
        {
            return Cvte.Mcu.Mcu.SwitchToHdmi1();
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
            return Cvte.Mcu.Mcu.OpenAndroidPen();
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
            return Cvte.Mcu.Mcu.ForbiddenAndroidPen();
        }
        catch
        {
            return false;
        }
    }

    public void Disconnect()
    {
        Cvte.Mcu.Mcu.CloseConnection();
    }

    public int GetDeviceVid()
    {
        var detectedMcu = Cvte.Mcu.McuHunter.GetOrFindMcu();
        if (detectedMcu != null)
        {
            return detectedMcu.Vid;
        }
        return -1;
    }

    public int GetDevicePid()
    {
        var detectedMcu = Cvte.Mcu.McuHunter.GetOrFindMcu();
        if (detectedMcu != null)
        {
            return detectedMcu.Pid;
        }
        return -1;
    }

    public string GetDevicePath()
    {
        var detectedMcu = Cvte.Mcu.McuHunter.GetOrFindMcu();
        if (detectedMcu != null)
        {
            return detectedMcu.DeviceName ?? "未知";
        }
        return "未知";
    }

    // 测试方法，用于验证MCU连接管理方式是否正常工作
    public void TestMcuConnection()
    {
        Console.WriteLine("=== 开始测试MCU连接管理方式 ===");
        
        // 测试查找MCU设备
        Console.WriteLine("1. 测试查找MCU设备...");
        bool found = FindAndConnect();
        Console.WriteLine($"   查找结果: {(found ? "成功" : "失败")}");
        
        if (found)
        {
            Console.WriteLine("2. 测试获取主板名称...");
            string boardName = GetBoardName();
            Console.WriteLine($"   主板名称: {boardName}");
            
            Console.WriteLine("3. 测试获取IP地址...");
            string ip = GetIP();
            Console.WriteLine($"   IP地址: {ip}");
            
            Console.WriteLine("4. 测试获取UID...");
            string uid = GetUid();
            Console.WriteLine($"   UID: {uid}");
            
            Console.WriteLine("5. 测试获取触摸屏尺寸...");
            int touchSize = GetCVTouchSize();
            Console.WriteLine($"   触摸屏尺寸: {touchSize}");
            
            Console.WriteLine("6. 测试获取MCU版本...");
            string version = GetMcuVersion();
            Console.WriteLine($"   MCU版本: {version}");
            
            Console.WriteLine("7. 测试音量控制...");
            bool volUp = AddVolume();
            Console.WriteLine($"   增加音量: {(volUp ? "成功" : "失败")}");
            
            bool volDown = DecreaseVolume();
            Console.WriteLine($"   降低音量: {(volDown ? "成功" : "失败")}");
            
            Console.WriteLine("8. 测试HDMI切换...");
            bool hdmi = SwitchToHdmi1();
            Console.WriteLine($"   切换到HDMI1: {(hdmi ? "成功" : "失败")}");
            
            Console.WriteLine("9. 测试触控笔控制...");
            bool openPen = OpenAndroidPen();
            Console.WriteLine($"   启用触控笔: {(openPen ? "成功" : "失败")}");
            
            bool forbiddenPen = ForbiddenAndroidPen();
            Console.WriteLine($"   禁用触控笔: {(forbiddenPen ? "成功" : "失败")}");
            
            Console.WriteLine("10. 测试设备信息...");
            Console.WriteLine($"   设备VID: 0x{GetDeviceVid():X4}");
            Console.WriteLine($"   设备PID: 0x{GetDevicePid():X4}");
            Console.WriteLine($"   设备路径: {GetDevicePath()}");
        }
        else
        {
            Console.WriteLine("   未找到MCU设备，无法进行进一步测试");
        }
        
        Console.WriteLine("=== MCU连接管理方式测试完成 ===");
    }
}