using FleetVision.Geofencing.Infrastructure.Services;
using System.Security.Claims;

namespace FleetVision.Geofencing.API.Middleware;

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
        if (!(context.User.Identity?.IsAuthenticated ?? false))
        {
            await _next(context);
            return;
        }

        var jwtTenantId = context.User.FindFirstValue("tenant_id");
        if (!Guid.TryParse(jwtTenantId, out var jwtTenantGuid))
        {
            _logger.LogWarning("Authenticated request missing tenant_id claim. IP={IP}",
                context.Connection.RemoteIpAddress);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

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
