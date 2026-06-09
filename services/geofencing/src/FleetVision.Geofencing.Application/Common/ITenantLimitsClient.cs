namespace FleetVision.Geofencing.Application.Common;

public interface ITenantLimitsClient
{
    Task<TenantLimitsResponse> GetLimitsAsync(Guid tenantId, CancellationToken ct = default);
}

public sealed record TenantLimitsResponse(
    Guid TenantId,
    string Plan,
    int MaxVehicles,
    int MaxUsers,
    int MaxGeofences,
    bool IsActive);

public sealed class TenantServiceUnavailableException : Exception
{
    public TenantServiceUnavailableException(string message) : base(message) { }
}
