using FleetVision.Billing.API;
using FleetVision.Billing.API.Middleware;
using FleetVision.Billing.Application.Subscriptions.Commands.CreateCheckoutSession;
using FleetVision.Billing.Infrastructure;
using FleetVision.Billing.Infrastructure.Persistence;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ─── Serilog ──────────────────────────────────────────────────────────────────
// {Properties:j} removed — would serialize all diagnostic context including PII from JWT claims
// and raw Stripe webhook payloads that pass through request logging.
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("service", "billing")
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
    cfg.RegisterServicesFromAssembly(typeof(CreateCheckoutSessionCommand).Assembly));

builder.Services.AddValidatorsFromAssembly(typeof(CreateCheckoutSessionCommand).Assembly);
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationPipelineBehavior<,>));

// ─── JWT Bearer ───────────────────────────────────────────────────────────────
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

// ─── Rate Limiting ────────────────────────────────────────────────────────────
builder.Services.AddRateLimiter(options =>
{
    // Stripe always delivers from known IPs, but unvalidated callers could flood
    // the DB connection pool via the webhook endpoint before HMAC verification.
    options.AddFixedWindowLimiter("webhook", opt =>
    {
        opt.PermitLimit          = 100;
        opt.Window               = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit           = 0;
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// ─── OpenTelemetry ────────────────────────────────────────────────────────────
var otelEndpoint = builder.Configuration["OpenTelemetry:Endpoint"] ?? "http://localhost:4317";

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("fleetvision-billing", serviceVersion: "1.0.0"))
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddEntityFrameworkCoreInstrumentation()
            .AddOtlpExporter(otlp => otlp.Endpoint = new Uri(otelEndpoint));
    });

// ─── Health Checks ────────────────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddNpgSql(
        builder.Configuration.GetConnectionString("BillingDb")!,
        name: "postgres",
        tags: ["db", "ready"]);

var app = builder.Build();

// ─── Middleware Pipeline ──────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Stripe webhook requires the raw, unbuffered body for HMAC validation.
// EnableBuffering allows the body stream to be read twice (once raw, once by MVC).
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/billing/webhook"))
        context.Request.EnableBuffering();
    await next();
});

app.UseMiddleware<ValidationExceptionMiddleware>();
app.UseSerilogRequestLogging(opts =>
{
    opts.EnrichDiagnosticContext = (diag, ctx) =>
    {
        diag.Set("RequestHost", ctx.Request.Host.Value);
        // Headers excluded: Authorization (JWT) and User-Agent not logged to avoid PII leakage.
    };
    opts.GetLevel = (ctx, _, ex) =>
        ex is not null
            ? Serilog.Events.LogEventLevel.Error
            : ctx.Request.Path.StartsWithSegments("/billing/webhook")
                ? Serilog.Events.LogEventLevel.Debug  // webhook body must not appear in INFO logs
                : Serilog.Events.LogEventLevel.Information;
});

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

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
    await db.Database.MigrateAsync();
    Log.Information("Database migrations applied");
}

Log.Information("FleetVision Billing Service started on {Environment}", app.Environment.EnvironmentName);
app.Run();

public partial class Program { }
