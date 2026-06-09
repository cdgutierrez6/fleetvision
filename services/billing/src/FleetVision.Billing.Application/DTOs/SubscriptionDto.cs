namespace FleetVision.Billing.Application.DTOs;

public sealed record SubscriptionDto(
    Guid TenantId,
    string Plan,
    string Status,
    DateTime? CurrentPeriodEnd,
    bool CancelAtPeriodEnd,
    string? StripeSubscriptionId);
