using FleetVision.Reporting.Application.Common.Dtos;
using FleetVision.Reporting.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace FleetVision.Reporting.Infrastructure.Fleet;

// Reads directly from fleet_db with a read-only connection (SELECT only).
// Cross-service DB read is acceptable for the reporting service — it is a
// dedicated read-aggregation concern and has no write surface.
public sealed class FleetStatusReader : IFleetStatusReader
{
    private readonly NpgsqlDataSource _db;

    public FleetStatusReader([FromKeyedServices("fleet")] NpgsqlDataSource db)
        => _db = db;

    public async Task<FleetStatusDto> GetFleetStatusAsync(Guid tenantId, CancellationToken ct)
    {
        const string sql = """
            SELECT
                COUNT(*)::int                                                             AS total,
                COUNT(*) FILTER (WHERE status = 'Active')::int                           AS active,
                COUNT(*) FILTER (WHERE status = 'Maintenance')::int                      AS maintenance,
                COUNT(*) FILTER (WHERE status = 'Inactive' OR status = 'Retired')::int   AS inactive
            FROM vehicles
            WHERE tenant_id = $1;
            """;

        await using var cmd = _db.CreateCommand(sql);
        cmd.Parameters.AddWithValue(tenantId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);

        return new FleetStatusDto(
            Total:       reader.GetInt32(0),
            Active:      reader.GetInt32(1),
            Maintenance: reader.GetInt32(2),
            Inactive:    reader.GetInt32(3));
    }
}
