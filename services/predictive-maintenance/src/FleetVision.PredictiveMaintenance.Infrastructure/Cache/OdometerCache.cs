using FleetVision.PredictiveMaintenance.Domain.Interfaces;
using FleetVision.PredictiveMaintenance.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace FleetVision.PredictiveMaintenance.Infrastructure.Cache;

public sealed class OdometerCache : IOdometerCache
{
    private readonly IConnectionMultiplexer      _redis;
    private readonly ILogger<OdometerCache> _logger;

    private static string OdometerKey(Guid tenantId, Guid vehicleId)
        => $"odometer:{tenantId}:{vehicleId}";

    private static string DeduplicationKey(Guid tenantId, Guid vehicleId, long offset)
        => $"odometer-inc:{tenantId}:{vehicleId}:{offset}";

    public OdometerCache(IConnectionMultiplexer redis, ILogger<OdometerCache> logger)
    {
        _redis  = redis;
        _logger = logger;
    }

    public async Task<OdometerSnapshot> GetAndIncrementAsync(
        Guid tenantId, Guid vehicleId, decimal distanceKm,
        long kafkaOffset, CancellationToken ct = default)
    {
        try
        {
            var db      = _redis.GetDatabase();
            var dedupKey = DeduplicationKey(tenantId, vehicleId, kafkaOffset);

            var alreadyProcessed = !(await db.StringSetAsync(
                dedupKey, "1",
                expiry: TimeSpan.FromHours(48),
                when: When.NotExists));

            if (!alreadyProcessed)
                await db.StringIncrementAsync(OdometerKey(tenantId, vehicleId), (double)distanceKm);

            var raw = await db.StringGetAsync(OdometerKey(tenantId, vehicleId));
            if (raw.IsNullOrEmpty) return OdometerSnapshot.Unknown;

            return OdometerSnapshot.FromKm(decimal.Parse(raw.ToString()));
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex, "Redis unavailable — odometer snapshot degraded for {VehicleId}", vehicleId);
            return OdometerSnapshot.Unknown;
        }
    }

    public async Task ResetAsync(Guid tenantId, Guid vehicleId, CancellationToken ct = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync(OdometerKey(tenantId, vehicleId));
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex, "Redis unavailable — could not reset odometer for {VehicleId}", vehicleId);
        }
    }
}
