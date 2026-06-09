using FleetVision.Billing.Application.Common.Interfaces;
using FleetVision.Billing.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FleetVision.Billing.Application.Subscriptions.Commands.CancelSubscription;

public sealed class CancelSubscriptionCommandHandler : IRequestHandler<CancelSubscriptionCommand>
{
    private readonly IBillingDbContext _db;
    private readonly IStripeService _stripe;
    private readonly ILogger<CancelSubscriptionCommandHandler> _logger;

    public CancelSubscriptionCommandHandler(
        IBillingDbContext db,
        IStripeService stripe,
        ILogger<CancelSubscriptionCommandHandler> logger)
    {
        _db     = db;
        _stripe = stripe;
        _logger = logger;
    }

    public async Task Handle(CancelSubscriptionCommand request, CancellationToken cancellationToken)
    {
        var sub = await _db.Subscriptions
            .FirstOrDefaultAsync(s => s.TenantId == request.TenantId, cancellationToken)
            ?? throw new SubscriptionNotFoundException(request.TenantId);

        if (string.IsNullOrEmpty(sub.StripeSubscriptionId))
            throw new NoActiveStripeSubscriptionException(request.TenantId);

        if (sub.CancelAtPeriodEnd)
            throw new SubscriptionAlreadyCanceledException(request.TenantId);

        await _stripe.SetCancelAtPeriodEndAsync(sub.StripeSubscriptionId, true, cancellationToken);

        sub.SetCancelAtPeriodEnd(true);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Subscription for tenant {TenantId} set to cancel at period end.", request.TenantId);
    }
}
