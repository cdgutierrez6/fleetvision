namespace FleetVision.Geofencing.Infrastructure.Services;

public interface ITenantContext
{
    Guid? TenantId { get; }
    void SetTenantId(Guid? tenantId);
}

public sealed class TenantContext : ITenantContext
{
    private Guid? _tenantId;
    public Guid? TenantId => _tenantId;
    public void SetTenantId(Guid? tenantId) => _tenantId = tenantId;
}
