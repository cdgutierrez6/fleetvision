using FleetVision.Telemetry.Domain.Entities;

namespace FleetVision.Telemetry.Application.Common;

public interface IEventPublisher
{
    Task EnqueueAsync(VehiclePosition position, CancellationToken ct = default);
}
