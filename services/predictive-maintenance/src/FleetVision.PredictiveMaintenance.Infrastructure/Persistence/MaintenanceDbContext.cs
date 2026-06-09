using FleetVision.PredictiveMaintenance.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FleetVision.PredictiveMaintenance.Infrastructure.Persistence;

public sealed class MaintenanceDbContext : DbContext
{
    public MaintenanceDbContext(DbContextOptions<MaintenanceDbContext> options) : base(options) { }

    public DbSet<MaintenanceRecord>     Records     => Set<MaintenanceRecord>();
    public DbSet<MaintenanceOutboxEvent> OutboxEvents => Set<MaintenanceOutboxEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(MaintenanceDbContext).Assembly);
}
