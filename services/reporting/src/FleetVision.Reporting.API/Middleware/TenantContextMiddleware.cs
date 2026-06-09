using System.Security.Claims;

namespace FleetVision.Reporting.API.Middleware;

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
        if (!(context.User.Identity?.IsAuthenticated ?? false))
        {
            await _next(context);
            return;
        }

        // JWT claim is the sole authoritative source — X-Tenant-Id header is not trusted
        // as a fallback because direct callers bypassing the gateway could forge it.
        var jwtTenantId = context.User.FindFirstValue("tenant_id");
        if (!Guid.TryParse(jwtTenantId, out var tenantId))
        {
            _logger.LogWarning("Authenticated request missing tenant_id claim. IP={IP}",
                context.Connection.RemoteIpAddress);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        context.Items["TenantId"] = tenantId;
        await _next(context);
    }
}
