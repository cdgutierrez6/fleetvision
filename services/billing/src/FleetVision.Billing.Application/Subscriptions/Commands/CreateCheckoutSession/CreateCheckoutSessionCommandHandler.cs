using FleetVision.Billing.Application.Common.Interfaces;
using FleetVision.Billing.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FleetVision.Billing.Application.Subscriptions.Commands.CreateCheckoutSession;

public sealed class CreateCheckoutSessionCommandHandler
    : IRequestHandler<CreateCheckoutSessionCommand, string>
{
    private readonly IBillingDbContext _db;
    private readonly IStripeService _stripe;
    private readonly ILogger<CreateCheckoutSessionCommandHandler> _logger;

    public CreateCheckoutSessionCommandHandler(
        IBillingDbContext db,
        IStripeService stripe,
        ILogger<CreateCheckoutSessionCommandHandler> logger)
    {
        _db     = db;
        _stripe = stripe;
        _logger = logger;
    }

    public async Task<string> Handle(
        CreateCheckoutSessionCommand request,
        CancellationToken cancellationToken)
    {
        var existing = await _db.Subscriptions
            .FirstOrDefaultAsync(s => s.TenantId == request.TenantId, cancellationToken);

        string stripeCustomerId;

        if (existing is not null)
        {
            stripeCustomerId = existing.StripeCustomerId;
        }
        else
        {
            stripeCustomerId = await _stripe.GetOrCreateCustomerAsync(
                request.TenantId, request.BillingEmail, cancellationToken);

            _db.Subscriptions.Add(
                Subscription.Create(request.TenantId, stripeCustomerId));

            await _db.SaveChangesAsync(cancellationToken);
        }

        var sessionUrl = await _stripe.CreateCheckoutSessionAsync(
            request.TenantId,
            stripeCustomerId,
            request.Plan,
            cancellationToken);

        _logger.LogInformation(
            "Created Stripe checkout session for tenant {TenantId} plan {Plan}",
            request.TenantId, request.Plan);

        return sessionUrl;
    }
}
