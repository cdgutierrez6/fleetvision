using FleetVision.Telemetry.Application.Common;
using FleetVision.Telemetry.Infrastructure.Kafka;
using FleetVision.Telemetry.Infrastructure.Persistence;
using FleetVision.Telemetry.Infrastructure.Redis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using StackExchange.Redis;

namespace FleetVision.Telemetry.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration config)
    {
        // ─── TimescaleDB via Npgsql directo ──────────────────────────────────
        var connStr = config.GetConnectionString("TelemetryDb")
            ?? throw new InvalidOperationException("ConnectionStrings:TelemetryDb is required.");

        var dataSource = new NpgsqlDataSourceBuilder(connStr).Build();
        services.AddSingleton(dataSource);

        // Read-only repository (GetLatestAsync). Writes go through ITelemetryWriter.
        services.AddScoped<ITelemetryRepository, TelemetryRepository>();

        // ─── Atomic writer: INSERT vehicle_positions + outbox_events en una sola tx ──
        var schemaId = config.GetValue<int>("Kafka:SchemaId");
        services.AddScoped<ITelemetryWriter>(sp =>
            new TelemetryWriter(
                sp.GetRequiredService<NpgsqlDataSource>(),
                sp.GetRequiredService<ILogger<TelemetryWriter>>(),
                schemaId));

        // ─── Kafka relay worker (outbox → Kafka) ──────────────────────────────
        services.AddHostedService<KafkaRelayWorker>();

        // ─── Redis cache ──────────────────────────────────────────────────────
        var redisConnStr = config.GetConnectionString("Redis")
            ?? throw new InvalidOperationException("ConnectionStrings:Redis is required.");

        services.AddSingleton<IConnectionMultiplexer>(sp =>
            ConnectionMultiplexer.Connect(redisConnStr));
        services.AddScoped<IPositionCache, PositionCache>();

        return services;
    }
}
