using Porsche928Diagnostics.Models;
using Porsche928Diagnostics.Protocol;

namespace Porsche928Diagnostics.Modules;

public sealed class EzkModule : BaseEcuModule
{
    public override byte EcuAddress => 0x12;
    public override string EcuName => "EZK Ignition Control";

    public EzkModule(IKLineInterface kLine) : base(kLine) { }

    public async Task<EzkSensorData> ReadSensorDataAsync(CancellationToken ct = default)
    {
        var parsed = await SendAndVerifyAsync(sid: 0x21, data: [0x01], ct: ct);
        var d = parsed.Data;
        if (d.Length < 7)
            throw new InvalidOperationException("EZK: Insufficient sensor data bytes");

        int rpm = (d[0] << 8) | d[1];
        double load = (d[2] / 255.0) * 100.0;
        double tempC = d[3] * 0.75 - 40.0;
        string transmission = (d[4] & 0x01) != 0 ? "Manual" : "Automatic";
        bool throttleActive = d[6] != 0x00;

        return new EzkSensorData(rpm, load, tempC, transmission, throttleActive,
            KnockCountPerCylinder: new int[8]);
    }

    public async Task<int[]> ReadKnockCountsAsync(CancellationToken ct = default)
    {
        var parsed = await SendAndVerifyAsync(sid: 0x21, data: [0x04], ct: ct);
        var d = parsed.Data;

        var counts = new int[8];
        for (int i = 0; i < Math.Min(8, d.Length); i++)
            counts[i] = d[i];

        return counts;
    }
}
