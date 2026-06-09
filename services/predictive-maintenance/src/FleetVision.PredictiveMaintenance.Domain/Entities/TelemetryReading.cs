namespace FleetVision.PredictiveMaintenance.Domain.Entities;

public sealed record TelemetryReading(
    Guid     TenantId,
    Guid     VehicleId,
    double   Latitude,
    double   Longitude,
    float    SpeedKmh,
    decimal  DistanceKm,
    string?  Obd2Code,
    DateTime Timestamp);
