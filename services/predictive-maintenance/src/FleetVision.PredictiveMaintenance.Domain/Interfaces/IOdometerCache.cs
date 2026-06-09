using FleetVision.PredictiveMaintenance.Domain.ValueObjects;

namespace FleetVision.PredictiveMaintenance.Domain.Interfaces;

public interface IOdometerCache
{
    Task<OdometerSnapshot> GetAndIncrementAsync(
        Guid tenantId, Guid vehicleId, decimal distanceKm,
        long kafkaOffset, CancellationToken ct = default);

    Task ResetAsync(Guid tenantId, Guid vehicleId, CancellationToken ct = default);
}
