using FleetVision.Billing.Application.Subscriptions.Commands.HandleStripeWebhook;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FleetVision.Billing.API.Controllers;

[ApiController]
[Route("billing")]
public sealed class WebhookController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<WebhookController> _logger;

    public WebhookController(IMediator mediator, ILogger<WebhookController> logger)
    {
        _mediator = mediator;
        _logger   = logger;
    }

    // ─── POST /billing/webhook ────────────────────────────────────────────────
    // AllowAnonymous: Stripe cannot send a JWT. Signature is validated inside the handler.
    // Raw body MUST be read before any JSON binding; EnableBuffering is set in Program.cs.
    [HttpPost("webhook")]
    [AllowAnonymous]
    [EnableRateLimiting("webhook")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> StripeWebhook(CancellationToken ct)
    {
        string rawPayload;
        using (var reader = new StreamReader(Request.Body))
            rawPayload = await reader.ReadToEndAsync(ct);

        var signature = Request.Headers["Stripe-Signature"].ToString();

        if (string.IsNullOrEmpty(signature))
        {
            _logger.LogWarning("Webhook received without Stripe-Signature header.");
            return BadRequest();
        }

        await _mediator.Send(new HandleStripeWebhookCommand(rawPayload, signature), ct);
        return Ok();
    }
}
