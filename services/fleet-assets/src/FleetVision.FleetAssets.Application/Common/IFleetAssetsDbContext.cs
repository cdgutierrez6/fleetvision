using FleetVision.FleetAssets.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FleetVision.FleetAssets.Application.Common;

public interface IFleetAssetsDbContext
{
    DbSet<Fleet> Fleets { get; }
    DbSet<Vehicle> Vehicles { get; }
    DbSet<Driver> Drivers { get; }
    DbSet<VehicleAssignment> VehicleAssignments { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
