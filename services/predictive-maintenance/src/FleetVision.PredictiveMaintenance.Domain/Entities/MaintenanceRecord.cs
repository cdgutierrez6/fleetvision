namespace FleetVision.PredictiveMaintenance.Domain.Entities;

public sealed class MaintenanceRecord
{
    public Guid   Id           { get; private set; }
    public Guid   TenantId     { get; private set; }
    public Guid   VehicleId    { get; private set; }
    public string RecordType   { get; private set; } = default!;
    public string TriggeredBy  { get; private set; } = default!;
    public string? Obd2Code    { get; private set; }
    public decimal? OdometerKm { get; private set; }
    public decimal? ThresholdKm { get; private set; }
    public string? Notes       { get; private set; }
    public DateTime? ResolvedAt { get; private set; }
    public DateTime CreatedAt  { get; private set; }

    private MaintenanceRecord() { }

    public static MaintenanceRecord CreateScheduled(
        Guid tenantId, Guid vehicleId, string triggeredBy,
        decimal? odometerKm = null, decimal? thresholdKm = null, string? notes = null)
        => new()
        {
            Id          = Guid.NewGuid(),
            TenantId    = tenantId,
            VehicleId   = vehicleId,
            RecordType  = "SCHEDULED",
            TriggeredBy = triggeredBy,
            OdometerKm  = odometerKm,
            ThresholdKm = thresholdKm,
            Notes       = notes,
            CreatedAt   = DateTime.UtcNow,
        };

    public static MaintenanceRecord CreateCriticalAlert(
        Guid tenantId, Guid vehicleId, string obd2Code, decimal? odometerKm = null)
        => new()
        {
            Id          = Guid.NewGuid(),
            TenantId    = tenantId,
            VehicleId   = vehicleId,
            RecordType  = "CRITICAL_ALERT",
            TriggeredBy = "OBD2_CODE",
            Obd2Code    = obd2Code,
            OdometerKm  = odometerKm,
            CreatedAt   = DateTime.UtcNow,
        };

    public void Resolve()
    {
        ResolvedAt = DateTime.UtcNow;
        RecordType = "COMPLETED";
    }
}
