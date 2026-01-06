using System.Text;
using System.Collections.Generic;
using System.Linq;

namespace SeewoMCUController.Mcu;

public static class McuFinder
{
    // 使用McuHunter中定义的MCU设备类型
    public static List<UsbId> DefineDeviceMcu => Cvte.Mcu.McuHunter.DefineDeviceMcu;

    // DetectedMcu is a single global for the whole process — there is only one MCU.
    public static UsbId? DetectedMcu
    {
        get => Cvte.Mcu.McuHunter.DetectedMcu;
        set => Cvte.Mcu.McuHunter.DetectedMcu = value;
    }

    public static bool FindMcu()
    {
        try
        {

            var devices = FindAll();

            return devices.Any();
        }
        catch (Exception)
        {


        }
        return Cvte.Mcu.McuHunter.FindMcu();
    }

    public static List<UsbId> FindAll()
    {

        List<UsbId> devices = Cvte.Mcu.McuHunter.FindAll();

        return devices;
    }

    public static UsbId? GetOrFindMcu()
    {
        return Cvte.Mcu.McuHunter.GetOrFindMcu();
    }

    public static bool FindAndConnection()
    {
        return Cvte.Mcu.McuHunter.FindAndConnection();
    }

    public static bool Write(byte[] array)
    {
        bool flag;
        try
        {
            var detectedMcu = DetectedMcu;
            if (detectedMcu == null && !FindAndConnection())
            {
                flag = false;
            }
            else if (!Usb.Connect(detectedMcu) && !FindAndConnection())
            {
                flag = false;
            }
            else if (!Usb.Write(array) && !FindAndConnection())
            {
                flag = false;
            }
            else
            {
                flag = Usb.Write(array);
            }
        }
        catch (Exception)
        {
            flag = false;
        }
        return flag;
    }

    public static bool Write(UsbId usbId, byte[] array)
    {
        UsbDevice usb = new UsbDevice();
        return usb.Connect(usbId) && usb.Write(array);
    }

    public static bool Read(int dataLength, out byte[] data)
    {
        data = new byte[dataLength];
        bool flag;
        try
        {
            flag = Usb.Read(dataLength, out data);
        }
        catch (Exception)
        {
            flag = false;
        }
        return flag;
    }

    public static void CloseConnection()
    {
        try
        {
            UsbDevice usb = Usb;
            if (usb != null)
            {
                usb.Disconnect();
            }
        }
        catch (Exception)
        {
        }
    }

    public static UsbDevice Usb = Cvte.Mcu.McuHunter.Usb;


}