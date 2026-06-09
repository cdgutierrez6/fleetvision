using FleetVision.PredictiveMaintenance.Domain.Entities;
using FleetVision.PredictiveMaintenance.Domain.Interfaces;
using FleetVision.PredictiveMaintenance.Infrastructure.Persistence;
using Google.Protobuf.WellKnownTypes;

namespace FleetVision.PredictiveMaintenance.Infrastructure.Kafka;

public sealed class MaintenanceOutboxEnqueuer : IMaintenanceOutboxEnqueuer
{
    private readonly MaintenanceDbContext _db;

    public MaintenanceOutboxEnqueuer(MaintenanceDbContext db) => _db = db;

    public void EnqueueScheduled(MaintenanceRecord record)
    {
        var payload = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new
        {
            record.Id,
            record.TenantId,
            record.VehicleId,
            record.RecordType,
            record.TriggeredBy,
            record.OdometerKm,
            record.ThresholdKm,
            record.Notes,
            OccurredAt = record.CreatedAt.ToString("O"),
        });

        _db.OutboxEvents.Add(
            MaintenanceOutboxEvent.Create(
                aggregateId:  record.Id,
                topic:        "maintenance.scheduled",
                partitionKey: record.VehicleId.ToString(),
                payload:      payload));
    }

    public void EnqueueAlert(MaintenanceRecord record)
    {
        var payload = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new
        {
            record.Id,
            record.TenantId,
            record.VehicleId,
            record.RecordType,
            record.Obd2Code,
            Severity    = "CRITICAL",
            record.OdometerKm,
            OccurredAt  = record.CreatedAt.ToString("O"),
        });

        _db.OutboxEvents.Add(
            MaintenanceOutboxEvent.Create(
                aggregateId:  record.Id,
                topic:        "vehicle.alert",
                partitionKey: record.VehicleId.ToString(),
                payload:      payload));
    }
}
