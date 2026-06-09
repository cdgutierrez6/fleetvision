using Confluent.Kafka;
using FleetVision.Geofencing.Application.TelemetryEvaluation;
using FleetVision.Proto.Telemetry;
using Google.Protobuf;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FleetVision.Geofencing.Infrastructure.Kafka;

/// <summary>
/// Consume mensajes del topic telemetry.raw y dispara la evaluación de geofences.
/// Consumer group: geofencing-service.
/// DLQ: telemetry.raw.dlq (mensajes inválidos o con error de procesamiento).
/// </summary>
public sealed class TelemetryKafkaConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TelemetryKafkaConsumer> _logger;
    private readonly IConsumer<string, byte[]> _consumer;
    private readonly IProducer<string, byte[]> _dlqProducer;
    private const string Topic    = "telemetry.raw";
    private const string DlqTopic = "telemetry.raw.dlq";

    public TelemetryKafkaConsumer(
        IServiceScopeFactory scopeFactory,
        ILogger<TelemetryKafkaConsumer> logger,
        IConfiguration config)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;

        var bootstrapServers = config["Kafka:BootstrapServers"]
            ?? throw new InvalidOperationException("Kafka:BootstrapServers is required.");

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers       = bootstrapServers,
            GroupId                = "geofencing-service",
            AutoOffsetReset        = AutoOffsetReset.Earliest,
            EnableAutoCommit       = false,   // manual commit tras procesamiento exitoso
            MaxPollIntervalMs      = 300_000,
        };

        _consumer = new ConsumerBuilder<string, byte[]>(consumerConfig).Build();

        var producerConfig = new ProducerConfig
        {
            BootstrapServers  = bootstrapServers,
            Acks              = Acks.All,       // perder un mensaje DLQ es peor que reintentarlo
            EnableIdempotence = true,
        };

        _dlqProducer = new ProducerBuilder<string, byte[]>(producerConfig).Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        _consumer.Subscribe(Topic);
        _logger.LogInformation("TelemetryKafkaConsumer subscribed to {Topic}.", Topic);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = _consumer.Consume(stoppingToken);
                if (result is null) continue;

                await ProcessMessageAsync(result, stoppingToken);
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
                _logger.LogError(ex, "Unexpected error in TelemetryKafkaConsumer.");
            }
        }

        _consumer.Close();
        _logger.LogInformation("TelemetryKafkaConsumer stopped.");
    }

    private async Task ProcessMessageAsync(ConsumeResult<string, byte[]> result, CancellationToken ct)
    {
        VehiclePositionEvent evt;

        try
        {
            evt = VehiclePositionEvent.Parser.ParseFrom(result.Message.Value);
        }
        catch (InvalidProtocolBufferException ex)
        {
            _logger.LogWarning(ex,
                "Failed to deserialize VehiclePositionEvent from partition {P} offset {O}. Sending to DLQ.",
                result.Partition.Value, result.Offset.Value);

            await _dlqProducer.ProduceAsync(DlqTopic, new Message<string, byte[]>
            {
                Key   = result.Message.Key,
                Value = result.Message.Value,
            }, ct);

            return;
        }

        if (!Guid.TryParse(evt.VehicleId, out var vehicleId) ||
            !Guid.TryParse(evt.TenantId,  out var tenantId))
        {
            _logger.LogWarning("Invalid VehicleId or TenantId in message. Skipping.");
            return;
        }

        Guid? driverId = Guid.TryParse(evt.DriverId, out var d) ? d : null;

        var cmd = new EvaluateTelemetryEventCommand(
            TenantId:  tenantId,
            VehicleId: vehicleId,
            DriverId:  driverId,
            Longitude: evt.Longitude,
            Latitude:  evt.Latitude,
            SpeedKmh:  evt.SpeedKmh >= 0 ? evt.SpeedKmh : null,  // sentinel -1 = not reported
            Timestamp: DateTimeOffset.FromUnixTimeMilliseconds(evt.TimestampUnixMs).UtcDateTime);

        await using var scope   = _scopeFactory.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        try
        {
            var evaluation = await mediator.Send(cmd, ct);

            if (evaluation.ViolationsDetected > 0)
                _logger.LogInformation(
                    "Vehicle {VehicleId}: {Count} violation(s) detected.",
                    vehicleId, evaluation.ViolationsDetected);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating telemetry for vehicle {VehicleId}.", vehicleId);

            await _dlqProducer.ProduceAsync(DlqTopic, new Message<string, byte[]>
            {
                Key   = result.Message.Key,
                Value = result.Message.Value,
            }, ct);
        }
    }

    public override void Dispose()
    {
        _consumer.Dispose();
        _dlqProducer.Dispose();
        base.Dispose();
    }
}
