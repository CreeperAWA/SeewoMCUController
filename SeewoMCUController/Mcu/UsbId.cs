using System;
using System.Linq;

namespace SeewoMCUController.Mcu
{
    public class UsbId
    {
        private string _deviceName = string.Empty;

        public UsbId(int pid, int vid, string key, int rev = 0)
        {
            Pid = pid;
            Vid = vid;
            Key = key;
            Rev = rev;
        }

        public UsbId(string deviceName, int rev = 0)
        {
            DeviceName = deviceName;
            Rev = rev;
        }

        public int Pid { get; private set; } = -1;
        public int Vid { get; private set; } = -1;
        public string Key { get; private set; } = string.Empty;
        public int Rev { get; }

        public string DeviceName
        {
            get => _deviceName;
            set
            {
                _deviceName = value;
                if (!TryParseDeviceName(_deviceName))
                {
                    Vid = -1;
                    Pid = -1;
                    Key = "";
                }
            }
        }

        private bool TryParseDeviceName(string deviceName)
        {
            if (string.IsNullOrEmpty(deviceName)) return false;
            string[] parts = deviceName.Split('#');
            if (parts.Length == 4)
            {
                string[] idParts = parts[1].Split('&');
                try
                {
                    string[] vidParts = idParts[0].Split('_');
                    if (vidParts.Length != 2) return false;
                    if (!int.TryParse(vidParts[1], System.Globalization.NumberStyles.AllowHexSpecifier, System.Globalization.CultureInfo.InvariantCulture, out int vid)) return false;

                    string[] pidParts = idParts[1].Split('_');
                    if (pidParts.Length != 2) return false;
                    if (!int.TryParse(pidParts[1], System.Globalization.NumberStyles.AllowHexSpecifier, System.Globalization.CultureInfo.InvariantCulture, out int pid)) return false;

                    Vid = vid;
                    Pid = pid;

                    int keyStartIndex = parts[1].IndexOf(idParts[1], StringComparison.Ordinal) + idParts[1].Length + 1;
                    Key = keyStartIndex < parts[1].Length ? parts[1].Substring(keyStartIndex, parts[1].Length - keyStartIndex) : "";
                    return true;
                }
                catch { return false; }
            }
            return false;
        }

        public override string ToString()
        {
            return $"Pid:0x{Pid:X4},Vid:0x{Vid:X4},Key:{Key}";
        }
    }
}