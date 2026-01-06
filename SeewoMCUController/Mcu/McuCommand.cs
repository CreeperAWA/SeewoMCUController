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
        private const byte COMMAND_MAIN_ANDROID_6 = 51;
        private const byte COMMAND_SUB_SEND_TOOLBAR_7 = 6;
        private const byte COMMAND_SUB_SEND_HOME_7 = 8;
        private const byte COMMAND_SUB_SEND_EMR_7 = 16;
        private const byte COMMAND_SUB_SEND_WRITING_7 = 21;
        private const int DATA_LENGTH_8 = 1;
        private const byte DATA_RESERVE_9 = 0;
        private const byte TOOLBAR_HIDE = 0;
        private const byte TOOLBAR_SHOW = 1;
        private const byte TOOLBAR_MARK_HIDE = 2;
        private const byte TOOLBAR_MARK_SHOW = 3;
        private const byte TOOLBAR_FOLD = 4;
        private const byte LAUNCHER_LEAVE = 2;
        private const byte EMR_PEN = 1;
        private const byte EMR_MULTITOUCH = 2;
        private const byte WRITING_LAUNCH = 0;
        private const byte WRITING_START = 1;
        private const byte WRITING_STOP = 2;
        private const byte WRITING_EXIT = 3;

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
                array[8] = DATA_RESERVE_9;
                array[9] = DATA_RESERVE_9;
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
                _commandBase = new byte[DATA_LENGTH];
                _commandBase[0] = HEADER_0;
                _commandBase[1] = HEADER_1;
                _commandBase[2] = HEADER_2_PC_TO_ANDROID;
                _commandBase[3] = HEADER_3;
                _commandBase[4] = HEADER_4;
                _commandBase[5] = DEVICE_ID_5;
                _commandBase[6] = COMMAND_MAIN_ANDROID_6;
                _commandBase[8] = DATA_LENGTH_8;
                _commandBase[9] = DATA_RESERVE_9;
                return _commandBase;
            }
        }

        private static byte[] _commandBase = new byte[0];
    }
}
