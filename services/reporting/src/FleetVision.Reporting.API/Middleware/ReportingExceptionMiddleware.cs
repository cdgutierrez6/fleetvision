using System.Text.Json;

namespace FleetVision.Reporting.API.Middleware;

public sealed class ReportingExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ReportingExceptionMiddleware> _logger;

    public ReportingExceptionMiddleware(
        RequestDelegate next,
        ILogger<ReportingExceptionMiddleware> logger)
    {
        _next   = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in Reporting service");
            context.Response.StatusCode  = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(new { error = "Internal server error." }));
        }
    }
}
