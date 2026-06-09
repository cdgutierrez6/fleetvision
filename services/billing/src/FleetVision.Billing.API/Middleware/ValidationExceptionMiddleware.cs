using FleetVision.Billing.Domain.Exceptions;
using FluentValidation;
using System.Text.Json;

namespace FleetVision.Billing.API.Middleware;

public sealed class ValidationExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ValidationExceptionMiddleware> _logger;

    public ValidationExceptionMiddleware(
        RequestDelegate next,
        ILogger<ValidationExceptionMiddleware> logger)
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
            context.Response.StatusCode  = StatusCodes.Status422UnprocessableEntity;
            context.Response.ContentType = "application/json";

            var errors = ex.Errors.Select(e => new { field = e.PropertyName, message = e.ErrorMessage });
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { errors }));
        }
        catch (WebhookSignatureException ex)
        {
            _logger.LogWarning(ex, "Invalid Stripe webhook signature from {IP}",
                context.Connection.RemoteIpAddress);
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
        }
        catch (SubscriptionNotFoundException ex)
        {
            _logger.LogWarning(ex, "Subscription not found");
            context.Response.StatusCode  = StatusCodes.Status404NotFound;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(new { error = "Subscription not found." }));
        }
        catch (NoActiveStripeSubscriptionException ex)
        {
            _logger.LogWarning(ex, "No active Stripe subscription");
            context.Response.StatusCode  = StatusCodes.Status422UnprocessableEntity;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(new { error = "No active subscription found." }));
        }
        catch (SubscriptionAlreadyCanceledException ex)
        {
            _logger.LogWarning(ex, "Subscription already canceled");
            context.Response.StatusCode  = StatusCodes.Status409Conflict;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(new { error = "Subscription is already canceled." }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in billing service");
            context.Response.StatusCode  = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(new { error = "Internal server error." }));
        }
    }
}
