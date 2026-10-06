using System.Text;

namespace SeewoMCUController.Mcu.Protocol;

/// <summary>帧封装模式。两种模式都承载同一份 64 字节「业务帧」，区别只在最前面是否带 5 字节传输头。</summary>
public enum FrameMode
{
    /// <summary>自动探测：先按当前猜测发送握手包，收到回包即锁定；也可由上行帧被动判定。</summary>
    Auto = 0,

    /// <summary>模式 A（无头）：线上帧就是业务帧本身，尾部带校验和。</summary>
    Raw = 1,

    /// <summary>模式 B（带头 <c>FC A5 05 3B 00</c>）。本项目历史实现使用的模式，实机可用。</summary>
    Framed = 2,
}

/// <summary>
/// 一份 64 字节「业务帧」，字段偏移定义如下：
/// <code>
/// [0]=0xFB frameType  [1]=MAIN  [2]=SUB  [3]=len/param  [4]=0x00  [5..]=payload
/// </code>
/// 发送时 <c>[0]</c> 会被强制为 <c>0xFB</c>，<c>[63]</c> 写入校验和。
/// </summary>
public sealed class McuFrame
{
    public const int Size = 64;
    public const int PayloadOffset = 5;

    /// <summary>5 字节传输头长度（FC A5 05 3B 00）。</summary>
    public const int TransportHeaderSize = 5;

    /// <summary>无头模式下的最大负载长度（[5..62] 共 58 字节）。</summary>
    public const int MaxRawPayload = Size - PayloadOffset - 1;

    /// <summary>带头模式下右移 5 字节后仍能送达的负载长度（原 [5..58]）。</summary>
    public const int MaxFramedPayload = MaxRawPayload - TransportHeaderSize + 1;

    /// <summary>上行可达的负载长度：剥掉传输头后只剩留 58 字节。</summary>
    public const int MaxUplinkPayload = MaxRawPayload - TransportHeaderSize;

    public const byte FrameTypeOk = 0xFB;
    public const byte FrameTypeUplinkHeader = 0xFC;

    public byte[] Buffer { get; private set; } = new byte[Size];

    public McuFrame()
    {
        Buffer[0] = FrameTypeOk;
    }

    public McuFrame(byte main, byte sub)
    {
        Buffer[0] = FrameTypeOk;
        Buffer[1] = main;
        Buffer[2] = sub;
    }

    public McuFrame(byte[] buffer)
    {
        Buffer = buffer.Length == Size ? buffer : new byte[Size];
        Array.Copy(buffer, Buffer, Math.Min(buffer.Length, Size));
    }

    public byte FrameType => Buffer[0];
    public byte Main => Buffer[1];
    public byte Sub => Buffer[2];

    /// <summary><c>[3]</c>。绝大多数命令是 payload 长度；例外：<code>(0x08,0x00)</code> 写常量 <c>1</c>，
/// <code>(0x34,0x0A)</code> 写常量 <c>0x01</c>，<code>(0x34,0x0B)</code> 是状态码，<code>(0x07/0x13,ch)</code> 是参数字节数。</summary>
    public byte Raw => Buffer[3];

    /// <summary><c>[3]</c> 作为长度解读后的负载长度，上限 58。</summary>
    public int PayloadLength => Math.Min((int)Raw, MaxRawPayload);

    public ReadOnlySpan<byte> Payload => Buffer.AsSpan(PayloadOffset, PayloadLength);

    public ReadOnlySpan<byte> PayloadBytes(int offset, int count)
    {
        int start = PayloadOffset + offset;
        if (start < 0 || start >= Size) return ReadOnlySpan<byte>.Empty;
        count = Math.Min(count, Size - start);
        return Buffer.AsSpan(start, count);
    }

    /// <summary>读取以 <c>\0</c> 结尾的字符串负载；<c>maxLen</c> 为 <c>[3]</c> 之外的长度上限。</summary>
    public string PayloadString(int skip = 0, int maxLen = -1)
    {
        int limit = maxLen < 0 ? PayloadLength : Math.Min(PayloadLength, skip + maxLen);
        if (limit <= skip) return string.Empty;
        int len = 0;
        for (int i = skip; i < limit; i++)
        {
            if (Buffer[PayloadOffset + i] == 0) break;
            len = i - skip + 1;
        }
        return Encoding.UTF8.GetString(Buffer, PayloadOffset + skip, len);
    }

    /// <summary>
    /// 读取以 <c>\0</c> 结尾的字符串，<b>不使用</b> <c>[3]</c> 长度。
    /// <c>(0x34,0x0D)</c> 板卡名、<c>(0x34,0x14)</c> 通知等命令的 <c>[3]</c> 不是长度，需按此规则读取。
    /// </summary>
    public string CString(int skip = 0)
    {
        int start = PayloadOffset + skip;
        if (start >= Size) return string.Empty;
        int len = 0;
        for (int i = start; i < Size; i++)
        {
            if (Buffer[i] == 0) break;
            len++;
        }
        return Encoding.UTF8.GetString(Buffer, start, len);
    }

    public byte PayloadByte(int offset)
    {
        int idx = PayloadOffset + offset;
        return idx < Size ? Buffer[idx] : (byte)0;
    }

    public McuFrame SetPayload(ReadOnlySpan<byte> payload)
    {
        int n = Math.Min(payload.Length, MaxRawPayload);
        Buffer[3] = (byte)n;
        payload[..n].CopyTo(Buffer.AsSpan(PayloadOffset));
        return this;
    }

    public McuFrame SetRawPayload(byte value)
    {
        Buffer[PayloadOffset] = value;
        return this;
    }

    /// <summary>封帧：强制 <c>[0]=0xFB</c>，并把 <c>[0..62]</c> 的字节和写进 <c>[63]</c>。</summary>
    public McuFrame Seal()
    {
        Buffer[0] = FrameTypeOk;
        uint sum = 0;
        for (int i = 0; i < Size - 1; i++)
        {
            sum += Buffer[i];
        }
        Buffer[Size - 1] = (byte)(sum & 0xFF);
        return this;
    }

    public McuFrame Clone() => new McuFrame(ToArray());

    public byte[] ToArray() => (byte[])Buffer.Clone();

    /// <summary>生成带 5 字节传输头的 64 字节线上帧：业务帧右移 5 字节，再在线首写入传输头。</summary>
    public byte[] ToTransport(FrameMode mode)
    {
        Seal();
        if (mode != FrameMode.Framed) return ToArray();

        var wire = new byte[Size];
        // 右移 5 字节：for (i = 0x3F; i >= 5; i--) f[i] = f[i-5];
        for (int i = Size - 1; i >= TransportHeaderSize; i--)
        {
            wire[i] = Buffer[i - TransportHeaderSize];
        }
        wire[0] = 0xFC;
        wire[1] = 0xA5;
        wire[2] = 0x05;
        wire[3] = 0x3B;
        wire[4] = 0x00;
        return wire;
    }

    public string ToHex(int count = Size)
    {
        var sb = new StringBuilder(count * 3);
        for (int i = 0; i < count && i < Size; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(Buffer[i].ToString("X2"));
        }
        return sb.ToString();
    }

    public override string ToString() =>
        string.Format("(0x{0:X2},0x{1:X2}) len={2} [{3}]", Main, Sub, Raw, ToHex(16));
}
