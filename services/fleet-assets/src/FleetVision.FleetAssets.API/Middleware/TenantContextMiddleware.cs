using FleetVision.FleetAssets.Infrastructure.Services;
using System.Security.Claims;

namespace FleetVision.FleetAssets.API.Middleware;

public sealed class TenantContextMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantContextMiddleware> _logger;

    public TenantContextMiddleware(RequestDelegate next, ILogger<TenantContextMiddleware> logger)
    {
        _next   = next;
        _logger = logger;
    }

    // Method injection: ITenantContext is scoped, middleware is singleton
    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        // Unauthenticated requests pass through — auth middleware handles them
        if (!(context.User.Identity?.IsAuthenticated ?? false))
        {
            await _next(context);
            return;
        }

        // Authenticated requests MUST have tenant_id in the JWT.
        // SuperAdmin tokens lack this claim and are handled by role-based policies.
        var jwtTenantId = context.User.FindFirstValue("tenant_id");
        if (!Guid.TryParse(jwtTenantId, out var jwtTenantGuid))
        {
            _logger.LogWarning("Authenticated request missing tenant_id claim. IP={IP}",
                context.Connection.RemoteIpAddress);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        // X-Tenant-Id header (set by YARP) is a cross-check only — JWT claim is authoritative.
        var header = context.Request.Headers["X-Tenant-Id"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(header))
        {
            if (!Guid.TryParse(header, out var headerTenantId) || headerTenantId != jwtTenantGuid)
            {
                _logger.LogWarning(
                    "TenantId mismatch: JWT={JwtTenantId} Header={HeaderTenantId} IP={IP}",
                    jwtTenantId, header, context.Connection.RemoteIpAddress);
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
        }

        tenantContext.SetTenantId(jwtTenantGuid);
        await _next(context);
    }
}
