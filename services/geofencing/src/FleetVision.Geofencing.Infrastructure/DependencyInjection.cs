using FleetVision.Geofencing.Application.Common;
using FleetVision.Geofencing.Infrastructure.Kafka;
using FleetVision.Geofencing.Infrastructure.Persistence;
using FleetVision.Geofencing.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using Npgsql;

namespace FleetVision.Geofencing.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<TenantRlsInterceptor>();

        var connStr = configuration.GetConnectionString("GeofencingDb")
            ?? throw new InvalidOperationException("ConnectionStrings:GeofencingDb is required.");

        services.AddDbContext<GeofencingDbContext>((sp, options) =>
        {
            var interceptor = sp.GetRequiredService<TenantRlsInterceptor>();
            options.UseNpgsql(connStr, npgsql => npgsql.UseNetTopologySuite())
                   .AddInterceptors(interceptor);
        });

        services.AddScoped<IGeofencingDbContext>(sp =>
            sp.GetRequiredService<GeofencingDbContext>());

        // Raw NpgsqlDataSource for ViolationRelayWorker (FOR UPDATE SKIP LOCKED)
        services.AddSingleton<NpgsqlDataSource>(_ =>
            new NpgsqlDataSourceBuilder(connStr).Build());

        // Outbox enqueuer — same DbContext scope as the handler, so writes are atomic
        services.AddScoped<IViolationPublisher>(sp =>
            new ViolationOutboxEnqueuer(sp.GetRequiredService<GeofencingDbContext>()));

        services.AddHttpClient<ITenantLimitsClient, TenantLimitsClient>(client =>
        {
            client.BaseAddress = new Uri(configuration["TenantManagement:BaseUrl"]
                ?? throw new InvalidOperationException("TenantManagement:BaseUrl is required."));
            client.Timeout = TimeSpan.FromSeconds(5);
        }).AddStandardResilienceHandler();

        // NTS GeometryFactory — SRID 4326 (WGS84)
        services.AddSingleton<GeometryFactory>(
            NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326));

        if (!string.IsNullOrEmpty(configuration["Kafka:BootstrapServers"]))
        {
            // Kafka consumer — telemetry.raw → EvaluateTelemetryEventCommand
            services.AddHostedService<TelemetryKafkaConsumer>();
            // Outbox relay — geofencing_outbox_events → geofencing.violations
            services.AddHostedService<ViolationRelayWorker>();
        }

        return services;
    }
}
