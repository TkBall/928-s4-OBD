using Porsche928Diagnostics.Models;
using Porsche928Diagnostics.Protocol;

namespace Porsche928Diagnostics.Modules;

public sealed class AlarmModule : BaseEcuModule
{
    public override byte EcuAddress => 0x45;
    public override string EcuName => "Alarm System";

    public AlarmModule(IKLineInterface kLine) : base(kLine) { }

    public async Task<AlarmData> ReadAlarmDataAsync(CancellationToken ct = default)
    {
        var parsed = await SendAndVerifyAsync(sid: 0x21, data: [0x01], ct: ct);
        var d = parsed.Data;
        if (d.Length < 3)
            throw new InvalidOperationException("Alarm: Insufficient response data");

        byte switchByte = d[0];
        string countryCode = $"{(char)d[1]}{(char)d[2]}";

        return new AlarmData(
            countryCode,
            EngineLidSwitchOpen:        (switchByte & 0x01) != 0,
            LuggageLidSwitchOpen:       (switchByte & 0x02) != 0,
            GloveCompartmentSwitchOpen: (switchByte & 0x04) != 0,
            InteriorMotionSensorActive: (switchByte & 0x08) != 0,
            switchByte
        );
    }

    public async Task SetCountryCodingAsync(string countryCode, CancellationToken ct = default)
    {
        if (countryCode.Length != 2)
            throw new ArgumentException("Country code must be exactly 2 characters", nameof(countryCode));

        byte b1 = (byte)countryCode[0];
        byte b2 = (byte)countryCode[1];
        await SendAndVerifyAsync(sid: 0x30, data: [b1, b2], ct: ct);
    }

    public async Task ActivateDriveLinkAsync(AlarmDriveLink link, CancellationToken ct = default)
    {
        await SendAndVerifyAsync(sid: 0x30, data: [(byte)link, 0x01], ct: ct);
    }

    public async Task StopDriveLinkAsync(AlarmDriveLink link, CancellationToken ct = default)
    {
        await SendAndVerifyAsync(sid: 0x30, data: [(byte)link, 0x00], ct: ct);
    }
}

public enum AlarmDriveLink : byte
{
    Horn           = 0x01,
    Indicators     = 0x02,
    InteriorLights = 0x03,
    CentralLock    = 0x04
}
