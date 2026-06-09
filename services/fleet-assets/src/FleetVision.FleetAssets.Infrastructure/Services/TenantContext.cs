namespace FleetVision.FleetAssets.Infrastructure.Services;

public interface ITenantContext
{
    Guid? TenantId { get; }
    void SetTenantId(Guid? tenantId);
}

public sealed class TenantContext : ITenantContext
{
    public Guid? TenantId { get; private set; }

    public void SetTenantId(Guid? tenantId) => TenantId = tenantId;
}
