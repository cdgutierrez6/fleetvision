using FleetVision.Geofencing.Domain.Enums;
using NetTopologySuite.Geometries;

namespace FleetVision.Geofencing.Domain.Entities;

public sealed class GeofenceViolation
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid GeofenceId { get; private set; }
    public Guid VehicleId { get; private set; }
    public Guid? DriverId { get; private set; }
    public ViolationType ViolationType { get; private set; }
    public Point Position { get; private set; } = null!;
    public double? ActualSpeedKmh { get; private set; }
    public int? LimitSpeedKmh { get; private set; }
    public DateTime OccurredAt { get; private set; }

    private GeofenceViolation() { }

    public static GeofenceViolation Create(
        Guid tenantId,
        Guid geofenceId,
        Guid vehicleId,
        Guid? driverId,
        ViolationType violationType,
        Point position,
        double? actualSpeedKmh = null,
        int? limitSpeedKmh = null)
    {
        if (tenantId == Guid.Empty)    throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));
        if (geofenceId == Guid.Empty)  throw new ArgumentException("GeofenceId cannot be empty.", nameof(geofenceId));
        if (vehicleId == Guid.Empty)   throw new ArgumentException("VehicleId cannot be empty.", nameof(vehicleId));
        if (position is null)          throw new ArgumentNullException(nameof(position));

        return new GeofenceViolation
        {
            Id             = Guid.NewGuid(),
            TenantId       = tenantId,
            GeofenceId     = geofenceId,
            VehicleId      = vehicleId,
            DriverId       = driverId,
            ViolationType  = violationType,
            Position       = position,
            ActualSpeedKmh = actualSpeedKmh,
            LimitSpeedKmh  = limitSpeedKmh,
            OccurredAt     = DateTime.UtcNow
        };
    }
}
