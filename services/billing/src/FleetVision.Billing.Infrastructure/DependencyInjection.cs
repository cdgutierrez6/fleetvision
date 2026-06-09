using FleetVision.Billing.Application.Common.Interfaces;
using FleetVision.Billing.Infrastructure.Kafka;
using FleetVision.Billing.Infrastructure.Persistence;
using FleetVision.Billing.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace FleetVision.Billing.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connStr = configuration.GetConnectionString("BillingDb")
            ?? throw new InvalidOperationException("ConnectionStrings:BillingDb is required.");

        services.AddDbContext<BillingDbContext>(options =>
            options.UseNpgsql(connStr, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(BillingDbContext).Assembly.FullName);
                npgsql.EnableRetryOnFailure(3);
            }));

        services.AddScoped<IBillingDbContext>(sp =>
            sp.GetRequiredService<BillingDbContext>());

        // Outbox enqueuer — same scope as BillingDbContext so writes are atomic
        services.AddScoped<IBillingEventPublisher>(sp =>
            new BillingOutboxEnqueuer(sp.GetRequiredService<BillingDbContext>()));

        // Raw NpgsqlDataSource for BillingRelayWorker (FOR UPDATE SKIP LOCKED)
        services.AddSingleton<NpgsqlDataSource>(_ =>
            new NpgsqlDataSourceBuilder(connStr).Build());

        services.AddScoped<IStripeService, StripeService>();

        var tmBaseUrl = configuration["TenantManagement:BaseUrl"]
            ?? throw new InvalidOperationException("TenantManagement:BaseUrl is required.");

        if (!Uri.TryCreate(tmBaseUrl, UriKind.Absolute, out var tmUri) ||
            (tmUri.Scheme != "http" && tmUri.Scheme != "https"))
            throw new InvalidOperationException(
                $"TenantManagement:BaseUrl '{tmBaseUrl}' must be an absolute http/https URI.");

        services.AddHttpClient<ITenantManagementClient, TenantManagementClient>(client =>
        {
            client.BaseAddress = tmUri;
            client.Timeout     = TimeSpan.FromSeconds(10);
        }).AddStandardResilienceHandler();

        if (!string.IsNullOrEmpty(configuration["Kafka:BootstrapServers"]))
            services.AddHostedService<BillingRelayWorker>();

        return services;
    }
}
