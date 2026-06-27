using Porsche928Diagnostics.Models;
using Porsche928Diagnostics.Protocol;

namespace Porsche928Diagnostics.Modules;

/// <summary>
/// Provides ReadEcuIdentification, ReadDtcs, and ClearDtcs for all ECU modules.
/// Subclasses add module-specific commands.
///
/// All methods assume an active ISO 9141-2 session has already been established
/// by Iso9141Session.InitializeAsync() before being called.
/// </summary>
public abstract class BaseEcuModule : IEcuModule
{
    protected readonly IKLineInterface KLine;

    public abstract byte EcuAddress { get; }
    public abstract string EcuName { get; }

    protected BaseEcuModule(IKLineInterface kLine)
    {
        KLine = kLine;
    }

    public async Task<EcuIdentification> ReadEcuIdentificationAsync(CancellationToken ct = default)
    {
        // SID 0x1A, Option 0x90 — returns chip code, Bosch part number, EPROM version
        var frame = MessageFrame.Build(EcuAddress, sid: 0x1A, data: [0x90]);
        var rawResponse = await KLine.SendFrameAsync(frame, timeoutMs: 2000, ct);
        var parsed = MessageFrame.Parse(rawResponse);

        if (!parsed.IsValid)
            throw new InvalidOperationException($"{EcuName}: Invalid ECU ID response");

        var data = parsed.Data;
        string chip = data.Length >= 4 ? BitConverter.ToString(data[..4]).Replace("-", "") : "Unknown";
        string part = data.Length >= 10 ? System.Text.Encoding.ASCII.GetString(data[4..10]).Trim() : "Unknown";
        string eprom = data.Length >= 11 ? $"v{data[10]:X2}" : "Unknown";

        return new EcuIdentification(chip, part, eprom, data);
    }

    public async Task<IReadOnlyList<DiagnosticTroubleCode>> ReadDtcsAsync(CancellationToken ct = default)
    {
        // SID 0x18 — returns stored fault code pairs
        var frame = MessageFrame.Build(EcuAddress, sid: 0x18, data: []);
        var rawResponse = await KLine.SendFrameAsync(frame, timeoutMs: 2000, ct);
        var parsed = MessageFrame.Parse(rawResponse);

        if (!parsed.IsValid)
            throw new InvalidOperationException($"{EcuName}: Invalid DTC response");

        var dtcs = new List<DiagnosticTroubleCode>();
        var data = parsed.Data;

        // DTC pairs: first byte is fault code, second is occurrence counter / status
        for (int i = 0; i + 1 < data.Length; i += 2)
        {
            if (data[i] == 0x00) continue; // 0x00 0x00 = end of list / no fault
            dtcs.Add(new DiagnosticTroubleCode(data[i], data[i + 1]));
        }

        return dtcs.AsReadOnly();
    }

    public async Task ClearDtcsAsync(CancellationToken ct = default)
    {
        // SID 0x14 — clears non-volatile fault RAM
        var frame = MessageFrame.Build(EcuAddress, sid: 0x14, data: []);
        var rawResponse = await KLine.SendFrameAsync(frame, timeoutMs: 2000, ct);
        var parsed = MessageFrame.Parse(rawResponse);

        if (!parsed.IsValid)
            throw new InvalidOperationException($"{EcuName}: Clear DTC command rejected");
    }

    /// <summary>
    /// Sends a frame and validates the response. Throws on invalid checksum or negative response SID.
    /// Negative response SID = 0x7F; second data byte is the original SID, third is error code.
    /// </summary>
    protected async Task<ParsedFrame> SendAndVerifyAsync(byte sid, byte[] data,
        int timeoutMs = 2000, CancellationToken ct = default)
    {
        var frame = MessageFrame.Build(EcuAddress, sid, data);
        var raw = await KLine.SendFrameAsync(frame, timeoutMs, ct);
        var parsed = MessageFrame.Parse(raw);

        if (!parsed.IsValid)
            throw new InvalidOperationException($"{EcuName}: Frame checksum invalid for SID 0x{sid:X2}");

        if (parsed.ServiceId == 0x7F)
            throw new InvalidOperationException(
                $"{EcuName}: Negative response to SID 0x{sid:X2}, error code 0x{(parsed.Data.Length > 1 ? parsed.Data[1] : 0):X2}");

        return parsed;
    }
}
