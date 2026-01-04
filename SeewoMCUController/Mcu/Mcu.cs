using System;
using System.Text;
using System.Threading.Tasks;

namespace Cvte.Mcu
{
    public static class Mcu
    {
        // 日志回调
        public static Action<string, string> Log { get; set; } = (log, msg) => { };

        // 查找MCU设备
        public static bool FindMcu()
        {
            return McuHunter.FindMcu();
        }

        // 禁用安卓触控笔
        public static bool ForbiddenAndroidPen()
        {
            bool flag;
            try
            {
                flag = McuHunter.Write(SeewoMCUController.Mcu.McuCommand.ForbiddenAndroidPenCommand);
            }
            catch
            {
                flag = false;
            }
            return flag;
        }

        // 启用安卓触控笔
        public static bool OpenAndroidPen()
        {
            bool flag;
            try
            {
                flag = McuHunter.Write(SeewoMCUController.Mcu.McuCommand.OpenAndroidPenCommand);
            }
            catch
            {
                flag = false;
            }
            return flag;
        }

        // 获取CV触摸屏尺寸
        public static int GetCVTouchSize()
        {
            int num;
            try
            {
                if (!McuHunter.Write(SeewoMCUController.Mcu.McuCommand.GetMcuVersionCommand))
                {
                    OnLog("WriteError", "GetCVTouchSize WriteError");
                    num = -1;
                }
                else
                {
                    for (int i = 0; i < 3; i++)
                    {
                        byte[] array;
                        if (!McuHunter.ReadWithShortTimeout(DataLength, out array))
                        {
                            OnLog("ReadError", "GetCVTouchSize ReadError");
                            return -1;
                        }
                        if (CVTouchAnalyzer.IsMatchCVTouchVersion(array))
                        {
                            OnLog("GetCVTouchSize", "读取到屏幕尺寸");
                            return CVTouchAnalyzer.GetCVTouchSize(array);
                        }
                        OnLog("Error", "GetCVTouchSize 读取到数据格式错误");
                    }
                    num = -1;
                }
            }
            catch (Exception)
            {
                num = -1;
            }
            return num;
        }

        // 异步获取CV触摸屏尺寸
        public static async Task<int> GetCVTouchSizeAsync()
        {
            int num;
            try
            {
                if (!McuHunter.Write(SeewoMCUController.Mcu.McuCommand.GetMcuVersionCommand))
                {
                    OnLog("WriteError", "GetCVTouchSize WriteError");
                    num = -1;
                }
                else
                {
                    for (int i = 0; i < 3; i++)
                    {
                        ValueTuple<bool, byte[]> result = await ReadAsync(-1, "");
                        bool item = result.Item1;
                        byte[] item2 = result.Item2;
                        if (!item)
                        {
                            OnLog("ReadError", "GetCVTouchSize ReadError");
                            return -1;
                        }
                        if (CVTouchAnalyzer.IsMatchCVTouchVersion(item2))
                        {
                            OnLog("GetCVTouchSize", "读取到屏幕尺寸");
                            return CVTouchAnalyzer.GetCVTouchSize(item2);
                        }
                        OnLog("Error", "GetCVTouchSize 读取到数据格式错误");
                    }
                    num = -1;
                }
            }
            catch (Exception)
            {
                num = -1;
            }
            return num;
        }

        // 切换到HDMI1
        public static bool SwitchToHdmi1()
        {
            bool flag;
            try
            {
                flag = McuHunter.Write(SeewoMCUController.Mcu.McuCommand.SwitchToHdmi1Command);
            }
            catch
            {
                flag = false;
            }
            return flag;
        }

        // 获取主板名称
        public static string GetBoardName()
        {
            string text;
            try
            {
                if (!McuHunter.Write(SeewoMCUController.Mcu.McuCommand.GetBoardNameCommand))
                {
                    OnLog("WriteError", "GetBoardName WriteError");
                    text = string.Empty;
                }
                else
                {
                    for (int i = 0; i < 3; i++)
                    {
                        byte[] array;
                        if (!McuHunter.ReadWithShortTimeout(DataLength, out array))
                        {
                            OnLog("ReadError", "GetBoardName ReadError");
                            return string.Empty;
                        }
                        if (array[6] == 52 && array[7] == 13)
                        {
                            short num = BitConverter.ToInt16(array, 8);
                            byte[] array2 = new byte[(int)num];
                            Buffer.BlockCopy(array, 10, array2, 0, (int)num);
                            return Encoding.UTF8.GetString(array2);
                        }
                    }
                    OnLog("Error", "GetBoardName 读取到的数据错误");
                    text = string.Empty;
                }
            }
            catch (Exception ex)
            {
                OnLog("ExceptionError", ex.Message);
                Console.WriteLine(ex);
                text = string.Empty;
            }
            return text;
        }

        // 异步获取主板名称
        public static async Task<string> GetBoardNameAsync()
        {
            string text;
            try
            {
                if (!McuHunter.Write(SeewoMCUController.Mcu.McuCommand.GetBoardNameCommand))
                {
                    OnLog("WriteError", "GetBoardName WriteError");
                    text = string.Empty;
                }
                else
                {
                    for (int i = 0; i < 3; i++)
                    {
                        ValueTuple<bool, byte[]> result = await ReadAsync(-1, "");
                        bool item = result.Item1;
                        byte[] item2 = result.Item2;
                        if (!item)
                        {
                            OnLog("ReadError", "GetBoardName ReadError");
                            return string.Empty;
                        }
                        if (item2[6] == 52 && item2[7] == 13)
                        {
                            short num = BitConverter.ToInt16(item2, 8);
                            byte[] array2 = new byte[(int)num];
                            Buffer.BlockCopy(item2, 10, array2, 0, (int)num);
                            return Encoding.UTF8.GetString(array2);
                        }
                    }
                    OnLog("Error", "GetBoardName 读取到的数据错误");
                    text = string.Empty;
                }
            }
            catch (Exception ex)
            {
                OnLog("ExceptionError", ex.Message);
                Console.WriteLine(ex);
                text = string.Empty;
            }
            return text;
        }

        // 获取IP地址
        public static string GetIP()
        {
            string text;
            try
            {
                if (!McuHunter.Write(SeewoMCUController.Mcu.McuCommand.GetIPCommand))
                {
                    text = string.Empty;
                }
                else
                {
                    for (int i = 0; i < 3; i++)
                    {
                        byte[] array;
                        if (!McuHunter.ReadWithShortTimeout(DataLength, out array))
                        {
                            return string.Empty;
                        }
                        if (array[6] == 52 && array[7] == 17)
                        {
                            short num = BitConverter.ToInt16(array, 8);
                            byte[] array2 = new byte[(int)num];
                            Buffer.BlockCopy(array, 10, array2, 0, (int)num);
                            return Encoding.UTF8.GetString(array2);
                        }
                    }
                    text = string.Empty;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                text = string.Empty;
            }
            return text;
        }

        // 异步获取IP地址
        public static async Task<string> GetIPAsync()
        {
            string text;
            try
            {
                if (!McuHunter.Write(SeewoMCUController.Mcu.McuCommand.GetIPCommand))
                {
                    text = string.Empty;
                }
                else
                {
                    for (int i = 0; i < 3; i++)
                    {
                        ValueTuple<bool, byte[]> result = await ReadAsync(-1, "");
                        bool item = result.Item1;
                        byte[] item2 = result.Item2;
                        if (!item)
                        {
                            return string.Empty;
                        }
                        if (item2[6] == 52 && item2[7] == 17)
                        {
                            short num = BitConverter.ToInt16(item2, 8);
                            byte[] array2 = new byte[(int)num];
                            Buffer.BlockCopy(item2, 10, array2, 0, (int)num);
                            return Encoding.UTF8.GetString(array2);
                        }
                    }
                    text = string.Empty;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                text = string.Empty;
            }
            return text;
        }

        // 获取UID
        public static string GetUid()
        {
            if (!McuHunter.Write(SeewoMCUController.Mcu.McuCommand.GetUidCommand))
            {
                OnLog("WriteError", "GetUid WriteError");
                return string.Empty;
            }
            byte[] array;
            if (!McuHunter.ReadWithShortTimeout(DataLength, out array))
            {
                OnLog("ReadError", "GetUid ReadError");
                return string.Empty;
            }
            StringBuilder stringBuilder = new StringBuilder();
            if (array[6] == 193)
            {
                for (int i = 0; i < 12; i++)
                {
                    stringBuilder.Append(array[10 + i].ToString("x2") + " ");
                }
                return stringBuilder.ToString();
            }
            OnLog("Error", "GetUid 数据错误");
            return "";
        }

        // 异步获取UID
        public static async Task<string> GetUidAsync()
        {
            string text;
            if (!McuHunter.Write(SeewoMCUController.Mcu.McuCommand.GetUidCommand))
            {
                OnLog("WriteError", "GetUid WriteError");
                text = string.Empty;
            }
            else
            {
                ValueTuple<bool, byte[]> result = await ReadAsync(-1, "");
                bool item = result.Item1;
                byte[] item2 = result.Item2;
                if (!item)
                {
                    OnLog("ReadError", "GetUid ReadError");
                    text = string.Empty;
                }
                else
                {
                    StringBuilder stringBuilder = new StringBuilder();
                    if (item2[6] == 193)
                    {
                        for (int i = 0; i < 12; i++)
                        {
                            stringBuilder.Append(item2[10 + i].ToString("x2") + " ");
                        }
                        text = stringBuilder.ToString();
                    }
                    else
                    {
                        OnLog("Error", "GetUid 数据错误");
                        text = "";
                    }
                }
            }
            return text;
        }

        // 增加音量
        public static bool AddVolume()
        {
            var cmd = SeewoMCUController.Mcu.McuCommand.AddVolumeCommand;
            Console.WriteLine($"[调试] Write: sent {cmd.Length} bytes: {BitConverter.ToString(cmd)}");
            if (!McuHunter.Write(cmd))
            {
                OnLog("WriteError", "AddVolume WriteError");
                return false;
            }
            
            // 添加10毫秒延时，避免操作过于频繁
            System.Threading.Thread.Sleep(10);
            
            OnLog("AddVolume", "增加音量指令已发送");
            return true;
        }

        // 异步增加音量
        public static async Task<bool> AddVolumeAsync()
        {
            if (!McuHunter.Write(SeewoMCUController.Mcu.McuCommand.AddVolumeCommand))
            {
                OnLog("WriteError", "AddVolume WriteError");
                return false;
            }
            
            // 添加10毫秒延时，避免操作过于频繁
            await Task.Delay(10);
            
            OnLog("AddVolume", "增加音量指令已发送");
            return true;
        }

        // 降低音量
        public static bool DecreaseVolume()
        {
            var cmd = SeewoMCUController.Mcu.McuCommand.DecreaseVolume;
            Console.WriteLine($"[调试] Write: sent {cmd.Length} bytes: {BitConverter.ToString(cmd)}");
            if (!McuHunter.Write(cmd))
            {
                OnLog("WriteError", "DecreaseVolume WriteError");
                return false;
            }
            
            // 添加10毫秒延时，避免操作过于频繁
            System.Threading.Thread.Sleep(10);
            
            OnLog("DecreaseVolume", "降低音量指令已发送");
            return true;
        }

        // 异步降低音量
        public static async Task<bool> DecreaseVolumeAsync()
        {
            if (!McuHunter.Write(SeewoMCUController.Mcu.McuCommand.DecreaseVolume))
            {
                OnLog("WriteError", "DecreaseVolume WriteError");
                return false;
            }
            
            // 添加10毫秒延时，避免操作过于频繁
            await Task.Delay(10);
            
            OnLog("DecreaseVolume", "降低音量指令已发送");
            return true;
        }

        // 写入数据（已过时，建议使用McuHunter.Write）
        [Obsolete("请使用McuHunter.Write")]
        public static bool Write(byte[] array)
        {
            return McuHunter.WriteToDevice(array);
        }

        // 读取数据（已过时，建议使用McuHunter.Read）
        [Obsolete("请使用McuHunter.Read")]
        public static bool Read(int dataLength, out byte[] data)
        {
            return McuHunter.ReadFromDevice(dataLength, out data);
        }

        // 异步读取数据（已过时，建议使用McuHunter.TryReadAsync）
        [Obsolete("请使用 McuHunter.TryReadAsync")]
        public static async Task<ValueTuple<bool, byte[]>> ReadAsync(int dataLength = -1, string methodName = "")
        {
            if (dataLength < 0)
            {
                dataLength = DataLength;
            }
            var asyncOp = McuHunter.TryReadFromDeviceAsync(dataLength, 3, methodName);
            // 等待异步操作完成
            while (!asyncOp.IsCompleted)
            {
                await Task.Delay(10);
            }
            return asyncOp.Result;
        }

        // 关闭连接（已过时，建议使用McuHunter.CloseConnection）
        [Obsolete("请使用 McuHunter.CloseConnection")]
        public static void CloseConnection()
        {
            McuHunter.CloseDeviceConnection();
        }

        // 内部日志方法
        private static void OnLog(string log, string msg)
        {
            Action<string, string> log2 = Log;
            if (log2 == null)
            {
                return;
            }
            log2(log, msg);
        }

        // 日志常量
        public const string LogError = "Error";
        public const string LogException = "ExceptionError";
        public const string WriteError = "WriteError";
        public const string ReadError = "ReadError";

        // 数据长度常量
        private const int DataLength = 64;
    }
}