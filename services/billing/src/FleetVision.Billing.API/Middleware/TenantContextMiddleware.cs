using System.Security.Claims;

namespace FleetVision.Billing.API.Middleware;

/// <summary>
/// Guards billing endpoints: authenticated non-SuperAdmin requests without a tenant_id JWT claim
/// are rejected with 403. Controllers extract tenant_id directly from the JWT claim.
/// </summary>
public sealed class TenantContextMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantContextMiddleware> _logger;

    public TenantContextMiddleware(RequestDelegate next, ILogger<TenantContextMiddleware> logger)
    {
        _next   = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Webhook endpoint is AllowAnonymous — Stripe sends no JWT
        if (!(context.User.Identity?.IsAuthenticated ?? false))
        {
            await _next(context);
            return;
        }

        var jwtTenantId = context.User.FindFirstValue("tenant_id");
        if (!Guid.TryParse(jwtTenantId, out _))
        {
            _logger.LogWarning("Authenticated billing request missing tenant_id claim. IP={IP}",
                context.Connection.RemoteIpAddress);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        await _next(context);
    }
}
