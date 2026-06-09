using FleetVision.Billing.Application.Common.Interfaces;
using FleetVision.Billing.Domain.Enums;
using FleetVision.Billing.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FleetVision.Billing.Application.Subscriptions.Commands.HandleStripeWebhook;

public sealed class HandleStripeWebhookCommandHandler : IRequestHandler<HandleStripeWebhookCommand>
{
    private readonly IBillingDbContext _db;
    private readonly IStripeService _stripe;
    private readonly ITenantManagementClient _tenantClient;
    private readonly IBillingEventPublisher _publisher;
    private readonly IConfiguration _config;
    private readonly ILogger<HandleStripeWebhookCommandHandler> _logger;

    public HandleStripeWebhookCommandHandler(
        IBillingDbContext db,
        IStripeService stripe,
        ITenantManagementClient tenantClient,
        IBillingEventPublisher publisher,
        IConfiguration config,
        ILogger<HandleStripeWebhookCommandHandler> logger)
    {
        _db           = db;
        _stripe       = stripe;
        _tenantClient = tenantClient;
        _publisher    = publisher;
        _config       = config;
        _logger       = logger;
    }

    public async Task Handle(HandleStripeWebhookCommand request, CancellationToken cancellationToken)
    {
        var webhookSecret = _config["Stripe:WebhookSecret"]
            ?? throw new InvalidOperationException("Stripe:WebhookSecret is required.");

        var evt = _stripe.ParseWebhookEvent(
            request.RawPayload,
            request.StripeSignature,
            webhookSecret);

        _logger.LogInformation("Processing Stripe webhook event {EventType}", evt.EventType);

        switch (evt.EventType)
        {
            case "checkout.session.completed":
                await HandleCheckoutSessionCompletedAsync(evt, cancellationToken);
                break;

            case "customer.subscription.updated":
                await HandleSubscriptionUpdatedAsync(evt, cancellationToken);
                break;

            case "customer.subscription.deleted":
                await HandleSubscriptionDeletedAsync(evt, cancellationToken);
                break;

            case "invoice.payment_failed":
                await HandleInvoicePaymentFailedAsync(evt, cancellationToken);
                break;

            default:
                _logger.LogDebug("Unhandled Stripe event type {EventType} — ACK only.", evt.EventType);
                break;
        }
    }

    private async Task HandleCheckoutSessionCompletedAsync(
        StripeWebhookEvent evt, CancellationToken ct)
    {
        if (!evt.TenantId.HasValue || evt.Plan is null ||
            string.IsNullOrEmpty(evt.StripeCustomerId) ||
            string.IsNullOrEmpty(evt.StripeSubscriptionId))
        {
            _logger.LogWarning(
                "checkout.session.completed missing required fields. Skipping. TenantId={TenantId}",
                evt.TenantId);
            return;
        }

        var sub = await _db.Subscriptions
            .FirstOrDefaultAsync(s => s.TenantId == evt.TenantId.Value, ct);

        // Idempotency guard: Stripe delivers at-least-once — skip if already activated.
        if (sub is not null &&
            sub.StripeSubscriptionId == evt.StripeSubscriptionId &&
            sub.Status == SubscriptionStatus.Active)
        {
            _logger.LogInformation(
                "Idempotent webhook: subscription {SubId} already active for tenant {TenantId}, skipping",
                evt.StripeSubscriptionId, evt.TenantId.Value);
            return;
        }

        bool isFirstActivation = sub is null || sub.StripeSubscriptionId is null;
        var  oldPlan           = sub?.Plan ?? PlanTier.Free;

        if (sub is null)
        {
            sub = Domain.Entities.Subscription.Create(evt.TenantId.Value, evt.StripeCustomerId);
            _db.Subscriptions.Add(sub);
        }

        sub.Activate(
            evt.StripeSubscriptionId,
            evt.Plan.Value,
            evt.CurrentPeriodStart,
            evt.CurrentPeriodEnd);

        _db.AddPlanChangeAudit(
            evt.TenantId.Value,
            oldPlan.ToString(),
            evt.Plan.Value.ToString(),
            source: "stripe_webhook:checkout.session.completed",
            stripeEventId: evt.StripeSubscriptionId);

        if (isFirstActivation)
            _publisher.EnqueueTenantProvisioned(
                evt.TenantId.Value, evt.Plan.Value, billingEmail: evt.StripeCustomerId);
        else
            _publisher.EnqueueSubscriptionChanged(
                evt.TenantId.Value, oldPlan, evt.Plan.Value, SubscriptionStatus.Active);

        await _tenantClient.UpdateTenantPlanAsync(evt.TenantId.Value, evt.Plan.Value, ct);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
            when (ex.InnerException?.Message.Contains("unique") == true ||
                  ex.InnerException?.Message.Contains("duplicate") == true)
        {
            // Two concurrent webhook deliveries both found sub==null and tried to insert.
            // The unique constraint on tenant_id means exactly one succeeds; the other
            // gets here. Stripe will retry — the idempotency guard will handle it then.
            _logger.LogWarning(
                "Concurrent checkout webhook for tenant {TenantId} — duplicate insert suppressed.",
                evt.TenantId.Value);
            return;
        }

        _logger.LogInformation(
            "Activated subscription for tenant {TenantId} on plan {Plan}",
            evt.TenantId.Value, evt.Plan.Value);
    }

    private async Task HandleSubscriptionUpdatedAsync(
        StripeWebhookEvent evt, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(evt.StripeSubscriptionId))
            return;

        var sub = await _db.Subscriptions
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == evt.StripeSubscriptionId, ct)
            ?? throw new SubscriptionNotFoundException(evt.StripeSubscriptionId);

        var oldPlan = sub.Plan;
        var newPlan = evt.Plan ?? sub.Plan;

        sub.Update(newPlan, evt.CurrentPeriodStart, evt.CurrentPeriodEnd, evt.CancelAtPeriodEnd);

        if (oldPlan != newPlan)
        {
            _db.AddPlanChangeAudit(
                sub.TenantId,
                oldPlan.ToString(),
                newPlan.ToString(),
                source: "stripe_webhook:customer.subscription.updated",
                stripeEventId: evt.StripeSubscriptionId);

            _publisher.EnqueueSubscriptionChanged(
                sub.TenantId, oldPlan, newPlan, SubscriptionStatus.Active);

            await _tenantClient.UpdateTenantPlanAsync(sub.TenantId, newPlan, ct);
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Updated subscription for tenant {TenantId}: plan {OldPlan} → {NewPlan}",
            sub.TenantId, oldPlan, newPlan);
    }

    private async Task HandleSubscriptionDeletedAsync(
        StripeWebhookEvent evt, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(evt.StripeSubscriptionId))
            return;

        var sub = await _db.Subscriptions
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == evt.StripeSubscriptionId, ct)
            ?? throw new SubscriptionNotFoundException(evt.StripeSubscriptionId);

        var oldPlan = sub.Plan;

        sub.Cancel();

        _db.AddPlanChangeAudit(
            sub.TenantId,
            oldPlan.ToString(),
            PlanTier.Free.ToString(),
            source: "stripe_webhook:customer.subscription.deleted",
            stripeEventId: evt.StripeSubscriptionId);

        _publisher.EnqueueSubscriptionChanged(
            sub.TenantId, oldPlan, PlanTier.Free, SubscriptionStatus.Canceled);

        await _tenantClient.UpdateTenantPlanAsync(sub.TenantId, PlanTier.Free, ct);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Canceled subscription for tenant {TenantId}, reverted to Free plan.",
            sub.TenantId);
    }

    private async Task HandleInvoicePaymentFailedAsync(
        StripeWebhookEvent evt, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(evt.StripeSubscriptionId))
            return;

        var sub = await _db.Subscriptions
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == evt.StripeSubscriptionId, ct);

        if (sub is null)
        {
            _logger.LogWarning(
                "invoice.payment_failed for unknown subscription {SubId}",
                evt.StripeSubscriptionId);
            return;
        }

        sub.MarkPastDue();
        await _db.SaveChangesAsync(ct);

        _logger.LogWarning(
            "Payment failed for tenant {TenantId} subscription {SubId}.",
            sub.TenantId, evt.StripeSubscriptionId);
    }
}
