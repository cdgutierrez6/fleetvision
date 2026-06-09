using FleetVision.Billing.Domain.Enums;

namespace FleetVision.Billing.Domain.Entities;

public sealed class Subscription
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string StripeCustomerId { get; private set; } = string.Empty;
    public string? StripeSubscriptionId { get; private set; }
    public PlanTier Plan { get; private set; }
    public SubscriptionStatus Status { get; private set; }
    public DateTime? CurrentPeriodStart { get; private set; }
    public DateTime? CurrentPeriodEnd { get; private set; }
    public bool CancelAtPeriodEnd { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private Subscription() { }

    public static Subscription Create(Guid tenantId, string stripeCustomerId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(stripeCustomerId))
            throw new ArgumentException("StripeCustomerId is required.", nameof(stripeCustomerId));

        var now = DateTime.UtcNow;
        return new Subscription
        {
            Id               = Guid.NewGuid(),
            TenantId         = tenantId,
            StripeCustomerId = stripeCustomerId,
            Plan             = PlanTier.Free,
            Status           = SubscriptionStatus.Active,
            CreatedAt        = now,
            UpdatedAt        = now
        };
    }

    public void Activate(
        string stripeSubscriptionId,
        PlanTier plan,
        DateTime? periodStart,
        DateTime? periodEnd)
    {
        StripeSubscriptionId = stripeSubscriptionId;
        Plan                 = plan;
        Status               = SubscriptionStatus.Active;
        CurrentPeriodStart   = periodStart;
        CurrentPeriodEnd     = periodEnd;
        CancelAtPeriodEnd    = false;
        UpdatedAt            = DateTime.UtcNow;
    }

    public void Update(
        PlanTier plan,
        DateTime? periodStart,
        DateTime? periodEnd,
        bool cancelAtPeriodEnd)
    {
        Plan               = plan;
        Status             = SubscriptionStatus.Active;
        CurrentPeriodStart = periodStart;
        CurrentPeriodEnd   = periodEnd;
        CancelAtPeriodEnd  = cancelAtPeriodEnd;
        UpdatedAt          = DateTime.UtcNow;
    }

    public void MarkPastDue()
    {
        Status    = SubscriptionStatus.PastDue;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Cancel()
    {
        Plan      = PlanTier.Free;
        Status    = SubscriptionStatus.Canceled;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetCancelAtPeriodEnd(bool cancelAtPeriodEnd)
    {
        CancelAtPeriodEnd = cancelAtPeriodEnd;
        UpdatedAt         = DateTime.UtcNow;
    }
}
