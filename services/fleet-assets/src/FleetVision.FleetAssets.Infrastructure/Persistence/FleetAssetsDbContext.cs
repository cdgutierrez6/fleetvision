using FleetVision.FleetAssets.Application.Common;
using FleetVision.FleetAssets.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FleetVision.FleetAssets.Infrastructure.Persistence;

public sealed class FleetAssetsDbContext : DbContext, IFleetAssetsDbContext
{
    public FleetAssetsDbContext(DbContextOptions<FleetAssetsDbContext> options) : base(options) { }

    public DbSet<Fleet> Fleets => Set<Fleet>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<VehicleAssignment> VehicleAssignments => Set<VehicleAssignment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FleetAssetsDbContext).Assembly);

        modelBuilder.Entity<Vehicle>().HasQueryFilter(v => !v.IsDeleted);
        modelBuilder.Entity<Driver>().HasQueryFilter(d => !d.IsDeleted);
    }
}
