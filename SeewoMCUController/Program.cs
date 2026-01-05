using SeewoMCUController.Mcu;
using Cvte.Mcu;

namespace SeewoMCUController;

class Program
{
    private static McuController? _mcuController;

    static string? devicePath = null;
    
    static void Main(string[] args)
    {
        try
        {
            // 解析命令行参数
            var remainingArgs = ParseCommandLineArgs(args);
            
            // 检查是否有命令行参数
            if (remainingArgs.Length > 0)
            {
                // 检查是否是测试命令
                if (remainingArgs[0].Equals("test", StringComparison.OrdinalIgnoreCase))
                {
                    // 运行测试
                    McuHunterTest.TestMcuDevices();
                    McuHunterTest.TestMcuConnection();
                }
                else
                {
                    // 命令行参数模式
                    ExecuteCommand(remainingArgs);
                }
            }
            else
            {
                // 交互式模式
                InteractiveMode();
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"发生错误: {ex.Message}");
            Console.ResetColor();
        }
    }
    
    /// <summary>
    /// 解析命令行参数，提取设备路径参数
    /// </summary>
    /// <param name="args">原始命令行参数</param>
    /// <returns>除设备路径参数外的其他参数</returns>
    static string[] ParseCommandLineArgs(string[] args)
    {
        var remainingArgs = new List<string>();
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i].ToLower();
            
            if (arg == "--device-path" || arg == "-d")
            {
                // 下一个参数是设备路径
                if (i + 1 < args.Length)
                {
                    devicePath = args[i + 1];
                    i++; // 跳过设备路径值
                }
                else
                {
                    Console.WriteLine("错误: --device-path/-d 参数需要指定设备路径");
                    Environment.Exit(1);
                }
            }
            else if (arg.StartsWith("--device-path=") || arg.StartsWith("-d="))
            {
                // 参数和值在同一个字符串中
                if (arg.StartsWith("--device-path="))
                {
                    devicePath = arg.Substring("--device-path=".Length);
                }
                else
                {
                    devicePath = arg.Substring("-d=".Length);
                }
            }
            else
            {
                // 非设备路径参数，添加到剩余参数列表
                remainingArgs.Add(args[i]);
            }
        }
        
        return remainingArgs.ToArray();
    }

    static void InteractiveMode()
    {
        Console.WriteLine("==========================================");
        Console.WriteLine("    Seewo MCU Controller - 交互式模式");
        Console.WriteLine("==========================================\n");

        // 查找并连接 MCU 设备
        Console.WriteLine("正在查找 MCU 设备...");
        _mcuController = new McuController(devicePath);
        
        if (!_mcuController.FindAndConnect())
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("错误: 未找到 MCU 设备!");
            Console.ResetColor();
            Console.WriteLine("\n按任意键退出...");
            Console.ReadKey();
            return;
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("MCU 设备已连接\n");
        Console.ResetColor();

        // 显示设备信息
        DisplayDeviceInfo();

        // 显示帮助信息
        DisplayHelp();

        // 命令循环
        Console.WriteLine("\n等待输入命令...");
        while (true)
        {
            Console.Write("\n> ");
            string? input = Console.ReadLine();
            
            if (string.IsNullOrWhiteSpace(input))
                continue;

            input = input.Trim();

            // 处理退出命令
            if (input.Equals("exit", StringComparison.OrdinalIgnoreCase) || 
                input.Equals("quit", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("正在退出...");
                _mcuController.Disconnect();
                break;
            }

            // 处理帮助命令
            if (input.Equals("help", StringComparison.OrdinalIgnoreCase) || input == "?")
            {
                DisplayHelp();
                continue;
            }

            // 处理刷新信息命令
            if (input.Equals("info", StringComparison.OrdinalIgnoreCase))
            {
                DisplayDeviceInfo();
                continue;
            }
            
            // 处理设备枚举命令
            if (input.Equals("list", StringComparison.OrdinalIgnoreCase))
            {
                EnumerateInteractiveDevices();
                continue;
            }

            // 处理其他命令
            ProcessCommand(input);
        }
    }

    static void DisplayDeviceInfo()
    {
        if (_mcuController == null) return;

        Console.WriteLine("==========================================");
        Console.WriteLine("           MCU 设备信息");
        Console.WriteLine("==========================================");

        try
        {
            Console.WriteLine("[调试] 开始获取设备信息...");
            
            // 首先获取所有信息
            string boardName = _mcuController.GetBoardName();
            Console.WriteLine($"[调试] 获取主板名称: {boardName}");
            
            string ip = _mcuController.GetIP();
            Console.WriteLine($"[调试] 获取IP: {ip}");
            
            string uid = _mcuController.GetUid();
            Console.WriteLine($"[调试] 获取UID: {uid}");
            
            int touchSize = _mcuController.GetCVTouchSize();
            Console.WriteLine($"[调试] 获取触摸屏尺寸: {touchSize}");
            
            string mcuVersion = _mcuController.GetMcuVersion();
            Console.WriteLine($"[调试] 获取MCU版本: {mcuVersion}");
            
             // 获取设备连接信息
             string devicePath = "未知";
             string vid = "未知";
             string pid = "未知";
             if (_mcuController.IsConnected)
             {
                 devicePath = _mcuController.GetDevicePath();
                 vid = $"0x{_mcuController.GetDeviceVid():X4}";
                 pid = $"0x{_mcuController.GetDevicePid():X4}";
                 Console.WriteLine($"[调试] 获取设备连接信息: VID={vid}, PID={pid}, Path={devicePath}");
             }

            Console.WriteLine("[调试] 开始打印设备信息...");
            
            // 然后统一打印所有信息
            Console.WriteLine($"主板名称: {(string.IsNullOrEmpty(boardName) ? "未知" : boardName)}");
            Console.WriteLine($"设备 IP:  {(string.IsNullOrEmpty(ip) ? "未知" : ip)}");
            Console.WriteLine($"设备 UID: {(string.IsNullOrEmpty(uid) ? "未知" : uid.Trim())}");
            Console.WriteLine($"触摸屏:   {(touchSize > 0 ? touchSize + " 英寸" : "未知")}");
            Console.WriteLine($"MCU 版本: {(string.IsNullOrEmpty(mcuVersion) ? "未知" : mcuVersion)}");
            
            Console.WriteLine($"设备 VID: {vid}");
            Console.WriteLine($"设备 PID: {pid}");
            Console.WriteLine($"设备路径: {devicePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[调试] 获取设备信息时发生异常: {ex.Message}");
            Console.WriteLine($"[调试] 异常堆栈: {ex.StackTrace}");
            
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"获取设备信息时出错: {ex.Message}");
            Console.ResetColor();
        }

        Console.WriteLine("==========================================");
    }
    
    static void EnumerateInteractiveDevices()
    {
        if (_mcuController == null) return;
        
        var devices = _mcuController.EnumerateDevices();
        
        if (devices.Count == 0)
        {
            Console.WriteLine("未找到任何MCU设备");
            return;
        }
        
        Console.WriteLine($"找到 {devices.Count} 个MCU设备:");
        for (int i = 0; i < devices.Count; i++)
        {
            var device = devices[i];
            Console.WriteLine($"[{i + 1}] VID:0x{device.Vid:X4}, PID:0x{device.Pid:X4}, 路径: {device.DeviceName}");
        }
    }

        static void DisplayHelp()
        {
            Console.WriteLine("\n可用命令:");
            Console.WriteLine("  vol +<数值>     - 增加音量 (例: vol +1, vol +5)");
            Console.WriteLine("  vol -<数值>     - 减少音量 (例: vol -1, vol -3)");
            Console.WriteLine("  vol up          - 增加音量 1 级");
            Console.WriteLine("  vol down        - 减少音量 1 级");
            Console.WriteLine("  hdmi            - 切换到 HDMI1");
            Console.WriteLine("  pen on          - 启用触控笔");
            Console.WriteLine("  pen off         - 禁用触控笔");
            Console.WriteLine("  info            - 重新显示设备信息");
            Console.WriteLine("  list            - 列出所有检测到的MCU设备");
            Console.WriteLine("  help / ?        - 显示此帮助信息");
            Console.WriteLine("  exit / quit     - 退出程序");
        }

        static void ProcessCommand(string input)
        {
            try
            {
                string[] parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0)
                    return;

                string command = parts[0].ToLower();

                switch (command)
                {
                    case "vol":
                    case "volume":
                        if (parts.Length < 2)
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine("用法: vol +<数值> 或 vol -<数值> 或 vol up/down");
                            Console.ResetColor();
                            return;
                        }
                        HandleVolumeCommand(parts[1]);
                        break;

                    case "hdmi":
                        HandleHdmiCommand();
                        break;

                    case "pen":
                        if (parts.Length < 2)
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine("用法: pen on 或 pen off");
                            Console.ResetColor();
                            return;
                        }
                        HandlePenCommand(parts[1]);
                        break;

                    default:
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"未知命令: {command}");
                        Console.WriteLine("输入 'help' 查看可用命令");
                        Console.ResetColor();
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"执行命令时出错: {ex.Message}");
                Console.ResetColor();
            }
        }

    static void HandleVolumeCommand(string param)
    {
        if (_mcuController == null) return;
        
        param = param.ToLower();

        if (param == "up")
        {
            if (_mcuController.AddVolume())
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("音量增加成功");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("音量增加失败");
                Console.ResetColor();
            }
            return;
        }

        if (param == "down")
        {
            if (_mcuController.DecreaseVolume())
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("音量减少成功");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("音量减少失败");
                Console.ResetColor();
            }
            return;
        }

        // 处理 +数值 或 -数值
        if (param.StartsWith("+"))
        {
            if (int.TryParse(param.Substring(1), out int count) && count > 0)
            {
                int success = 0;
                for (int i = 0; i < count; i++)
                {
                    if (_mcuController.AddVolume())
                        success++;
                    Thread.Sleep(100); // 短暂延迟
                }
                
                if (success == count)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"音量增加 {count} 级成功");
                    Console.ResetColor();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"⚠ 音量增加部分成功 ({success}/{count})");
                    Console.ResetColor();
                }
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("无效的数值");
                Console.ResetColor();
            }
        }
        else if (param.StartsWith("-"))
        {
            if (int.TryParse(param.Substring(1), out int count) && count > 0)
            {
                int success = 0;
                for (int i = 0; i < count; i++)
                {
                    if (_mcuController.DecreaseVolume())
                        success++;
                    Thread.Sleep(100); // 短暂延迟
                }
                
                if (success == count)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"音量减少 {count} 级成功");
                    Console.ResetColor();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"⚠ 音量减少部分成功 ({success}/{count})");
                    Console.ResetColor();
                }
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("无效的数值");
                Console.ResetColor();
            }
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("无效的参数格式，请使用: vol +<数值>, vol -<数值>, vol up, 或 vol down");
            Console.ResetColor();
        }
    }

    static void HandleHdmiCommand()
    {
        if (_mcuController == null) return;
        
        if (_mcuController.SwitchToHdmi1())
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("已切换到 HDMI1");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("切换到 HDMI1 失败");
            Console.ResetColor();
        }
    }

    static void HandlePenCommand(string param)
    {
        if (_mcuController == null) return;
        
        param = param.ToLower();
        
        if (param == "on" || param == "enable")
        {
            if (_mcuController.OpenAndroidPen())
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("触控笔已启用");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("启用触控笔失败");
                Console.ResetColor();
            }
        }
        else if (param == "off" || param == "disable")
        {
            if (_mcuController.ForbiddenAndroidPen())
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("触控笔已禁用");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("禁用触控笔失败");
                Console.ResetColor();
            }
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("无效的参数，请使用: pen on 或 pen off");
            Console.ResetColor();
        }
    }

    static void ExecuteCommand(string[] args)
    {
        // 查找并连接 MCU 设备
        _mcuController = new McuController(devicePath);
        
        if (!_mcuController.FindAndConnect())
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("错误: 未找到 MCU 设备！");
            Console.ResetColor();
            Environment.Exit(1);
            return;
        }

            try
            {
                // 解析命令行参数
                for (int i = 0; i < args.Length; i++)
                {
                    string arg = args[i].ToLower();

                    switch (arg)
                    {
                        case "-vol":
                        case "-volume":
                        case "--vol":
                        case "--volume":
                            if (i + 1 < args.Length)
                            {
                                i++;
                                HandleVolumeCommandLine(args[i]);
                            }
                            else
                            {
                                Console.WriteLine("错误: -vol 参数需要指定数值");
                                Environment.Exit(1);
                            }
                            break;

                        case "-hdmi":
                        case "--hdmi":
                            HandleHdmiCommand();
                            break;

                        case "-pen":
                        case "--pen":
                            if (i + 1 < args.Length)
                            {
                                i++;
                                HandlePenCommand(args[i]);
                            }
                            else
                            {
                                Console.WriteLine("错误: -pen 参数需要指定 on 或 off");
                                Environment.Exit(1);
                            }
                            break;

                        case "-info":
                        case "--info":
                            DisplayDeviceInfo();
                            break;

                        case "-list":
                        case "--list":
                        case "-enumerate":
                        case "--enumerate":
                            EnumerateDevices();
                            break;

                        case "-h":
                        case "-help":
                        case "--help":
                            DisplayCommandLineHelp();
                            break;

                        default:
                            Console.WriteLine($"未知参数: {args[i]}");
                            Console.WriteLine("使用 --help 查看帮助信息");
                            Environment.Exit(1);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"执行命令时出错: {ex.Message}");
                Console.ResetColor();
                Environment.Exit(1);
            }
        }

    static void HandleVolumeCommandLine(string param)
    {
        if (_mcuController == null) return;
        
        param = param.ToLower();

        if (param == "up")
        {
            if (_mcuController.AddVolume())
                Console.WriteLine("音量增加成功");
            else
            {
                Console.WriteLine("音量增加失败");
                Environment.Exit(1);
            }
            return;
        }

        if (param == "down")
        {
            if (_mcuController.DecreaseVolume())
                Console.WriteLine("音量减少成功");
            else
            {
                Console.WriteLine("音量减少失败");
                Environment.Exit(1);
            }
            return;
        }

        // 处理 +数值 或 -数值
        if (param.StartsWith("+"))
        {
            if (int.TryParse(param.Substring(1), out int count) && count > 0)
            {
                int success = 0;
                for (int i = 0; i < count; i++)
                {
                    if (_mcuController.AddVolume())
                        success++;
                    Thread.Sleep(100);
                }
                
                if (success == count)
                    Console.WriteLine($"音量增加 {count} 级成功");
                else
                {
                    Console.WriteLine($"音量增加部分成功 ({success}/{count})");
                    Environment.Exit(1);
                }
            }
            else
            {
                Console.WriteLine("无效的数值");
                Environment.Exit(1);
            }
        }
        else if (param.StartsWith("-"))
        {
            if (int.TryParse(param.Substring(1), out int count) && count > 0)
            {
                int success = 0;
                for (int i = 0; i < count; i++)
                {
                    if (_mcuController.DecreaseVolume())
                        success++;
                    Thread.Sleep(100);
                }
                
                if (success == count)
                    Console.WriteLine($"音量减少 {count} 级成功");
                else
                {
                    Console.WriteLine($"音量减少部分成功 ({success}/{count})");
                    Environment.Exit(1);
                }
            }
            else
            {
                Console.WriteLine("无效的数值");
                Environment.Exit(1);
            }
        }
        else
        {
            Console.WriteLine("无效的参数格式，请使用: +<数值>, -<数值>, up, 或 down");
            Environment.Exit(1);
        }
    }

    static void DisplayCommandLineHelp()
    {
        Console.WriteLine("Seewo MCU Controller - 命令行工具\n");
        Console.WriteLine("用法:");
        Console.WriteLine("  SeewoMCUController.exe              启动交互式模式");
        Console.WriteLine("  SeewoMCUController.exe [选项]       执行命令后退出\n");
        Console.WriteLine("选项:");
        Console.WriteLine("  -vol +<数值>      增加音量 (例: -vol +1, -vol +5)");
        Console.WriteLine("  -vol -<数值>      减少音量 (例: -vol -1, -vol -3)");
        Console.WriteLine("  -vol up           增加音量 1 级");
        Console.WriteLine("  -vol down         减少音量 1 级");
        Console.WriteLine("  -hdmi             切换到 HDMI1");
        Console.WriteLine("  -pen on           启用触控笔");
        Console.WriteLine("  -pen off          禁用触控笔");
        Console.WriteLine("  -info             显示设备信息");
        Console.WriteLine("  -list             列出所有检测到的MCU设备");
        Console.WriteLine("  -d, --device-path 路径  指定要使用的MCU设备路径");
        Console.WriteLine("  -h, --help        显示此帮助信息\n");
        Console.WriteLine("示例:");
        Console.WriteLine("  SeewoMCUController.exe -vol +1");
        Console.WriteLine("  SeewoMCUController.exe -hdmi");
        Console.WriteLine("  SeewoMCUController.exe -pen off");
        Console.WriteLine("  SeewoMCUController.exe --device-path \"\\\\?\\hid#vid_1fe7&pid_0004#6&314b457f&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}\" -vol +1");
        Console.WriteLine("  SeewoMCUController.exe -list");
    }
    
    /// <summary>
    /// 枚举并显示所有检测到的MCU设备
    /// </summary>
    static void EnumerateDevices()
    {
        if (_mcuController == null)
        {
            _mcuController = new McuController();
        }
        
        var devices = _mcuController.EnumerateDevices();
        
        if (devices.Count == 0)
        {
            Console.WriteLine("未找到任何MCU设备");
            return;
        }
        
        Console.WriteLine($"找到 {devices.Count} 个MCU设备:");
        for (int i = 0; i < devices.Count; i++)
        {
            var device = devices[i];
            Console.WriteLine($"[{i + 1}] VID:0x{device.Vid:X4}, PID:0x{device.Pid:X4}, 路径: {device.DeviceName}");
        }
    }
}
