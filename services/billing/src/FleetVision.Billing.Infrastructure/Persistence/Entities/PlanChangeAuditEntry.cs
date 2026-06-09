namespace FleetVision.Billing.Infrastructure.Persistence.Entities;

public sealed class PlanChangeAuditEntry
{
    private PlanChangeAuditEntry() { }

    public Guid    Id            { get; private set; }
    public Guid    TenantId      { get; private set; }
    public string  OldPlan       { get; private set; } = default!;
    public string  NewPlan       { get; private set; } = default!;
    public string  Source        { get; private set; } = default!;
    public string? StripeEventId { get; private set; }
    public DateTime OccurredAt   { get; private set; }

    public static PlanChangeAuditEntry Create(
        Guid    tenantId,
        string  oldPlan,
        string  newPlan,
        string  source,
        string? stripeEventId = null) => new()
    {
        Id            = Guid.NewGuid(),
        TenantId      = tenantId,
        OldPlan       = oldPlan,
        NewPlan       = newPlan,
        Source        = source,
        StripeEventId = stripeEventId,
        OccurredAt    = DateTime.UtcNow,
    };
}
