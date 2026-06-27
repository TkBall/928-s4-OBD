namespace Porsche928Diagnostics.Models;

/// <summary>
/// Decoded from SID 0x21, PID 0x40 single-byte bit field.
/// Each bit represents a discrete switch state.
/// </summary>
public record LhInputSignals(byte RawByte)
{
    public bool ThrottleIdleSwitch     => (RawByte & 0x01) != 0;  // Bit 0
    public bool WideOpenThrottleSwitch => (RawByte & 0x02) != 0;  // Bit 1
    public bool AircoCompressorDemand  => (RawByte & 0x04) != 0;  // Bit 2
    public bool IdleDropGearEngagement => (RawByte & 0x08) != 0;  // Bit 3
}
