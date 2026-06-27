namespace Porsche928Diagnostics.Models;

public record AlarmData(
    string CountryCode,
    bool EngineLidSwitchOpen,
    bool LuggageLidSwitchOpen,
    bool GloveCompartmentSwitchOpen,
    bool InteriorMotionSensorActive,
    byte RawSwitchStateByte
);
