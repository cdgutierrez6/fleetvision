using FleetVision.Telemetry.Application.Common;
using FleetVision.Telemetry.Domain.Entities;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Globalization;

namespace FleetVision.Telemetry.Infrastructure.Redis;

public sealed class PositionCache : IPositionCache
{
    private readonly IDatabase _redis;
    private readonly ILogger<PositionCache> _logger;
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    public PositionCache(IConnectionMultiplexer redis, ILogger<PositionCache> logger)
    {
        _redis  = redis.GetDatabase();
        _logger = logger;
    }

    private static string Key(Guid vehicleId) => $"vehicle:last_pos:{vehicleId}";

    public async Task SetAsync(VehiclePosition position, CancellationToken ct = default)
    {
        var key = Key(position.VehicleId);

        var fields = new HashEntry[]
        {
            new("lat",       position.Latitude.ToString("R", CultureInfo.InvariantCulture)),
            new("lon",       position.Longitude.ToString("R", CultureInfo.InvariantCulture)),
            new("speed",     position.SpeedKmh?.ToString("R", CultureInfo.InvariantCulture) ?? ""),
            new("ts",        new DateTimeOffset(position.Time).ToUnixTimeMilliseconds().ToString()),
            new("driver_id", position.DriverId?.ToString() ?? ""),
            new("engine_on", position.EngineOn?.ToString() ?? ""),
            new("tenant_id", position.TenantId.ToString()),
        };

        try
        {
            // Pipeline: ambos comandos se envían en un solo round-trip,
            // minimizando la ventana donde la key existe sin TTL.
            var batch     = _redis.CreateBatch();
            var hashTask  = batch.HashSetAsync(key, fields);
            var expireTask = batch.KeyExpireAsync(key, Ttl);
            batch.Execute();
            await Task.WhenAll(hashTask, expireTask);
        }
        catch (Exception ex)
        {
            // Redis cache miss es tolerable — no interrumpir el flujo de ingesta
            _logger.LogWarning(ex, "Redis cache write failed for vehicle {VehicleId}. Continuing without cache.", position.VehicleId);
        }
    }

    public async Task<VehiclePosition?> GetAsync(Guid vehicleId, CancellationToken ct = default)
    {
        var key = Key(vehicleId);

        try
        {
            var hash = await _redis.HashGetAllAsync(key);
            if (hash.Length == 0) return null;

            var dict = hash.ToDictionary(h => h.Name.ToString(), h => h.Value.ToString());

            if (!double.TryParse(dict.GetValueOrDefault("lat"), NumberStyles.Float, CultureInfo.InvariantCulture, out var lat)) return null;
            if (!double.TryParse(dict.GetValueOrDefault("lon"), NumberStyles.Float, CultureInfo.InvariantCulture, out var lon)) return null;
            if (!long.TryParse(dict.GetValueOrDefault("ts"), out var tsMs)) return null;

            double? speed = double.TryParse(dict.GetValueOrDefault("speed"), NumberStyles.Float, CultureInfo.InvariantCulture, out var s) ? s : null;
            Guid? driverId = Guid.TryParse(dict.GetValueOrDefault("driver_id"), out var d) ? d : null;
            bool? engineOn = bool.TryParse(dict.GetValueOrDefault("engine_on"), out var e) ? e : null;
            Guid? tenantId = Guid.TryParse(dict.GetValueOrDefault("tenant_id"), out var t) ? t : null;

            if (tenantId is null) return null;

            var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(tsMs).UtcDateTime;

            return VehiclePosition.Create(
                vehicleId: vehicleId,
                tenantId:  tenantId.Value,
                timestamp: timestamp,
                latitude:  lat,
                longitude: lon,
                driverId:  driverId,
                speedKmh:  speed,
                engineOn:  engineOn);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis cache read failed for vehicle {VehicleId}. Cache miss.", vehicleId);
            return null;
        }
    }
}
