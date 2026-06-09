namespace FleetVision.Billing.Infrastructure.Persistence.Entities;

public sealed class BillingOutboxEvent
{
    public Guid Id { get; private set; }
    public string Topic { get; private set; } = string.Empty;
    public string PartitionKey { get; private set; } = string.Empty;
    public byte[] Payload { get; private set; } = Array.Empty<byte>();
    public DateTime? PublishedAt { get; private set; }
    public int RetryCount { get; private set; }
    public string? LastError { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private BillingOutboxEvent() { }

    public static BillingOutboxEvent Create(string topic, string partitionKey, byte[] payload)
        => new()
        {
            Id           = Guid.NewGuid(),
            Topic        = topic,
            PartitionKey = partitionKey,
            Payload      = payload,
            CreatedAt    = DateTime.UtcNow
        };
}
