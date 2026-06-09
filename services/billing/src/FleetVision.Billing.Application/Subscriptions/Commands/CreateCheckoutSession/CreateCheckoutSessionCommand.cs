using FleetVision.Billing.Domain.Enums;
using MediatR;

namespace FleetVision.Billing.Application.Subscriptions.Commands.CreateCheckoutSession;

public sealed record CreateCheckoutSessionCommand(
    Guid TenantId,
    string BillingEmail,
    PlanTier Plan) : IRequest<string>;
