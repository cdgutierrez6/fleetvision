using FleetVision.Billing.Domain.Enums;

namespace FleetVision.Billing.Application.Common.Interfaces;

public interface IBillingEventPublisher
{
    void EnqueueTenantProvisioned(Guid tenantId, PlanTier plan, string billingEmail);

    void EnqueueSubscriptionChanged(
        Guid tenantId,
        PlanTier oldPlan,
        PlanTier newPlan,
        SubscriptionStatus status);
}
