namespace FleetVision.PredictiveMaintenance.Domain.Entities;

public sealed class MaintenanceOutboxEvent
{
    public Guid     Id          { get; private set; }
    public Guid     AggregateId { get; private set; }
    public string   Topic       { get; private set; } = default!;
    public string   PartitionKey { get; private set; } = default!;
    public byte[]   Payload     { get; private set; } = default!;
    public int      RetryCount  { get; set; }
    public string?  LastError   { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime CreatedAt   { get; private set; }

    private MaintenanceOutboxEvent() { }

    public static MaintenanceOutboxEvent Create(
        Guid aggregateId, string topic, string partitionKey, byte[] payload)
        => new()
        {
            Id           = Guid.NewGuid(),
            AggregateId  = aggregateId,
            Topic        = topic,
            PartitionKey = partitionKey,
            Payload      = payload,
            RetryCount   = 0,
            CreatedAt    = DateTime.UtcNow,
        };
}
