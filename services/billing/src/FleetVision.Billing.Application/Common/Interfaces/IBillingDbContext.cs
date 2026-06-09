using FleetVision.Billing.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FleetVision.Billing.Application.Common.Interfaces;

public interface IBillingDbContext
{
    DbSet<Subscription> Subscriptions { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends a plan-change audit entry to the current unit of work.
    /// The entry is persisted when SaveChangesAsync is called.
    /// </summary>
    void AddPlanChangeAudit(Guid tenantId, string oldPlan, string newPlan,
        string source, string? stripeEventId = null);
}
