using FleetVision.PredictiveMaintenance.Infrastructure.Services;
using System.Security.Claims;

namespace FleetVision.PredictiveMaintenance.API.Middleware;

public sealed class TenantContextMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantContextMiddleware> _logger;

    public TenantContextMiddleware(RequestDelegate next, ILogger<TenantContextMiddleware> logger)
    {
        _next   = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        // Unauthenticated requests pass through — auth middleware handles them
        if (!(context.User.Identity?.IsAuthenticated ?? false))
        {
            await _next(context);
            return;
        }

        // Authenticated requests MUST have a valid tenant_id in the JWT
        var jwtTenantId = context.User.FindFirstValue("tenant_id");
        if (!Guid.TryParse(jwtTenantId, out var jwtTenantGuid))
        {
            _logger.LogWarning("Authenticated request missing tenant_id claim. IP={IP}",
                context.Connection.RemoteIpAddress);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        // If X-Tenant-Id header is present, it MUST match the JWT claim
        var header = context.Request.Headers["X-Tenant-Id"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(header))
        {
            if (!Guid.TryParse(header, out var headerTenantId) || headerTenantId != jwtTenantGuid)
            {
                _logger.LogWarning(
                    "TenantId mismatch or invalid header. JWT={Jwt} Header={Header} IP={IP}",
                    jwtTenantId, header, context.Connection.RemoteIpAddress);
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
        }

        // tenant_id always comes from JWT — the header is only a cross-check
        tenantContext.SetTenantId(jwtTenantGuid);
        await _next(context);
    }
}
