using FleetVision.TenantManagement.Infrastructure.Services;
using System.Security.Claims;

namespace FleetVision.TenantManagement.API.Middleware;

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
        // Unauthenticated requests (AllowAnonymous endpoints such as /internal/*) pass through.
        if (!(context.User.Identity?.IsAuthenticated ?? false))
        {
            await _next(context);
            return;
        }

        // SuperAdmin tokens have no tenant_id claim — they operate across all tenants
        // and are restricted by [Authorize(Roles="SuperAdmin")] on sensitive endpoints.
        var jwtTenantId = context.User.FindFirstValue("tenant_id");
        if (!Guid.TryParse(jwtTenantId, out var jwtTenantGuid))
        {
            // Allow SuperAdmin through; non-SuperAdmin without tenant_id is denied.
            var isSuperAdmin = context.User.IsInRole("SuperAdmin");
            if (!isSuperAdmin)
            {
                _logger.LogWarning("Authenticated non-SuperAdmin request missing tenant_id claim. IP={IP}",
                    context.Connection.RemoteIpAddress);
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            await _next(context);
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
        _logger.LogDebug("Tenant context set: {TenantId}", jwtTenantGuid);
        await _next(context);
    }
}
