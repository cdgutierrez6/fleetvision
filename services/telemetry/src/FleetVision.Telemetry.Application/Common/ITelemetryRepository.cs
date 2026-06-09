using FleetVision.Telemetry.Domain.Entities;

namespace FleetVision.Telemetry.Application.Common;

public interface ITelemetryRepository
{
    Task<VehiclePosition?> GetLatestAsync(Guid vehicleId, CancellationToken ct = default);
}
