using System.Text;

namespace SeewoMCUController.Mcu;

internal static class CVTouchAnalyzer
{
    public static bool IsMatchCVTouchVersion(byte[] data)
    {
        return data.Length > 7 && data[6] == 198 && data[7] == 2;
    }

    public static int GetCVTouchSize(byte[] data)
    {
        bool foundNumber = false;
        var sb = new StringBuilder();
        for (int i = 16; i < data.Length; i++)
        {
            if (IsNumber(data[i]))
            {
                sb.Append((data[i] - 48).ToString());
                foundNumber = true;
            }
            else if (!IsNumber(data[i]) && foundNumber)
            {
                if (int.TryParse(sb.ToString(), out int size))
                {
                    return size;
                }
                break;
            }
        }
        return -1;
    }

    private static bool IsNumber(byte data)
    {
        return data >= 48 && data <= 57;
    }
}