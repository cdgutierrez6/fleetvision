using FleetVision.Reporting.Application.Common.Interfaces;
using FleetVision.Reporting.Infrastructure.Fleet;
using FleetVision.Reporting.Infrastructure.Pdf;
using FleetVision.Reporting.Infrastructure.TimeSeries;
using FleetVision.Reporting.Infrastructure.Violations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace FleetVision.Reporting.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ─── Keyed NpgsqlDataSources (read-only connections) ──────────────────
        var telemetryCs = configuration.GetConnectionString("TelemetryDb")
            ?? throw new InvalidOperationException("ConnectionStrings:TelemetryDb is required.");

        var geofencingCs = configuration.GetConnectionString("GeofencingDb")
            ?? throw new InvalidOperationException("ConnectionStrings:GeofencingDb is required.");

        var fleetCs = configuration.GetConnectionString("FleetAssetsDb")
            ?? throw new InvalidOperationException("ConnectionStrings:FleetAssetsDb is required.");

        services.AddKeyedSingleton<NpgsqlDataSource>("telemetry",
            new NpgsqlDataSourceBuilder(telemetryCs).Build());

        services.AddKeyedSingleton<NpgsqlDataSource>("geofencing",
            new NpgsqlDataSourceBuilder(geofencingCs).Build());

        services.AddKeyedSingleton<NpgsqlDataSource>("fleet",
            new NpgsqlDataSourceBuilder(fleetCs).Build());

        // ─── Readers ──────────────────────────────────────────────────────────
        services.AddScoped<ITimeSeriesReader, TimeSeriesReader>();
        services.AddScoped<IViolationsReader, ViolationsReader>();
        services.AddScoped<IFleetStatusReader, FleetStatusReader>();
        services.AddScoped<IPdfGenerator, QuestPdfReportGenerator>();

        return services;
    }
}
