using FleetVision.Billing.Application.Common.Interfaces;
using FleetVision.Billing.Application.DTOs;
using FleetVision.Billing.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FleetVision.Billing.Application.Subscriptions.Queries.GetSubscription;

public sealed class GetSubscriptionQueryHandler
    : IRequestHandler<GetSubscriptionQuery, SubscriptionDto>
{
    private readonly IBillingDbContext _db;

    public GetSubscriptionQueryHandler(IBillingDbContext db) => _db = db;

    public async Task<SubscriptionDto> Handle(
        GetSubscriptionQuery request,
        CancellationToken cancellationToken)
    {
        var sub = await _db.Subscriptions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == request.TenantId, cancellationToken);

        // Tenants with no subscription record are implicitly on Free/Active
        if (sub is null)
        {
            return new SubscriptionDto(
                TenantId:            request.TenantId,
                Plan:                PlanTier.Free.ToString(),
                Status:              SubscriptionStatus.Active.ToString(),
                CurrentPeriodEnd:    null,
                CancelAtPeriodEnd:   false,
                StripeSubscriptionId: null);
        }

        return new SubscriptionDto(
            TenantId:            sub.TenantId,
            Plan:                sub.Plan.ToString(),
            Status:              sub.Status.ToString(),
            CurrentPeriodEnd:    sub.CurrentPeriodEnd,
            CancelAtPeriodEnd:   sub.CancelAtPeriodEnd,
            StripeSubscriptionId: sub.StripeSubscriptionId);
    }
}
