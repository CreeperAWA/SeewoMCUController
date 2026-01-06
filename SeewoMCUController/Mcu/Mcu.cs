using System;
using System.Text;
using System.Threading.Tasks;
using SeewoMCUController.Mcu;

namespace Cvte.Mcu
{
    /// <summary>
    /// MCU设备控制类，提供对MCU设备的各种控制和查询功能
    /// </summary>
    public static class Mcu
    {
        /// <summary>
        /// 日志回调，用于记录操作日志
        /// </summary>
        public static Action<string, string> Log { get; set; } = (log, msg) => { };

        /// <summary>
        /// 查找MCU设备
        /// </summary>
        /// <returns>是否找到MCU设备</returns>
        public static bool FindMcu()
        {
            return McuHunter.FindMcu();
        }

        /// <summary>
        /// 禁用安卓触控笔
        /// </summary>
        /// <returns>操作是否成功</returns>
        public static bool ForbiddenAndroidPen()
        {
            bool flag;
            try
            {
                flag = McuHunter.Write(McuCommand.ForbiddenAndroidPenCommand);
            }
            catch
            {
                flag = false;
            }
            return flag;
        }

        /// <summary>
        /// 启用安卓触控笔
        /// </summary>
        /// <returns>操作是否成功</returns>
        public static bool OpenAndroidPen()
        {
            bool flag;
            try
            {
                flag = McuHunter.Write(McuCommand.OpenAndroidPenCommand);
            }
            catch
            {
                flag = false;
            }
            return flag;
        }

        /// <summary>
        /// 获取CV触摸屏尺寸
        /// </summary>
        /// <returns>触摸屏尺寸（英寸），如果获取失败返回-1</returns>
        public static int GetCVTouchSize()
        {
            int num;
            try
            {
                if (!McuHunter.Write(McuCommand.GetMcuVersionCommand))
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

        /// <summary>
        /// 异步获取CV触摸屏尺寸
        /// </summary>
        /// <returns>触摸屏尺寸（英寸），如果获取失败返回-1</returns>
        public static async Task<int> GetCVTouchSizeAsync()
        {
            int num;
            try
            {
                if (!McuHunter.Write(McuCommand.GetMcuVersionCommand))
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

        /// <summary>
        /// 切换到HDMI1
        /// </summary>
        /// <returns>操作是否成功</returns>
        public static bool SwitchToHdmi1()
        {
            bool flag;
            try
            {
                flag = McuHunter.Write(McuCommand.SwitchToHdmi1Command);
            }
            catch
            {
                flag = false;
            }
            return flag;
        }

        /// <summary>
        /// 获取主板名称
        /// </summary>
        /// <returns>主板名称字符串</returns>
        public static string GetBoardName()
        {
            string text;
            try
            {
                if (!McuHunter.Write(McuCommand.GetBoardNameCommand))
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

                text = string.Empty;
            }
            return text;
        }

        /// <summary>
        /// 异步获取主板名称
        /// </summary>
        /// <returns>主板名称字符串</returns>
        public static async Task<string> GetBoardNameAsync()
        {
            string text;
            try
            {
                if (!McuHunter.Write(McuCommand.GetBoardNameCommand))
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

                text = string.Empty;
            }
            return text;
        }

        /// <summary>
        /// 获取设备IP地址
        /// </summary>
        /// <returns>IP地址字符串</returns>
        public static string GetIP()
        {
            string text;
            try
            {
                if (!McuHunter.Write(McuCommand.GetIPCommand))
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
            catch (Exception)
            {

                text = string.Empty;
            }
            return text;
        }

        /// <summary>
        /// 异步获取设备IP地址
        /// </summary>
        /// <returns>IP地址字符串</returns>
        public static async Task<string> GetIPAsync()
        {
            string text;
            try
            {
                if (!McuHunter.Write(McuCommand.GetIPCommand))
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
            catch (Exception)
            {

                text = string.Empty;
            }
            return text;
        }

        /// <summary>
        /// 获取设备UID
        /// </summary>
        /// <returns>UID字符串</returns>
        public static string GetUid()
        {
            if (!McuHunter.Write(McuCommand.GetUidCommand))
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

        /// <summary>
        /// 异步获取设备UID
        /// </summary>
        /// <returns>UID字符串</returns>
        public static async Task<string> GetUidAsync()
        {
            string text;
            if (!McuHunter.Write(McuCommand.GetUidCommand))
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

        /// <summary>
        /// 增加音量
        /// </summary>
        /// <returns>操作是否成功</returns>
        public static bool AddVolume()
        {
            // 添加用户操作提示
            OnLog("Info", "正在增加音量...");
            
            var cmd = McuCommand.AddVolumeCommand;
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

        /// <summary>
        /// 异步增加音量
        /// </summary>
        /// <returns>操作是否成功</returns>
        public static async Task<bool> AddVolumeAsync()
        {
            // 添加用户操作提示
            OnLog("Info", "正在增加音量...");
            
            if (!McuHunter.Write(McuCommand.AddVolumeCommand))
            {
                OnLog("WriteError", "AddVolume WriteError");
                return false;
            }
            
            // 添加10毫秒延时，避免操作过于频繁
            await Task.Delay(10);
            
            OnLog("AddVolume", "增加音量指令已发送");
            return true;
        }

        /// <summary>
        /// 降低音量
        /// </summary>
        /// <returns>操作是否成功</returns>
        public static bool DecreaseVolume()
        {
            // 添加用户操作提示
            OnLog("Info", "正在降低音量...");
            
            var cmd = McuCommand.DecreaseVolume;
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

        /// <summary>
        /// 异步降低音量
        /// </summary>
        /// <returns>操作是否成功</returns>
        public static async Task<bool> DecreaseVolumeAsync()
        {
            // 添加用户操作提示
            OnLog("Info", "正在降低音量...");
            
            if (!McuHunter.Write(McuCommand.DecreaseVolume))
            {
                OnLog("WriteError", "DecreaseVolume WriteError");
                return false;
            }
            
            // 添加10毫秒延时，避免操作过于频繁
            await Task.Delay(10);
            
            OnLog("DecreaseVolume", "降低音量指令已发送");
            return true;
        }

        /// <summary>
        /// 写入数据（已过时，建议使用McuHunter.Write）
        /// </summary>
        /// <param name="array">要写入的数据</param>
        /// <returns>操作是否成功</returns>
        [Obsolete("请使用McuHunter.Write")]
        public static bool Write(byte[] array)
        {
            return McuHunter.WriteToDevice(array);
        }

        /// <summary>
        /// 读取数据（已过时，建议使用McuHunter.Read）
        /// </summary>
        /// <param name="dataLength">数据长度</param>
        /// <param name="data">读取到的数据</param>
        /// <returns>操作是否成功</returns>
        [Obsolete("请使用McuHunter.Read")]
        public static bool Read(int dataLength, out byte[] data)
        {
            return McuHunter.ReadFromDevice(dataLength, out data);
        }

        /// <summary>
        /// 异步读取数据（已过时，建议使用McuHunter.TryReadFromDeviceAsync）
        /// </summary>
        /// <param name="dataLength">数据长度</param>
        /// <param name="methodName">方法名称</param>
        /// <returns>包含操作结果和数据的元组</returns>
        [Obsolete("请使用 McuHunter.TryReadFromDeviceAsync")]
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

        /// <summary>
        /// 关闭连接（已过时，建议使用McuHunter.CloseConnection）
        /// </summary>
        [Obsolete("请使用 McuHunter.CloseConnection")]
        public static void CloseConnection()
        {
            McuHunter.CloseDeviceConnection();
        }

        /// <summary>
        /// 内部日志方法
        /// </summary>
        /// <param name="log">日志类型</param>
        /// <param name="msg">日志消息</param>
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