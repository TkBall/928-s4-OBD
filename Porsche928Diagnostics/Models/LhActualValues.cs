namespace Porsche928Diagnostics.Models;

public record LhActualValues(
    double BatteryVoltage,
    double ReferenceVoltage,
    bool EzkOnSignal,
    double EngineTemperatureDegC,
    double MafVoltage,
    double LambdaVoltage,
    double VehicleSpeedKph,
    bool Coding4Cylinder,
    bool IsActiveReading
);
