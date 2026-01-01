using SeewoMCUController.Mcu;

namespace SeewoMCUController;

class Program
{
    private static McuController? _mcuController;

    static void Main(string[] args)
    {
        try
        {
            // 检查是否有命令行参数
            if (args.Length > 0)
            {
                // 命令行参数模式
                ExecuteCommand(args);
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

    static void InteractiveMode()
    {
        Console.WriteLine("==========================================");
        Console.WriteLine("    Seewo MCU Controller - 交互式模式");
        Console.WriteLine("==========================================\n");

        // 查找并连接 MCU 设备
        Console.WriteLine("正在查找 MCU 设备...");
        _mcuController = new McuController();
        
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
        Console.WriteLine("✓ MCU 设备已连接\n");
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
            string boardName = _mcuController.GetBoardName();
            Console.WriteLine($"主板名称: {(string.IsNullOrEmpty(boardName) ? "未知" : boardName)}");

            string ip = _mcuController.GetIP();
            Console.WriteLine($"设备 IP:  {(string.IsNullOrEmpty(ip) ? "未知" : ip)}");

            string uid = _mcuController.GetUid();
            Console.WriteLine($"设备 UID: {(string.IsNullOrEmpty(uid) ? "未知" : uid.Trim())}");

            int touchSize = _mcuController.GetCVTouchSize();
            Console.WriteLine($"触摸屏:   {(touchSize > 0 ? touchSize + " 英寸" : "未知")}");
            
            // 添加更多设备信息
            if (_mcuController.IsConnected)
            {
                Console.WriteLine($"设备 VID: 0x{_mcuController.GetDeviceVid():X4}");
                Console.WriteLine($"设备 PID: 0x{_mcuController.GetDevicePid():X4}");
                Console.WriteLine($"设备路径: {_mcuController.GetDevicePath()}");
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"获取设备信息时出错: {ex.Message}");
            Console.ResetColor();
        }

        Console.WriteLine("==========================================");
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
                Console.WriteLine("✓ 音量增加成功");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("✗ 音量增加失败");
                Console.ResetColor();
            }
            return;
        }

        if (param == "down")
        {
            if (_mcuController.DecreaseVolume())
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✓ 音量减少成功");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("✗ 音量减少失败");
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
                    Console.WriteLine($"✓ 音量增加 {count} 级成功");
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
                    Console.WriteLine($"✓ 音量减少 {count} 级成功");
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
            Console.WriteLine("✓ 已切换到 HDMI1");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("✗ 切换到 HDMI1 失败");
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
                Console.WriteLine("✓ 触控笔已启用");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("✗ 启用触控笔失败");
                Console.ResetColor();
            }
        }
        else if (param == "off" || param == "disable")
        {
            if (_mcuController.ForbiddenAndroidPen())
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✓ 触控笔已禁用");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("✗ 禁用触控笔失败");
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
        _mcuController = new McuController();
        
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
        Console.WriteLine("  -h, --help        显示此帮助信息\n");
        Console.WriteLine("示例:");
        Console.WriteLine("  SeewoMCUController.exe -vol +1");
        Console.WriteLine("  SeewoMCUController.exe -hdmi");
        Console.WriteLine("  SeewoMCUController.exe -pen off");
    }
}
