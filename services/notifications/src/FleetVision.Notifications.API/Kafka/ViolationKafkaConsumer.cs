using Confluent.Kafka;
using FleetVision.Notifications.API.Hubs;
using FleetVision.Proto.Geofencing;
using Google.Protobuf;
using Microsoft.AspNetCore.SignalR;

namespace FleetVision.Notifications.API.Kafka;

/// <summary>
/// Consumes "geofencing.violations" Kafka topic and broadcasts each event via SignalR
/// to the matching tenant group. Consumer group: notifications-service.
/// Idempotency: SignalR broadcast is best-effort — the violation is already persisted
/// in the Geofencing DB and can be queried by the Angular dashboard on reconnect.
/// </summary>
public sealed class ViolationKafkaConsumer : BackgroundService
{
    private readonly IHubContext<ViolationHub> _hub;
    private readonly ILogger<ViolationKafkaConsumer> _logger;
    private readonly IConsumer<string, byte[]> _consumer;
    private readonly IProducer<string, byte[]> _dlqProducer;
    private const string Topic    = "geofencing.violations";
    private const string DlqTopic = "geofencing.violations.dlq";

    public ViolationKafkaConsumer(
        IHubContext<ViolationHub> hub,
        ILogger<ViolationKafkaConsumer> logger,
        IConfiguration config)
    {
        _hub    = hub;
        _logger = logger;

        var bootstrapServers = config["Kafka:BootstrapServers"]
            ?? throw new InvalidOperationException("Kafka:BootstrapServers is required.");

        _consumer = new ConsumerBuilder<string, byte[]>(new ConsumerConfig
        {
            BootstrapServers  = bootstrapServers,
            GroupId           = "notifications-service",
            AutoOffsetReset   = AutoOffsetReset.Earliest,
            EnableAutoCommit  = false,
            MaxPollIntervalMs = 300_000,
        }).Build();

        _dlqProducer = new ProducerBuilder<string, byte[]>(new ProducerConfig
        {
            BootstrapServers = bootstrapServers,
            Acks             = Acks.All,
        }).Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        _consumer.Subscribe(Topic);
        _logger.LogInformation("ViolationKafkaConsumer subscribed to {Topic}.", Topic);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = _consumer.Consume(stoppingToken);
                if (result is null) continue;

                await BroadcastAsync(result, stoppingToken);
                _consumer.Commit(result);
            }
            catch (ConsumeException ex)
            {
                _logger.LogError(ex, "Kafka consume error: {Reason}", ex.Error.Reason);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in ViolationKafkaConsumer.");
            }
        }

        _consumer.Close();
        _logger.LogInformation("ViolationKafkaConsumer stopped.");
    }

    private async Task BroadcastAsync(ConsumeResult<string, byte[]> result, CancellationToken ct)
    {
        ViolationDetectedEvent evt;

        try
        {
            evt = ViolationDetectedEvent.Parser.ParseFrom(result.Message.Value);
        }
        catch (InvalidProtocolBufferException ex)
        {
            _logger.LogWarning(ex,
                "Failed to deserialize ViolationDetectedEvent at partition {P} offset {O}. Routing to DLQ.",
                result.Partition.Value, result.Offset.Value);

            await _dlqProducer.ProduceAsync(DlqTopic,
                new Message<string, byte[]>
                {
                    Key   = result.Message.Key,
                    Value = result.Message.Value
                }, ct);
            return;
        }

        if (string.IsNullOrEmpty(evt.TenantId))
        {
            _logger.LogWarning("ViolationDetectedEvent missing tenant_id at partition {P} offset {O}. Routing to DLQ.",
                result.Partition.Value, result.Offset.Value);

            await _dlqProducer.ProduceAsync(DlqTopic,
                new Message<string, byte[]>
                {
                    Key   = result.Message.Key,
                    Value = result.Message.Value
                }, ct);
            return;
        }

        var payload = new
        {
            id             = evt.Id,
            vehicleId      = evt.VehicleId,
            driverId       = string.IsNullOrEmpty(evt.DriverId) ? (string?)null : evt.DriverId,
            geofenceId     = evt.GeofenceId,
            geofenceName   = evt.GeofenceName,
            violationType  = evt.ViolationType,
            latitude       = evt.Latitude,
            longitude      = evt.Longitude,
            // Sentinel -1 maps to null for non-speed violations
            actualSpeedKmh = evt.ActualSpeedKmh >= 0 ? evt.ActualSpeedKmh : (float?)null,
            limitSpeedKmh  = evt.LimitSpeedKmh  >= 0 ? evt.LimitSpeedKmh  : (int?)null,
            occurredAt     = DateTimeOffset.FromUnixTimeMilliseconds(evt.OccurredAtUnixMs).UtcDateTime,
        };

        try
        {
            await _hub.Clients
                      .Group(ViolationHub.GroupName(evt.TenantId))
                      .SendAsync("ViolationDetected", payload, ct);

            _logger.LogDebug(
                "Broadcasted {ViolationType} for vehicle {VehicleId} to tenant {TenantId}.",
                evt.ViolationType, evt.VehicleId, evt.TenantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "SignalR broadcast failed for violation {Id} (tenant {TenantId}).",
                evt.Id, evt.TenantId);
        }
    }

    public override void Dispose()
    {
        _dlqProducer.Flush(TimeSpan.FromSeconds(5));
        _dlqProducer.Dispose();
        _consumer.Dispose();
        base.Dispose();
    }
}
