using Porsche928Diagnostics.Models;
using Porsche928Diagnostics.Protocol;

namespace Porsche928Diagnostics.Modules;

/// <summary>
/// LH Jetronic 2.3 fuel injection ECU, K-Line address 0x11.
/// Supports: ECU ID, fault codes, drive links (tank vent/resonance flap/injectors/ISV),
/// input signals (throttle/WOT/airco), actual values (battery voltage, temp, MAF, lambda),
/// and the System Adaptation Program (SAP).
/// </summary>
public sealed class LhModule : BaseEcuModule
{
    public override byte EcuAddress => 0x11;
    public override string EcuName => "LH Jetronic 2.3";

    public LhModule(IKLineInterface kLine) : base(kLine) { }

    /// <summary>
    /// Reads discrete switch/flag states via SID 0x21, PID 0x40.
    /// Returns a single byte with individual bit flags.
    /// </summary>
    public async Task<LhInputSignals> ReadInputSignalsAsync(CancellationToken ct = default)
    {
        var parsed = await SendAndVerifyAsync(sid: 0x21, data: [0x40], ct: ct);
        if (parsed.Data.Length < 1)
            throw new InvalidOperationException("LH: Empty input signals response");
        return new LhInputSignals(parsed.Data[0]);
    }

    /// <summary>
    /// Reads static ECU sensor values via SID 0x21.
    /// Battery voltage: RawByte × 0.065V.
    /// Engine temp uses NTC thermistor lookup table (voltage drop → °C).
    /// </summary>
    public async Task<LhActualValues> ReadActualValuesAsync(CancellationToken ct = default)
    {
        var parsed = await SendAndVerifyAsync(sid: 0x21, data: [0x01], ct: ct);
        var d = parsed.Data;
        if (d.Length < 4)
            throw new InvalidOperationException("LH: Insufficient actual values data");

        double battery = d[0] * 0.065;
        double refVolt = d[1] * (5.0 / 255.0);
        bool ezkOn = d[2] != 0x00;
        double tempC = NtcLookup(d[3]);

        return new LhActualValues(battery, refVolt, ezkOn, tempC,
            MafVoltage: 0, LambdaVoltage: 0, VehicleSpeedKph: 0,
            Coding4Cylinder: false, IsActiveReading: false);
    }

    /// <summary>
    /// Reads active (engine-running) values: MAF voltage, lambda, vehicle speed.
    /// SID 0x21 with PID 0x02 for active readings.
    /// </summary>
    public async Task<LhActualValues> ReadActiveValuesAsync(CancellationToken ct = default)
    {
        var parsed = await SendAndVerifyAsync(sid: 0x21, data: [0x02], ct: ct);
        var d = parsed.Data;
        if (d.Length < 4)
            throw new InvalidOperationException("LH: Insufficient active values data");

        double mafVoltage = d[0] * (5.0 / 255.0);
        double lambdaMv = d[1] * 5.0;
        double speedKph = d[2] * 1.0;
        bool coding4Cyl = (d[3] & 0x01) != 0;

        return new LhActualValues(0, 0, false, 0,
            mafVoltage, lambdaMv, speedKph, coding4Cyl, IsActiveReading: true);
    }

    /// <summary>
    /// Activates a drive link actuator via SID 0x30.
    /// State byte 0x01 = active; 0x00 = stop.
    /// </summary>
    public async Task ActivateDriveLinkAsync(LhDriveLink link, CancellationToken ct = default)
    {
        byte deviceId = (byte)link;
        await SendAndVerifyAsync(sid: 0x30, data: [deviceId, 0x01], ct: ct);
    }

    public async Task StopDriveLinkAsync(LhDriveLink link, CancellationToken ct = default)
    {
        byte deviceId = (byte)link;
        await SendAndVerifyAsync(sid: 0x30, data: [deviceId, 0x00], ct: ct);
    }

    /// <summary>
    /// Executes the System Adaptation Program (SAP) via SID 0x31, PID 0x0A.
    /// Engine must be at operating temperature and idling. The LH module
    /// monitors the lambda closed-loop system and writes corrected base
    /// injector pulse offsets to non-volatile RAM.
    /// This process takes approximately 60 seconds; progress is reported via callback.
    /// </summary>
    public async Task RunSystemAdaptationAsync(
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        progress?.Report("Starting System Adaptation Program. Engine must be at operating temperature, idling.");

        // SID 0x31 (Start Routine), PID 0x0A (Adaptation routine identifier)
        var startFrame = MessageFrame.Build(EcuAddress, sid: 0x31, data: [0x0A]);
        var startResponse = await KLine.SendFrameAsync(startFrame, timeoutMs: 3000, ct);
        var startParsed = MessageFrame.Parse(startResponse);
        if (!startParsed.IsValid)
            throw new InvalidOperationException("LH: SAP start command rejected");

        progress?.Report("SAP running — monitoring lambda closed-loop. Do not blip throttle.");

        // Poll routine status every 5 seconds for up to 90 seconds
        var deadline = DateTime.UtcNow.AddSeconds(90);
        while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            await Task.Delay(5000, ct);

            // SID 0x33 (Request Routine Results)
            var pollFrame = MessageFrame.Build(EcuAddress, sid: 0x33, data: [0x0A]);
            var pollResponse = await KLine.SendFrameAsync(pollFrame, timeoutMs: 2000, ct);
            var pollParsed = MessageFrame.Parse(pollResponse);

            if (pollParsed.IsValid && pollParsed.Data.Length > 0)
            {
                byte status = pollParsed.Data[0];
                if (status == 0x02)
                {
                    progress?.Report("SAP complete. Adaptation values written to ECU non-volatile RAM.");
                    return;
                }
                progress?.Report($"SAP in progress (status=0x{status:X2})...");
            }
        }

        progress?.Report("SAP timed out. Check idle quality and retry.");
    }

    /// <summary>
    /// NTC thermistor lookup table for LH engine temperature sensor.
    /// Maps raw ADC byte (0–255 representing ~0–5V) to degrees Celsius.
    /// Values derived from Bosch NTC M12 sensor characteristic curve.
    /// </summary>
    private static double NtcLookup(byte rawByte)
    {
        return rawByte switch
        {
            >= 0xE0 => -40.0,
            >= 0xC0 => -20.0,
            >= 0xA0 => 0.0,
            >= 0x80 => 20.0,
            >= 0x6A => 80.0,
            >= 0x50 => 100.0,
            >= 0x3A => 120.0,
            _ => 140.0
        };
    }
}

public enum LhDriveLink : byte
{
    TankVentValve  = 0x01,
    ResonanceFlap  = 0x02,
    FuelInjectors  = 0x03,
    IdleStabilizerValve = 0x04
}
