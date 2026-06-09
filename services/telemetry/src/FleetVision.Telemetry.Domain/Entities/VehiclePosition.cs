namespace FleetVision.Telemetry.Domain.Entities;

public sealed class VehiclePosition
{
    public DateTime Time { get; private set; }
    public Guid VehicleId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid? DriverId { get; private set; }
    public double Latitude { get; private set; }
    public double Longitude { get; private set; }
    public double? SpeedKmh { get; private set; }
    public short? HeadingDeg { get; private set; }
    public double? AccuracyM { get; private set; }
    public double? Hdop { get; private set; }
    public short? SatelliteCount { get; private set; }
    public double? FuelPct { get; private set; }
    public bool? EngineOn { get; private set; }
    public string[]? Obd2Codes { get; private set; }
    public double? OdometerKm { get; private set; }

    private VehiclePosition() { }

    public static VehiclePosition Create(
        Guid vehicleId,
        Guid tenantId,
        DateTime timestamp,
        double latitude,
        double longitude,
        Guid? driverId = null,
        double? speedKmh = null,
        short? headingDeg = null,
        double? accuracyM = null,
        double? hdop = null,
        short? satelliteCount = null,
        double? fuelPct = null,
        bool? engineOn = null,
        string[]? obd2Codes = null,
        double? odometerKm = null)
    {
        if (vehicleId == Guid.Empty)  throw new ArgumentException("VehicleId cannot be empty.", nameof(vehicleId));
        if (tenantId == Guid.Empty)   throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));
        if (latitude  < -90  || latitude  > 90)  throw new ArgumentOutOfRangeException(nameof(latitude),  "Latitude must be between -90 and 90.");
        if (longitude < -180 || longitude > 180)  throw new ArgumentOutOfRangeException(nameof(longitude), "Longitude must be between -180 and 180.");
        if (speedKmh.HasValue && speedKmh.Value < 0) throw new ArgumentOutOfRangeException(nameof(speedKmh), "Speed cannot be negative.");
        if (fuelPct.HasValue  && (fuelPct.Value < 0 || fuelPct.Value > 100))
            throw new ArgumentOutOfRangeException(nameof(fuelPct), "Fuel percentage must be 0-100.");
        if (odometerKm.HasValue && odometerKm.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(odometerKm), "Odometer cannot be negative.");

        return new VehiclePosition
        {
            Time            = timestamp.Kind == DateTimeKind.Utc ? timestamp : timestamp.ToUniversalTime(),
            VehicleId       = vehicleId,
            TenantId        = tenantId,
            DriverId        = driverId,
            Latitude        = latitude,
            Longitude       = longitude,
            SpeedKmh        = speedKmh,
            HeadingDeg      = headingDeg,
            AccuracyM       = accuracyM,
            Hdop            = hdop,
            SatelliteCount  = satelliteCount,
            FuelPct         = fuelPct,
            EngineOn        = engineOn,
            Obd2Codes       = obd2Codes,
            OdometerKm      = odometerKm
        };
    }

    public bool HasGoodAccuracy() =>
        AccuracyM is null or <= 50.0 &&
        Hdop      is null or <= 2.0;
}
