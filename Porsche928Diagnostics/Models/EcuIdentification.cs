namespace Porsche928Diagnostics.Models;

public record EcuIdentification(
    string ChipCode,
    string BoschPartNumber,
    string EpromVersion,
    byte[] RawBytes
)
{
    public override string ToString() =>
        $"Chip: {ChipCode} | Part: {BoschPartNumber} | EPROM: {EpromVersion}";
}
