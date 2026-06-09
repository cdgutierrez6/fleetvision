namespace FleetVision.Geofencing.Domain.Entities;

public sealed class VehicleGeofenceState
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid VehicleId { get; private set; }
    public Guid GeofenceId { get; private set; }
    public bool IsInside { get; private set; }
    public DateTime LastEvaluatedAt { get; private set; }

    private VehicleGeofenceState() { }

    public static VehicleGeofenceState Create(Guid tenantId, Guid vehicleId, Guid geofenceId, bool isInside)
    {
        return new VehicleGeofenceState
        {
            Id              = Guid.NewGuid(),
            TenantId        = tenantId,
            VehicleId       = vehicleId,
            GeofenceId      = geofenceId,
            IsInside        = isInside,
            LastEvaluatedAt = DateTime.UtcNow
        };
    }

    public void UpdateState(bool isInside)
    {
        IsInside        = isInside;
        LastEvaluatedAt = DateTime.UtcNow;
    }
}
