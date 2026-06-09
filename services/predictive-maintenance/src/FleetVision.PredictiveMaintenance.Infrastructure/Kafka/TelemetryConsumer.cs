using Confluent.Kafka;
using FleetVision.PredictiveMaintenance.Application.Services;
using FleetVision.PredictiveMaintenance.Domain.Entities;
using FleetVision.Proto.Telemetry;
using Google.Protobuf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FleetVision.PredictiveMaintenance.Infrastructure.Kafka;

public sealed class TelemetryConsumer : BackgroundService
{
    private readonly IServiceScopeFactory              _scopeFactory;
    private readonly ILogger<TelemetryConsumer>        _logger;
    private readonly string                            _bootstrapServers;
    private IConsumer<string, byte[]>?                 _consumer;
    private IProducer<string, byte[]>?                 _dlqProducer;
    private const string Topic    = "telemetry.raw";
    private const string DlqTopic = "telemetry.raw.dlq";

    public TelemetryConsumer(
        IServiceScopeFactory scopeFactory,
        ILogger<TelemetryConsumer> logger,
        Microsoft.Extensions.Configuration.IConfiguration config)
    {
        _scopeFactory     = scopeFactory;
        _logger           = logger;
        _bootstrapServers = config["Kafka:BootstrapServers"]
            ?? throw new InvalidOperationException("Kafka:BootstrapServers is required.");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Yield immediately so the host can finish starting all IHostedServices
        // (including GenericWebHostService / Kestrel) before this consumer blocks on Consume().
        await Task.Yield();

        _consumer = new ConsumerBuilder<string, byte[]>(new ConsumerConfig
        {
            BootstrapServers  = _bootstrapServers,
            GroupId           = "predictive-maintenance-service",
            AutoOffsetReset   = AutoOffsetReset.Earliest,
            EnableAutoCommit  = false,
            MaxPollIntervalMs = 300_000,
        }).Build();

        _dlqProducer = new ProducerBuilder<string, byte[]>(new ProducerConfig
        {
            BootstrapServers  = _bootstrapServers,
            Acks              = Acks.All,
        }).Build();
        _consumer.Subscribe(Topic);
        _logger.LogInformation("TelemetryConsumer subscribed to {Topic}.", Topic);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = _consumer.Consume(stoppingToken);
                if (result is null) continue;

                await ProcessAsync(result, stoppingToken);
                _consumer.Commit(result);
            }
            catch (ConsumeException ex)
            {
                _logger.LogError(ex, "Kafka consume error: {Reason}", ex.Error.Reason);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in TelemetryConsumer.");
            }
        }

        _consumer.Close();
        _logger.LogInformation("TelemetryConsumer stopped.");
    }

    private async Task ProcessAsync(ConsumeResult<string, byte[]> result, CancellationToken ct)
    {
        VehiclePositionEvent evt;
        try
        {
            evt = VehiclePositionEvent.Parser.ParseFrom(result.Message.Value);
        }
        catch (InvalidProtocolBufferException ex)
        {
            _logger.LogWarning(ex,
                "Failed to deserialize VehiclePositionEvent at offset {O}. Sending to DLQ.",
                result.Offset.Value);
            await SendToDlqAsync(result, ct);
            return;
        }

        if (!Guid.TryParse(evt.VehicleId, out var vehicleId) ||
            !Guid.TryParse(evt.TenantId,  out var tenantId))
        {
            _logger.LogWarning("Invalid VehicleId or TenantId. Skipping.");
            return;
        }

        // obd2_codes is a repeated field — take the first code for rule evaluation
        var firstCode = evt.Obd2Codes.FirstOrDefault();

        var reading = new TelemetryReading(
            TenantId:   tenantId,
            VehicleId:  vehicleId,
            Latitude:   evt.Latitude,
            Longitude:  evt.Longitude,
            SpeedKmh:   evt.SpeedKmh,
            // odometer_km is the total accumulated km (sentinel -1 = unreported)
            DistanceKm: (decimal)(evt.OdometerKm > 0 ? evt.OdometerKm : 0),
            Obd2Code:   string.IsNullOrWhiteSpace(firstCode) ? null : firstCode,
            Timestamp:  DateTimeOffset.FromUnixTimeMilliseconds(evt.TimestampUnixMs).UtcDateTime);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<MaintenanceOrchestrator>();

        try
        {
            await orchestrator.ProcessAsync(reading, result.Offset.Value, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing telemetry for vehicle {VehicleId}.", vehicleId);
            await SendToDlqAsync(result, ct);
        }
    }

    private Task SendToDlqAsync(ConsumeResult<string, byte[]> result, CancellationToken ct)
        => _dlqProducer!.ProduceAsync(DlqTopic, new Message<string, byte[]>
        {
            Key   = result.Message.Key,
            Value = result.Message.Value,
        }, ct);

    public override void Dispose()
    {
        _consumer?.Dispose();
        _dlqProducer?.Dispose();
        base.Dispose();
    }
}
