using Porsche928Diagnostics.Protocol;

namespace Porsche928Diagnostics.Modules;

public sealed class PsdModule : BaseEcuModule
{
    public override byte EcuAddress => 0x28;
    public override string EcuName => "PSD Slip Differential";

    private const int DefaultBleedDurationSeconds = 60;

    public PsdModule(IKLineInterface kLine) : base(kLine) { }

    public async Task StartBleedProcedureAsync(
        int durationSeconds = DefaultBleedDurationSeconds,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        if (durationSeconds < 0) durationSeconds = DefaultBleedDurationSeconds;

        progress?.Report("Activating PSD hydraulic pump and solenoid valve...");

        await SendAndVerifyAsync(sid: 0x30, data: [0x01, 0x01], ct: ct);

        progress?.Report($"Bleed sequence active. Crack bleeder screw. Running for {durationSeconds}s.");

        try
        {
            for (int elapsed = 0; elapsed < durationSeconds; elapsed++)
            {
                await Task.Delay(1000, ct);
                int remaining = durationSeconds - elapsed - 1;
                progress?.Report($"Bleed active — {remaining}s remaining. Keep bleeder cracked until fluid flows clear.");
            }
        }
        finally
        {
            progress?.Report("Stopping bleed actuator — tighten bleeder screw now.");
            var stopFrame = MessageFrame.Build(EcuAddress, sid: 0x30, data: [0x01, 0x00]);
            await KLine.SendFrameAsync(stopFrame, timeoutMs: 2000, CancellationToken.None);
            progress?.Report("PSD bleed procedure complete.");
        }
    }

    public async Task CheckTransverseLockAsync(CancellationToken ct = default)
    {
        await SendAndVerifyAsync(sid: 0x30, data: [0x02, 0x01], ct: ct);
        await Task.Delay(3000, ct);
        await SendAndVerifyAsync(sid: 0x30, data: [0x02, 0x00], ct: ct);
    }
}
