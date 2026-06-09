using FleetVision.Geofencing.Application.Common;
using FleetVision.Geofencing.Domain.Entities;
using FleetVision.Geofencing.Infrastructure.Persistence;
using FleetVision.Proto.Geofencing;
using Google.Protobuf;

namespace FleetVision.Geofencing.Infrastructure.Kafka;

/// <summary>
/// Serializes a GeofenceViolation into a ViolationDetectedEvent proto and adds it
/// to the geofencing_outbox_events table via the shared GeofencingDbContext.
/// The caller (EvaluateTelemetryEventCommandHandler) owns SaveChangesAsync —
/// outbox event and violation are committed atomically in a single transaction.
/// </summary>
public sealed class ViolationOutboxEnqueuer : IViolationPublisher
{
    private readonly GeofencingDbContext _db;

    public ViolationOutboxEnqueuer(GeofencingDbContext db)
    {
        _db = db;
    }

    public void Enqueue(GeofenceViolation violation, string geofenceName)
    {
        var evt = new ViolationDetectedEvent
        {
            Id              = violation.Id.ToString(),
            TenantId        = violation.TenantId.ToString(),
            VehicleId       = violation.VehicleId.ToString(),
            DriverId        = violation.DriverId?.ToString() ?? string.Empty,
            GeofenceId      = violation.GeofenceId.ToString(),
            GeofenceName    = geofenceName,
            ViolationType   = violation.ViolationType.ToString(),
            // NTS convention: X = longitude, Y = latitude
            Latitude        = violation.Position.Y,
            Longitude       = violation.Position.X,
            // Sentinel -1 for non-speed violations — consumer checks >= 0 before using
            ActualSpeedKmh  = (float)(violation.ActualSpeedKmh ?? -1.0),
            LimitSpeedKmh   = violation.LimitSpeedKmh ?? -1,
            OccurredAtUnixMs = new DateTimeOffset(violation.OccurredAt, TimeSpan.Zero)
                                   .ToUnixTimeMilliseconds(),
        };

        _db.OutboxEvents.Add(
            GeofencingOutboxEvent.Create(
                topic:        "geofencing.violations",
                partitionKey: violation.VehicleId.ToString(),
                payload:      evt.ToByteArray()));
    }
}
