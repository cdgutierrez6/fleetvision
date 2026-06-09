using MediatR;

namespace FleetVision.Billing.Application.Subscriptions.Queries.GetBillingPortalUrl;

public sealed record GetBillingPortalUrlQuery(Guid TenantId, string ReturnUrl) : IRequest<string>;
