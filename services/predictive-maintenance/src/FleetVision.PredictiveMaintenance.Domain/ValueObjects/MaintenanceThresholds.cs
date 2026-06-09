namespace FleetVision.PredictiveMaintenance.Domain.ValueObjects;

public sealed record MaintenanceThresholds(
    decimal OdometerKm   = 10_000m,
    int     TimeBasedDays = 180);
