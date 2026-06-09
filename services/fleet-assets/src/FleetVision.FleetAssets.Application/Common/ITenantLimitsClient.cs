namespace FleetVision.FleetAssets.Application.Common;

public interface ITenantLimitsClient
{
    Task<TenantLimitsResponse> GetLimitsAsync(Guid tenantId, CancellationToken ct = default);
}

public sealed record TenantLimitsResponse(
    Guid TenantId,
    string Plan,
    int MaxVehicles,
    int MaxUsers,
    bool IsActive);

public sealed class TenantServiceUnavailableException : Exception
{
    public TenantServiceUnavailableException(string message, Exception? inner = null)
        : base(message, inner) { }
}
