using FleetVision.Notifications.API.Hubs;
using FleetVision.Notifications.API.Kafka;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using System.Security.Cryptography;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("service", "notifications")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

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
            RoleClaimType            = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role",
        };

        // SignalR WebSocket protocol doesn't support custom headers after the handshake.
        // Clients must pass the JWT via ?access_token= query string.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path        = context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    context.Token = accessToken;

                return Task.CompletedTask;
            },
        };
    });

builder.Services.AddAuthorization();

// ─── SignalR ─────────────────────────────────────────────────────────────────
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.HandshakeTimeout     = TimeSpan.FromSeconds(15);
    options.KeepAliveInterval    = TimeSpan.FromSeconds(15);
});

// ─── Kafka consumer (only when BootstrapServers is configured) ────────────────
if (!string.IsNullOrEmpty(builder.Configuration["Kafka:BootstrapServers"]))
    builder.Services.AddHostedService<ViolationKafkaConsumer>();

// ─── OpenTelemetry ────────────────────────────────────────────────────────────
var otelEndpoint = builder.Configuration["OpenTelemetry:Endpoint"] ?? "http://localhost:4317";
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("fleetvision-notifications", serviceVersion: "1.0.0"))
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddOtlpExporter(otlp => otlp.Endpoint = new Uri(otelEndpoint));
    });

builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapHub<ViolationHub>("/hubs/violations");
app.MapHealthChecks("/health");

Log.Information("FleetVision Notifications Service started on {Environment}", app.Environment.EnvironmentName);
app.Run();

public partial class Program { }
