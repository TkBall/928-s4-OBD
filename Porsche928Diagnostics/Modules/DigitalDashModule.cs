namespace Porsche928Diagnostics.Modules;

/// <summary>
/// Helper for triggering the built-in self-test mode of the 928 digital instrument cluster.
///
/// The cluster does NOT communicate over K-Line. Its diagnostic mode is activated by
/// grounding Pin 6 of the 19-pin connector for exactly 3 seconds with the ignition ON.
/// This causes the cluster microcontroller to loop through all display segments and
/// then report sensor values sequentially on its LCD.
///
/// Readings available in dash self-test mode:
///   Oil pressure (bar), Oil level (L), Brake fluid level (OK/LOW),
///   Engine temperature (°C), Coolant level (OK/LOW),
///   Toothed belt tension (OK/FAULT — critical safety item).
/// </summary>
public sealed class DigitalDashModule
{
    public record DashTestStep(int StepNumber, string Instruction, int DurationSeconds);

    private static readonly DashTestStep[] Steps =
    [
        new(1, "Turn ignition ON (do not start engine). Dashboard should illuminate normally.", 5),
        new(2, "Ground Pin 6 of the 19-pin diagnostic connector. Use a jumper wire from Pin 6 to Pin 1 (chassis ground). Hold for 3 seconds.", 3),
        new(3, "Release the ground on Pin 6. The dashboard display will now enter segment-check mode — all segments light simultaneously.", 4),
        new(4, "READING: Oil pressure display. Normal idle range: 2.0–4.5 bar. Record value.", 5),
        new(5, "READING: Oil level. Min line = 4.0L low. Record display.", 5),
        new(6, "READING: Brake fluid level. Display shows OK or LOW. LOW = inspect reservoir.", 5),
        new(7, "READING: Engine coolant temperature (°C). Should match ECU actual value ±5°C.", 5),
        new(8, "READING: Coolant level. OK = sufficient, LOW = check expansion tank.", 5),
        new(9, "CRITICAL READING: Toothed belt tension sensor. OK = belt tensioner within spec. FAULT = inspect tensioner roller immediately.", 8),
        new(10, "Self-test sequence complete. The display will return to normal operation. Turn ignition OFF.", 3)
    ];

    public IReadOnlyList<DashTestStep> GetTestSteps() => Steps;

    /// <summary>
    /// Runs the guided dashboard test sequence, reporting each step via the progress callback.
    /// Each step waits for its specified duration before advancing.
    /// </summary>
    public async Task RunGuidedSequenceAsync(
        IProgress<DashTestStep> progress,
        CancellationToken ct = default)
    {
        foreach (var step in Steps)
        {
            ct.ThrowIfCancellationRequested();
            progress.Report(step);
            await Task.Delay(step.DurationSeconds * 1000, ct);
        }
    }
}
