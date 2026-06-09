using FleetVision.TenantManagement.Application.Common.Interfaces;
using FleetVision.TenantManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FleetVision.TenantManagement.Infrastructure.Persistence;

public sealed class TenantManagementDbContext : DbContext, ITenantManagementDbContext
{
    public TenantManagementDbContext(DbContextOptions<TenantManagementDbContext> options)
        : base(options) { }

    public DbSet<TenantProfile> TenantProfiles => Set<TenantProfile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TenantManagementDbContext).Assembly);
    }
}
