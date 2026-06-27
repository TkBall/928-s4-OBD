namespace Porsche928Diagnostics.Models;

public record RdkSensorData(
    bool[] PressureSwitchStates,
    bool HfReceiverActive,
    double[] AbsSpeedKph
);
