using FleetVision.TenantManagement.API;
using FleetVision.TenantManagement.API.Middleware;
using FleetVision.TenantManagement.Application.TenantProfiles.Commands.CreateTenantProfile;
using FleetVision.TenantManagement.Infrastructure;
using FleetVision.TenantManagement.Infrastructure.Persistence;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ─── Serilog ──────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("service", "tenant-management")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

// ─── Services ─────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(CreateTenantProfileCommand).Assembly));

builder.Services.AddValidatorsFromAssembly(typeof(CreateTenantProfileCommand).Assembly);
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationPipelineBehavior<,>));

// ─── JWT Bearer ───────────────────────────────────────────────
var signingKey = builder.Configuration["Jwt:SigningKey"];
if (string.IsNullOrWhiteSpace(signingKey) || signingKey.Length < 32)
    throw new InvalidOperationException("Jwt:SigningKey is required and must be at least 32 characters.");

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
            RoleClaimType           = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"
        };
    });

builder.Services.AddAuthorization();

// ─── OpenTelemetry ────────────────────────────────────────────
var otelEndpoint = builder.Configuration["OpenTelemetry:Endpoint"] ?? "http://localhost:4317";

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("fleetvision-tenant-management", serviceVersion: "1.0.0"))
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddEntityFrameworkCoreInstrumentation()
            .AddOtlpExporter(otlp => otlp.Endpoint = new Uri(otelEndpoint));
    });

// ─── Health Checks ────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddNpgSql(
        builder.Configuration.GetConnectionString("TenantManagementDb")!,
        name: "postgres",
        tags: ["db", "ready"]);

var app = builder.Build();

// ─── Middleware Pipeline ──────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<ValidationExceptionMiddleware>();
app.UseSerilogRequestLogging(opts => opts.EnrichDiagnosticContext = (diag, ctx) =>
{
    diag.Set("RequestHost", ctx.Request.Host.Value);
    diag.Set("UserAgent", ctx.Request.Headers.UserAgent.ToString());
});

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Reads X-Tenant-Id header (set by Gateway) and seeds the RLS context
app.UseMiddleware<TenantContextMiddleware>();

app.MapControllers();

app.MapHealthChecks("/health");
app.MapHealthChecks("/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<TenantManagementDbContext>();
    await db.Database.MigrateAsync();
    Log.Information("Database migrations applied");
}

Log.Information("FleetVision Tenant Management Service started on {Environment}", app.Environment.EnvironmentName);
app.Run();

public partial class Program { }
