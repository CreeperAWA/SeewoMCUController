using System.Collections.Generic;

namespace SeewoMCUController.Mcu
{
    internal class UsbIdSortComparer : IComparer<UsbId?>
    {
        private readonly List<UsbIdIndex> UsbIdSortList;

        public UsbIdSortComparer(IEnumerable<UsbId> usbIdSortList)
        {
            var list = new List<UsbIdIndex>();
            int i = 0;
            foreach (var temp in usbIdSortList) list.Add(new UsbIdIndex(temp, i++));
            this.UsbIdSortList = list;
        }

        public int Compare(UsbId? x, UsbId? y)
        {
            if (x == y) return 0;
            if (y == null) return -1;
            if (x == null) return 1;
            UsbIdIndex usbIdIndex = GetUsbIdIndex(x);
            UsbIdIndex usbIdIndex2 = GetUsbIdIndex(y);
            if (usbIdIndex.Index == usbIdIndex2.Index) return 0;
            return usbIdIndex.Index.CompareTo(usbIdIndex2.Index);
        }

        private UsbIdIndex GetUsbIdIndex(UsbId? usbId)
        {
            foreach (var usbIdIndex in this.UsbIdSortList)
            {
                if (IsUsbIdEquals(usbIdIndex.UsbId, usbId)) return usbIdIndex;
            }
            // podium special case
            if (IsUsbIdEquals(McuPodium, usbId)) return new UsbIdIndex(usbId, 10001);
            return new UsbIdIndex(usbId, 10000);
        }

        private static bool IsUsbIdEquals(UsbId? patternUsbId, UsbId? usbId)
        {
            return patternUsbId != null && usbId != null && patternUsbId.Pid == usbId.Pid && patternUsbId.Vid == usbId.Vid && (patternUsbId.Rev == 0 || usbId.Rev == 0 || patternUsbId.Rev == usbId.Rev) && (string.IsNullOrEmpty(patternUsbId.Key) || IsStringContainIgnoreCase(usbId.Key, patternUsbId.Key) || IsStringContainIgnoreCase(usbId.DeviceName, patternUsbId.Key));
        }

        private static bool IsStringContainIgnoreCase(string? str, string? pattern)
        {
            return str != null && (string.IsNullOrEmpty(pattern) || str.IndexOf(pattern, System.StringComparison.OrdinalIgnoreCase) > -1);
        }

        private static UsbId McuPodium = new UsbId(0x0F31, 0x1FF7, "mi_00", 0);

        private readonly struct UsbIdIndex
        {
            public UsbIdIndex(UsbId? usbId, int index) { UsbId = usbId; Index = index; }
            public UsbId? UsbId { get; }
            public int Index { get; }
        }
    }
}
