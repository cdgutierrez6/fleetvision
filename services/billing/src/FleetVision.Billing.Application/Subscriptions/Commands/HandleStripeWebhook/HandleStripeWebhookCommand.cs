using MediatR;

namespace FleetVision.Billing.Application.Subscriptions.Commands.HandleStripeWebhook;

public sealed record HandleStripeWebhookCommand(
    string RawPayload,
    string StripeSignature) : IRequest;
