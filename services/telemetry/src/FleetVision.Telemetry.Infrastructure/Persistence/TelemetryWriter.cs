using FleetVision.Proto.Telemetry;
using FleetVision.Telemetry.Application.Common;
using FleetVision.Telemetry.Domain.Entities;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace FleetVision.Telemetry.Infrastructure.Persistence;

/// <summary>
/// Inserts vehicle_positions AND outbox_events in the same Npgsql transaction.
/// Enforces the Outbox pattern invariant: either both writes commit or neither does.
/// </summary>
public sealed class TelemetryWriter : ITelemetryWriter
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<TelemetryWriter> _logger;
    private readonly int _schemaId;

    public TelemetryWriter(NpgsqlDataSource dataSource, ILogger<TelemetryWriter> logger, int schemaId)
    {
        _dataSource = dataSource;
        _logger     = logger;
        _schemaId   = schemaId;
    }

    public async Task PersistAndEnqueueAsync(VehiclePosition position, CancellationToken ct = default)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var tx   = await conn.BeginTransactionAsync(ct);

        try
        {
            await InsertPositionAsync(conn, tx, position, ct);
            await InsertOutboxEventAsync(conn, tx, position, ct);
            await tx.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist+enqueue vehicle position for vehicle {VehicleId}. Rolling back.", position.VehicleId);
            await tx.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static async Task InsertPositionAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx, VehiclePosition position, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO vehicle_positions
                (time, vehicle_id, tenant_id, driver_id,
                 latitude, longitude, speed_kmh, heading_deg,
                 accuracy_m, hdop, satellite_count,
                 fuel_pct, engine_on, obd2_codes, odometer_km)
            VALUES
                (@time, @vehicleId, @tenantId, @driverId,
                 @latitude, @longitude, @speedKmh, @headingDeg,
                 @accuracyM, @hdop, @satelliteCount,
                 @fuelPct, @engineOn, @obd2Codes, @odometerKm)
            """;

        await using var cmd = new NpgsqlCommand(sql, conn, tx);

        cmd.Parameters.AddWithValue("time",           position.Time);
        cmd.Parameters.AddWithValue("vehicleId",      position.VehicleId);
        cmd.Parameters.AddWithValue("tenantId",       position.TenantId);
        cmd.Parameters.AddWithValue("driverId",       position.DriverId.HasValue ? position.DriverId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("latitude",       position.Latitude);
        cmd.Parameters.AddWithValue("longitude",      position.Longitude);
        cmd.Parameters.AddWithValue("speedKmh",       position.SpeedKmh.HasValue ? position.SpeedKmh.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("headingDeg",     position.HeadingDeg.HasValue ? position.HeadingDeg.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("accuracyM",      position.AccuracyM.HasValue ? position.AccuracyM.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("hdop",           position.Hdop.HasValue ? position.Hdop.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("satelliteCount", position.SatelliteCount.HasValue ? position.SatelliteCount.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("fuelPct",        position.FuelPct.HasValue ? position.FuelPct.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("engineOn",       position.EngineOn.HasValue ? position.EngineOn.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("obd2Codes",      position.Obd2Codes is { Length: > 0 } ? position.Obd2Codes : DBNull.Value);
        cmd.Parameters.AddWithValue("odometerKm",     position.OdometerKm.HasValue ? position.OdometerKm.Value : DBNull.Value);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task InsertOutboxEventAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx, VehiclePosition position, CancellationToken ct)
    {
        var evt = new VehiclePositionEvent
        {
            VehicleId       = position.VehicleId.ToString(),
            TenantId        = position.TenantId.ToString(),
            DriverId        = position.DriverId?.ToString() ?? string.Empty,
            TimestampUnixMs = new DateTimeOffset(position.Time).ToUnixTimeMilliseconds(),
            Latitude        = position.Latitude,
            Longitude       = position.Longitude,
            // Speed/fuel/heading use negative sentinel (-1) to distinguish "not reported" from 0.
            // Consumer checks: value >= 0 ? value : null
            SpeedKmh        = (float)(position.SpeedKmh ?? -1),
            HeadingDeg      = (float)(position.HeadingDeg ?? -1),
            AccuracyM       = (float)(position.AccuracyM ?? -1),
            Hdop            = (float)(position.Hdop ?? -1),
            SatelliteCount  = position.SatelliteCount ?? -1,
            FuelPct         = (float)(position.FuelPct ?? -1),
            EngineOn        = position.EngineOn ?? false,
        };

        if (position.Obd2Codes is { Length: > 0 })
            evt.Obd2Codes.AddRange(position.Obd2Codes);

        var payload = evt.ToByteArray();

        const string sql = """
            INSERT INTO outbox_events (topic, partition_key, payload, schema_id)
            VALUES (@topic, @partitionKey, @payload, @schemaId)
            """;

        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("topic",        "telemetry.raw");
        cmd.Parameters.AddWithValue("partitionKey", position.VehicleId.ToString());
        cmd.Parameters.AddWithValue("payload",      payload);
        cmd.Parameters.AddWithValue("schemaId",     _schemaId);

        await cmd.ExecuteNonQueryAsync(ct);
    }
}
