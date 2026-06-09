using FleetVision.Identity.Application.Common.Interfaces;
using FleetVision.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FleetVision.Identity.Infrastructure.Persistence;

public sealed class IdentityDbContext : DbContext, IIdentityDbContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options) { }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);

        // OpenIddict tables (gestionadas por OpenIddict)
        modelBuilder.UseOpenIddict();
    }
}
