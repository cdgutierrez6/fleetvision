using FleetVision.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FleetVision.Identity.Application.Common.Interfaces;

public interface IIdentityDbContext
{
    DbSet<Tenant> Tenants { get; }
    DbSet<User> Users { get; }
    DbSet<RefreshToken> RefreshTokens { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
