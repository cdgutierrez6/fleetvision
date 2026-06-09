using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace FleetVision.FleetAssets.Infrastructure.Services;

public sealed class TenantRlsInterceptor : DbConnectionInterceptor
{
    private readonly ITenantContext _tenantContext;

    public TenantRlsInterceptor(ITenantContext tenantContext) => _tenantContext = tenantContext;

    public override async Task ConnectionOpenedAsync(
        DbConnection connection, ConnectionEndEventData eventData, CancellationToken ct = default)
    {
        await SetTenantConfig(connection, ct);
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        SetTenantConfig(connection, CancellationToken.None).GetAwaiter().GetResult();
    }

    private async Task SetTenantConfig(DbConnection connection, CancellationToken ct)
    {
        await using var cmd = connection.CreateCommand();
        // Parameterized — never string-interpolated (SQL injection prevention)
        cmd.CommandText = "SELECT set_config('app.tenant_id', @tenantId, true)";
        var param = cmd.CreateParameter();
        param.ParameterName = "@tenantId";
        param.Value = _tenantContext.TenantId?.ToString() ?? string.Empty;
        cmd.Parameters.Add(param);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
