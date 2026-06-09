using FleetVision.PredictiveMaintenance.Application.Services;
using FleetVision.PredictiveMaintenance.Domain.Interfaces;
using FleetVision.PredictiveMaintenance.Domain.Rules;
using FleetVision.PredictiveMaintenance.Domain.Services;
using FleetVision.PredictiveMaintenance.Infrastructure.Cache;
using FleetVision.PredictiveMaintenance.Infrastructure.Kafka;
using FleetVision.PredictiveMaintenance.Infrastructure.Persistence;
using FleetVision.PredictiveMaintenance.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using StackExchange.Redis;

namespace FleetVision.PredictiveMaintenance.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<TenantRlsInterceptor>();

        var connStr = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default is required.");

        services.AddDbContext<MaintenanceDbContext>((sp, options) =>
        {
            var interceptor = sp.GetRequiredService<TenantRlsInterceptor>();
            options.UseNpgsql(connStr).AddInterceptors(interceptor);
        });

        services.AddSingleton<NpgsqlDataSource>(_ => new NpgsqlDataSourceBuilder(connStr).Build());

        var redisConnStr = configuration["Redis:ConnectionString"]
            ?? throw new InvalidOperationException("Redis:ConnectionString is required.");

        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(redisConnStr));

        services.AddScoped<IMaintenanceRepository, MaintenanceRepository>();
        services.AddScoped<IOdometerCache, OdometerCache>();

        services.AddScoped<IMaintenanceOutboxEnqueuer>(sp =>
            new MaintenanceOutboxEnqueuer(sp.GetRequiredService<MaintenanceDbContext>()));

        services.AddSingleton<MaintenanceRuleEngine>(_ => new MaintenanceRuleEngine(new IMaintenanceRule[]
        {
            new OBD2CriticalRule(),
            new OBD2WarningRule(),
            new OdometerRule(),
            new TimeBasedRule(),
        }));

        services.AddScoped<MaintenanceOrchestrator>();

        if (!string.IsNullOrEmpty(configuration["Kafka:BootstrapServers"]))
        {
            services.AddHostedService<TelemetryConsumer>();
            services.AddHostedService<MaintenanceRelayWorker>();
        }

        return services;
    }
}
