using FleetVision.Telemetry.Domain.Entities;

namespace FleetVision.Telemetry.Application.Common;

/// <summary>
/// Atomically inserts a position into vehicle_positions AND enqueues the outbox event
/// in a single database transaction — guarantees the Outbox pattern invariant.
/// </summary>
public interface ITelemetryWriter
{
    Task PersistAndEnqueueAsync(VehiclePosition position, CancellationToken ct = default);
}
