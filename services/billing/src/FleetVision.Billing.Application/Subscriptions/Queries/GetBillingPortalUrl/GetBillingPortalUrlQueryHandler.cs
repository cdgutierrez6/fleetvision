using FleetVision.Billing.Application.Common.Interfaces;
using FleetVision.Billing.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FleetVision.Billing.Application.Subscriptions.Queries.GetBillingPortalUrl;

public sealed class GetBillingPortalUrlQueryHandler
    : IRequestHandler<GetBillingPortalUrlQuery, string>
{
    private readonly IBillingDbContext _db;
    private readonly IStripeService _stripe;

    public GetBillingPortalUrlQueryHandler(IBillingDbContext db, IStripeService stripe)
    {
        _db     = db;
        _stripe = stripe;
    }

    public async Task<string> Handle(
        GetBillingPortalUrlQuery request,
        CancellationToken cancellationToken)
    {
        var sub = await _db.Subscriptions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == request.TenantId, cancellationToken)
            ?? throw new SubscriptionNotFoundException(request.TenantId);

        if (string.IsNullOrEmpty(sub.StripeSubscriptionId))
            throw new NoActiveStripeSubscriptionException(request.TenantId);

        return await _stripe.CreateCustomerPortalSessionAsync(
            sub.StripeCustomerId,
            request.ReturnUrl,
            cancellationToken);
    }
}
