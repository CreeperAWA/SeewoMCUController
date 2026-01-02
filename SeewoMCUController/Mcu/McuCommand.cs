using System;

namespace SeewoMCUController.Mcu
{
    internal static class McuCommand
    {
        private const byte HEADER_0 = 252;
        private const byte HEADER_1 = 165;
        private const byte HEADER_2_PC_TO_ANDROID = 5;
        private const byte HEADER_3 = 59;
        private const byte HEADER_4 = 0;
        private static readonly byte DEVICE_ID_5 = 251;
        private const int DATA_LENGTH = 64;

        public static byte[] ForbiddenAndroidPenCommand
        {
            get
            {
                byte[] array = new byte[DATA_LENGTH];
                array[0] = HEADER_0;
                array[1] = HEADER_1;
                array[2] = HEADER_2_PC_TO_ANDROID;
                array[3] = HEADER_3;
                array[4] = HEADER_4;
                array[5] = DEVICE_ID_5;
                array[6] = 42;
                array[7] = 0;
                array[8] = 1;
                array[9] = 0;
                array[10] = 0;
                return array;
            }
        }

        public static byte[] OpenAndroidPenCommand
        {
            get
            {
                byte[] array = new byte[DATA_LENGTH];
                array[0] = HEADER_0;
                array[1] = HEADER_1;
                array[2] = HEADER_2_PC_TO_ANDROID;
                array[3] = HEADER_3;
                array[4] = HEADER_4;
                array[5] = DEVICE_ID_5;
                array[6] = 42;
                array[7] = 0;
                array[8] = 1;
                array[9] = 0;
                array[10] = 1;
                return array;
            }
        }

        public static byte[] GetMcuVersionCommand
        {
            get
            {
                byte[] array = new byte[DATA_LENGTH];
                array[0] = HEADER_0;
                array[1] = HEADER_1;
                array[2] = HEADER_2_PC_TO_ANDROID;
                array[3] = HEADER_3;
                array[4] = HEADER_4;
                array[5] = DEVICE_ID_5;
                array[6] = 22;
                array[7] = 2;
                return array;
            }
        }

        public static byte[] SwitchToHdmi1Command
        {
            get
            {
                byte[] array = new byte[DATA_LENGTH];
                array[0] = HEADER_0;
                array[1] = HEADER_1;
                array[2] = HEADER_2_PC_TO_ANDROID;
                array[3] = HEADER_3;
                array[4] = HEADER_4;
                array[5] = DEVICE_ID_5;
                array[6] = 7;
                array[7] = 9;
                array[8] = 0;
                return array;
            }
        }

        public static byte[] GetBoardNameCommand
        {
            get
            {
                byte[] array = new byte[DATA_LENGTH];
                array[0] = HEADER_0;
                array[1] = HEADER_1;
                array[2] = HEADER_2_PC_TO_ANDROID;
                array[3] = HEADER_3;
                array[4] = HEADER_4;
                array[5] = DEVICE_ID_5;
                array[6] = 52;
                array[7] = 12;
                array[8] = 0;
                array[9] = 0;
                return array;
            }
        }

        public static byte[] GetUidCommand
        {
            get
            {
                byte[] array = new byte[DATA_LENGTH];
                array[0] = HEADER_0;
                array[1] = HEADER_1;
                array[2] = HEADER_2_PC_TO_ANDROID;
                array[3] = HEADER_3;
                array[4] = HEADER_4;
                array[5] = DEVICE_ID_5;
                array[6] = 64;
                array[7] = 0;
                array[8] = 0;
                array[9] = 0;
                return array;
            }
        }

        public static byte[] GetIPCommand
        {
            get
            {
                byte[] array = new byte[DATA_LENGTH];
                array[0] = HEADER_0;
                array[1] = HEADER_1;
                array[2] = HEADER_2_PC_TO_ANDROID;
                array[3] = HEADER_3;
                array[4] = HEADER_4;
                array[5] = DEVICE_ID_5;
                array[6] = 52;
                array[7] = 16;
                array[8] = 0;
                array[9] = 0;
                return array;
            }
        }

        public static byte[] AddVolumeCommand
        {
            get
            {
                byte[] array = new byte[DATA_LENGTH];
                array[0] = HEADER_0;
                array[1] = HEADER_1;
                array[2] = HEADER_2_PC_TO_ANDROID;
                array[3] = HEADER_3;
                array[4] = HEADER_4;
                array[5] = DEVICE_ID_5;
                array[6] = 8;
                array[7] = 2;
                array[8] = 0;
                array[9] = 0;
                return array;
            }
        }

        public static byte[] DecreaseVolume
        {
            get
            {
                byte[] array = new byte[DATA_LENGTH];
                array[0] = HEADER_0;
                array[1] = HEADER_1;
                array[2] = HEADER_2_PC_TO_ANDROID;
                array[3] = HEADER_3;
                array[4] = HEADER_4;
                array[5] = DEVICE_ID_5;
                array[6] = 8;
                array[7] = 1;
                array[8] = 0;
                array[9] = 0;
                return array;
            }
        }

        public static byte[] CommandBase
        {
            get
            {
                var cmd = new byte[DATA_LENGTH];
                cmd[0] = HEADER_0;
                cmd[1] = HEADER_1;
                cmd[2] = HEADER_2_PC_TO_ANDROID;
                cmd[3] = HEADER_3;
                cmd[4] = HEADER_4;
                cmd[5] = DEVICE_ID_5;
                cmd[6] = 51;
                cmd[8] = 1;
                cmd[9] = 0;
                return cmd;
            }
        }
    }
}
