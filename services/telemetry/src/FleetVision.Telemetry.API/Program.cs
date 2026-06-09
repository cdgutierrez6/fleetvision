using FleetVision.Telemetry.API.Grpc;
using FleetVision.Telemetry.API.Interceptors;
using FleetVision.Telemetry.Infrastructure;
using FluentValidation;
using MediatR;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using System.Reflection;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ─── Logging ─────────────────────────────────────────────────────────────────
builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

// ─── gRPC ─────────────────────────────────────────────────────────────────────
builder.Services.AddGrpc(opt =>
{
    opt.EnableDetailedErrors = builder.Environment.IsDevelopment();
    opt.Interceptors.Add<TenantAuthInterceptor>();
});

if (builder.Environment.IsDevelopment())
    builder.Services.AddGrpcReflection();

// ─── Auth (JWT — same symmetric key as all other services) ──────────────────
var signingKey = builder.Configuration["Jwt:SigningKey"];
if (string.IsNullOrWhiteSpace(signingKey) || signingKey.Length < 32)
    throw new InvalidOperationException("Jwt:SigningKey is required and must be at least 32 characters.");

builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
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
        opt.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    });
builder.Services.AddAuthorization();

// ─── MediatR + FluentValidation pipeline ──────────────────────────────────────
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(
        typeof(FleetVision.Telemetry.Application.Commands.IngestTelemetryCommand).Assembly));

builder.Services.AddValidatorsFromAssembly(
    typeof(FleetVision.Telemetry.Application.Commands.IngestTelemetryCommand).Assembly);

// ─── Infrastructure (TimescaleDB, Redis, Kafka outbox relay) ──────────────────
builder.Services.AddInfrastructure(builder.Configuration);

// ─── OpenTelemetry ────────────────────────────────────────────────────────────
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .SetResourceBuilder(ResourceBuilder.CreateDefault()
            .AddService("FleetVision.Telemetry", serviceVersion: "1.0.0"))
        .AddAspNetCoreInstrumentation()
        .AddOtlpExporter(opt =>
            opt.Endpoint = new Uri(builder.Configuration["Otel:Endpoint"] ?? "http://localhost:4317")));

// ─── Health checks ────────────────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddNpgSql(
        builder.Configuration.GetConnectionString("TelemetryDb")!,
        name: "timescaledb")
    .AddRedis(
        builder.Configuration.GetConnectionString("Redis")!,
        name: "redis");

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapGrpcService<TelemetryGrpcService>();
app.MapHealthChecks("/health");

// Swagger para desarrollo (gRPC reflection)
if (app.Environment.IsDevelopment())
    app.MapGrpcReflectionService();

await app.RunAsync();

// Accesible desde los tests de integración
public partial class Program { }
