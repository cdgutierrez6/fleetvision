using FleetVision.Geofencing.Application.Common;
using FleetVision.Geofencing.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FleetVision.Geofencing.Infrastructure.Persistence;

public sealed class GeofencingDbContext : DbContext, IGeofencingDbContext
{
    public GeofencingDbContext(DbContextOptions<GeofencingDbContext> options) : base(options) { }

    public DbSet<Geofence> Geofences => Set<Geofence>();
    public DbSet<GeofenceViolation> Violations => Set<GeofenceViolation>();
    public DbSet<VehicleGeofenceState> VehicleGeofenceStates => Set<VehicleGeofenceState>();
    public DbSet<GeofencingOutboxEvent> OutboxEvents => Set<GeofencingOutboxEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GeofencingDbContext).Assembly);
    }
}
