namespace FleetVision.Gateway.Middleware;

/// <summary>
/// Reads the validated tenant_id JWT claim and forwards it as X-Tenant-Id to downstream services.
/// SuperAdmin tokens (tenant_id = null) do not get the header — downstream services handle the
/// unrestricted access case when the header is absent.
/// Must run AFTER UseAuthentication() so HttpContext.User is populated.
/// </summary>
public sealed class TenantPropagationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantPropagationMiddleware> _logger;

    public const string TenantIdClaimType = "tenant_id";
    public const string TenantIdHeaderName = "X-Tenant-Id";

    public TenantPropagationMiddleware(RequestDelegate next, ILogger<TenantPropagationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var tenantId = context.User.FindFirst(TenantIdClaimType)?.Value;

            if (!string.IsNullOrWhiteSpace(tenantId))
            {
                context.Request.Headers[TenantIdHeaderName] = tenantId;
                _logger.LogDebug("Propagating tenant {TenantId} via {Header}", tenantId, TenantIdHeaderName);
            }
        }

        await _next(context);
    }
}
