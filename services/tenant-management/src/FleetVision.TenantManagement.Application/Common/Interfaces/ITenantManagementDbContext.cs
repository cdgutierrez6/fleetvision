using FleetVision.TenantManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FleetVision.TenantManagement.Application.Common.Interfaces;

public interface ITenantManagementDbContext
{
    DbSet<TenantProfile> TenantProfiles { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
