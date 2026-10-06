namespace SeewoMCUController.Mcu.Protocol;

/// <summary>
/// 上行 64 字节报告的拆帧：
/// <c>raw[0] == 0xFC</c> 时剥掉 5 字节传输头，随后要求 <c>f[0] == 0xFB</c>。
/// </summary>
public static class McuFrameCodec
{
    /// <summary>拆帧。<paramref name="mode"/> 给出该报告的封装形式（0xFC 头 → Framed，否则 Raw）。</summary>
    public static bool TryParse(byte[] raw, out McuFrame frame, out FrameMode mode)
    {
        frame = new McuFrame();
        mode = FrameMode.Raw;
        if (raw is null || raw.Length < McuFrame.Size) return false;

        int offset;
        if (raw[0] == McuFrame.FrameTypeUplinkHeader)
        {
            mode = FrameMode.Framed;
            offset = McuFrame.TransportHeaderSize;
        }
        else if (raw[0] != McuFrame.FrameTypeOk)
        {
            // 少数固件会在报告首字节填别的值，扫描 0xFB 作为兜底
            int found = Array.IndexOf(raw, McuFrame.FrameTypeOk, 1, McuFrame.Size - 1);
            if (found < 0) return false;
            mode = FrameMode.Framed;
            offset = found;
        }
        else
        {
            offset = 0;
        }

        // 带头模式下只前移 0x3A = 58 字节（f[5..57] 为上行负载上限），
        // 无头模式下则是原始 64 字节整帧透传。
        int count = offset == 0 ? McuFrame.Size : McuFrame.PayloadOffset + McuFrame.MaxUplinkPayload;
        for (int i = 0; i < count; i++)
        {
            frame.Buffer[i] = raw[offset + i];
        }
        return frame.Buffer[0] == McuFrame.FrameTypeOk;
    }

    public static bool TryParse(byte[] raw, out McuFrame frame) => TryParse(raw, out frame, out _);
}
