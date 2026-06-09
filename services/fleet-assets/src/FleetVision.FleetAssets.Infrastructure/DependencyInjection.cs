using FleetVision.FleetAssets.Application.Common;
using FleetVision.FleetAssets.Infrastructure.Persistence;
using FleetVision.FleetAssets.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FleetVision.FleetAssets.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<TenantRlsInterceptor>();

        services.AddDbContext<FleetAssetsDbContext>((sp, options) =>
        {
            var interceptor = sp.GetRequiredService<TenantRlsInterceptor>();
            options.UseNpgsql(
                configuration.GetConnectionString("FleetAssetsDb"),
                npgsql => npgsql.UseNetTopologySuite())
                .AddInterceptors(interceptor);
        });

        services.AddScoped<IFleetAssetsDbContext>(
            sp => sp.GetRequiredService<FleetAssetsDbContext>());

        var tenantMgmtUrl = configuration["TenantManagement:BaseUrl"]
            ?? throw new InvalidOperationException("TenantManagement:BaseUrl is required.");

        services.AddHttpClient<ITenantLimitsClient, TenantLimitsClient>(client =>
        {
            client.BaseAddress = new Uri(tenantMgmtUrl);
            client.Timeout = TimeSpan.FromSeconds(5);
        }).AddStandardResilienceHandler();

        return services;
    }
}
