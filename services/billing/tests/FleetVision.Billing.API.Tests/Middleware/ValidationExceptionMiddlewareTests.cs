using FleetVision.Billing.API.Middleware;
using FleetVision.Billing.Domain.Exceptions;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace FleetVision.Billing.API.Tests.Middleware;

public sealed class ValidationExceptionMiddlewareTests
{
    private readonly ILogger<ValidationExceptionMiddleware> _logger =
        NullLogger<ValidationExceptionMiddleware>.Instance;

    private ValidationExceptionMiddleware CreateMiddleware(RequestDelegate next)
        => new(next, _logger);

    private static DefaultHttpContext BuildContext()
    {
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();
        return ctx;
    }

    private static async Task<string> ReadResponseBodyAsync(HttpContext ctx)
    {
        ctx.Response.Body.Seek(0, SeekOrigin.Begin);
        return await new StreamReader(ctx.Response.Body).ReadToEndAsync();
    }

    // ─── SubscriptionNotFoundException → 404, generic message ───────────────

    [Fact]
    public async Task InvokeAsync_SubscriptionNotFoundException_Returns404WithGenericMessage()
    {
        var ctx = BuildContext();
        var middleware = CreateMiddleware(_ =>
            throw new SubscriptionNotFoundException("do-not-expose-this-id"));

        await middleware.InvokeAsync(ctx);

        ctx.Response.StatusCode.Should().Be(404);
        var body = await ReadResponseBodyAsync(ctx);
        var json = JsonDocument.Parse(body).RootElement;
        json.GetProperty("error").GetString().Should().Be("Subscription not found.");
        body.Should().NotContain("do-not-expose-this-id");
    }

    // ─── NoActiveStripeSubscriptionException → 422, generic message ──────────

    [Fact]
    public async Task InvokeAsync_NoActiveStripeSubscriptionException_Returns422WithGenericMessage()
    {
        var ctx = BuildContext();
        var tenantId = Guid.NewGuid();
        var middleware = CreateMiddleware(_ =>
            throw new NoActiveStripeSubscriptionException(tenantId));

        await middleware.InvokeAsync(ctx);

        ctx.Response.StatusCode.Should().Be(422);
        var body = await ReadResponseBodyAsync(ctx);
        var json = JsonDocument.Parse(body).RootElement;
        json.GetProperty("error").GetString().Should().Be("No active subscription found.");
        // The tenant id is embedded in the exception message; it must never leak to the client
        body.Should().NotContain(tenantId.ToString());
    }

    // ─── SubscriptionAlreadyCanceledException → 409, generic message ─────────

    [Fact]
    public async Task InvokeAsync_SubscriptionAlreadyCanceledException_Returns409WithGenericMessage()
    {
        var ctx = BuildContext();
        var tenantId = Guid.NewGuid();
        var middleware = CreateMiddleware(_ =>
            throw new SubscriptionAlreadyCanceledException(tenantId));

        await middleware.InvokeAsync(ctx);

        ctx.Response.StatusCode.Should().Be(409);
        var body = await ReadResponseBodyAsync(ctx);
        var json = JsonDocument.Parse(body).RootElement;
        json.GetProperty("error").GetString().Should().Be("Subscription is already canceled.");
        // The tenant id is embedded in the exception message; it must never leak to the client
        body.Should().NotContain(tenantId.ToString());
    }

    // ─── WebhookSignatureException → 400, no body ────────────────────────────

    [Fact]
    public async Task InvokeAsync_WebhookSignatureException_Returns400()
    {
        var ctx = BuildContext();
        var middleware = CreateMiddleware(_ => throw new WebhookSignatureException());

        await middleware.InvokeAsync(ctx);

        ctx.Response.StatusCode.Should().Be(400);
    }

    // ─── ValidationException → 422 with field errors ─────────────────────────

    [Fact]
    public async Task InvokeAsync_ValidationException_Returns422WithFieldErrors()
    {
        var ctx = BuildContext();
        var failures = new List<ValidationFailure>
        {
            new("Plan", "Plan is required."),
            new("SuccessUrl", "Must be a valid URL."),
        };
        var middleware = CreateMiddleware(_ => throw new ValidationException(failures));

        await middleware.InvokeAsync(ctx);

        ctx.Response.StatusCode.Should().Be(422);
        var body = await ReadResponseBodyAsync(ctx);
        var json = JsonDocument.Parse(body).RootElement;
        var errors = json.GetProperty("errors").EnumerateArray().ToList();
        errors.Should().HaveCount(2);
        errors.Select(e => e.GetProperty("field").GetString())
              .Should().Contain("Plan").And.Contain("SuccessUrl");
    }

    // ─── Unhandled exception → 500, opaque message ───────────────────────────

    [Fact]
    public async Task InvokeAsync_UnhandledException_Returns500WithOpaqueMessage()
    {
        var ctx = BuildContext();
        var middleware = CreateMiddleware(_ =>
            throw new InvalidOperationException("ConnectionString=Server=internal-host;..."));

        await middleware.InvokeAsync(ctx);

        ctx.Response.StatusCode.Should().Be(500);
        var body = await ReadResponseBodyAsync(ctx);
        var json = JsonDocument.Parse(body).RootElement;
        json.GetProperty("error").GetString().Should().Be("Internal server error.");
        // Connection string detail must NEVER reach the client
        body.Should().NotContain("internal-host");
    }

    // ─── Happy path — middleware passes through ───────────────────────────────

    [Fact]
    public async Task InvokeAsync_NoException_Returns200()
    {
        var ctx = BuildContext();
        ctx.Response.StatusCode = 200;
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(ctx);

        ctx.Response.StatusCode.Should().Be(200);
    }
}
