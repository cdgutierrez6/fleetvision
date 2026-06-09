using FleetVision.Reporting.Application.Common.Dtos;
using FleetVision.Reporting.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace FleetVision.Reporting.Infrastructure.Violations;

public sealed class ViolationsReader : IViolationsReader
{
    private readonly NpgsqlDataSource _db;

    public ViolationsReader([FromKeyedServices("geofencing")] NpgsqlDataSource db)
        => _db = db;

    public async Task<ViolationsSummaryDto> GetSummaryAsync(
        Guid tenantId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        var byType      = await GetByTypeAsync(tenantId, from, to, ct);
        var topVehicles = await GetTopVehiclesAsync(tenantId, from, to, ct);

        var total = byType.Sum(x => x.Count);

        return new ViolationsSummaryDto(total, byType, topVehicles);
    }

    private async Task<IReadOnlyList<ViolationByTypeDto>> GetByTypeAsync(
        Guid tenantId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        const string sql = """
            SELECT violation_type, COUNT(*)::int AS cnt
            FROM violation_events
            WHERE tenant_id  = $1
              AND occurred_at >= $2
              AND occurred_at < $3
            GROUP BY violation_type
            ORDER BY cnt DESC;
            """;

        await using var cmd = _db.CreateCommand(sql);
        cmd.Parameters.AddWithValue(tenantId);
        cmd.Parameters.AddWithValue(from.UtcDateTime);
        cmd.Parameters.AddWithValue(to.UtcDateTime);

        var rows = new List<(string Type, int Count)>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            rows.Add((reader.GetString(0), reader.GetInt32(1)));

        var total = rows.Sum(r => r.Count);
        return rows.Select(r => new ViolationByTypeDto(
            r.Type,
            r.Count,
            total == 0 ? 0 : Math.Round((double)r.Count / total * 100, 1)
        )).ToList();
    }

    private async Task<IReadOnlyList<ViolationByVehicleDto>> GetTopVehiclesAsync(
        Guid tenantId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        const string sql = """
            SELECT vehicle_id, COUNT(*)::int AS cnt
            FROM violation_events
            WHERE tenant_id  = $1
              AND occurred_at >= $2
              AND occurred_at < $3
            GROUP BY vehicle_id
            ORDER BY cnt DESC
            LIMIT 10;
            """;

        await using var cmd = _db.CreateCommand(sql);
        cmd.Parameters.AddWithValue(tenantId);
        cmd.Parameters.AddWithValue(from.UtcDateTime);
        cmd.Parameters.AddWithValue(to.UtcDateTime);

        var result = new List<ViolationByVehicleDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result.Add(new ViolationByVehicleDto(
                reader.GetGuid(0),
                reader.GetInt32(1)));

        return result;
    }
}
