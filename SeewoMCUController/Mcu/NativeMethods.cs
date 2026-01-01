using System.Runtime.InteropServices;

namespace SeewoMCUController.Mcu;

internal static class NativeMethods
{
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, EntryPoint = "SetupDiGetDeviceRegistryPropertyW", SetLastError = true)]
    internal static extern bool SetupDiGetDeviceRegistryProperty(
        IntPtr deviceInfoSet,
        ref SP_DEVINFO_DATA deviceInfoData,
        int propertyVal,
        out int propertyRegDataType,
        IntPtr propertyBuffer,
        int propertyBufferSize,
        out int requiredSize);

    [DllImport("setupapi.dll", SetLastError = true)]
    internal static extern bool SetupDiEnumDeviceInfo(
        IntPtr deviceInfoSet,
        int memberIndex,
        ref SP_DEVINFO_DATA deviceInfoData);

    [DllImport("setupapi.dll", SetLastError = true)]
    internal static extern bool SetupDiEnumDeviceInterfaces(
        IntPtr deviceInfoSet,
        ref SP_DEVINFO_DATA deviceInfoData,
        ref Guid interfaceClassGuid,
        int memberIndex,
        ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData);

    [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true, EntryPoint = "SetupDiGetDeviceInterfaceDetail")]
    internal static extern bool SetupDiGetDeviceInterfaceDetailBuffer(
        IntPtr deviceInfoSet,
        ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData,
        IntPtr deviceInterfaceDetailData,
        int deviceInterfaceDetailDataSize,
        ref int requiredSize,
        IntPtr deviceInfoData);

    [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
    internal static extern bool SetupDiGetDeviceInterfaceDetail(
        IntPtr deviceInfoSet,
        ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData,
        ref SP_DEVICE_INTERFACE_DETAIL_DATA deviceInterfaceDetailData,
        int deviceInterfaceDetailDataSize,
        ref int requiredSize,
        IntPtr deviceInfoData);

    [DllImport("setupapi.dll")]
    internal static extern int SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

    [DllImport("setupapi.dll", CharSet = CharSet.Auto)]
    internal static extern IntPtr SetupDiGetClassDevs(
        ref Guid classGuid,
        string enumerator,
        int hwndParent,
        int flags);

    [DllImport("hid.dll")]
    internal static extern void HidD_GetHidGuid(ref Guid hidGuid);

    [DllImport("hid.dll")]
    internal static extern bool HidD_GetAttributes(
        IntPtr hidDeviceObject,
        ref HIDD_ATTRIBUTES attributes);

    [DllImport("hid.dll")]
    internal static extern bool HidD_GetPreparsedData(
        IntPtr hidDeviceObject,
        ref IntPtr preparsedData);

    [DllImport("hid.dll")]
    internal static extern bool HidD_FreePreparsedData(IntPtr preparsedData);

    [DllImport("hid.dll")]
    internal static extern int HidP_GetCaps(IntPtr preparsedData, ref HIDP_CAPS capabilities);

    internal const int DIGCF_PRESENT = 0x00000002;
    internal const int DIGCF_DEVICEINTERFACE = 0x00000010;
    internal const int SPDRP_HARDWAREID = 0x00000001;
    internal const int SPDRP_DEVICEDESC = 0x00000000;

    [StructLayout(LayoutKind.Sequential)]
    internal struct SP_DEVINFO_DATA
    {
        internal int cbSize;
        internal Guid ClassGuid;
        internal int DevInst;
        internal IntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SP_DEVICE_INTERFACE_DATA
    {
        internal int cbSize;
        internal Guid InterfaceClassGuid;
        internal int Flags;
        internal IntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    internal struct SP_DEVICE_INTERFACE_DETAIL_DATA
    {
        internal int Size;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        internal string DevicePath;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct HIDD_ATTRIBUTES
    {
        internal int Size;
        internal ushort VendorID;
        internal ushort ProductID;
        internal short VersionNumber;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct HIDP_CAPS
    {
        internal short Usage;
        internal short UsagePage;
        internal short InputReportByteLength;
        internal short OutputReportByteLength;
        internal short FeatureReportByteLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
        internal short[] Reserved;
        internal short NumberLinkCollectionNodes;
        internal short NumberInputButtonCaps;
        internal short NumberInputValueCaps;
        internal short NumberInputDataIndices;
        internal short NumberOutputButtonCaps;
        internal short NumberOutputValueCaps;
        internal short NumberOutputDataIndices;
        internal short NumberFeatureButtonCaps;
        internal short NumberFeatureValueCaps;
        internal short NumberFeatureDataIndices;
    }
}
