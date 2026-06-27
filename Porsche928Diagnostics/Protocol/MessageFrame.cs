namespace Porsche928Diagnostics.Protocol;

/// <summary>
/// Builds and parses ISO 9141-2 / KWP1281-style message frames for 928 ECU communication.
/// Frame layout: [Format] [Target] [0xF1] [SID] [Data...] [Checksum]
/// Format byte = 0x80 | (SID+Data length). Source address 0xF1 identifies the tester.
/// </summary>
public static class MessageFrame
{
    private const byte TesterAddress = 0xF1;

    public static byte[] Build(byte targetAddress, byte sid, ReadOnlySpan<byte> data)
    {
        int dataLen = 1 + data.Length; // SID counts as a data byte
        byte format = (byte)(0x80 | (dataLen & 0x3F));

        var frame = new byte[4 + data.Length + 1]; // format+target+source+sid + data + cs
        frame[0] = format;
        frame[1] = targetAddress;
        frame[2] = TesterAddress;
        frame[3] = sid;
        data.CopyTo(frame.AsSpan(4));
        frame[^1] = ChecksumHelper.Calculate(frame.AsSpan(0, frame.Length - 1));
        return frame;
    }

    public static ParsedFrame Parse(ReadOnlySpan<byte> raw)
    {
        if (raw.Length < 5) return ParsedFrame.Invalid;
        if (!ChecksumHelper.Verify(raw)) return ParsedFrame.Invalid;

        // Derive data length from actual frame length:
        // frame = [format, target, source, sid, data..., checksum]
        // data bytes = total length - 5 (format+target+source+sid+checksum)
        int dataLen = raw.Length - 5;
        if (dataLen < 0) dataLen = 0;

        return new ParsedFrame(
            IsValid: true,
            SourceAddress: raw[1],
            DestAddress: raw[2],
            ServiceId: raw[3],
            Data: dataLen > 0 ? raw.Slice(4, dataLen).ToArray() : []
        );
    }
}

public record ParsedFrame(
    bool IsValid,
    byte SourceAddress,
    byte DestAddress,
    byte ServiceId,
    byte[] Data)
{
    public static readonly ParsedFrame Invalid = new(false, 0, 0, 0, []);
}
