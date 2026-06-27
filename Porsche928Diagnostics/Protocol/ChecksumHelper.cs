namespace Porsche928Diagnostics.Protocol;

/// <summary>
/// ISO 9141-2 Modulo-256 checksum. The checksum byte appended to a frame equals
/// the sum of all preceding bytes, masked to 8 bits.
/// </summary>
public static class ChecksumHelper
{
    public static byte Calculate(ReadOnlySpan<byte> data)
    {
        int sum = 0;
        foreach (var b in data)
            sum += b;
        return (byte)(sum & 0xFF);
    }

    /// <summary>
    /// Verifies a frame where the last byte is the checksum over all preceding bytes.
    /// </summary>
    public static bool Verify(ReadOnlySpan<byte> frame)
    {
        if (frame.Length < 2) return false;
        var payload = frame[..^1];
        var expected = frame[^1];
        return Calculate(payload) == expected;
    }
}
