using Porsche928Diagnostics.Models;
using Porsche928Diagnostics.Protocol;

namespace Porsche928Diagnostics.Modules;

public sealed class AirbagModule : BaseEcuModule
{
    public override byte EcuAddress => 0x40;
    public override string EcuName => "Airbag (TRW)";

    public AirbagModule(IKLineInterface kLine) : base(kLine) { }

    public async Task<AirbagData> ReadAirbagDataAsync(CancellationToken ct = default)
    {
        var parsed = await SendAndVerifyAsync(sid: 0x21, data: [0x01], ct: ct);
        var d = parsed.Data;
        if (d.Length < 4)
            throw new InvalidOperationException("Airbag: Insufficient response data");

        int downtimeSeconds = (d[0] << 16) | (d[1] << 8) | d[2];
        byte crashByte = d[3];

        bool driverFired    = (crashByte & 0x01) != 0;
        bool passengerFired = (crashByte & 0x02) != 0;
        bool seatbeltFired  = (crashByte & 0x04) != 0;
        bool anyCrash = driverFired || passengerFired || seatbeltFired;

        return new AirbagData(
            TimeSpan.FromSeconds(downtimeSeconds),
            anyCrash, driverFired, passengerFired, seatbeltFired,
            crashByte
        );
    }
}
