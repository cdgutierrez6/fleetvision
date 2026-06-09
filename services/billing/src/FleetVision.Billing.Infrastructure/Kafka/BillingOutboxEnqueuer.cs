using System.Text;
using System.Text.Json;
using FleetVision.Billing.Application.Common.Interfaces;
using FleetVision.Billing.Domain.Enums;
using FleetVision.Billing.Infrastructure.Persistence;
using FleetVision.Billing.Infrastructure.Persistence.Entities;

namespace FleetVision.Billing.Infrastructure.Kafka;

/// <summary>
/// Writes billing events to billing_outbox_events in the same DbContext transaction
/// as the domain change. BillingRelayWorker picks them up and publishes to Kafka.
/// </summary>
public sealed class BillingOutboxEnqueuer : IBillingEventPublisher
{
    private readonly BillingDbContext _db;

    public BillingOutboxEnqueuer(BillingDbContext db) => _db = db;

    public void EnqueueTenantProvisioned(Guid tenantId, PlanTier plan, string billingEmail)
    {
        var payload = new
        {
            tenantId     = tenantId,
            plan         = plan.ToString(),
            billingEmail = billingEmail,
            provisionedAt = DateTime.UtcNow
        };

        _db.OutboxEvents.Add(BillingOutboxEvent.Create(
            topic:        "tenant.provisioned",
            partitionKey: tenantId.ToString(),
            payload:      JsonBytes(payload)));
    }

    public void EnqueueSubscriptionChanged(
        Guid tenantId,
        PlanTier oldPlan,
        PlanTier newPlan,
        SubscriptionStatus status)
    {
        var payload = new
        {
            tenantId    = tenantId,
            oldPlan     = oldPlan.ToString(),
            newPlan     = newPlan.ToString(),
            status      = status.ToString(),
            effectiveAt = DateTime.UtcNow
        };

        _db.OutboxEvents.Add(BillingOutboxEvent.Create(
            topic:        "billing.subscription.changed",
            partitionKey: tenantId.ToString(),
            payload:      JsonBytes(payload)));
    }

    private static byte[] JsonBytes(object obj)
        => Encoding.UTF8.GetBytes(JsonSerializer.Serialize(obj));
}
