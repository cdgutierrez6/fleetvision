using MediatR;

namespace FleetVision.Billing.Application.Subscriptions.Commands.CancelSubscription;

public sealed record CancelSubscriptionCommand(Guid TenantId) : IRequest;
