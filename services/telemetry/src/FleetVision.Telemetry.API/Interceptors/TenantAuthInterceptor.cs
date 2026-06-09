using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace FleetVision.Telemetry.API.Interceptors;

/// <summary>
/// Interceptor gRPC que valida el claim tenant_id del JWT contra el tenant_id del mensaje.
/// Los dispositivos IoT llevan un JWT emitido por Identity Service con claim tenant_id.
/// </summary>
public sealed class TenantAuthInterceptor : Interceptor
{
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        ValidateTenantClaim(context);
        return await continuation(request, context);
    }

    public override async Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        ServerCallContext context,
        ClientStreamingServerMethod<TRequest, TResponse> continuation)
    {
        ValidateTenantClaim(context);
        return await continuation(requestStream, context);
    }

    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        ValidateTenantClaim(context);
        await continuation(request, responseStream, context);
    }

    private static void ValidateTenantClaim(ServerCallContext context)
    {
        var user = context.GetHttpContext().User;

        if (!user.Identity?.IsAuthenticated ?? true)
            throw new RpcException(new Status(StatusCode.Unauthenticated, "JWT token required."));

        var tenantClaim = user.FindFirstValue("tenant_id");
        if (string.IsNullOrEmpty(tenantClaim) || !Guid.TryParse(tenantClaim, out _))
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Invalid or missing tenant_id claim."));
    }
}
