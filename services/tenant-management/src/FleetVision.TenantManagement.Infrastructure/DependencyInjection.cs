using FleetVision.TenantManagement.Application.Common.Interfaces;
using FleetVision.TenantManagement.Infrastructure.Persistence;
using FleetVision.TenantManagement.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FleetVision.TenantManagement.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("TenantManagementDb")
            ?? throw new InvalidOperationException("ConnectionStrings:TenantManagementDb is not configured.");

        // Scoped so it's per-request and bound to the HTTP context lifecycle
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<TenantRlsInterceptor>();

        services.AddDbContext<TenantManagementDbContext>((sp, options) =>
        {
            var interceptor = sp.GetRequiredService<TenantRlsInterceptor>();
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(TenantManagementDbContext).Assembly.FullName);
                npgsql.EnableRetryOnFailure(3);
            });
            options.AddInterceptors(interceptor);
        });

        services.AddScoped<ITenantManagementDbContext>(sp =>
            sp.GetRequiredService<TenantManagementDbContext>());

        return services;
    }
}
