using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace FleetVision.Billing.Infrastructure.Kafka;

/// <summary>
/// Polls billing_outbox_events and publishes to Kafka topics.
/// Identical pattern to ViolationRelayWorker: FOR UPDATE SKIP LOCKED, Acks.All, DLQ on failure.
/// </summary>
public sealed class BillingRelayWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BillingRelayWorker> _logger;
    private readonly IProducer<string, byte[]> _producer;
    private readonly TimeSpan _pollInterval = TimeSpan.FromMilliseconds(500);
    private const int BatchSize            = 50;
    private const int MaxRetries           = 10;
    private const int CleanupEvery         = 120;
    private const int ExpiredRetentionDays = 7;
    private int _cycleCount                = 0;

    public BillingRelayWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<BillingRelayWorker> logger,
        IConfiguration config)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;

        _producer = new ProducerBuilder<string, byte[]>(new ProducerConfig
        {
            BootstrapServers  = config["Kafka:BootstrapServers"]
                ?? throw new InvalidOperationException("Kafka:BootstrapServers is required."),
            Acks              = Acks.All,
            EnableIdempotence = true,
            MessageTimeoutMs  = 10_000,
        }).Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BillingRelayWorker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);

                if (++_cycleCount % CleanupEvery == 0)
                    await PurgeExpiredAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BillingRelayWorker batch error. Retrying.");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }

        _producer.Flush(TimeSpan.FromSeconds(10));
        _logger.LogInformation("BillingRelayWorker stopped.");
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        await using var scope      = _scopeFactory.CreateAsyncScope();
        var dataSource             = scope.ServiceProvider.GetRequiredService<NpgsqlDataSource>();

        const string fetchSql = """
            SELECT id, topic, partition_key, payload
            FROM billing_outbox_events
            WHERE published_at IS NULL
              AND retry_count < @maxRetries
            ORDER BY created_at ASC
            LIMIT @batchSize
            FOR UPDATE SKIP LOCKED
            """;

        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var tx   = await conn.BeginTransactionAsync(ct);

        var ids   = new List<Guid>();
        var tasks = new List<Task<DeliveryResult<string, byte[]>>>();

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
                tasks.Add(_producer.ProduceAsync(topic,
                    new Message<string, byte[]> { Key = partitionKey, Value = payload }, ct));
            }
        }

        if (ids.Count == 0)
        {
            await tx.RollbackAsync(ct);
            return;
        }

        var results = await Task.WhenAll(tasks.Select(async (t, i) =>
        {
            try
            {
                await t;
                return (i, error: (string?)null);
            }
            catch (Exception ex) { return (i, error: ex.Message); }
        }));

        var successIds = new List<Guid>();
        var errors     = new List<(Guid id, string error)>();

        foreach (var (idx, error) in results)
        {
            if (error is null) successIds.Add(ids[idx]);
            else               errors.Add((ids[idx], error));
        }

        if (successIds.Count > 0)
        {
            const string markSql = """
                UPDATE billing_outbox_events
                SET published_at = NOW()
                WHERE id = ANY(@ids)
                """;
            await using var markCmd = new NpgsqlCommand(markSql, conn, tx);
            markCmd.Parameters.AddWithValue("ids", successIds.ToArray());
            await markCmd.ExecuteNonQueryAsync(ct);
        }

        foreach (var (id, error) in errors)
        {
            _logger.LogWarning(
                "Kafka publish failed for billing outbox event {Id}: {Error}", id, error);
            const string errSql = """
                UPDATE billing_outbox_events
                SET retry_count = retry_count + 1, last_error = @error
                WHERE id = @id
                """;
            await using var errCmd = new NpgsqlCommand(errSql, conn, tx);
            errCmd.Parameters.AddWithValue("id",    id);
            errCmd.Parameters.AddWithValue("error", error);
            await errCmd.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);

        if (successIds.Count > 0)
            _logger.LogDebug("Relayed {Count} billing events to Kafka.", successIds.Count);
    }

    private async Task PurgeExpiredAsync(CancellationToken ct)
    {
        await using var scope      = _scopeFactory.CreateAsyncScope();
        var dataSource             = scope.ServiceProvider.GetRequiredService<NpgsqlDataSource>();
        await using var conn       = await dataSource.OpenConnectionAsync(ct);

        const string purgeSql = """
            DELETE FROM billing_outbox_events
            WHERE retry_count >= @maxRetries
              AND created_at < NOW() - (@retentionDays || ' days')::INTERVAL
            """;

        await using var cmd = new NpgsqlCommand(purgeSql, conn);
        cmd.Parameters.AddWithValue("maxRetries",    MaxRetries);
        cmd.Parameters.AddWithValue("retentionDays", ExpiredRetentionDays);

        var deleted = await cmd.ExecuteNonQueryAsync(ct);
        if (deleted > 0)
            _logger.LogWarning(
                "Purged {Count} expired billing outbox events.", deleted);
    }

    public override void Dispose()
    {
        _producer.Dispose();
        base.Dispose();
    }
}
