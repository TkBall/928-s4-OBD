namespace Porsche928Diagnostics.Models;

public record AirbagData(
    TimeSpan DowntimeClock,
    bool CrashEventRecorded,
    bool DriverBagFired,
    bool PassengerBagFired,
    bool SeatbeltPretensionerFired,
    byte RawCrashDataByte
);
