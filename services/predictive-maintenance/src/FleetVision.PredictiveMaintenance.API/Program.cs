using FleetVision.PredictiveMaintenance.API.Middleware;
using FleetVision.PredictiveMaintenance.Application.Commands;
using FleetVision.PredictiveMaintenance.Application.Services;
using FleetVision.PredictiveMaintenance.Infrastructure;
using FleetVision.PredictiveMaintenance.Infrastructure.Persistence;
using HealthChecks.UI.Client;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Explicit URL binding — overrides ASPNETCORE_HTTP_PORTS if already set
builder.WebHost.UseUrls("http://+:8080");

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("service", "predictive-maintenance")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.Configure<MaintenanceOptions>(opt =>
{
    opt.OdometerThresholdKm = builder.Configuration.GetValue<decimal>("Maintenance:OdometerThresholdKm", 10_000m);
    opt.TimeBasedDays       = builder.Configuration.GetValue<int>("Maintenance:TimeBasedDays", 180);
});

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(CompleteMaintenanceCommand).Assembly));

var signingKey = builder.Configuration["Jwt:SigningKey"]
    ?? throw new InvalidOperationException("Jwt:SigningKey is required.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey        = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ValidateIssuer          = true,
            ValidIssuer             = builder.Configuration["Jwt:Issuer"] ?? "fleetvision-identity",
            ValidateAudience        = true,
            ValidAudience           = builder.Configuration["Jwt:Audience"] ?? "fleetvision-api",
            ValidateLifetime        = true,
            ClockSkew               = TimeSpan.FromSeconds(30),
            RoleClaimType           = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role",
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(opts =>
{
    opts.AddPolicy("maintenance-api", context =>
    {
        // Partition by JWT tenant_id — not the header, which can be forged
        var tenantId = context.User.FindFirst("tenant_id")?.Value
            ?? context.Connection.RemoteIpAddress?.ToString()
            ?? "anonymous";

        return RateLimitPartition.GetFixedWindowLimiter(
            tenantId,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window      = TimeSpan.FromMinutes(1),
            });
    });
    opts.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

var otelEndpoint = builder.Configuration["OpenTelemetry:Endpoint"] ?? "http://localhost:4317";
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("fleetvision-predictive-maintenance", serviceVersion: "1.0.0"))
    .WithTracing(t => t
        .AddAspNetCoreInstrumentation(o => o.RecordException = true)
        .AddEntityFrameworkCoreInstrumentation(o => o.SetDbStatementForText = false)
        .AddOtlpExporter(o => o.Endpoint = new Uri(otelEndpoint)));

builder.Services.AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(), tags: ["live"])
    .AddNpgSql(
        builder.Configuration.GetConnectionString("Default")!,
        name: "postgres",
        tags: ["ready"])
    .AddRedis(
        builder.Configuration["Redis:ConnectionString"]!,
        name: "redis",
        tags: ["ready"]);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"]        = "DENY";
    context.Response.Headers["Referrer-Policy"]        = "strict-origin-when-cross-origin";
    context.Response.Headers["X-XSS-Protection"]      = "0";
    if (!app.Environment.IsDevelopment())
        context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
    await next();
});

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<TenantContextMiddleware>();
app.UseRateLimiter();

app.MapControllers().RequireRateLimiting("maintenance-api");

app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate      = _ => true,
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
});
app.MapHealthChecks("/ready", new HealthCheckOptions
{
    Predicate      = c => c.Tags.Contains("ready"),
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
});

if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<MaintenanceDbContext>();
    await db.Database.MigrateAsync();
    Log.Information("Database migrations applied.");
}

Log.Information("FleetVision Predictive Maintenance Service started on {Environment}", app.Environment.EnvironmentName);
Console.WriteLine("[DIAG] Calling app.Run()...");
try
{
    app.Run();
    Console.WriteLine("[DIAG] app.Run() completed (normal shutdown).");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly.");
    Console.Error.WriteLine($"[DIAG] app.Run() THREW: {ex.GetType().Name}: {ex.Message}");
    throw;
}

public partial class Program { }
