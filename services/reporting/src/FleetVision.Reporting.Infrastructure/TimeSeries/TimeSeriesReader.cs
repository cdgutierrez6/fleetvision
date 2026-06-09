using FleetVision.Reporting.Application.Common.Dtos;
using FleetVision.Reporting.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace FleetVision.Reporting.Infrastructure.TimeSeries;

public sealed class TimeSeriesReader : ITimeSeriesReader
{
    private readonly NpgsqlDataSource _db;

    public TimeSeriesReader([FromKeyedServices("telemetry")] NpgsqlDataSource db)
        => _db = db;

    public async Task<FleetKpisDto> GetFleetKpisAsync(
        Guid tenantId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        // Reads from daily_fleet_kpis continuous aggregate (TimescaleDB).
        // Aggregates across all vehicles for the tenant in the period.
        const string sql = """
            SELECT
                COUNT(DISTINCT vehicle_id)::int          AS active_vehicles,
                COALESCE(SUM(distance_km), 0)            AS total_distance_km,
                COALESCE(AVG(avg_speed_kmh), 0)          AS avg_speed_kmh,
                COALESCE(MAX(max_speed_kmh), 0)          AS max_speed_kmh,
                COALESCE(SUM(position_count), 0)::bigint AS total_positions
            FROM daily_fleet_kpis
            WHERE tenant_id = $1
              AND day >= $2::date
              AND day <  $3::date;
            """;

        await using var cmd = _db.CreateCommand(sql);
        cmd.Parameters.AddWithValue(tenantId);
        cmd.Parameters.AddWithValue(from.UtcDateTime);
        cmd.Parameters.AddWithValue(to.UtcDateTime);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);

        return new FleetKpisDto(
            ActiveVehicles:       reader.GetInt32(0),
            TotalDistanceKm:      Math.Round(reader.GetDouble(1), 1),
            AvgSpeedKmh:          Math.Round(reader.GetDouble(2), 1),
            MaxSpeedKmh:          Math.Round(reader.GetDouble(3), 1),
            TotalPositionRecords: reader.GetInt64(4),
            PeriodStart:          from,
            PeriodEnd:            to);
    }

    public async Task<IReadOnlyList<PositionPointDto>> GetVehicleHistoryAsync(
        Guid tenantId, Guid vehicleId, DateTimeOffset from, CancellationToken ct)
    {
        // Reads from raw hypertable. Capped at 10,000 rows to protect response size.
        // tenant_id guard enforces ownership — returns empty if vehicleId does not belong to tenant.
        const string sql = """
            SELECT latitude, longitude, speed, heading, odometer, timestamp
            FROM vehicle_positions
            WHERE vehicle_id = $1
              AND tenant_id  = $2
              AND timestamp  >= $3
            ORDER BY timestamp ASC
            LIMIT 10000;
            """;

        await using var cmd = _db.CreateCommand(sql);
        cmd.Parameters.AddWithValue(vehicleId);
        cmd.Parameters.AddWithValue(tenantId);
        cmd.Parameters.AddWithValue(from.UtcDateTime);

        var result = new List<PositionPointDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var ts = DateTime.SpecifyKind(reader.GetDateTime(5), DateTimeKind.Utc);
            result.Add(new PositionPointDto(
                Latitude:  reader.GetDouble(0),
                Longitude: reader.GetDouble(1),
                Speed:     reader.GetDouble(2),
                Heading:   reader.GetInt32(3),
                Odometer:  reader.GetDouble(4),
                Timestamp: new DateTimeOffset(ts)));
        }

        return result;
    }
}
