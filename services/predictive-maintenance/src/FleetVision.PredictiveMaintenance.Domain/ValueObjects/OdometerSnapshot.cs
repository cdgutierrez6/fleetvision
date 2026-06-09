namespace FleetVision.PredictiveMaintenance.Domain.ValueObjects;

public sealed record OdometerSnapshot(decimal Km, bool IsUnknown = false)
{
    public static OdometerSnapshot Unknown => new(0m, IsUnknown: true);
    public static OdometerSnapshot FromKm(decimal km) => new(km);
}
