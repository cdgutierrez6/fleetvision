using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace FleetVision.TenantManagement.Infrastructure.Services;

/// <summary>
/// Sets app.tenant_id on every new PostgreSQL connection so RLS policies apply.
/// Uses set_config (parameterized) — injection-safe even though tenantId is a GUID.
/// Connection pooling is safe because Npgsql resets session variables on connection return.
/// </summary>
public sealed class TenantRlsInterceptor : DbConnectionInterceptor
{
    private readonly ITenantContext _tenantContext;

    public TenantRlsInterceptor(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        SetTenantId(connection);
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await SetTenantIdAsync(connection, cancellationToken);
    }

    private void SetTenantId(DbConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT set_config('app.tenant_id', @tenantId, true)";
        var param = cmd.CreateParameter();
        param.ParameterName = "@tenantId";
        param.Value         = _tenantContext.TenantId?.ToString() ?? string.Empty;
        cmd.Parameters.Add(param);
        cmd.ExecuteNonQuery();
    }

    private async Task SetTenantIdAsync(DbConnection connection, CancellationToken ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT set_config('app.tenant_id', @tenantId, true)";
        var param = cmd.CreateParameter();
        param.ParameterName = "@tenantId";
        param.Value         = _tenantContext.TenantId?.ToString() ?? string.Empty;
        cmd.Parameters.Add(param);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
