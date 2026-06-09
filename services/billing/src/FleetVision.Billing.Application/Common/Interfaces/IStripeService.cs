using FleetVision.Billing.Domain.Enums;

namespace FleetVision.Billing.Application.Common.Interfaces;

public interface IStripeService
{
    Task<string> GetOrCreateCustomerAsync(Guid tenantId, string email, CancellationToken ct);

    Task<string> CreateCheckoutSessionAsync(
        Guid tenantId,
        string stripeCustomerId,
        PlanTier plan,
        CancellationToken ct);

    Task<string> CreateCustomerPortalSessionAsync(
        string stripeCustomerId,
        string returnUrl,
        CancellationToken ct);

    Task SetCancelAtPeriodEndAsync(
        string stripeSubscriptionId,
        bool cancelAtPeriodEnd,
        CancellationToken ct);

    // Validates the Stripe-Signature header and parses event data.
    // Throws WebhookSignatureException on invalid signature.
    StripeWebhookEvent ParseWebhookEvent(
        string payload,
        string stripeSignature,
        string webhookSecret);
}

public sealed record StripeWebhookEvent
{
    public required string EventType { get; init; }

    // Populated for checkout.session.completed (from session metadata)
    public Guid? TenantId { get; init; }
    public PlanTier? Plan { get; init; }

    // Populated for all subscription-related events
    public string? StripeCustomerId { get; init; }
    public string? StripeSubscriptionId { get; init; }
    public DateTime? CurrentPeriodStart { get; init; }
    public DateTime? CurrentPeriodEnd { get; init; }
    public bool CancelAtPeriodEnd { get; init; }
}
