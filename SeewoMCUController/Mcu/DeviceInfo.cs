namespace SeewoMCUController.Mcu
{
    public class DeviceInfo
    {
        public DeviceInfo(string devicePath, int rev)
        {
            DevicePath = devicePath;
            Rev = rev;
        }

        public string DevicePath { get; }
        public int Rev { get; }
    }
}
