using FleetVision.Billing.Application.DTOs;
using MediatR;

namespace FleetVision.Billing.Application.Subscriptions.Queries.GetSubscription;

public sealed record GetSubscriptionQuery(Guid TenantId) : IRequest<SubscriptionDto>;
