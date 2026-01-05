using System.Text;
using Cvte.Mcu;

namespace SeewoMCUController.Mcu;

/// <summary>
/// MCU控制器类，用于与MCU设备进行通信和控制
/// </summary>
public class McuController
{
    private const int DATA_LENGTH = 64;

    /// <summary>
    /// 获取设备是否已连接的状态
    /// </summary>
    public bool IsConnected => McuHunter.Usb.IsConnected;
        
    private string? _devicePath;
        
    // 保留无参构造函数以确保向后兼容
    /// <summary>
    /// 无参构造函数，使用默认设备路径
    /// </summary>
    public McuController() : this(null) { }
        
    /// <summary>
    /// 初始化McuController实例
    /// </summary>
    /// <param name="devicePath">指定的设备路径，如果为null则自动查找设备</param>
    public McuController(string? devicePath = null)
    {
        _devicePath = devicePath;
    }
        
    #region 连接管理方法
    /// <summary>
    /// 尝试发送命令并读取响应数据
    /// </summary>
    /// <param name="cmd">要发送的命令字节数组</param>
    /// <param name="data">读取到的响应数据</param>
    /// <param name="timeoutMs">超时时间（毫秒）</param>
    /// <returns>操作是否成功</returns>
    // Use only the already-set DetectedMcu. Do NOT call FindAll or rescan on failure.
    private bool TrySendAndRead(byte[] cmd, out byte[] data, int timeoutMs = 10000)
    {
        data = new byte[DATA_LENGTH];
        try
        {
            if (!McuHunter.Write(cmd))
            {
                Console.WriteLine("[调试] TrySendAndRead: Write 失败");
                return false;
            }
                
            Console.WriteLine($"[调试] Write: sent {cmd.Length} bytes: {BitConverter.ToString(cmd)}");
                        
            if (McuHunter.ReadWithShortTimeout(DATA_LENGTH, out byte[] readData))
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
    
    /// <summary>
    /// 查找并连接到MCU设备
    /// </summary>
    /// <returns>是否成功连接到设备</returns>
    public bool FindAndConnect()
    {
        try
        {
            // 如果指定了设备路径，优先使用指定的设备路径
            if (!string.IsNullOrEmpty(_devicePath) && _devicePath != "auto")
            {
                Console.WriteLine($"正在连接指定设备路径: {_devicePath}");
                var usbId = new UsbId(_devicePath, 0); // 使用默认版本号
                bool connected = FindAndConnect(usbId);
                    
                if (!connected)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"错误: 无法连接到指定的设备路径: {_devicePath}");
                    Console.ResetColor();
                    return false;
                }
                    
                Console.WriteLine($"[调试] McuController 连接结果: {McuHunter.Usb.IsConnected}");
                return McuHunter.Usb.IsConnected;
            }
                
            // 获取所有可用的MCU设备
            var allDevices = McuHunter.FindAll();
                
            if (allDevices.Count == 0)
            {
                Console.WriteLine("[调试] 未找到任何MCU设备");
                return false;
            }
                
            // 如果有多个设备，提示用户选择
            if (allDevices.Count > 1)
            {
                Console.WriteLine($"发现 {allDevices.Count} 个MCU设备:");
                for (int i = 0; i < allDevices.Count; i++)
                {
                    var device = allDevices[i];
                    Console.WriteLine($"  [{i + 1}] VID:0x{device.Vid:X4}, PID:0x{device.Pid:X4}, 路径: {device.DeviceName}");
                }
                    
                Console.Write($"请选择设备 (1-{allDevices.Count}) 或按回车自动选择第一个: ");
                string? input = Console.ReadLine();
                    
                if (int.TryParse(input, out int selection) && selection >= 1 && selection <= allDevices.Count)
                {
                    // 用户选择了特定设备
                    var selectedDevice = allDevices[selection - 1];
                    bool connected = FindAndConnect(selectedDevice);
                    if (connected)
                    {
                        Console.WriteLine($"[调试] 已连接到选择的设备: {selectedDevice.DeviceName}");
                        Console.WriteLine($"[调试] McuController 连接结果: {McuHunter.Usb.IsConnected}");
                        return McuHunter.Usb.IsConnected;
                    }
                }
                else
                {
                    // 用户按回车或输入无效，使用第一个设备
                    Console.WriteLine("使用第一个设备...");
                }
            }
                
            // 如果只有一个设备或者用户没有选择特定设备，尝试自动连接
            bool result = McuHunter.FindAndConnection();
            if (result)
            {
                Console.WriteLine($"[调试] McuController 连接结果: {McuHunter.Usb.IsConnected}");
                return McuHunter.Usb.IsConnected;
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
    
    /// <summary>
    /// 连接到指定的USB设备
    /// </summary>
    /// <param name="usbId">要连接的USB设备ID</param>
    /// <returns>是否成功连接到设备</returns>
    public bool FindAndConnect(UsbId usbId)
    {
        try
        {
            Console.WriteLine($"[调试] 尝试连接指定设备: {usbId.DeviceName}");
            bool connected = McuHunter.Usb.Connect(usbId.DeviceName);
            Console.WriteLine($"[调试] 连接结果: {connected}");
            if (connected)
            {
                McuHunter.DetectedMcu = usbId;
            }
            return connected;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[调试] 连接异常: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// 断开与MCU设备的连接
    /// </summary>
    public void Disconnect()
    {
        Cvte.Mcu.Mcu.CloseConnection();
    }
    #endregion
        
    #region 设备信息获取方法
    /// <summary>
    /// 获取主板名称
    /// </summary>
    /// <returns>主板名称字符串</returns>
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
    
    /// <summary>
    /// 获取设备IP地址
    /// </summary>
    /// <returns>设备IP地址字符串</returns>
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
    
    /// <summary>
    /// 获取设备UID
    /// </summary>
    /// <returns>设备UID字符串</returns>
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
    
    /// <summary>
    /// 获取CV触摸屏尺寸
    /// </summary>
    /// <returns>触摸屏尺寸（英寸），如果获取失败返回-1</returns>
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
    
    /// <summary>
    /// 获取MCU版本信息
    /// </summary>
    /// <returns>MCU版本字符串</returns>
    public string GetMcuVersion()
    {
        if (!IsConnected) return string.Empty;
    
        try
        {
            // 由于 Mcu 类中没有直接的 GetMcuVersion 方法，
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
                
            Console.WriteLine("[调试] GetMcuVersion 读取到的数据错误");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[调试] ExceptionError: {ex.Message}");
            Console.WriteLine(ex);
        }
    
        return string.Empty;
    }
    #endregion
        
    #region 控制命令方法
    /// <summary>
    /// 增加音量
    /// </summary>
    /// <returns>操作是否成功</returns>
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
    
    /// <summary>
    /// 降低音量
    /// </summary>
    /// <returns>操作是否成功</returns>
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
    
    /// <summary>
    /// 切换到HDMI1
    /// </summary>
    /// <returns>操作是否成功</returns>
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
    
    /// <summary>
    /// 启用安卓触控笔
    /// </summary>
    /// <returns>操作是否成功</returns>
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
    
    /// <summary>
    /// 禁用安卓触控笔
    /// </summary>
    /// <returns>操作是否成功</returns>
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
    #endregion
        
    #region 设备信息方法
    /// <summary>
    /// 获取设备VID
    /// </summary>
    /// <returns>设备VID值</returns>
    public int GetDeviceVid()
    {
        var detectedMcu = McuHunter.GetOrFindMcu();
        if (detectedMcu != null)
        {
            return detectedMcu.Vid;
        }
        return -1;
    }
    
    /// <summary>
    /// 获取设备PID
    /// </summary>
    /// <returns>设备PID值</returns>
    public int GetDevicePid()
    {
        var detectedMcu = McuHunter.GetOrFindMcu();
        if (detectedMcu != null)
        {
            return detectedMcu.Pid;
        }
        return -1;
    }
    
    /// <summary>
    /// 获取设备路径
    /// </summary>
    /// <returns>设备路径字符串</returns>
    public string GetDevicePath()
    {
        var detectedMcu = McuHunter.GetOrFindMcu();
        if (detectedMcu != null)
        {
            return detectedMcu.DeviceName ?? "未知";
        }
        return "未知";
    }
    #endregion
        
    #region 枚举和测试方法
    /// <summary>
    /// 枚举所有检测到的MCU设备
    /// </summary>
    /// <returns>设备列表</returns>
    public List<UsbId> EnumerateDevices()
    {
        try
        {
            var allDevices = McuHunter.FindAll();
            return allDevices;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[调试] 枚举设备时发生异常: {ex.Message}");
            return new List<UsbId>();
        }
    }
        
    /// <summary>
    /// 测试MCU连接管理方式是否正常工作
    /// </summary>
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
    #endregion
}