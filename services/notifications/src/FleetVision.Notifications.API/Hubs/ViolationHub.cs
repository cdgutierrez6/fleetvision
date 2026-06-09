using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FleetVision.Notifications.API.Hubs;

/// <summary>
/// Real-time violation alert hub. Each authenticated FleetAdmin joins the group
/// "tenant:{tenantId}" on connect. ViolationKafkaConsumer broadcasts to the group
/// when a violation event arrives from Kafka.
///
/// Multi-tenant isolation: a client can only be in its own tenant group.
/// The tenant_id claim is extracted from the JWT — clients cannot self-assign groups.
///
/// WebSocket clients must pass the JWT via query string: ?access_token=...
/// (required by the WebSocket protocol, which doesn't support custom headers).
/// </summary>
[Authorize]
public sealed class ViolationHub : Hub
{
    private const string TenantIdClaim = "tenant_id";

    public override async Task OnConnectedAsync()
    {
        var tenantId = Context.User?.FindFirst(TenantIdClaim)?.Value;

        if (string.IsNullOrEmpty(tenantId))
            throw new HubException("tenant_id claim is required.");

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(tenantId));
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var tenantId = Context.User?.FindFirst(TenantIdClaim)?.Value;

        if (!string.IsNullOrEmpty(tenantId))
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(tenantId));

        await base.OnDisconnectedAsync(exception);
    }

    public static string GroupName(string tenantId) => $"tenant:{tenantId}";
}
