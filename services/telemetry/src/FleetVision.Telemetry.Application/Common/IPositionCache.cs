using FleetVision.Telemetry.Domain.Entities;

namespace FleetVision.Telemetry.Application.Common;

public interface IPositionCache
{
    Task SetAsync(VehiclePosition position, CancellationToken ct = default);
    Task<VehiclePosition?> GetAsync(Guid vehicleId, CancellationToken ct = default);
}
