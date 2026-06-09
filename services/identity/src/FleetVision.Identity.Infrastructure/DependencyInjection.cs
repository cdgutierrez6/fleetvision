using FleetVision.Identity.Application.Common.Interfaces;
using FleetVision.Identity.Infrastructure.Persistence;
using FleetVision.Identity.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FleetVision.Identity.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("IdentityDb")
            ?? throw new InvalidOperationException("ConnectionStrings:IdentityDb is not configured.");

        services.AddDbContext<IdentityDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(IdentityDbContext).Assembly.FullName);
                npgsql.EnableRetryOnFailure(3);
            });

            options.UseOpenIddict();
        });

        services.AddScoped<IIdentityDbContext>(provider =>
            provider.GetRequiredService<IdentityDbContext>());

        services.AddSingleton<IPasswordHasher, Argon2PasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();

        return services;
    }
}
