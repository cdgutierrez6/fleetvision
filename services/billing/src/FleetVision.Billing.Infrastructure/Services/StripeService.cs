using FleetVision.Billing.Application.Common.Interfaces;
using FleetVision.Billing.Domain.Exceptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Stripe;
using Stripe.Checkout;
using PlanTier = FleetVision.Billing.Domain.Enums.PlanTier;

namespace FleetVision.Billing.Infrastructure.Services;

public sealed class StripeService : IStripeService
{
    private readonly IConfiguration _config;
    private readonly ILogger<StripeService> _logger;
    private readonly Dictionary<PlanTier, string> _priceIds;
    private readonly Dictionary<string, PlanTier> _priceToTier;

    public StripeService(IConfiguration config, ILogger<StripeService> logger)
    {
        _config = config;
        _logger = logger;

        StripeConfiguration.ApiKey = config["Stripe:SecretKey"]
            ?? throw new InvalidOperationException("Stripe:SecretKey is required.");

        _priceIds = new Dictionary<PlanTier, string>
        {
            [PlanTier.Starter]      = config["Stripe:PriceIds:Starter"]      ?? string.Empty,
            [PlanTier.Professional] = config["Stripe:PriceIds:Professional"]  ?? string.Empty,
            [PlanTier.Enterprise]   = config["Stripe:PriceIds:Enterprise"]    ?? string.Empty,
        };

        _priceToTier = _priceIds
            .Where(kv => !string.IsNullOrEmpty(kv.Value))
            .ToDictionary(kv => kv.Value, kv => kv.Key);
    }

    public async Task<string> GetOrCreateCustomerAsync(
        Guid tenantId, string email, CancellationToken ct)
    {
        var service = new CustomerService();

        var existing = await service.ListAsync(
            new CustomerListOptions { Email = email, Limit = 1 },
            cancellationToken: ct);

        if (existing.Data.Count > 0)
            return existing.Data[0].Id;

        var customer = await service.CreateAsync(
            new CustomerCreateOptions
            {
                Email    = email,
                Metadata = new Dictionary<string, string> { ["tenant_id"] = tenantId.ToString() }
            },
            cancellationToken: ct);

        _logger.LogInformation(
            "Created Stripe customer {CustomerId} for tenant {TenantId}", customer.Id, tenantId);

        return customer.Id;
    }

    public async Task<string> CreateCheckoutSessionAsync(
        Guid tenantId, string stripeCustomerId, PlanTier plan, CancellationToken ct)
    {
        if (!_priceIds.TryGetValue(plan, out var priceId) || string.IsNullOrEmpty(priceId))
            throw new InvalidPlanTierException(plan.ToString());

        var successUrl = _config["Stripe:SuccessUrl"]
            ?? throw new InvalidOperationException("Stripe:SuccessUrl is required.");
        var cancelUrl = _config["Stripe:CancelUrl"]
            ?? throw new InvalidOperationException("Stripe:CancelUrl is required.");

        var service = new SessionService();
        var session = await service.CreateAsync(
            new SessionCreateOptions
            {
                Customer           = stripeCustomerId,
                Mode               = "subscription",
                LineItems          =
                [
                    new SessionLineItemOptions
                    {
                        Price    = priceId,
                        Quantity = 1
                    }
                ],
                SuccessUrl         = successUrl + "?session_id={CHECKOUT_SESSION_ID}",
                CancelUrl          = cancelUrl,
                Metadata           = new Dictionary<string, string>
                {
                    ["tenant_id"]  = tenantId.ToString(),
                    ["plan_tier"]  = plan.ToString()
                }
            },
            cancellationToken: ct);

        return session.Url;
    }

    public async Task<string> CreateCustomerPortalSessionAsync(
        string stripeCustomerId, string returnUrl, CancellationToken ct)
    {
        var service = new Stripe.BillingPortal.SessionService();
        var session = await service.CreateAsync(
            new Stripe.BillingPortal.SessionCreateOptions
            {
                Customer  = stripeCustomerId,
                ReturnUrl = returnUrl
            },
            cancellationToken: ct);

        return session.Url;
    }

    public async Task SetCancelAtPeriodEndAsync(
        string stripeSubscriptionId, bool cancelAtPeriodEnd, CancellationToken ct)
    {
        var service = new SubscriptionService();
        await service.UpdateAsync(
            stripeSubscriptionId,
            new SubscriptionUpdateOptions { CancelAtPeriodEnd = cancelAtPeriodEnd },
            cancellationToken: ct);
    }

    public StripeWebhookEvent ParseWebhookEvent(
        string payload, string stripeSignature, string webhookSecret)
    {
        Stripe.Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(
                payload,
                stripeSignature,
                webhookSecret,
                throwOnApiVersionMismatch: false);
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Stripe webhook signature validation failed.");
            throw new WebhookSignatureException();
        }

        return stripeEvent.Type switch
        {
            "checkout.session.completed"      => ParseCheckoutSession(stripeEvent),
            "customer.subscription.updated"   => ParseSubscription(stripeEvent),
            "customer.subscription.deleted"   => ParseSubscriptionDeleted(stripeEvent),
            "invoice.payment_failed"          => ParseInvoice(stripeEvent),
            _                                 => new StripeWebhookEvent { EventType = stripeEvent.Type }
        };
    }

    // ─── Private parsers ─────────────────────────────────────────────────────

    private StripeWebhookEvent ParseCheckoutSession(Stripe.Event evt)
    {
        var session = evt.Data.Object as Session;
        if (session is null)
            return new StripeWebhookEvent { EventType = evt.Type };

        session.Metadata.TryGetValue("tenant_id", out var tenantIdStr);
        session.Metadata.TryGetValue("plan_tier", out var planTierStr);

        Guid? tenantId = Guid.TryParse(tenantIdStr, out var tid) ? tid : null;
        PlanTier? plan = Enum.TryParse<PlanTier>(planTierStr, out var pt) ? pt : null;

        return new StripeWebhookEvent
        {
            EventType            = evt.Type,
            TenantId             = tenantId,
            Plan                 = plan,
            StripeCustomerId     = session.CustomerId,
            StripeSubscriptionId = session.SubscriptionId,
        };
    }

    private StripeWebhookEvent ParseSubscription(Stripe.Event evt)
    {
        var sub = evt.Data.Object as Stripe.Subscription;
        if (sub is null)
            return new StripeWebhookEvent { EventType = evt.Type };

        var priceId = sub.Items?.Data?.FirstOrDefault()?.Price?.Id;
        PlanTier? plan = priceId is not null && _priceToTier.TryGetValue(priceId, out var p)
            ? p : null;

        return new StripeWebhookEvent
        {
            EventType            = evt.Type,
            Plan                 = plan,
            StripeCustomerId     = sub.CustomerId,
            StripeSubscriptionId = sub.Id,
            CurrentPeriodStart   = sub.CurrentPeriodStart,
            CurrentPeriodEnd     = sub.CurrentPeriodEnd,
            CancelAtPeriodEnd    = sub.CancelAtPeriodEnd,
        };
    }

    private StripeWebhookEvent ParseSubscriptionDeleted(Stripe.Event evt)
    {
        var sub = evt.Data.Object as Stripe.Subscription;
        return new StripeWebhookEvent
        {
            EventType            = evt.Type,
            StripeCustomerId     = sub?.CustomerId,
            StripeSubscriptionId = sub?.Id,
        };
    }

    private StripeWebhookEvent ParseInvoice(Stripe.Event evt)
    {
        var invoice = evt.Data.Object as Invoice;
        return new StripeWebhookEvent
        {
            EventType            = evt.Type,
            StripeSubscriptionId = invoice?.SubscriptionId,
        };
    }
}
