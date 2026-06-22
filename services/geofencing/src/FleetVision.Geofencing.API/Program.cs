using FleetVision.Geofencing.API;
using FleetVision.Geofencing.API.Middleware;
using FleetVision.Geofencing.Application.Geofences.Commands;
using FleetVision.Geofencing.Infrastructure;
using FleetVision.Geofencing.Infrastructure.Persistence;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
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
    .Enrich.WithProperty("service", "geofencing")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(CreateGeofenceCommand).Assembly));

builder.Services.AddValidatorsFromAssembly(typeof(CreateGeofenceCommand).Assembly);
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationPipelineBehavior<,>));

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

var otelEndpoint = builder.Configuration["OpenTelemetry:Endpoint"] ?? "http://localhost:4317";
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("fleetvision-geofencing", serviceVersion: "1.0.0"))
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddEntityFrameworkCoreInstrumentation()
            .AddOtlpExporter(otlp => otlp.Endpoint = new Uri(otelEndpoint));
    });

builder.Services.AddHealthChecks()
    .AddNpgSql(
        builder.Configuration.GetConnectionString("GeofencingDb")!,
        name: "postgres",
        tags: ["db", "ready"]);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<ValidationExceptionMiddleware>();
app.UseSerilogRequestLogging();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
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
    var db = scope.ServiceProvider.GetRequiredService<GeofencingDbContext>();
    await db.Database.MigrateAsync();
    Log.Information("Database migrations applied");
}

Log.Information("FleetVision Geofencing Service started on {Environment}", app.Environment.EnvironmentName);
app.Run();

public partial class Program { }
