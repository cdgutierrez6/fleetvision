using FleetVision.Reporting.API.Middleware;
using FleetVision.Reporting.Application.Queries.GetFleetKpis;
using FleetVision.Reporting.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using QuestPDF.Infrastructure;
using Serilog;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ─── Serilog ──────────────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("service", "reporting")
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

// ─── Services ─────────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(GetFleetKpisQuery).Assembly));

// QuestPDF license — set once at startup, not per PDF generation call
QuestPDF.Settings.License = LicenseType.Community;

// ─── JWT Bearer ───────────────────────────────────────────────────────────────
// RS256 primary + HS256 legacy during transition (remove Jwt:SigningKey after 15 min post-deploy)
var rsaPublicPem = builder.Configuration["Jwt:RsaPublicKey"]
    ?? (File.Exists(builder.Configuration["Jwt:RsaPublicKeyPath"] ?? "")
        ? File.ReadAllText(builder.Configuration["Jwt:RsaPublicKeyPath"]!)
        : null)
    ?? throw new InvalidOperationException(
        "Jwt:RsaPublicKey or Jwt:RsaPublicKeyPath is required.");

var rsaForValidation = RSA.Create();
rsaForValidation.ImportFromPem(rsaPublicPem.AsSpan());
var jwtSigningKeys = new List<SecurityKey>
{
    new RsaSecurityKey(rsaForValidation) { KeyId = "rsa-1" }
};

var legacyJwtKey = builder.Configuration["Jwt:SigningKey"];
if (!string.IsNullOrWhiteSpace(legacyJwtKey))
{
    jwtSigningKeys.Add(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(legacyJwtKey)));
    Log.Warning("[JWT] Transition mode: Jwt:SigningKey (HS256) is set. " +
                "Remove from this service 15 min after Identity deploys RS256.");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys        = jwtSigningKeys,
            ValidateIssuer           = true,
            ValidIssuer              = builder.Configuration["Jwt:Issuer"] ?? "fleetvision-identity",
            ValidateAudience         = true,
            ValidAudience            = builder.Configuration["Jwt:Audience"] ?? "fleetvision-api",
            ValidateLifetime         = true,
            ClockSkew                = TimeSpan.FromSeconds(30),
            RoleClaimType            = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"
        };
    });

builder.Services.AddAuthorization();

// ─── Rate Limiting ────────────────────────────────────────────────────────────
// PDF export is CPU-intensive (QuestPDF renders full document).
// 5 exports per minute per authenticated user prevents CPU exhaustion.
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("pdf-export", opt =>
    {
        opt.PermitLimit          = 5;
        opt.Window               = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit           = 0;
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// ─── OpenTelemetry ────────────────────────────────────────────────────────────
var otelEndpoint = builder.Configuration["OpenTelemetry:Endpoint"] ?? "http://localhost:4317";

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("fleetvision-reporting", serviceVersion: "1.0.0"))
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddOtlpExporter(otlp => otlp.Endpoint = new Uri(otelEndpoint));
    });

// ─── Health Checks ────────────────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddNpgSql(
        builder.Configuration.GetConnectionString("TelemetryDb")!,
        name: "timescaledb",
        tags: ["db", "ready"])
    .AddNpgSql(
        builder.Configuration.GetConnectionString("GeofencingDb")!,
        name: "geofencing-db",
        tags: ["db", "ready"]);

var app = builder.Build();

// ─── Middleware Pipeline ──────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<ReportingExceptionMiddleware>();
app.UseSerilogRequestLogging();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<TenantContextMiddleware>();

app.MapControllers();
app.MapHealthChecks("/health");
app.MapHealthChecks("/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

Log.Information("FleetVision Reporting Service started on {Environment}", app.Environment.EnvironmentName);
app.Run();

public partial class Program { }
