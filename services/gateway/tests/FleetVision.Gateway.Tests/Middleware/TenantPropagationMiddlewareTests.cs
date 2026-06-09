using FleetVision.Gateway.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Claims;
using Xunit;

namespace FleetVision.Gateway.Tests.Middleware;

public sealed class TenantPropagationMiddlewareTests
{
    private static TenantPropagationMiddleware CreateMiddleware(RequestDelegate? next = null)
    {
        next ??= _ => Task.CompletedTask;
        return new TenantPropagationMiddleware(next, NullLogger<TenantPropagationMiddleware>.Instance);
    }

    private static HttpContext CreateAuthenticatedContext(string? tenantId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new("email", "user@test.com")
        };

        if (tenantId is not null)
            claims.Add(new Claim(TenantPropagationMiddleware.TenantIdClaimType, tenantId));

        var identity = new ClaimsIdentity(claims, authenticationType: "Bearer");
        var principal = new ClaimsPrincipal(identity);

        var context = new DefaultHttpContext();
        context.User = principal;
        return context;
    }

    [Fact]
    public async Task InvokeAsync_AuthenticatedWithTenantId_ShouldAddXTenantIdHeader()
    {
        var tenantId = Guid.NewGuid().ToString();
        var context = CreateAuthenticatedContext(tenantId);
        var middleware = CreateMiddleware();

        await middleware.InvokeAsync(context);

        context.Request.Headers[TenantPropagationMiddleware.TenantIdHeaderName]
            .ToString().Should().Be(tenantId);
    }

    [Fact]
    public async Task InvokeAsync_AuthenticatedSuperAdminWithoutTenantId_ShouldNotAddHeader()
    {
        // SuperAdmin has no tenant_id claim (null tenantId = no claim added)
        var context = CreateAuthenticatedContext(tenantId: null);
        var middleware = CreateMiddleware();

        await middleware.InvokeAsync(context);

        context.Request.Headers.ContainsKey(TenantPropagationMiddleware.TenantIdHeaderName)
            .Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_UnauthenticatedRequest_ShouldNotAddHeader()
    {
        var context = new DefaultHttpContext();
        // Default HttpContext has anonymous principal (IsAuthenticated = false)

        var middleware = CreateMiddleware();
        await middleware.InvokeAsync(context);

        context.Request.Headers.ContainsKey(TenantPropagationMiddleware.TenantIdHeaderName)
            .Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_ShouldAlwaysCallNext()
    {
        var nextCalled = false;
        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(new DefaultHttpContext());

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_AuthenticatedWithEmptyTenantId_ShouldNotAddHeader()
    {
        // Edge case: claim exists but value is empty/whitespace
        var identity = new ClaimsIdentity(
            [
                new Claim(TenantPropagationMiddleware.TenantIdClaimType, "   ")
            ],
            authenticationType: "Bearer");

        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(identity);

        var middleware = CreateMiddleware();
        await middleware.InvokeAsync(context);

        context.Request.Headers.ContainsKey(TenantPropagationMiddleware.TenantIdHeaderName)
            .Should().BeFalse();
    }
}
