using Porsche928Diagnostics.Models;
using Porsche928Diagnostics.Protocol;

namespace Porsche928Diagnostics.Modules;

public sealed class RdkModule : BaseEcuModule
{
    public override byte EcuAddress => 0x30;
    public override string EcuName => "RDK Tire Pressure Monitor";

    public RdkModule(IKLineInterface kLine) : base(kLine) { }

    public async Task<RdkSensorData> ReadSensorDataAsync(CancellationToken ct = default)
    {
        var parsed = await SendAndVerifyAsync(sid: 0x21, data: [0x01], ct: ct);
        var d = parsed.Data;
        if (d.Length < 1)
            throw new InvalidOperationException("RDK: Empty sensor response");

        byte switchByte = d[0];
        bool[] pressureStates =
        [
            (switchByte & 0x01) != 0,  // FL
            (switchByte & 0x02) != 0,  // FR
            (switchByte & 0x04) != 0,  // RL
            (switchByte & 0x08) != 0   // RR
        ];

        bool hfActive = d.Length > 1 && d[1] != 0x00;

        double[] absSpeed = new double[4];
        for (int i = 0; i < 4 && (i + 2) < d.Length; i++)
            absSpeed[i] = d[i + 2] * 1.5;

        return new RdkSensorData(pressureStates, hfActive, absSpeed);
    }
}
