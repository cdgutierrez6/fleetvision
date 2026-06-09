using FleetVision.Billing.Application.Common.Interfaces;
using FleetVision.Billing.Domain.Entities;
using FleetVision.Billing.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace FleetVision.Billing.Infrastructure.Persistence;

public sealed class BillingDbContext : DbContext, IBillingDbContext
{
    public BillingDbContext(DbContextOptions<BillingDbContext> options) : base(options) { }

    public DbSet<Subscription>          Subscriptions  => Set<Subscription>();
    public DbSet<BillingOutboxEvent>    OutboxEvents   => Set<BillingOutboxEvent>();
    public DbSet<PlanChangeAuditEntry>  PlanChangeAudit => Set<PlanChangeAuditEntry>();

    public void AddPlanChangeAudit(
        Guid tenantId, string oldPlan, string newPlan,
        string source, string? stripeEventId = null)
        => PlanChangeAudit.Add(
            PlanChangeAuditEntry.Create(tenantId, oldPlan, newPlan, source, stripeEventId));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BillingDbContext).Assembly);
    }
}
