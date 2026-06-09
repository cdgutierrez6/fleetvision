namespace FleetVision.FleetAssets.Domain.Entities;

public sealed class VehicleAssignment
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid VehicleId { get; private set; }
    public Guid DriverId { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? EndedAt { get; private set; }

    private VehicleAssignment() { }

    public static VehicleAssignment Create(Guid tenantId, Guid vehicleId, Guid driverId)
    {
        if (tenantId == Guid.Empty)  throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));
        if (vehicleId == Guid.Empty) throw new ArgumentException("VehicleId cannot be empty.", nameof(vehicleId));
        if (driverId == Guid.Empty)  throw new ArgumentException("DriverId cannot be empty.", nameof(driverId));

        return new VehicleAssignment
        {
            Id        = Guid.NewGuid(),
            TenantId  = tenantId,
            VehicleId = vehicleId,
            DriverId  = driverId,
            StartedAt = DateTime.UtcNow,
            EndedAt   = null
        };
    }

    public void Close()
    {
        if (EndedAt.HasValue)
            throw new InvalidOperationException("Assignment is already closed.");
        EndedAt = DateTime.UtcNow;
    }
}
