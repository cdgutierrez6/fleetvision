using FleetVision.Geofencing.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FleetVision.Geofencing.Application.Common;

public interface IGeofencingDbContext
{
    DbSet<Geofence> Geofences { get; }
    DbSet<GeofenceViolation> Violations { get; }
    DbSet<VehicleGeofenceState> VehicleGeofenceStates { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
