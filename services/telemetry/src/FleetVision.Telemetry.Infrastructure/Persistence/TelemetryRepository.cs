using FleetVision.Telemetry.Application.Common;
using FleetVision.Telemetry.Domain.Entities;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace FleetVision.Telemetry.Infrastructure.Persistence;

public sealed class TelemetryRepository : ITelemetryRepository
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<TelemetryRepository> _logger;

    public TelemetryRepository(NpgsqlDataSource dataSource, ILogger<TelemetryRepository> logger)
    {
        _dataSource = dataSource;
        _logger     = logger;
    }

    public async Task<VehiclePosition?> GetLatestAsync(Guid vehicleId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT time, vehicle_id, tenant_id, driver_id,
                   latitude, longitude, speed_kmh, heading_deg,
                   accuracy_m, hdop, satellite_count, fuel_pct, engine_on, obd2_codes
            FROM vehicle_positions
            WHERE vehicle_id = @vehicleId
            ORDER BY time DESC
            LIMIT 1
            """;

        try
        {
            await using var conn = await _dataSource.OpenConnectionAsync(ct);
            await using var cmd  = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("vehicleId", vehicleId);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) return null;

            return VehiclePosition.Create(
                vehicleId:      reader.GetGuid(1),
                tenantId:       reader.GetGuid(2),
                timestamp:      reader.GetDateTime(0),
                latitude:       reader.GetDouble(4),
                longitude:      reader.GetDouble(5),
                driverId:       reader.IsDBNull(3) ? null : reader.GetGuid(3),
                speedKmh:       reader.IsDBNull(6) ? null : reader.GetDouble(6),
                headingDeg:     reader.IsDBNull(7) ? null : (short?)reader.GetInt16(7),
                accuracyM:      reader.IsDBNull(8) ? null : reader.GetDouble(8),
                hdop:           reader.IsDBNull(9) ? null : reader.GetDouble(9),
                satelliteCount: reader.IsDBNull(10) ? null : (short?)reader.GetInt16(10),
                fuelPct:        reader.IsDBNull(11) ? null : reader.GetDouble(11),
                engineOn:       reader.IsDBNull(12) ? null : reader.GetBoolean(12),
                obd2Codes:      reader.IsDBNull(13) ? null : (string[])reader.GetValue(13));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get latest position for vehicle {VehicleId}", vehicleId);
            throw;
        }
    }
}
