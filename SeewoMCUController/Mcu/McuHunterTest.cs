using System;
using System.Threading;

namespace Cvte.Mcu
{
    public class McuHunterTest
    {
        public static void TestMcuConnection()
        {
            Console.WriteLine("=== MCU 连接管理方式测试 ===\n");

            // 测试 1: 查找 MCU 设备
            Console.WriteLine("1. 测试查找 MCU 设备...");
            bool found = McuHunter.FindMcu();
            Console.WriteLine($"   查找结果: {(found ? "成功找到设备" : "未找到设备")}");
            
            if (found)
            {
                Console.WriteLine($"   检测到的设备: {McuHunter.DetectedMcu?.DeviceName ?? "无"}");
                Console.WriteLine($"   设备 VID: 0x{McuHunter.DetectedMcu?.Vid:X4}");
                Console.WriteLine($"   设备 PID: 0x{McuHunter.DetectedMcu?.Pid:X4}");
            }
            
            Console.WriteLine();

            // 测试 2: 查找所有设备
            Console.WriteLine("2. 测试查找所有 MCU 设备...");
            var allDevices = McuHunter.FindAll();
            Console.WriteLine($"   找到 {allDevices.Count} 个设备:");
            foreach (var device in allDevices)
            {
                Console.WriteLine($"     - {device.DeviceName} (VID:0x{device.Vid:X4}, PID:0x{device.Pid:X4})");
            }
            Console.WriteLine();

            // 测试 3: 连接和断开连接
            Console.WriteLine("3. 测试连接和断开连接...");
            bool connected = McuHunter.FindAndConnection();
            Console.WriteLine($"   连接结果: {(connected ? "成功" : "失败")}");
            
            if (connected)
            {
                Console.WriteLine($"   连接状态: {McuHunter.Usb.IsConnected}");
                
                // 测试 4: 写入测试命令
                Console.WriteLine("4. 测试写入命令...");
                byte[] testCommand = { 0xFC, 0xA5, 0x05, 0x3B, 0x00, 0xFB, 0x33, 0x00, 0x01, 0x00, 0x00 }; // 示例命令
                bool writeResult = McuHunter.WriteToDevice(testCommand);
                Console.WriteLine($"   写入结果: {(writeResult ? "成功" : "失败")}");
                
                // 测试 5: 读取测试
                Console.WriteLine("5. 测试读取数据...");
                byte[] readData;
                bool readResult = McuHunter.ReadFromDevice(64, out readData);
                Console.WriteLine($"   读取结果: {(readResult ? "成功" : "失败")}");
                
                if (readResult)
                {
                    Console.WriteLine($"   读取数据长度: {readData.Length}");
                    Console.WriteLine($"   前10字节: {BitConverter.ToString(readData, 0, Math.Min(10, readData.Length))}");
                }
            }
            
            Console.WriteLine();

            // 测试 6: 关闭连接
            Console.WriteLine("6. 测试关闭连接...");
            McuHunter.CloseDeviceConnection();
            Console.WriteLine($"   连接状态: {!McuHunter.Usb.IsConnected}");
            
            Console.WriteLine("\n=== MCU 连接管理方式测试完成 ===");
        }
        
        public static void TestMcuDevices()
        {
            Console.WriteLine("\n=== MCU 设备类型测试 ===");
            
            Console.WriteLine($"预定义设备类型数量: {McuHunter.DefineDeviceMcu.Count}");
            
            // 列出一些已定义的设备类型
            Console.WriteLine("已定义的设备类型:");
            Console.WriteLine($"  - CommonMcu: VID=0x{McuHunter.CommonMcu.Vid:X4}, PID=0x{McuHunter.CommonMcu.Pid:X4}");
            Console.WriteLine($"  - McuV: VID=0x{McuHunter.McuV.Vid:X4}, PID=0x{McuHunter.McuV.Pid:X4}");
            Console.WriteLine($"  - McuVI: VID=0x{McuHunter.McuVI.Vid:X4}, PID=0x{McuHunter.McuVI.Pid:X4}, REV=0x{McuHunter.McuVI.Rev:X4}");
            Console.WriteLine($"  - McuPodium: VID=0x{McuHunter.McuPodium.Vid:X4}, PID=0x{McuHunter.McuPodium.Pid:X4}");
            
            Console.WriteLine("=== MCU 设备类型测试完成 ===");
        }
    }
}