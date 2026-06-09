using FleetVision.Geofencing.Application.Common;
using FleetVision.Geofencing.Domain.Exceptions;
using FluentValidation;
using System.Text.Json;

namespace FleetVision.Geofencing.API.Middleware;

public sealed class ValidationExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ValidationExceptionMiddleware> _logger;

    public ValidationExceptionMiddleware(RequestDelegate next, ILogger<ValidationExceptionMiddleware> logger)
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
        catch (ValidationException ex)
        {
            var errors = ex.Errors.Select(e => new { field = e.PropertyName, message = e.ErrorMessage });
            await WriteJson(context, 400, new { errors });
        }
        catch (InvalidPolygonException ex)
        {
            await WriteJson(context, 400, new { error = ex.Message });
        }
        catch (Exception ex) when (ex is GeofenceNotFoundException or ViolationNotFoundException)
        {
            await WriteJson(context, 404, new { error = ex.Message });
        }
        catch (GeofenceNameAlreadyExistsException ex)
        {
            await WriteJson(context, 409, new { error = ex.Message });
        }
        catch (GeofencePlanLimitExceededException ex)
        {
            await WriteJson(context, 422, new { error = ex.Message });
        }
        catch (TenantServiceUnavailableException ex)
        {
            _logger.LogError(ex, "Tenant management service unavailable");
            await WriteJson(context, 503, new { error = "Tenant service unavailable. Please retry." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            await WriteJson(context, 500, new { error = "Internal server error" });
        }
    }

    private static async Task WriteJson(HttpContext context, int status, object body)
    {
        context.Response.StatusCode  = status;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(body));
    }
}
