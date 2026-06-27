namespace Porsche928Diagnostics.Models;

public record DiagnosticTroubleCode(byte RawByte1, byte RawByte2)
{
    public string Code => $"{RawByte1:X2}{RawByte2:X2}";
    public string Description => KnownDtcDescriptions.TryGet(RawByte1, RawByte2);
    public override string ToString() => $"DTC {Code}: {Description}";
}

/// <summary>
/// Maps known 928 ECU DTC byte pairs to human-readable descriptions.
/// Codes follow Bosch/Porsche diagnostic specification.
/// </summary>
public static class KnownDtcDescriptions
{
    private static readonly Dictionary<(byte, byte), string> _map = new()
    {
        { (0x21, 0x00), "MAF sensor signal out of range" },
        { (0x22, 0x00), "Engine coolant temperature sensor fault" },
        { (0x23, 0x00), "Throttle position sensor fault" },
        { (0x24, 0x00), "Oxygen sensor (Lambda) fault" },
        { (0x25, 0x00), "Idle speed control fault" },
        { (0x26, 0x00), "EZK communication fault (LH↔EZK link)" },
        { (0x27, 0x00), "Fuel injector output fault" },
        { (0x28, 0x00), "Tank vent solenoid fault" },
        { (0x31, 0x00), "Knock sensor cylinder 1 fault" },
        { (0x32, 0x00), "Knock sensor cylinder 2 fault" },
        { (0x33, 0x00), "Knock sensor cylinder 3 fault" },
        { (0x34, 0x00), "Knock sensor cylinder 4 fault" },
        { (0x35, 0x00), "Crank position sensor fault" },
        { (0x41, 0x00), "Airbag squib circuit fault" },
        { (0x42, 0x00), "Airbag safing sensor fault" },
        { (0x51, 0x00), "PSD hydraulic pressure fault" },
        { (0x52, 0x00), "PSD solenoid valve fault" },
    };

    public static string TryGet(byte b1, byte b2) =>
        _map.TryGetValue((b1, b2), out var desc) ? desc : "Unknown fault code";
}
