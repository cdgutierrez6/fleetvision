using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace FleetVision.Telemetry.Infrastructure.Kafka;

/// <summary>
/// Background service que hace polling sobre outbox_events y publica a Kafka.
/// Garantiza at-least-once delivery: si el proceso muere después del INSERT en outbox
/// pero antes del ACK de Kafka, el evento se reintenta en el próximo ciclo.
/// </summary>
public sealed class KafkaRelayWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<KafkaRelayWorker> _logger;
    private readonly IProducer<string, byte[]> _producer;
    private readonly TimeSpan _pollInterval = TimeSpan.FromMilliseconds(500);
    private const int BatchSize    = 100;
    // Mensajes que superan este límite se consideran "poison" y se excluyen del relay.
    // Deben procesarse manualmente o descartarse via dead-letter fuera del worker.
    private const int MaxRetries   = 10;

    public KafkaRelayWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<KafkaRelayWorker> logger,
        IConfiguration config)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;

        var producerConfig = new ProducerConfig
        {
            BootstrapServers  = config["Kafka:BootstrapServers"]
                ?? throw new InvalidOperationException("Kafka:BootstrapServers is required."),
            Acks              = Acks.All,
            EnableIdempotence = true,
            MessageTimeoutMs  = 10_000,
        };

        _producer = new ProducerBuilder<string, byte[]>(producerConfig).Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("KafkaRelayWorker started. Polling every {Interval}ms.", _pollInterval.TotalMilliseconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "KafkaRelayWorker batch error. Retrying after interval.");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }

        _producer.Flush(TimeSpan.FromSeconds(10));
        _logger.LogInformation("KafkaRelayWorker stopped.");
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dataSource = scope.ServiceProvider.GetRequiredService<NpgsqlDataSource>();

        const string fetchSql = """
            SELECT id, topic, partition_key, payload
            FROM outbox_events
            WHERE published_at IS NULL
              AND retry_count < @maxRetries
            ORDER BY created_at ASC
            LIMIT @batchSize
            FOR UPDATE SKIP LOCKED
            """;

        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var tx   = await conn.BeginTransactionAsync(ct);

        var ids     = new List<Guid>();
        var errors  = new List<(Guid id, string error)>();
        var tasks   = new List<Task<DeliveryResult<string, byte[]>>>();

        await using (var cmd = new NpgsqlCommand(fetchSql, conn, tx))
        {
            cmd.Parameters.AddWithValue("batchSize",  BatchSize);
            cmd.Parameters.AddWithValue("maxRetries", MaxRetries);
            await using var reader = await cmd.ExecuteReaderAsync(ct);

            while (await reader.ReadAsync(ct))
            {
                var id           = reader.GetGuid(0);
                var topic        = reader.GetString(1);
                var partitionKey = reader.GetString(2);
                var payload      = (byte[])reader.GetValue(3);

                ids.Add(id);

                var message = new Message<string, byte[]>
                {
                    Key   = partitionKey,
                    Value = payload,
                };

                tasks.Add(_producer.ProduceAsync(topic, message, ct));
            }
        }

        if (ids.Count == 0)
        {
            await tx.RollbackAsync(ct);
            return;
        }

        // Esperar todos los ACKs de Kafka antes de marcar como publicados
        var results = await Task.WhenAll(tasks.Select(async (t, i) =>
        {
            try { return (i, error: (string?)null, await t); }
            catch (Exception ex) { return (i, error: ex.Message, result: (DeliveryResult<string, byte[]>?)null); }
        }));

        var successIds = new List<Guid>();
        foreach (var (idx, error, _) in results)
        {
            if (error is null) successIds.Add(ids[idx]);
            else               errors.Add((ids[idx], error));
        }

        if (successIds.Count > 0)
        {
            const string markSql = """
                UPDATE outbox_events
                SET published_at = NOW()
                WHERE id = ANY(@ids)
                """;

            await using var markCmd = new NpgsqlCommand(markSql, conn, tx);
            markCmd.Parameters.AddWithValue("ids", successIds.ToArray());
            await markCmd.ExecuteNonQueryAsync(ct);
        }

        if (errors.Count > 0)
        {
            const string errorSql = """
                UPDATE outbox_events
                SET retry_count = retry_count + 1,
                    last_error  = @error
                WHERE id = @id
                """;

            foreach (var (id, error) in errors)
            {
                _logger.LogWarning("Kafka publish failed for outbox event {Id}: {Error}", id, error);
                await using var errCmd = new NpgsqlCommand(errorSql, conn, tx);
                errCmd.Parameters.AddWithValue("id",    id);
                errCmd.Parameters.AddWithValue("error", error);
                await errCmd.ExecuteNonQueryAsync(ct);
            }
        }

        await tx.CommitAsync(ct);

        if (successIds.Count > 0)
            _logger.LogDebug("Published {Count} events to Kafka.", successIds.Count);
    }

    public override void Dispose()
    {
        _producer.Dispose();
        base.Dispose();
    }
}
