namespace Porsche928Diagnostics.Models;

public record EzkSensorData(
    int EngineRpm,
    double LoadPercent,
    double EngineTemperatureDegC,
    string TransmissionCoding,
    bool ThrottleSignalActive,
    int[] KnockCountPerCylinder
);
