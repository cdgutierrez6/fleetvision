namespace FleetVision.Billing.Domain.Exceptions;

public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
}

public sealed class SubscriptionNotFoundException : DomainException
{
    public SubscriptionNotFoundException(Guid tenantId)
        : base($"No subscription found for tenant {tenantId}.") { }

    public SubscriptionNotFoundException(string stripeSubscriptionId)
        : base($"No subscription found for Stripe subscription {stripeSubscriptionId}.") { }
}

public sealed class NoActiveStripeSubscriptionException : DomainException
{
    public NoActiveStripeSubscriptionException(Guid tenantId)
        : base($"Tenant {tenantId} has no active Stripe subscription.") { }
}

public sealed class SubscriptionAlreadyCanceledException : DomainException
{
    public SubscriptionAlreadyCanceledException(Guid tenantId)
        : base($"Subscription for tenant {tenantId} is already set to cancel.") { }
}

public sealed class WebhookSignatureException : DomainException
{
    public WebhookSignatureException()
        : base("Stripe webhook signature validation failed.") { }
}

public sealed class InvalidPlanTierException : DomainException
{
    public InvalidPlanTierException(string value)
        : base($"'{value}' is not a valid plan tier.") { }
}
