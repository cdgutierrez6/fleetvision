using FleetVision.Billing.Application.DTOs;
using FleetVision.Billing.Application.Subscriptions.Commands.CancelSubscription;
using FleetVision.Billing.Application.Subscriptions.Commands.CreateCheckoutSession;
using FleetVision.Billing.Application.Subscriptions.Queries.GetBillingPortalUrl;
using FleetVision.Billing.Application.Subscriptions.Queries.GetSubscription;
using FleetVision.Billing.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FleetVision.Billing.API.Controllers;

[ApiController]
[Route("billing")]
[Authorize]
public sealed class BillingController : ControllerBase
{
    private readonly IMediator _mediator;

    public BillingController(IMediator mediator) => _mediator = mediator;

    // ─── GET /billing/subscription ────────────────────────────────────────────
    [HttpGet("subscription")]
    [ProducesResponseType(typeof(SubscriptionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetSubscription(CancellationToken ct)
    {
        var tenantId = GetTenantId();
        var result   = await _mediator.Send(new GetSubscriptionQuery(tenantId), ct);
        return Ok(result);
    }

    // ─── POST /billing/checkout ───────────────────────────────────────────────
    [HttpPost("checkout")]
    [ProducesResponseType(typeof(CheckoutResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateCheckout(
        [FromBody] CreateCheckoutRequest request,
        CancellationToken ct)
    {
        var tenantId     = GetTenantId();
        var billingEmail = GetBillingEmail();

        var sessionUrl = await _mediator.Send(
            new CreateCheckoutSessionCommand(tenantId, billingEmail, request.Plan), ct);

        return Ok(new CheckoutResponse(sessionUrl));
    }

    // ─── POST /billing/portal ─────────────────────────────────────────────────
    [HttpPost("portal")]
    [ProducesResponseType(typeof(PortalResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetPortal(
        [FromBody] PortalRequest request,
        CancellationToken ct)
    {
        var tenantId  = GetTenantId();
        var portalUrl = await _mediator.Send(
            new GetBillingPortalUrlQuery(tenantId, request.ReturnUrl), ct);

        return Ok(new PortalResponse(portalUrl));
    }

    // ─── DELETE /billing/subscription ─────────────────────────────────────────
    [HttpDelete("subscription")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelSubscription(CancellationToken ct)
    {
        var tenantId = GetTenantId();
        await _mediator.Send(new CancelSubscriptionCommand(tenantId), ct);
        return NoContent();
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private Guid GetTenantId()
    {
        var claim = User.FindFirstValue("tenant_id")
            ?? User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");

        return Guid.TryParse(claim, out var id)
            ? id
            : throw new UnauthorizedAccessException("tenant_id claim is missing or invalid.");
    }

    private string GetBillingEmail()
        => User.FindFirstValue("email")
           ?? User.FindFirstValue(ClaimTypes.Email)
           ?? throw new UnauthorizedAccessException("Email claim is missing.");
}

// ─── Request / Response bodies ────────────────────────────────────────────────

public sealed record CreateCheckoutRequest(PlanTier Plan);
public sealed record CheckoutResponse(string SessionUrl);
public sealed record PortalRequest(string ReturnUrl);
public sealed record PortalResponse(string PortalUrl);
