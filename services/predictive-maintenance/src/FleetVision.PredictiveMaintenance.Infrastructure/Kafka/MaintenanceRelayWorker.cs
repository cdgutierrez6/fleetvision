using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace FleetVision.PredictiveMaintenance.Infrastructure.Kafka;

public sealed class MaintenanceRelayWorker : BackgroundService
{
    private readonly IServiceScopeFactory             _scopeFactory;
    private readonly ILogger<MaintenanceRelayWorker>  _logger;
    private readonly string                           _bootstrapServers;
    private IProducer<string, byte[]>?                _producer;
    private readonly TimeSpan _pollInterval = TimeSpan.FromMilliseconds(500);
    private const int BatchSize            = 100;
    private const int MaxRetries           = 10;
    private const int CleanupEvery         = 120;
    private const int ExpiredRetentionDays = 7;
    private int _cycleCount                = 0;

    public MaintenanceRelayWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<MaintenanceRelayWorker> logger,
        Microsoft.Extensions.Configuration.IConfiguration config)
    {
        _scopeFactory     = scopeFactory;
        _logger           = logger;
        _bootstrapServers = config["Kafka:BootstrapServers"]
            ?? throw new InvalidOperationException("Kafka:BootstrapServers is required.");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        _producer = new ProducerBuilder<string, byte[]>(new ProducerConfig
        {
            BootstrapServers  = _bootstrapServers,
            Acks              = Acks.All,
            EnableIdempotence = true,
            MessageTimeoutMs  = 10_000,
        }).Build();

        _logger.LogInformation("MaintenanceRelayWorker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);

                if (++_cycleCount % CleanupEvery == 0)
                    await PurgeExpiredAsync(stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MaintenanceRelayWorker batch error. Retrying.");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }

        _producer?.Flush(TimeSpan.FromSeconds(10));
        _logger.LogInformation("MaintenanceRelayWorker stopped.");
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        await using var scope      = _scopeFactory.CreateAsyncScope();
        var dataSource             = scope.ServiceProvider.GetRequiredService<NpgsqlDataSource>();

        const string fetchSql = """
            SELECT id, topic, partition_key, payload
            FROM maintenance_outbox_events
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
                ids.Add(reader.GetGuid(0));
                tasks.Add(_producer!.ProduceAsync(
                    reader.GetString(1),
                    new Message<string, byte[]>
                    {
                        Key   = reader.GetString(2),
                        Value = (byte[])reader.GetValue(3),
                    }, ct));
            }
        }

        if (ids.Count == 0) { await tx.RollbackAsync(ct); return; }

        var results = await Task.WhenAll(tasks.Select(async (t, i) =>
        {
            try
            {
                await t;
                return (i, error: (string?)null);
            }
            catch (Exception ex) { return (i, error: ex.Message); }
        }));

        var successIds = results.Where(r => r.error is null).Select(r => ids[r.i]).ToList();
        var errors     = results.Where(r => r.error is not null).ToList();

        if (successIds.Count > 0)
        {
            const string markSql = "UPDATE maintenance_outbox_events SET published_at = NOW() WHERE id = ANY(@ids)";
            await using var markCmd = new NpgsqlCommand(markSql, conn, tx);
            markCmd.Parameters.AddWithValue("ids", successIds.ToArray());
            await markCmd.ExecuteNonQueryAsync(ct);
        }

        foreach (var (i, error) in errors)
        {
            _logger.LogWarning("Kafka publish failed for outbox event {Id}: {Error}", ids[i], error);
            const string errSql = """
                UPDATE maintenance_outbox_events
                SET retry_count = retry_count + 1, last_error = @error
                WHERE id = @id
                """;
            await using var errCmd = new NpgsqlCommand(errSql, conn, tx);
            errCmd.Parameters.AddWithValue("id",    ids[i]);
            errCmd.Parameters.AddWithValue("error", error!);
            await errCmd.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
        if (successIds.Count > 0)
            _logger.LogDebug("Relayed {Count} maintenance events to Kafka.", successIds.Count);
    }

    private async Task PurgeExpiredAsync(CancellationToken ct)
    {
        await using var scope  = _scopeFactory.CreateAsyncScope();
        var dataSource         = scope.ServiceProvider.GetRequiredService<NpgsqlDataSource>();
        await using var conn   = await dataSource.OpenConnectionAsync(ct);

        const string purgeSql = """
            DELETE FROM maintenance_outbox_events
            WHERE retry_count >= @maxRetries
              AND created_at < NOW() - (@retentionDays || ' days')::INTERVAL
            """;
        await using var cmd = new NpgsqlCommand(purgeSql, conn);
        cmd.Parameters.AddWithValue("maxRetries",    MaxRetries);
        cmd.Parameters.AddWithValue("retentionDays", ExpiredRetentionDays);

        var deleted = await cmd.ExecuteNonQueryAsync(ct);
        if (deleted > 0)
            _logger.LogWarning("Purged {Count} expired maintenance outbox events.", deleted);
    }

    public override void Dispose()
    {
        _producer?.Dispose();
        base.Dispose();
    }
}

