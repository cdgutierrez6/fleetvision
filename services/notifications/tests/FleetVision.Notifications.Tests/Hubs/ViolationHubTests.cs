using FleetVision.Notifications.API.Hubs;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;
using System.Security.Claims;
using Xunit;

namespace FleetVision.Notifications.Tests.Hubs;

public sealed class ViolationHubTests
{
    private readonly IGroupManager _groups = Substitute.For<IGroupManager>();
    private readonly HubCallerContext _context = Substitute.For<HubCallerContext>();
    private readonly ViolationHub _hub;

    public ViolationHubTests()
    {
        _hub = new ViolationHub
        {
            Context = _context,
            Groups  = _groups,
        };
    }

    [Fact]
    public async Task OnConnectedAsync_WithValidTenantId_AddsClientToTenantGroup()
    {
        var tenantId = Guid.NewGuid().ToString();
        _context.User.Returns(UserWithClaim("tenant_id", tenantId));
        _context.ConnectionId.Returns("conn-1");

        await _hub.OnConnectedAsync();

        await _groups.Received(1).AddToGroupAsync("conn-1", $"tenant:{tenantId}", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnConnectedAsync_WithMissingTenantId_ThrowsHubException()
    {
        _context.User.Returns(UserWithClaim("other_claim", "some-value"));

        var act = async () => await _hub.OnConnectedAsync();

        await act.Should().ThrowAsync<HubException>()
                 .WithMessage("*tenant_id*");
    }

    [Fact]
    public async Task OnConnectedAsync_WithNullUser_ThrowsHubException()
    {
        _context.User.Returns((ClaimsPrincipal?)null);

        var act = async () => await _hub.OnConnectedAsync();

        await act.Should().ThrowAsync<HubException>();
    }

    [Fact]
    public async Task OnDisconnectedAsync_WithValidTenantId_RemovesClientFromTenantGroup()
    {
        var tenantId = Guid.NewGuid().ToString();
        _context.User.Returns(UserWithClaim("tenant_id", tenantId));
        _context.ConnectionId.Returns("conn-2");

        await _hub.OnDisconnectedAsync(null);

        await _groups.Received(1).RemoveFromGroupAsync("conn-2", $"tenant:{tenantId}", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnDisconnectedAsync_WithMissingTenantId_DoesNotCallRemoveFromGroup()
    {
        _context.User.Returns(UserWithClaim("other_claim", "val"));
        _context.ConnectionId.Returns("conn-3");

        await _hub.OnDisconnectedAsync(null);

        await _groups.DidNotReceiveWithAnyArgs()
                     .RemoveFromGroupAsync(default!, default!, default);
    }

    [Theory]
    [InlineData("tenant-abc")]
    [InlineData("00000000-0000-0000-0000-000000000001")]
    public void GroupName_ReturnsExpectedFormat(string tenantId)
    {
        ViolationHub.GroupName(tenantId).Should().Be($"tenant:{tenantId}");
    }

    [Fact]
    public async Task OnConnectedAsync_DifferentTenants_JoinDifferentGroups()
    {
        var tenantA = Guid.NewGuid().ToString();
        var tenantB = Guid.NewGuid().ToString();

        _context.ConnectionId.Returns("conn-A");
        _context.User.Returns(UserWithClaim("tenant_id", tenantA));
        await _hub.OnConnectedAsync();

        _context.ConnectionId.Returns("conn-B");
        _context.User.Returns(UserWithClaim("tenant_id", tenantB));
        await _hub.OnConnectedAsync();

        await _groups.Received(1).AddToGroupAsync("conn-A", $"tenant:{tenantA}", Arg.Any<CancellationToken>());
        await _groups.Received(1).AddToGroupAsync("conn-B", $"tenant:{tenantB}", Arg.Any<CancellationToken>());
    }

    private static ClaimsPrincipal UserWithClaim(string type, string value)
        => new(new ClaimsIdentity(new[] { new Claim(type, value) }, "test"));
}
