using FleetVision.Gateway.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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
    .Enrich.WithProperty("service", "gateway")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

// ─── JWT Bearer — Gateway validates tokens issued by Identity service ──
var signingKey = builder.Configuration["Jwt:SigningKey"];
if (string.IsNullOrWhiteSpace(signingKey) || signingKey.Length < 32)
    throw new InvalidOperationException("Jwt:SigningKey is required and must be at least 32 characters.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "fleetvision-identity",
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "fleetvision-api",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

// Secure-by-default: any route without an explicit policy requires authentication.
// Public YARP routes must set AuthorizationPolicy: "Anonymous" in appsettings.
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// ─── YARP Reverse Proxy ────────────────────────────────────────
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// ─── OpenTelemetry ────────────────────────────────────────────
var otelEndpoint = builder.Configuration["OpenTelemetry:Endpoint"] ?? "http://localhost:4317";

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("fleetvision-gateway", serviceVersion: "1.0.0"))
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddOtlpExporter(otlp => otlp.Endpoint = new Uri(otelEndpoint));
    });

// ─── Health Checks ────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddUrlGroup(
        new Uri(builder.Configuration["Downstream:IdentityUrl"] + "/health"),
        name: "identity",
        tags: ["ready"]);

var app = builder.Build();

// ─── Middleware Pipeline ──────────────────────────────────────
app.UseSerilogRequestLogging(opts => opts.EnrichDiagnosticContext = (diag, ctx) =>
{
    diag.Set("RequestHost", ctx.Request.Host.Value);
    diag.Set("UserAgent", ctx.Request.Headers.UserAgent.ToString());
});

// Security headers applied at the gateway edge — all downstream traffic is covered
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"]    = "nosniff";
    context.Response.Headers["X-Frame-Options"]           = "DENY";
    context.Response.Headers["Referrer-Policy"]           = "strict-origin-when-cross-origin";
    context.Response.Headers["X-XSS-Protection"]          = "0";
    context.Response.Headers["Permissions-Policy"]        = "geolocation=(), microphone=(), camera=()";
    if (context.Request.IsHttps || !app.Environment.IsDevelopment())
        context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains; preload";
    context.Response.Headers["Content-Security-Policy"]   =
        "default-src 'none'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; connect-src 'self'; frame-ancestors 'none';";
    await next();
});

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Runs after auth is resolved — safe to read User claims
app.UseMiddleware<TenantPropagationMiddleware>();

app.MapHealthChecks("/health").AllowAnonymous();
app.MapHealthChecks("/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
}).AllowAnonymous();

app.MapReverseProxy();

Log.Information("FleetVision Gateway started on {Environment}", app.Environment.EnvironmentName);
app.Run();

public partial class Program { }
